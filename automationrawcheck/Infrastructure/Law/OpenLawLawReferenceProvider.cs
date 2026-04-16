// =============================================================================
// OpenLawLawReferenceProvider.cs
// 법제처 국가법령정보 공동활용 DRF API 실구현체
//
// [호출 흐름]
//   Step1: lawSearch (target=lstrm) → 법령용어 검색 (키워드 확인용)
//   Step2: lawSearch (target=law)   → 법령 검색 → MST(법령일련번호) 획득
//   Step3: lawService (target=law)  → MST 기반 조문 조회
//
// [주의]
//   - OC: "bim-law-check-api" (query parameter)
//   - type=JSON 고정
//   - 실패 시 빈 목록 반환 (fallback), 전체 API 영향 없음
//   - 건폐율/용적률 확정 판단에 사용 금지
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.Law;

#region OpenLawLawReferenceProvider 클래스

/// <summary>
/// 법제처 국가법령정보 공동활용 DRF API를 통해 법령 참조 정보를 조회하는 구현체입니다.
/// <para>
/// 실패 시 빈 목록을 반환하여 전체 API의 정상 동작을 보장합니다.
/// </para>
/// </summary>
public sealed class OpenLawLawReferenceProvider : ILawReferenceProvider
{
    #region 상수

    private const string OcValue       = "bim-law-check-api";
    private const string SearchBaseUrl = "http://www.law.go.kr/DRF/lawSearch.do";
    private const string ServiceBaseUrl = "http://www.law.go.kr/DRF/lawService.do";

    /// <summary>법령 검색 결과 상위 N건만 사용</summary>
    private const int MaxLawResults = 1;

    /// <summary>조문 상위 N개만 포함</summary>
    private const int MaxArticles = 3;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    #endregion

    #region 필드 및 생성자

    private readonly IHttpClientFactory                  _httpFactory;
    private readonly ILogger<OpenLawLawReferenceProvider> _logger;

    /// <summary>OpenLawLawReferenceProvider를 초기화합니다.</summary>
    public OpenLawLawReferenceProvider(
        IHttpClientFactory                  httpFactory,
        ILogger<OpenLawLawReferenceProvider> logger)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _logger      = logger      ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region 용도지역명 → 관련 법령 매핑

    /// <summary>
    /// 용도지역명에서 검색할 법제처 법령명을 결정합니다.
    /// 용도지역은 국토계획법 제36조 기준이므로 공통 법령을 사용하고,
    /// 개발제한구역은 별도 특별법을 우선합니다.
    /// </summary>
    private static string ResolveSearchKeyword(string zoningName)
    {
        if (zoningName.Contains("개발제한"))
            return "개발제한구역의 지정 및 관리에 관한 특별조치법";

        // 모든 용도지역 → 국토의 계획 및 이용에 관한 법률
        return "국토의 계획 및 이용에 관한 법률";
    }

    #endregion

    #region ILawReferenceProvider 구현

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LawReference>> GetReferencesAsync(
        string zoningKeyword,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(zoningKeyword))
            return Array.Empty<LawReference>();

        var searchKeyword = ResolveSearchKeyword(zoningKeyword);

