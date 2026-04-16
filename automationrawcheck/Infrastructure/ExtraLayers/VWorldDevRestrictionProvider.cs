// =============================================================================
// VWorldDevRestrictionProvider.cs
// VWorld LT_C_UD801 레이어 기반 개발제한구역 API 조회 프로바이더
//
// [호출 조건]
//   VWorldApiOptions.Enabled == true 일 때만 RegulationCheckService에서 호출됩니다.
//   실패 시 throw 없이 ILogger 로그 후 null 반환 → SHP 결과 유지.
// =============================================================================

using System.Text.Json;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.ExtraLayers;

#region VWorldDevRestrictionProvider 클래스

/// <summary>
/// VWorld LT_C_UD801 레이어를 사용해 좌표 기준 개발제한구역 포함 여부를 조회합니다.
/// <para>
/// API 실패 시 null을 반환합니다. 호출자는 null이면 SHP 결과를 사용해야 합니다.
/// </para>
/// </summary>
public sealed class VWorldDevRestrictionProvider
{
    #region 상수

    private const string HttpClientName = "vworld-dev-restriction";

    #endregion

    #region 필드 및 생성자

    private readonly IHttpClientFactory                      _httpFactory;
    private readonly VWorldApiOptions                        _options;
    private readonly ILogger<VWorldDevRestrictionProvider>   _logger;

    /// <summary>VWorldDevRestrictionProvider를 초기화합니다.</summary>
    public VWorldDevRestrictionProvider(
        IHttpClientFactory                      httpFactory,
        IOptions<VWorldApiOptions>              options,
        ILogger<VWorldDevRestrictionProvider>   logger)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _options     = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger      = logger         ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region 공개 메서드

    /// <summary>
    /// VWorld LT_C_UD801 레이어를 조회하여 개발제한구역 포함 여부를 반환합니다.
    /// </summary>
    /// <param name="longitude">WGS84 경도</param>
    /// <param name="latitude">WGS84 위도</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>포함이면 true, 미포함(NOT_FOUND 포함)이면 false, API 실패이면 null</returns>
    public async Task<bool?> IsInDevelopmentRestrictionAsync(
        double longitude,
        double latitude,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("[VWorldDevRestriction] ApiKey가 설정되지 않았습니다. API 호출을 건너뜁니다.");
            return null;
        }

        // geomFilter: POINT(경도 위도) — 경도 먼저, EPSG:4326
        var geomFilter = $"POINT({longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)} {latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)})";

        var queryParams = new Dictionary<string, string>
        {
            ["service"]    = "data",
            ["version"]    = "2.0",
            ["request"]    = "GetFeature",
            ["format"]     = "json",
            ["data"]       = "LT_C_UD801",
            ["geomFilter"] = geomFilter,
            ["geometry"]   = "false",
            ["attribute"]  = "true",
            ["crs"]        = "EPSG:4326",
            ["key"]        = _options.ApiKey,
            ["domain"]     = _options.Domain ?? string.Empty,
        };

        var queryString = string.Join("&",
            queryParams.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var requestUrl = $"{_options.BaseUrl.TrimEnd('/')}?{queryString}";

        _logger.LogDebug(
            "[VWorldDevRestriction] API 요청: geomFilter={GeomFilter}",
            geomFilter);

        try
        {
            var client   = _httpFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(requestUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[VWorldDevRestriction] HTTP 오류: StatusCode={StatusCode}, Url={Url}",
                    response.StatusCode, requestUrl);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            return ParseIsInside(json, longitude, latitude);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "[VWorldDevRestriction] 타임아웃 발생 (Point={Lon},{Lat})",
                longitude, latitude);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[VWorldDevRestriction] API 호출 실패 (Point={Lon},{Lat}): {Message}",
                longitude, latitude, ex.Message);
            return null;
        }
    }

    #endregion

    #region 응답 파싱

    /// <summary>
    /// VWorld GetFeature 응답 JSON을 파싱해 개발제한구역 포함 여부를 반환합니다.
    /// features 배열 1개 이상 → true, 없음 → false.
    /// </summary>
    private bool? ParseIsInside(string json, double longitude, double latitude)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            // response.status 확인
            if (root.TryGetProperty("response", out var responseEl))
            {
                if (responseEl.TryGetProperty("status", out var statusEl))
                {
                    var status = statusEl.GetString();

                    // NOT_FOUND = 해당 좌표에 개발제한구역 없음 → false (확정)
                    if (string.Equals(status, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug(
                            "[VWorldDevRestriction] 응답 status=NOT_FOUND → 개발제한구역 미포함 (Point={Lon},{Lat})",
                            longitude, latitude);
                        return false;
                    }

                    if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "[VWorldDevRestriction] 응답 status={Status} (Point={Lon},{Lat})",
                            status, longitude, latitude);
                        return null;
                    }
                }

                // response.result.featureCollection.features
                if (responseEl.TryGetProperty("result", out var resultEl) &&
                    resultEl.TryGetProperty("featureCollection", out var fcEl))
                {
                    // totalCount 보조 판정 (features 배열보다 먼저 확인)
                    if (fcEl.TryGetProperty("totalCount", out var tcEl) &&
                        tcEl.TryGetInt32(out var totalCount))
                    {
                        if (totalCount == 0)
                        {
                            _logger.LogDebug(
                                "[VWorldDevRestriction] totalCount=0 → 개발제한구역 미포함 (Point={Lon},{Lat})",
                                longitude, latitude);
                            return false;
                        }
                        if (totalCount >= 1)
                        {
                            _logger.LogDebug(
                                "[VWorldDevRestriction] totalCount={Count} → 개발제한구역 포함 (Point={Lon},{Lat})",
                                totalCount, longitude, latitude);
                            return true;
                        }
                    }

                    // totalCount 없으면 features 배열로 판정
                    if (fcEl.TryGetProperty("features", out var featuresEl) &&
                        featuresEl.ValueKind == JsonValueKind.Array)
                    {
                        var count = featuresEl.GetArrayLength();
                        _logger.LogDebug(
                            "[VWorldDevRestriction] 개발제한구역 feature 개수={Count} (Point={Lon},{Lat})",
                            count, longitude, latitude);
                        return count >= 1;
                    }

                    // featureCollection은 있으나 totalCount/features 없음 = 미포함
                    _logger.LogDebug(
                        "[VWorldDevRestriction] featureCollection 비어있음 → 개발제한구역 미포함 (Point={Lon},{Lat})",
                        longitude, latitude);
                    return false;
                }
            }

            _logger.LogWarning(
                "[VWorldDevRestriction] 응답 구조 파싱 실패 (Point={Lon},{Lat}): {Json}",
                longitude, latitude, json.Length > 300 ? json[..300] : json);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "[VWorldDevRestriction] JSON 파싱 오류 (Point={Lon},{Lat})",
                longitude, latitude);
            return null;
        }
    }

    #endregion
}

#endregion
