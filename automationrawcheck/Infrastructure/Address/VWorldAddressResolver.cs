// =============================================================================
// VWorldAddressResolver.cs
// V-World 주소 좌표 검색 API 실구현체 (IAddressResolver)
//
// [API 엔드포인트]
//   https://api.vworld.kr/req/address
//   service=address&request=getcoord&key={key}&address={encoded}&type={type}&format=json
//
// [검색 전략]
//   1차: type=road (도로명 주소) — 결과가 있으면 road 후보 전체 반환
//   2차: type=parcel (지번 주소) — road NOT_FOUND 시 재시도, parcel 후보 전체 반환
//   두 타입 결과를 혼합하지 않음 (road 결과가 있으면 parcel 시도 안 함)
//
// [복수 후보 처리]
//   V-World result 배열의 모든 항목을 개별 AddressResolveResult로 반환합니다.
//   0번 인덱스 = V-World 최우선 후보 (API 정렬 순서 유지).
//   호출자(컨트롤러)가 전체 목록을 사용자에게 노출하고 최우선 후보로 법규 검토를 수행합니다.
//
// [인증]
//   KEY={apiKey} + Referer: http://localhost (VWorldData HttpClient에 사전 설정됨)
// =============================================================================

using System.Text.Json;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Address;

#region VWorldAddressResolver 클래스

/// <summary>
/// V-World 주소 좌표 검색 API를 통해 주소 텍스트를 WGS84 좌표 후보 목록으로 변환하는 구현체입니다.
/// <para>
/// 도로명(type=road) 우선 시도. 결과 없으면 지번(type=parcel) 재시도.
/// 복수 후보가 있으면 전체를 반환하며, 0번 인덱스가 최우선 후보입니다.
/// </para>
/// </summary>
public sealed class VWorldAddressResolver : IAddressResolver
{
    #region 상수

    private const string ClientName   = "VWorldData";
    private const string ProviderName = "VWorld";
    private const string TypeRoad     = "road";
    private const string TypeParcel   = "parcel";

    private static readonly IReadOnlyList<AddressResolveResult> Empty =
        Array.Empty<AddressResolveResult>();

    #endregion

    #region 필드 및 생성자

    private readonly IHttpClientFactory             _httpFactory;
    private readonly VWorldOptions                  _options;
    private readonly ILogger<VWorldAddressResolver> _logger;

