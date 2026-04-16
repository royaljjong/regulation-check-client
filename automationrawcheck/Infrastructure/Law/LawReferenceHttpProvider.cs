// =============================================================================
// LawReferenceHttpProvider.cs
// 법제처 국가법령정보 공동활용 API 연동 HttpClient 기반 프로바이더 골격
//
// [현재 상태]
//   - appsettings.json "LawApi:Enabled" = false 이면 stub 모드 (빈 목록 반환)
//   - "LawApi:Enabled" = true 이고 ServiceKey가 설정된 경우에만 실제 API 호출
//   - 현재 MVP에서는 기본값 Enabled=false로 동작합니다.
//
// [향후 활성화 방법]
//   1. appsettings.json의 "LawApi:Enabled" = true 설정
//   2. "LawApi:ServiceKey"에 법제처 API 키 입력
//   3. Program.cs DI에서 StubLawReferenceProvider → LawReferenceHttpProvider 교체
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationRawCheck.Infrastructure.Law;

#region API 응답 내부 모델

/// <summary>법제처 API 법령 목록 응답 최상위 모델 (내부용)</summary>
internal sealed class LawApiResponse
{
    [JsonPropertyName("LawSearch")]
    public LawSearchResult? LawSearch { get; init; }
}

/// <summary>법령 검색 결과 (내부용)</summary>
internal sealed class LawSearchResult
{
    [JsonPropertyName("law")]
    public List<LawItem>? Laws { get; init; }
}

/// <summary>법령 항목 (내부용)</summary>
internal sealed class LawItem
{
    [JsonPropertyName("법령명한글")]
    public string? LawName { get; init; }

    [JsonPropertyName("법령일련번호")]
    public string? LawId { get; init; }
}

#endregion

#region LawReferenceHttpProvider 클래스

/// <summary>
/// 법제처 국가법령정보 공동활용 API HttpClient 기반 프로바이더입니다.
/// <para>
/// 현재 상태: <c>LawApi:Enabled = false</c> 인 경우 stub 모드로 동작합니다.
/// </para>
/// TODO (활성화 절차):
/// <list type="number">
///   <item>appsettings.json → <c>LawApi:Enabled: true</c></item>
///   <item>appsettings.json → <c>LawApi:ServiceKey</c>에 API 키 입력</item>
///   <item>Program.cs DI → <c>StubLawReferenceProvider</c> 주석 처리 후 <c>LawReferenceHttpProvider</c> 등록</item>
/// </list>
/// </summary>
public sealed class LawReferenceHttpProvider : ILawReferenceProvider
{
    #region 상수

