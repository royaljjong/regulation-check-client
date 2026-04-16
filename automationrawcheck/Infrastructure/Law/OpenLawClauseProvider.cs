// =============================================================================
// OpenLawClauseProvider.cs
// normalizedKey 기반 법제처 DRF API 조문 조회 구현체
//
// [호출 흐름]
//   1. normalizedKey 파싱 (NormalizedKeyParser)
//   2. 법령 별칭 → 검색어 변환 (LawAliasMap)
//      null이면 API 조회 불가 → null 반환 (고시/조례/지침 플레이스홀더)
//   3. MST 캐시 조회 / 미스 시 lawSearch.do 호출
//   4. 조문 캐시 조회 / 미스 시 lawService.do?target=article 호출
//   5. LawClauseResult 반환
//
// [캐싱 (IMemoryCache)]
//   "law_mst_v1:{searchKeyword}"      → MST 문자열       72h (법령번호 불변에 가까움)
//   "law_clause_v1:{normalizedKey}"   → LawClauseResult  24h (조문 내용 안정적)
//   위 두 경우 모두 null 저장 시 1h (실패 재시도 방지)
//
// [재시도]
//   HTTP 5xx / 네트워크 오류 시 LawApiOptions.MaxRetries 횟수만큼 지수 백오프 재시도.
//   4xx 오류 및 타임아웃은 즉시 실패 처리 (재시도 없음).
//
// [Fallback]
//   HTTP 오류·타임아웃·파싱 실패 시 null 반환 — 전체 API 중단 없음
//   별표·부칙: text=null인 LawClauseResult 반환 (URL은 제공)
//   고시/지침/조례: LawAliasMap.IsApiUnavailable() → 즉시 null
//
// [HTML 스트립]
//   법제처 응답에 포함된 HTML 태그를 제거하고 연속 공백을 정규화합니다.
//
// [활성화]
//   appsettings.json "LawApi:ClauseEnabled" = true
//   환경변수 또는 appsettings.json "LawApi:OcValue" = {법제처 API 인증키}
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Application.Law;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Law;

/// <summary>
/// 법제처 DRF API를 통해 normalizedKey 기반 조문 텍스트를 조회하는 구현체입니다.
/// </summary>
public sealed class OpenLawClauseProvider : ILawClauseProvider
{
    // ── 상수 ─────────────────────────────────────────────────────────────────

    private const string SearchEndpoint  = "lawSearch.do";
    private const string ServiceEndpoint = "lawService.do";
    private const int    MaxConcurrency  = 3;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    // HTML 태그 및 연속 공백 제거 패턴
    private static readonly Regex HtmlTagRegex     = new("<[^>]+>",       RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex  = new(@"\s{2,}",       RegexOptions.Compiled);
    private static readonly Regex HtmlEntityRegex  = new(@"&[a-zA-Z#\d]+;", RegexOptions.Compiled);

    // ── 필드 및 생성자 ────────────────────────────────────────────────────────

    private readonly string          _ocValue;
    private readonly string          _drfBase;
    private readonly bool            _enabled;
    private readonly int             _maxRetries;
    private readonly int             _clauseMaxLength;
    private readonly HttpClient      _http;
    private readonly IMemoryCache    _cache;
    private readonly ILogger<OpenLawClauseProvider> _logger;

    /// <summary>OpenLawClauseProvider를 초기화합니다.</summary>
    public OpenLawClauseProvider(
        IOptions<LawApiOptions>         options,
        IHttpClientFactory              httpFactory,
        IMemoryCache                    cache,
        ILogger<OpenLawClauseProvider>  logger)
    {
        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // OcValue 우선, 미설정 시 ServiceKey fallback
        _ocValue         = !string.IsNullOrWhiteSpace(opts.OcValue)
                           ? opts.OcValue
                           : opts.ServiceKey;
        _drfBase         = opts.BaseUrl.TrimEnd('/');
        _enabled         = opts.ClauseEnabled && !string.IsNullOrWhiteSpace(_ocValue);
        _maxRetries      = Math.Max(0, opts.MaxRetries);
        _clauseMaxLength = opts.ClauseMaxLength > 0 ? opts.ClauseMaxLength : 500;
        _http            = httpFactory?.CreateClient("LawApi")
                           ?? throw new ArgumentNullException(nameof(httpFactory));
        _cache           = cache   ?? throw new ArgumentNullException(nameof(cache));
        _logger          = logger  ?? throw new ArgumentNullException(nameof(logger));

        if (!_enabled)
            _logger.LogInformation(
                "OpenLawClauseProvider 비활성화: ClauseEnabled={Enabled}, OcValue 설정={HasKey}",
                opts.ClauseEnabled, !string.IsNullOrWhiteSpace(_ocValue));
    }

    // ── ILawClauseProvider 구현 ───────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<LawClauseResult?> GetClauseAsync(
        string normalizedKey, CancellationToken ct = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(normalizedKey)) return null;