    /// <summary>VWorldAddressResolver를 초기화합니다.</summary>
    public VWorldAddressResolver(
        IHttpClientFactory             httpFactory,
        IOptions<VWorldOptions>        options,
        ILogger<VWorldAddressResolver> logger)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _options     = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger      = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IAddressResolver 구현

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AddressResolveResult>> ResolveAsync(
        string addressText,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(addressText))
            return Empty;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("V-World API 키 미설정 — 주소 변환 불가. 주소: {Address}", addressText);
            return Empty;
        }

        // ── 1차: 도로명 주소 검색 ──────────────────────────────────────────────
        var roadResults = await FetchCandidatesAsync(addressText, TypeRoad, ct);
        if (roadResults.Count > 0)
        {
            _logger.LogInformation(
                "주소 좌표 변환 성공(road): {Address} → {Count}건 후보",
                addressText, roadResults.Count);
            return roadResults;
        }

        // ── 2차: 지번 주소 검색 (fallback) ────────────────────────────────────
        _logger.LogDebug("도로명 검색 미발견, 지번 재시도: {Address}", addressText);
        var parcelResults = await FetchCandidatesAsync(addressText, TypeParcel, ct);

        if (parcelResults.Count > 0)
        {
            _logger.LogInformation(
                "주소 좌표 변환 성공(parcel): {Address} → {Count}건 후보",
                addressText, parcelResults.Count);
        }
        else
        {
            _logger.LogWarning("주소 변환 실패 — road/parcel 모두 미발견: {Address}", addressText);
        }

        return parcelResults;
    }

    #endregion

    #region 내부 구현

    /// <summary>단일 address type에 대해 V-World를 호출하고 후보 목록을 반환합니다.</summary>
    private async Task<IReadOnlyList<AddressResolveResult>> FetchCandidatesAsync(
        string addressText,
        string addressType,
        CancellationToken ct)
    {
        var url = BuildUrl(addressText, addressType);

        try
        {
            var client = _httpFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "V-World 주소 API HTTP 오류: {Code}, type={Type}, 주소={Address}",
                    (int)response.StatusCode, addressType, addressText);
                return Empty;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(body, addressText, addressType);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning(
                "V-World 주소 API 타임아웃: type={Type}, 주소={Address}", addressType, addressText);
            return Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "V-World 주소 API 호출 실패: type={Type}, 주소={Address}", addressType, addressText);
            return Empty;
        }
    }

    private string BuildUrl(string addressText, string addressType) =>
        $"{_options.GeocodingBaseUrl}" +
        $"?service=address" +
        $"&request=getcoord" +
        $"&key={Uri.EscapeDataString(_options.ApiKey)}" +
        $"&address={Uri.EscapeDataString(addressText)}" +
        $"&type={addressType}" +
        $"&format=json";

    /// <summary>V-World 응답 body를 파싱해 후보 목록을 반환합니다.</summary>
    private IReadOnlyList<AddressResolveResult> ParseResponse(
        string body,
        string addressText,
        string addressType)
    {
        try
        {
            using var doc  = JsonDocument.Parse(body);
            var resp   = doc.RootElement.GetProperty("response");
            var status = resp.TryGetProperty("status", out var sProp)
                ? sProp.GetString() ?? ""
                : "";

            if (string.Equals(status, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("V-World 주소 미발견: type={Type}, 주소={Address}", addressType, addressText);
                return Empty;
            }

            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                var errText = resp.TryGetProperty("error", out var e)
                    && e.TryGetProperty("text", out var t) ? t.GetString() : null;
                _logger.LogWarning(
                    "V-World 주소 API 오류: status={Status}, error={Err}, 주소={Address}",
                    status, errText, addressText);
                return Empty;
            }

            if (!resp.TryGetProperty("result", out var result))
                return Empty;

            // ── result는 배열 또는 단일 객체 둘 다 가능 ──────────────────────
            var candidates = new List<AddressResolveResult>();

            if (result.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in result.EnumerateArray())
                {
                    var r = ParseSingleItem(item, addressType);
                    if (r is not null) candidates.Add(r);
                }
            }
            else if (result.ValueKind == JsonValueKind.Object)
            {
                var r = ParseSingleItem(result, addressType);
                if (r is not null) candidates.Add(r);
            }

            if (candidates.Count > 1)
                _logger.LogDebug(
                    "복수 후보 {Count}건 반환 (type={Type}): {Address}",
                    candidates.Count, addressType, addressText);

            return candidates;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "V-World 주소 API JSON 파싱 실패: 주소={Address}", addressText);
            return Empty;
        }
    }

    /// <summary>result 배열의 개별 항목 하나를 AddressResolveResult로 변환합니다.</summary>
    private AddressResolveResult? ParseSingleItem(JsonElement item, string addressType)
    {
        if (!item.TryGetProperty("point", out var point)) return null;

        var xStr = point.TryGetProperty("x", out var xProp) ? xProp.GetString() : null;
        var yStr = point.TryGetProperty("y", out var yProp) ? yProp.GetString() : null;

        if (!double.TryParse(xStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lon) ||
            !double.TryParse(yStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat))
        {
            _logger.LogDebug("좌표 파싱 실패 — 해당 후보 건너뜀: x={X}, y={Y}", xStr, yStr);
            return null;
        }

        var normalizedText = item.TryGetProperty("text", out var textProp)
            ? textProp.GetString()?.Trim()
            : null;

        return new AddressResolveResult(
            coordinate:        new CoordinateQuery(lon, lat),
            normalizedAddress: normalizedText,
            provider:          ProviderName,
            addressType:       addressType);
    }

    #endregion
}

#endregion
