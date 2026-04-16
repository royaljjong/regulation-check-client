// =============================================================================
// WfsDistrictUnitPlanProvider.cs
// 지구단위계획구역 V-World Data API 실구현체 (IMemoryCache 캐싱 포함)
//
// [확인된 API 구조]
//   엔드포인트: https://api.vworld.kr/req/data
//   레이어:     LT_C_UPISUQ161 (지구단위계획구역)
//   필터:       geomFilter=POINT(lon lat)
//   인증:       KEY={apiKey} + Referer 헤더 (HttpClient에서 설정)
//
//   isInside=true  → response.status == "OK"   && features.length > 0
//   isInside=false → response.status == "NOT_FOUND"
//
// [응답에서 추출하는 필드]
//   properties.dgm_nm   : 지구단위계획구역명 (예: "국제교류복합지구 지구단위계획구역")
//   properties.atrb_se  : 속성구분코드 (예: "UQQ301")
//   properties.sig_nam  : 시군구명 (예: "서울특별시")
//
// [캐싱 전략]
//   캐시 키  : "dup_{lat:F5}_{lon:F5}"  (소수점 5자리 ≈ 1.1m 정밀도)
//   TTL      : Normal 결과 1시간 / DataUnavailable 결과 5분
//   구현     : IMemoryCache (프로세스 내, thread-safe)
//   캐시 대상: 정상 응답(Normal) + API 오류(DataUnavailable) 모두 캐싱
//             단, 예외가 throw 되기 전 단계 오류만 캐싱 (예외 자체는 저장 안 함)
//
// [fallback]
//   API 오류 / timeout / JSON 파싱 실패 → DataUnavailable 반환
// =============================================================================

using System.Text.Json;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.ExtraLayers;

#region WfsDistrictUnitPlanProvider 클래스

/// <summary>
/// V-World Data API를 통해 지구단위계획구역 포함 여부를 판정하는 구현체입니다.
/// <para>
/// LT_C_UPISUQ161 레이어에 geomFilter(POINT)를 적용해 Point-in-Polygon 판정을 수행합니다.
/// </para>
/// </summary>
public sealed class WfsDistrictUnitPlanProvider : IDistrictUnitPlanProvider
{
    #region 상수

    private const string ClientName = "VWorldData";
    private const string SourceDesc = "V-World Data API (지구단위계획구역, LT_C_UPISUQ161)";

    /// <summary>정상 판정 결과(Normal) 캐시 유효 시간: 1시간</summary>
    private static readonly TimeSpan NormalTtl = TimeSpan.FromHours(1);

    /// <summary>DataUnavailable 결과 캐시 유효 시간: 5분 (API 장애 시 빠른 재시도 허용)</summary>
    private static readonly TimeSpan UnavailableTtl = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region 필드 및 생성자

    private readonly IHttpClientFactory                  _httpFactory;
    private readonly VWorldOptions                       _options;
    private readonly IMemoryCache                        _cache;
    private readonly ILogger<WfsDistrictUnitPlanProvider> _logger;

    /// <summary>WfsDistrictUnitPlanProvider를 초기화합니다.</summary>
    public WfsDistrictUnitPlanProvider(
        IHttpClientFactory                   httpFactory,
        IOptions<VWorldOptions>              options,
        IMemoryCache                         cache,
        ILogger<WfsDistrictUnitPlanProvider> logger)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _options     = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache       = cache   ?? throw new ArgumentNullException(nameof(cache));
        _logger      = logger  ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IDistrictUnitPlanProvider 구현