        try
        {
            return await FetchReferencesAsync(searchKeyword, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "법제처 API 조회 실패 (zoningName={ZoneName}, keyword={Keyword}). 빈 목록 반환.",
                zoningKeyword, searchKeyword);
            return Array.Empty<LawReference>();
        }
    }

    #endregion

    #region 3단계 조회 파이프라인

    private async Task<IReadOnlyList<LawReference>> FetchReferencesAsync(
        string keyword,
        CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("LawApi");

        // ── Step 2: 법령 검색 → MST 획득 ─────────────────────────────────────
        var lawItems = await SearchLawAsync(client, keyword, ct);
        if (lawItems.Count == 0)
        {
            _logger.LogDebug("법령 검색 결과 없음 (keyword={Keyword})", keyword);
            return Array.Empty<LawReference>();
        }

        var references = new List<LawReference>();

        foreach (var law in lawItems.Take(MaxLawResults))
        {
            if (string.IsNullOrWhiteSpace(law.Mst))
                continue;

            // ── Step 3: 조문 조회 ─────────────────────────────────────────────
            var articles = await FetchArticlesAsync(client, law.Mst, law.LawName, ct);
            references.AddRange(articles);
        }

        _logger.LogInformation(
            "법제처 법령 참조 조회 완료: keyword={Keyword}, 결과={Count}건",
            keyword, references.Count);

        return references;
    }

    /// <summary>Step 2: 법령명 검색 → MST 목록 반환</summary>
    private async Task<List<LawSearchItem>> SearchLawAsync(
        HttpClient client,
        string keyword,
        CancellationToken ct)
    {
        var url = $"{SearchBaseUrl}?OC={OcValue}&target=law&q={Uri.EscapeDataString(keyword)}&type=JSON";

        _logger.LogDebug("법령 검색 요청: {Url}", url);

        var resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("법령 검색 HTTP 오류: {Status}", resp.StatusCode);
            return new List<LawSearchItem>();
        }

        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseLawSearchResult(json);
    }

    /// <summary>Step 3: MST 기반 조문 조회 → LawReference 목록 반환</summary>
    private async Task<List<LawReference>> FetchArticlesAsync(
        HttpClient client,
        string mst,
        string lawName,
        CancellationToken ct)
    {
        var url = $"{ServiceBaseUrl}?OC={OcValue}&target=law&MST={Uri.EscapeDataString(mst)}&type=JSON";

        _logger.LogDebug("조문 조회 요청: LawName={LawName}, MST={Mst}", lawName, mst);

        var resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("조문 조회 HTTP 오류: {Status}", resp.StatusCode);
            return new List<LawReference>();
        }

        var json = await resp.Content.ReadAsStringAsync(ct);
        return ParseLawServiceResult(json, lawName, mst);
    }

    #endregion

    #region JSON 파싱

    /// <summary>
    /// lawSearch (target=law) 응답 파싱.
    /// 응답 형태:
    /// {
    ///   "LawSearch": {
    ///     "law": [ { "법령일련번호": "...", "법령명한글": "...", ... } ]
    ///              또는
    ///             { "법령일련번호": "...", "법령명한글": "..." }  (단건)
    ///   }
    /// }
    /// </summary>
    private List<LawSearchItem> ParseLawSearchResult(string json)
    {
        var result = new List<LawSearchItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 최상위 키: LawSearch
            if (!root.TryGetProperty("LawSearch", out var lawSearch))
                return result;

            // "law" 키: 배열 또는 단일 오브젝트
            if (!lawSearch.TryGetProperty("law", out var lawEl))
                return result;

            if (lawEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in lawEl.EnumerateArray())
                    TryAddLawItem(item, result);
            }
            else if (lawEl.ValueKind == JsonValueKind.Object)
            {
                TryAddLawItem(lawEl, result);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "법령 검색 JSON 파싱 실패");
        }
        return result;
    }

    private static void TryAddLawItem(JsonElement el, List<LawSearchItem> list)
    {
        var mst     = el.TryGetProperty("법령일련번호", out var mstEl)     ? mstEl.GetString()     : null;
        var lawName = el.TryGetProperty("법령명한글",   out var nameEl)    ? nameEl.GetString()    : null;

        if (!string.IsNullOrWhiteSpace(mst) && !string.IsNullOrWhiteSpace(lawName))
            list.Add(new LawSearchItem(mst, lawName!));
    }

    /// <summary>
    /// lawService (target=law) 응답 파싱.
    /// 응답 형태:
    /// {
    ///   "법령": {
    ///     "기본정보": { "법령명_한글": "...", ... },
    ///     "조문": {
    ///       "조문단위": [
    ///         { "조문번호": "1", "조문제목": "...", "조문내용": "..." },
    ///         ...
    ///       ]
    ///     }
    ///   }
    /// }
    /// </summary>
    private List<LawReference> ParseLawServiceResult(string json, string fallbackLawName, string mst)
    {
        var result = new List<LawReference>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 최상위 키: "법령"
            if (!root.TryGetProperty("법령", out var lawEl))
                return result;

            // 법령명 (기본정보에서 우선 사용)
            var resolvedLawName = fallbackLawName;
            if (lawEl.TryGetProperty("기본정보", out var basicInfo) &&
                basicInfo.TryGetProperty("법령명_한글", out var nameEl))
            {
                var n = nameEl.GetString();
                if (!string.IsNullOrWhiteSpace(n))
                    resolvedLawName = n;
            }

            // 조문 목록
            if (!lawEl.TryGetProperty("조문", out var 조문El))
                return result;
            if (!조문El.TryGetProperty("조문단위", out var 조문단위El))
                return result;

            var articles = 조문단위El.ValueKind == JsonValueKind.Array
                ? 조문단위El.EnumerateArray().ToList()
                : new List<JsonElement> { 조문단위El };

            foreach (var art in articles.Take(MaxArticles))
            {
                var 번호    = art.TryGetProperty("조문번호",  out var n1) ? n1.GetString() : null;
                var 제목    = art.TryGetProperty("조문제목",  out var n2) ? n2.GetString() : null;
                var 내용    = art.TryGetProperty("조문내용",  out var n3) ? n3.GetString() : null;

                var articleRef = string.IsNullOrWhiteSpace(번호) ? "조문" : $"제{번호}조";
                if (!string.IsNullOrWhiteSpace(제목))
                    articleRef += $" ({제목})";

                // 내용 요약 — 200자 초과 시 truncate
                var note = 내용 is { Length: > 200 }
                    ? 내용[..200] + "…"
                    : 내용;

                var lawUrl = $"http://www.law.go.kr/DRF/lawService.do?OC={OcValue}&target=law&MST={mst}&type=HTML";

                result.Add(new LawReference(
                    lawName:    resolvedLawName,
                    articleRef: articleRef,
                    url:        lawUrl,
                    note:       string.IsNullOrWhiteSpace(note) ? null : note));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "조문 JSON 파싱 실패 (LawName={LawName})", fallbackLawName);
        }
        return result;
    }

    #endregion

    #region 내부 DTO

    private sealed record LawSearchItem(string Mst, string LawName);

    #endregion
}

#endregion