    /// <summary>용도지역 코드 → 법령 검색 키워드 매핑</summary>
    /// TODO: 용도지역 코드에 맞는 법령 검색 키워드를 추가/수정하세요.
    private static readonly IReadOnlyDictionary<string, string> ZoningToKeyword =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 주거지역 계열
            { "UQ110", "제1종전용주거지역 건축제한" },
            { "UQ120", "제2종전용주거지역 건축제한" },
            { "UQ130", "제1종일반주거지역 건축제한" },
            { "UQ140", "제2종일반주거지역 건축제한" },
            { "UQ150", "제3종일반주거지역 건축제한" },
            { "UQ160", "준주거지역 건축제한" },
            // 상업지역 계열
            { "UQ210", "중심상업지역 건축제한" },
            { "UQ220", "일반상업지역 건축제한" },
            { "UQ230", "근린상업지역 건축제한" },
            { "UQ240", "유통상업지역 건축제한" },
            // 공업지역 계열
            { "UQ310", "전용공업지역 건축제한" },
            { "UQ320", "일반공업지역 건축제한" },
            { "UQ330", "준공업지역 건축제한" },
            // 녹지지역 계열
            { "UQ410", "보전녹지지역 건축제한" },
            { "UQ420", "생산녹지지역 건축제한" },
            { "UQ430", "자연녹지지역 건축제한" },
        };

    #endregion

    #region 필드 및 생성자

    private readonly LawApiOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LawReferenceHttpProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>LawReferenceHttpProvider를 초기화합니다.</summary>
    /// <param name="options">법제처 API 설정 (IOptions로 주입)</param>
    /// <param name="httpClientFactory">IHttpClientFactory (named: "LawApi")</param>
    /// <param name="logger">로거</param>
    public LawReferenceHttpProvider(
        IOptions<LawApiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<LawReferenceHttpProvider> logger)
    {
        _options    = options?.Value           ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClientFactory?.CreateClient("LawApi")
                      ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger     = logger                   ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region ILawReferenceProvider 구현

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LawReference>> GetReferencesAsync(
        string zoningCode,
        CancellationToken ct = default)
    {
        // ── Enabled 체크 ───────────────────────────────────────────────────
        if (!_options.Enabled)
        {
            _logger.LogDebug(
                "법제처 API 비활성 상태 (LawApi:Enabled=false). ZoningCode={Code}",
                zoningCode);
            return Array.Empty<LawReference>();
        }

        if (string.IsNullOrWhiteSpace(_options.ServiceKey))
        {
            _logger.LogWarning(
                "LawApi:ServiceKey가 설정되지 않았습니다. " +
                "appsettings.json 또는 환경변수(LawApi__ServiceKey)에 API 키를 설정하세요.");
            return Array.Empty<LawReference>();
        }

        // ── 검색 키워드 결정 ────────────────────────────────────────────────
        var keyword = ZoningToKeyword.TryGetValue(zoningCode, out var kw)
            ? kw
            : $"{zoningCode} 건축제한";

        _logger.LogInformation(
            "법제처 API 호출. ZoningCode={Code}, Keyword={Keyword}",
            zoningCode, keyword);

        // ── API 호출 ────────────────────────────────────────────────────────
        try
        {
            var url = BuildRequestUrl(keyword);
            using var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "법제처 API 오류 응답. Status={Status}, ZoningCode={Code}",
                    response.StatusCode, zoningCode);
                return Array.Empty<LawReference>();
            }

            var json   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<LawApiResponse>(json, JsonOptions);

            return MapToLawReferences(result, zoningCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "법제처 API HTTP 요청 실패. ZoningCode={Code}, URL={Url}",
                zoningCode, _options.BaseUrl);
            return Array.Empty<LawReference>();
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning(
                "법제처 API 요청 타임아웃 또는 취소됨. ZoningCode={Code}",
                zoningCode);
            return Array.Empty<LawReference>();
        }
    }

    #endregion

    #region URL 빌더

    /// <summary>
    /// 법제처 법령 검색 API URL을 생성합니다.
    /// TODO: 실제 법제처 API 명세에 맞게 파라미터를 조정하세요.
    /// </summary>
    private string BuildRequestUrl(string keyword) =>
        $"{_options.BaseUrl}" +
        $"?target=law" +
        $"&type=JSON" +
        $"&query={Uri.EscapeDataString(keyword)}" +
        $"&OC={Uri.EscapeDataString(_options.ServiceKey)}" +
        $"&display=5" +
        $"&page=1";

    #endregion

    #region 응답 매핑

    /// <summary>
    /// API 응답 JSON을 <see cref="LawReference"/> 목록으로 변환합니다.
    /// </summary>
    private IReadOnlyList<LawReference> MapToLawReferences(
        LawApiResponse? response,
        string zoningCode)
    {
        var laws = response?.LawSearch?.Laws;
        if (laws is null || laws.Count == 0)
        {
            _logger.LogDebug(
                "법제처 API 검색 결과 없음. ZoningCode={Code}",
                zoningCode);
            return Array.Empty<LawReference>();
        }

        return laws
            .Where(l => !string.IsNullOrWhiteSpace(l.LawName))
            .Select(l => new LawReference(
                lawName:    l.LawName!,
                articleRef: "법제처 API 검색 결과",
                url:        BuildLawDetailUrl(l.LawId),
                note:       $"용도지역 코드 {zoningCode} 관련 법령"))
            .ToList();
    }

    /// <summary>법령 원문 URL을 생성합니다.</summary>
    private static string? BuildLawDetailUrl(string? lawId) =>
        string.IsNullOrWhiteSpace(lawId)
            ? null
            : $"https://www.law.go.kr/법령/{Uri.EscapeDataString(lawId)}";

    #endregion
}

#endregion