    /// <inheritdoc/>
    public async Task<OverlayZoneResult> GetOverlayAsync(
        CoordinateQuery query,
        CancellationToken ct = default)
    {
        // ── 캐시 조회 ─────────────────────────────────────────────────────────
        var cacheKey = BuildCacheKey(query);
        if (_cache.TryGetValue(cacheKey, out OverlayZoneResult? cached) && cached is not null)
        {
            _logger.LogDebug(
                "지구단위계획 캐시 히트: Lon={Lon}, Lat={Lat}, IsInside={Inside}",
                query.Longitude, query.Latitude, cached.IsInside);
            return cached;
        }

        // ── API 키 검사 ───────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning(
                "V-World API 키 미설정 — appsettings.json VWorld:ApiKey를 설정하세요.");
            // API 키 없음은 캐싱하지 않음 (설정 후 재시작하면 즉시 반영돼야 함)
            return Unavailable(
                "V-World API 키가 설정되지 않았습니다. " +
                "appsettings.json의 VWorld:ApiKey 설정 후 재시작하세요.");
        }

        var url = BuildUrl(query);
        _logger.LogDebug(
            "V-World 지구단위계획 조회 (캐시 미스): Lon={Lon}, Lat={Lat}",
            query.Longitude, query.Latitude);

        // ── HTTP 호출 ─────────────────────────────────────────────────────────
        try
        {
            var client = _httpFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "V-World Data API HTTP 오류: {Code}, Lon={Lon}, Lat={Lat}",
                    (int)response.StatusCode, query.Longitude, query.Latitude);
                return Unavailable(
                    $"V-World Data API 응답 오류 (HTTP {(int)response.StatusCode}). " +
                    "API 키 유효성을 확인하세요.");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var result = ParseResponse(body, query);
            var ttl = result.Confidence == OverlayConfidenceLevel.DataUnavailable
                ? UnavailableTtl : NormalTtl;
            _cache.Set(cacheKey, result, ttl);
            return result;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning(
                "V-World Data API 타임아웃 (>{Sec}초): Lon={Lon}, Lat={Lat}",
                _options.TimeoutSeconds, query.Longitude, query.Latitude);
            return Unavailable(
                $"V-World API 응답 시간 초과 ({_options.TimeoutSeconds}초). " +
                "토지이음(eum.go.kr) 직접 확인을 권장합니다.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "V-World Data API 호출 실패: Lon={Lon}, Lat={Lat}",
                query.Longitude, query.Latitude);
            return Unavailable(
                "V-World API 연결에 실패했습니다. " +
                "토지이음(eum.go.kr) 직접 확인을 권장합니다.");
        }
    }

    #endregion

    #region URL 조립

    private string BuildUrl(CoordinateQuery query)
    {
        // geomFilter: POINT(lon lat) — 공백은 %20으로 인코딩
        var point = Uri.EscapeDataString(
            FormattableString.Invariant($"POINT({query.Longitude:F6} {query.Latitude:F6})"));

        return $"{_options.DataBaseUrl}" +
               $"?service=data" +
               $"&request=GetFeature" +
               $"&data={Uri.EscapeDataString(_options.DistrictUnitPlanLayer)}" +
               $"&geomFilter={point}" +
               $"&geometry=false" +
               $"&attribute=true" +
               $"&crs=EPSG%3A4326" +
               $"&size=1" +
               $"&format=json" +
               $"&key={Uri.EscapeDataString(_options.ApiKey)}";
    }

    #endregion

    #region 응답 파싱

    private OverlayZoneResult ParseResponse(string body, CoordinateQuery query)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var resp = doc.RootElement.GetProperty("response");

            var status = resp.TryGetProperty("status", out var sProp)
                ? sProp.GetString() ?? ""
                : "";

            // ── NOT_FOUND → 비포함 (API 정상 응답) ──────────────────────────────
            if (string.Equals(status, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "지구단위계획구역 비포함: Lon={Lon}, Lat={Lat}",
                    query.Longitude, query.Latitude);

                return new OverlayZoneResult(
                    IsInside:   false,
                    Name:       null,
                    Code:       null,
                    Source:     "api",
                    Note:       "지구단위계획구역 비포함으로 확인됩니다. " +
                                "경계 변경 가능성이 있어 토지이음(eum.go.kr)에서 최종 확인을 권장합니다.",
                    Confidence: OverlayConfidenceLevel.Normal);
            }

            // ── 오류 응답 ─────────────────────────────────────────────────────
            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                var errText = resp.TryGetProperty("error", out var e)
                    && e.TryGetProperty("text", out var t)
                    ? t.GetString()
                    : null;
                _logger.LogWarning(
                    "V-World Data API 오류: status={Status}, error={Err}",
                    status, errText);
                return Unavailable(
                    $"V-World API 오류 (status={status}): {errText ?? "알 수 없음"}. " +
                    "API 키 및 레이어명을 확인하세요.");
            }

            // ── OK → features 파싱 ────────────────────────────────────────────
            if (!resp.TryGetProperty("result", out var result)
                || !result.TryGetProperty("featureCollection", out var fc)
                || !fc.TryGetProperty("features", out var features)
                || features.GetArrayLength() == 0)
            {
                _logger.LogDebug(
                    "지구단위계획구역 비포함 (status=OK, features 없음): Lon={Lon}, Lat={Lat}",
                    query.Longitude, query.Latitude);

                return new OverlayZoneResult(
                    IsInside:   false,
                    Name:       null,
                    Code:       null,
                    Source:     "api",
                    Note:       "지구단위계획구역 비포함으로 확인됩니다.",
                    Confidence: OverlayConfidenceLevel.Normal);
            }

            // ── 포함 확인 ─────────────────────────────────────────────────────
            var props = features[0].TryGetProperty("properties", out var p) ? p : default;
            var name  = PickString(props, "dgm_nm");
            var code  = PickString(props, "atrb_se");
            var area  = PickString(props, "sig_nam");

            _logger.LogInformation(
                "지구단위계획구역 포함 확인: Name={Name}, Code={Code}, 시군구={Area}, Lon={Lon}, Lat={Lat}",
                name ?? "(이름 없음)", code ?? "-", area ?? "-",
                query.Longitude, query.Latitude);

            return new OverlayZoneResult(
                IsInside: true,
                Name:     name,
                Code:     code,
                Source:   "api",
                Note:     "지구단위계획구역 내로 확인됩니다. " +
                          "개별 지구단위계획서 기준의 건축 제한·허용 용도를 추가 검토하세요.",
                Confidence: OverlayConfidenceLevel.Normal);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "V-World Data API JSON 파싱 실패");
            return Unavailable(
                "V-World API 응답 파싱에 실패했습니다. " +
                "토지이음(eum.go.kr) 직접 확인을 권장합니다.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "V-World Data API 응답 구조 불일치");
            return Unavailable(
                "V-World API 응답 구조가 예상과 다릅니다. " +
                "토지이음(eum.go.kr) 직접 확인을 권장합니다.");
        }
    }

    private static string? PickString(JsonElement el, string key)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return null;
        if (!el.TryGetProperty(key, out var v)) return null;
        var s = v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
        return string.IsNullOrEmpty(s) ? null : s;
    }

    #endregion

    #region 유틸리티

    private static string BuildCacheKey(CoordinateQuery q) =>
        FormattableString.Invariant($"dup_{Math.Round(q.Latitude, 5):F5}_{Math.Round(q.Longitude, 5):F5}");

    private static OverlayZoneResult Unavailable(string note) =>
        new(IsInside:   false,
            Name:       null,
            Code:       null,
            Source:     "none",
            Note:       note,
            Confidence: OverlayConfidenceLevel.DataUnavailable);

    #endregion
}

#endregion