        // ── 캐시 확인 ──────────────────────────────────────────────────────────
        var cacheKey = $"law_clause_v1:{normalizedKey}";
        if (_cache.TryGetValue(cacheKey, out LawClauseResult? cached))
        {
            if (cached is not null)
            {
                _logger.LogDebug(
                    "조문 캐시 HIT: key={Key}, law={Law}",
                    normalizedKey, cached.LawName);
                return cached with { FromCache = true };
            }
            // null이 캐시된 경우 (이전 실패) — 재시도 없이 즉시 null
            _logger.LogDebug("조문 캐시 NULL-HIT (이전 실패): key={Key}", normalizedKey);
            return null;
        }

        _logger.LogDebug("조문 캐시 MISS: key={Key}", normalizedKey);

        // ── 조회 실행 ──────────────────────────────────────────────────────────
        var result = await FetchClauseInternalAsync(normalizedKey, ct).ConfigureAwait(false);

        var ttl = result is not null ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1);
        _cache.Set(cacheKey, result, ttl);

        if (result is not null)
            _logger.LogDebug(
                "조문 조회 성공 → 캐시 저장(24h): key={Key}, law={Law}, hasText={HasText}",
                normalizedKey, result.LawName, result.ClauseText is not null);
        else
            _logger.LogDebug("조문 조회 실패 → null 캐시 저장(1h): key={Key}", normalizedKey);

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, LawClauseResult>> GetClausesAsync(
        IEnumerable<string> normalizedKeys,
        CancellationToken   ct = default)
    {
        var keys    = normalizedKeys.Distinct(StringComparer.Ordinal).ToList();
        var results = new Dictionary<string, LawClauseResult>(StringComparer.Ordinal);
        if (keys.Count == 0) return results;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 최대 MaxConcurrency개 병렬 — 캐시 히트 키는 즉시 반환
        var sem   = new SemaphoreSlim(MaxConcurrency);
        var tasks = keys.Select(async key =>
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try   { return (key, await GetClauseAsync(key, ct).ConfigureAwait(false)); }
            finally { sem.Release(); }
        });

        var all = await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (var (key, result) in all)
            if (result is not null) results[key] = result;

        sw.Stop();

        var cacheHits  = all.Count(x => x.Item2?.FromCache == true);
        var apiHits    = all.Count(x => x.Item2 is not null && x.Item2.FromCache == false);
        var misses     = keys.Count - results.Count;

        _logger.LogInformation(
            "legalBasis 일괄 조회 완료: 요청={Total}, 캐시히트={CacheHit}, API성공={ApiHit}, 실패/비대상={Miss}, 경과={ElapsedMs}ms",
            keys.Count, cacheHits, apiHits, misses, sw.ElapsedMilliseconds);

        return results;
    }

    // ── 내부 조회 파이프라인 ──────────────────────────────────────────────────

    private async Task<LawClauseResult?> FetchClauseInternalAsync(
        string normalizedKey, CancellationToken ct)
    {
        // Step 1: 파싱
        var parsed = NormalizedKeyParser.Parse(normalizedKey);
        if (parsed is null)
        {
            _logger.LogDebug("normalizedKey 파싱 불가: {Key}", normalizedKey);
            return null;
        }

        // Step 2: API 조회 불가 alias (고시/조례/지침)
        if (LawAliasMap.IsApiUnavailable(parsed.LawAlias))
        {
            _logger.LogDebug(
                "API 조회 불가 alias (고시/조례/지침): alias={Alias}, key={Key}",
                parsed.LawAlias, normalizedKey);
            return null;
        }

        // SectionId인 경우에도 조회 불가
        if (parsed.IsSectionRef)
        {
            _logger.LogDebug(
                "섹션 참조 (API 조회 불가): sectionId={Section}, key={Key}",
                parsed.SectionId, normalizedKey);
            return null;
        }

        var searchKeyword = LawAliasMap.Resolve(parsed.LawAlias);
        if (searchKeyword is null) return null;

        try
        {
            // Step 3: MST 획득
            var mst = await GetOrFetchMstAsync(searchKeyword, ct).ConfigureAwait(false);
            if (mst is null)
            {
                _logger.LogWarning(
                    "MST 획득 실패: alias={Alias}, keyword={Keyword}, key={Key}",
                    parsed.LawAlias, searchKeyword, normalizedKey);
                return null;
            }

            // Step 4: 조문 또는 별표 조회
            return parsed.IsAppendixRef
                ? await FetchAppendixAsync(mst, parsed, normalizedKey, ct).ConfigureAwait(false)
                : await FetchArticleAsync(mst, parsed, normalizedKey, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("법제처 조문 조회 취소됨: key={Key}", normalizedKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "법제처 조문 조회 예외 (key={Key}): {Message}",
                normalizedKey, ex.Message);
            return null;
        }
    }

    // ── MST 획득 ─────────────────────────────────────────────────────────────

    private async Task<string?> GetOrFetchMstAsync(
        string searchKeyword, CancellationToken ct)
    {
        var cacheKey = $"law_mst_v1:{searchKeyword}";
        if (_cache.TryGetValue(cacheKey, out string? cachedMst))
        {
            _logger.LogDebug("MST 캐시 HIT: keyword={Keyword}", searchKeyword);
            return cachedMst;
        }

        _logger.LogDebug("MST 캐시 MISS: keyword={Keyword}", searchKeyword);
        var mst = await FetchMstWithRetryAsync(searchKeyword, ct).ConfigureAwait(false);

        var ttl = mst is not null ? TimeSpan.FromHours(72) : TimeSpan.FromHours(2);
        _cache.Set(cacheKey, mst, ttl);

        if (mst is not null)
            _logger.LogDebug("MST 획득 → 캐시 저장(72h): keyword={Keyword}, MST={Mst}", searchKeyword, mst);
        else
            _logger.LogDebug("MST 조회 실패 → null 캐시 저장(2h): keyword={Keyword}", searchKeyword);

        return mst;
    }

    private async Task<string?> FetchMstWithRetryAsync(string keyword, CancellationToken ct)
    {
        var url = $"{_drfBase}/{SearchEndpoint}" +
                  $"?OC={Uri.EscapeDataString(_ocValue)}" +
                  $"&target=law" +
                  $"&q={Uri.EscapeDataString(keyword)}" +
                  $"&type=JSON";

        return await ExecuteWithRetryAsync(
            $"MST 검색 ({keyword})",
            async () =>
            {
                using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "법령 MST 검색 HTTP 오류: status={Status}, keyword={Keyword}",
                        (int)resp.StatusCode, keyword);
                    // 4xx → null 즉시 반환 (재시도 의미 없음)
                    if ((int)resp.StatusCode < 500) return (success: true, value: (string?)null);
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                }

                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return (success: true, value: ParseMst(json, keyword));
            },
            ct).ConfigureAwait(false);
    }

    private string? ParseMst(string json, string keyword)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;

            // 정상 구조: { "LawSearch": { "law": [...] } }
            if (!root.TryGetProperty("LawSearch", out var ls))
            {
                _logger.LogDebug("MST 응답에 LawSearch 키 없음: keyword={Keyword}", keyword);
                return null;
            }

            if (!ls.TryGetProperty("law", out var lawEl))
            {
                _logger.LogDebug("LawSearch에 law 키 없음 (검색 결과 없음): keyword={Keyword}", keyword);
                return null;
            }

            // "law" 키: 배열 또는 단일 오브젝트
            JsonElement? firstEl = lawEl.ValueKind switch
            {
                JsonValueKind.Array  => lawEl.EnumerateArray()
                    .Cast<JsonElement?>()
                    .FirstOrDefault(),
                JsonValueKind.Object => lawEl,
                _                    => null,
            };

            if (firstEl is null) return null;

            // 법령일련번호 추출
            var mst = firstEl.Value
                .TryGetProperty("법령일련번호", out var mstEl)
                ? mstEl.GetString()?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(mst))
            {
                _logger.LogDebug("법령일련번호 없음: keyword={Keyword}", keyword);
                return null;
            }

            _logger.LogDebug("MST 파싱 성공: keyword={Keyword}, MST={Mst}", keyword, mst);
            return mst;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "MST JSON 파싱 실패: keyword={Keyword}, reason={Reason}",
                keyword, ex.Message);
            return null;
        }
    }

    // ── 조문 조회 ─────────────────────────────────────────────────────────────

    private async Task<LawClauseResult?> FetchArticleAsync(
        string mst, ParsedNormalizedKey parsed, string normalizedKey, CancellationToken ct)
    {
        var url = $"{_drfBase}/{ServiceEndpoint}" +
                  $"?OC={Uri.EscapeDataString(_ocValue)}" +
                  $"&target=article" +
                  $"&MST={Uri.EscapeDataString(mst)}" +
                  $"&조문번호={parsed.Article}" +
                  $"&type=JSON";

        _logger.LogDebug(
            "조문 조회 시작: MST={Mst}, Article={Art}, key={Key}",
            mst, parsed.Article, normalizedKey);

        var (text, title) = await ExecuteWithRetryAsync(
            $"조문 조회 ({normalizedKey})",
            async () =>
            {
                using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "조문 조회 HTTP 오류: status={Status}, key={Key}",
                        (int)resp.StatusCode, normalizedKey);
                    if ((int)resp.StatusCode < 500)
                        return (success: true, value: ((string?)null, (string?)null));
                    throw new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                }
                var json   = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var result = ParseArticleText(json, parsed.Article!.Value, normalizedKey);
                return (success: true, value: result);
            },
            ct).ConfigureAwait(false);

        if (text is null)
        {
            _logger.LogDebug(
                "조문 텍스트 추출 실패 (null): key={Key}, MST={Mst}",
                normalizedKey, mst);
            return null;
        }

        // 조문 참조 문자열 조립
        var articleRef = $"제{parsed.Article}조";
        if (!string.IsNullOrWhiteSpace(title))
            articleRef += $" ({title.Trim()})";
        if (parsed.Paragraph.HasValue)
            articleRef += $" 제{parsed.Paragraph}항";
        if (!string.IsNullOrWhiteSpace(parsed.SubParagraph))
            articleRef += $" {parsed.SubParagraph}";

        var lawName = LawAliasMap.Resolve(parsed.LawAlias) ?? parsed.LawAlias;

        _logger.LogInformation(
            "조문 조회 성공: key={Key}, law={Law}, ref={Ref}, textLen={Len}",
            normalizedKey, lawName, articleRef, text.Length);

        return new LawClauseResult
        {
            NormalizedKey = normalizedKey,
            LawName       = lawName,
            ArticleRef    = articleRef,
            ClauseText    = TruncateText(text),
            Url           = BuildHtmlUrl(mst),
            FromCache     = false,
        };
    }

    private (string? Text, string? Title) ParseArticleText(
        string json, int articleNo, string normalizedKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;

            // 구조: { "법령": { "조문": { "조문단위": [...] } } }
            if (!root.TryGetProperty("법령", out var 법령El))
            {
                _logger.LogDebug(
                    "조문 응답에 '법령' 키 없음: key={Key}", normalizedKey);
                return (null, null);
            }

            if (!법령El.TryGetProperty("조문", out var 조문El))
            {
                _logger.LogDebug(
                    "법령에 '조문' 키 없음: key={Key}", normalizedKey);
                return (null, null);
            }

            if (!조문El.TryGetProperty("조문단위", out var 단위El))
            {
                _logger.LogDebug(
                    "조문에 '조문단위' 키 없음: key={Key}", normalizedKey);
                return (null, null);
            }

            // 조문단위: 배열 또는 단일 오브젝트
            JsonElement? artEl = 단위El.ValueKind switch
            {
                JsonValueKind.Array  => FindArticleInArray(단위El, articleNo),
                JsonValueKind.Object => 단위El,
                _                    => null,
            };

            if (artEl is null)
            {
                _logger.LogDebug(
                    "조문번호={Art} 엔트리 없음: key={Key}", articleNo, normalizedKey);
                return (null, null);
            }

            var rawText  = GetStringProp(artEl.Value, "조문내용");
            var rawTitle = GetStringProp(artEl.Value, "조문제목");

            // 조문내용이 없으면 항(항번호+항내용)에서 조합 시도
            if (string.IsNullOrWhiteSpace(rawText))
                rawText = TryExtractParagraphText(artEl.Value, normalizedKey);

            var cleanText  = rawText  is not null ? StripHtml(rawText)  : null;
            var cleanTitle = rawTitle is not null ? StripHtml(rawTitle) : null;

            return (cleanText, cleanTitle);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "조문 JSON 파싱 실패: key={Key}, reason={Reason}",
                normalizedKey, ex.Message);
            return (null, null);
        }
    }

    private static JsonElement? FindArticleInArray(JsonElement arrayEl, int articleNo)
    {
        var target = articleNo.ToString();
        foreach (var el in arrayEl.EnumerateArray())
        {
            if (!el.TryGetProperty("조문번호", out var no)) continue;
            var noStr = no.ValueKind == JsonValueKind.Number
                ? no.GetInt32().ToString()
                : no.GetString();
            if (noStr == target) return el;
        }
        // 번호 매칭 실패 시 첫 번째 엔트리 반환 (단일 조문 응답 대응)
        var first = arrayEl.EnumerateArray().Cast<JsonElement?>().FirstOrDefault();
        return first;
    }

    private static string? TryExtractParagraphText(JsonElement artEl, string key)
    {
        // 항 구조: { "항": [ { "항번호":"1", "항내용":"..." } ] }
        if (!artEl.TryGetProperty("항", out var 항El)) return null;

        var sb = new System.Text.StringBuilder();
        var items = 항El.ValueKind == JsonValueKind.Array
            ? 항El.EnumerateArray().ToList()
            : new List<JsonElement> { 항El };

        foreach (var item in items)
        {
            var content = GetStringProp(item, "항내용");
            if (!string.IsNullOrWhiteSpace(content))
                sb.Append(content.Trim()).Append(' ');
            if (sb.Length > 800) break; // 과도한 수집 방지
        }

        return sb.Length > 0 ? sb.ToString().Trim() : null;
    }

    // ── 별표·부칙 조회 ────────────────────────────────────────────────────────

    private async Task<LawClauseResult?> FetchAppendixAsync(
        string mst, ParsedNormalizedKey parsed, string normalizedKey, CancellationToken ct)
    {
        var appendixRef = parsed.AppendixRef!;
        var lawName     = LawAliasMap.Resolve(parsed.LawAlias) ?? parsed.LawAlias;
        var htmlUrl     = BuildHtmlUrl(mst);

        // 별표번호 추출: "별표1" → 1, "별표4" → 4
        var numStr = appendixRef
            .TrimStart('별', '표', '부', '칙')
            .Trim();

        if (!int.TryParse(numStr, out int appendixNo))
        {
            _logger.LogDebug(
                "별표 번호 파싱 불가 → URL fallback: ref={Ref}, key={Key}",
                appendixRef, normalizedKey);
            return BuildAppendixFallback(normalizedKey, lawName, appendixRef, htmlUrl);
        }

        var url = $"{_drfBase}/{ServiceEndpoint}" +
                  $"?OC={Uri.EscapeDataString(_ocValue)}" +
                  $"&target=별표" +
                  $"&MST={Uri.EscapeDataString(mst)}" +
                  $"&별표번호={appendixNo}" +
                  $"&type=JSON";

        _logger.LogDebug(
            "별표 조회 시작: MST={Mst}, AppendixNo={No}, key={Key}",
            mst, appendixNo, normalizedKey);

        try
        {
            var text = await ExecuteWithRetryAsync(
                $"별표 조회 ({normalizedKey})",
                async () =>
                {
                    using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "별표 조회 HTTP 오류: status={Status}, ref={Ref}",
                            (int)resp.StatusCode, appendixRef);
                        if ((int)resp.StatusCode < 500)
                            return (success: true, value: (string?)null);
                        throw new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                    }
                    var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    return (success: true, value: ParseAppendixText(json, normalizedKey));
                },
                ct).ConfigureAwait(false);

            var displayRef = appendixRef;
            if (!string.IsNullOrWhiteSpace(parsed.SubParagraph))
                displayRef += $" {parsed.SubParagraph}";

            var cleanText = text is not null ? TruncateText(StripHtml(text)) : null;

            if (cleanText is null)
                _logger.LogDebug(
                    "별표 텍스트 없음 (HTML이미지 또는 조회 실패) → URL 제공: key={Key}",
                    normalizedKey);
            else
                _logger.LogInformation(
                    "별표 조회 성공: key={Key}, ref={Ref}, textLen={Len}",
                    normalizedKey, displayRef, cleanText.Length);

            return new LawClauseResult
            {
                NormalizedKey = normalizedKey,
                LawName       = lawName,
                ArticleRef    = displayRef,
                ClauseText    = cleanText,
                Url           = htmlUrl,
                FromCache     = false,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "별표 조회 실패 → URL fallback: ref={Ref}, key={Key}, reason={Msg}",
                appendixRef, normalizedKey, ex.Message);
            return BuildAppendixFallback(normalizedKey, lawName, appendixRef, htmlUrl);
        }
    }

    private string? ParseAppendixText(string json, string normalizedKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;

            if (!root.TryGetProperty("법령", out var 법령El))
            {
                _logger.LogDebug("별표 응답에 '법령' 키 없음: key={Key}", normalizedKey);
                return null;
            }

            if (!법령El.TryGetProperty("별표", out var 별표El))
            {
                _logger.LogDebug("법령에 '별표' 키 없음: key={Key}", normalizedKey);
                return null;
            }

            // 직접 별표내용 필드
            var direct = GetStringProp(별표El, "별표내용");
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            // 별표단위 배열에서 첫 번째 별표내용
            if (별표El.TryGetProperty("별표단위", out var 단위El))
            {
                JsonElement? first = 단위El.ValueKind == JsonValueKind.Array
                    ? 단위El.EnumerateArray().Cast<JsonElement?>().FirstOrDefault()
                    : 단위El;

                if (first.HasValue)
                {
                    var nested = GetStringProp(first.Value, "별표내용");
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }

            _logger.LogDebug("별표 내용 없음 (이미지 전용 가능성): key={Key}", normalizedKey);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "별표 JSON 파싱 실패: key={Key}, reason={Reason}",
                normalizedKey, ex.Message);
            return null;
        }
    }

    // ── 재시도 래퍼 ──────────────────────────────────────────────────────────

    /// <summary>
    /// HTTP 호출을 _maxRetries 횟수만큼 지수 백오프로 재시도합니다.
    /// <para>5xx / 네트워크 오류만 재시도. 4xx / cancel은 즉시 throw.</para>
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        string operationName,
        Func<Task<(bool success, T value)>> operation,
        CancellationToken ct)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                var (success, value) = await operation().ConfigureAwait(false);
                if (success) return value;
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException ex) when (attempt < _maxRetries)
            {
                attempt++;
                var delayMs = (int)Math.Pow(2, attempt - 1) * 1000; // 1s, 2s, 4s
                _logger.LogWarning(
                    "법제처 API 재시도 {Attempt}/{Max}: op={Op}, reason={Msg}, delay={Delay}ms",
                    attempt, _maxRetries, operationName, ex.Message, delayMs);
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _maxRetries &&
                                        ex is not OperationCanceledException)
            {
                attempt++;
                var delayMs = (int)Math.Pow(2, attempt - 1) * 1000;
                _logger.LogWarning(
                    "법제처 API 재시도 {Attempt}/{Max}: op={Op}, reason={Msg}, delay={Delay}ms",
                    attempt, _maxRetries, operationName, ex.Message, delayMs);
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
        }
    }

    // ── 빌드/유틸 헬퍼 ───────────────────────────────────────────────────────

    private static LawClauseResult BuildAppendixFallback(
        string normalizedKey, string lawName, string appendixRef, string? url) =>
        new()
        {
            NormalizedKey = normalizedKey,
            LawName       = lawName,
            ArticleRef    = appendixRef,
            ClauseText    = null,   // 별표 텍스트는 HTML 원문 확인 필요
            Url           = url,
            FromCache     = false,
        };

    private string BuildHtmlUrl(string mst) =>
        $"{_drfBase}/{ServiceEndpoint}" +
        $"?OC={Uri.EscapeDataString(_ocValue)}" +
        $"&target=law&MST={mst}&type=HTML";

    private string? TruncateText(string? text)
    {
        if (text is null) return null;
        return text.Length > _clauseMaxLength
            ? text[.._clauseMaxLength] + "…"
            : text;
    }

    /// <summary>HTML 태그, 엔티티, 연속 공백을 제거합니다.</summary>
    private static string? StripHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // 1. HTML 태그 제거
        var stripped = HtmlTagRegex.Replace(text, " ");
        // 2. 일반 HTML 엔티티 디코드 (간단한 패턴만)
        stripped = HtmlEntityRegex.Replace(stripped, m => m.Value switch
        {
            "&amp;"  => "&",
            "&lt;"   => "<",
            "&gt;"   => ">",
            "&nbsp;" => " ",
            "&quot;" => "\"",
            _        => " ",
        });
        // 3. 연속 공백·줄바꿈 정규화
        stripped = MultiSpaceRegex.Replace(stripped, " ").Trim();
        return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
    }

    private static string? GetStringProp(JsonElement el, string propName) =>
        el.TryGetProperty(propName, out var p) ? p.GetString() : null;
}
