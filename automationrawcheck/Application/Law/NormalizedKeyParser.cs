// =============================================================================
// NormalizedKeyParser.cs
// normalizedKey 문자열 → ParsedNormalizedKey 구조체 파싱
//
// [형식]
//   조문: {lawAlias}/{article}[/{paragraph}][/{subParagraph}]
//   별표: {lawAlias}/{appendixRef}[/{subParagraph}]
//         appendixRef는 "별표N" 또는 "부칙N" 형태
//   섹션: {lawAlias}/{sectionId}  ← 고시/조례 플레이스홀더 (API 조회 불가)
//
// [예시]
//   "건축법/11"           → Article=11
//   "건축법/11/1"         → Article=11, Paragraph=1
//   "건축법/11/1/2"       → Article=11, Paragraph=1, SubParagraph="2"
//   "건축법시행령/별표1"   → AppendixRef="별표1"
//   "건축법/별표1/4호"     → AppendixRef="별표1", SubParagraph="4호"
//   "주차장조례/지역별기준" → SectionId="지역별기준" (API 호출 안 함)
// =============================================================================

namespace AutomationRawCheck.Application.Law;

/// <summary>파싱된 normalizedKey 구조체</summary>
public sealed record ParsedNormalizedKey
{
    /// <summary>법령 별칭 (예: "건축법", "국토계획법시행령")</summary>
    public string  LawAlias     { get; init; } = string.Empty;

    /// <summary>조문 번호 (양수). 별표/섹션 참조 시 null.</summary>
    public int?    Article      { get; init; }

    /// <summary>항 번호. 미지정 시 null.</summary>
    public int?    Paragraph    { get; init; }

    /// <summary>호·목 식별자 (예: "1", "가", "4호"). 미지정 시 null.</summary>
    public string? SubParagraph { get; init; }

    /// <summary>별표·부칙 식별자 (예: "별표1", "부칙1"). 미지정 시 null.</summary>
    public string? AppendixRef  { get; init; }

    /// <summary>고시·조례 섹션 식별자 플레이스홀더. API 조회 대상 아님. 미지정 시 null.</summary>
    public string? SectionId    { get; init; }

    /// <summary>일반 조문 참조 여부</summary>
    public bool IsArticleRef  => Article.HasValue;

    /// <summary>별표·부칙 참조 여부</summary>
    public bool IsAppendixRef => AppendixRef is not null;

    /// <summary>고시·조례 플레이스홀더 여부 (API 호출 불가)</summary>
    public bool IsSectionRef  => SectionId is not null && !IsArticleRef && !IsAppendixRef;
}

/// <summary>
/// normalizedKey 문자열을 파싱하는 정적 유틸리티입니다.
/// </summary>
public static class NormalizedKeyParser
{
    /// <summary>
    /// normalizedKey를 파싱합니다.
    /// </summary>
    /// <param name="normalizedKey">조회할 normalizedKey</param>
    /// <returns>
    /// 파싱 성공 시 <see cref="ParsedNormalizedKey"/>.<br/>
    /// 빈 문자열이거나 슬래시 구분자 없으면 null.
    /// </returns>
    public static ParsedNormalizedKey? Parse(string? normalizedKey)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey)) return null;

        var parts = normalizedKey.Split('/');
        if (parts.Length < 2) return null;

        var lawAlias = parts[0].Trim();
        if (string.IsNullOrEmpty(lawAlias)) return null;

        var part1 = parts[1].Trim();
        if (string.IsNullOrEmpty(part1)) return null;

        // ── 조문 참조: part1이 순수 숫자 ─────────────────────────────────────
        if (int.TryParse(part1, out int article))
        {
            int?    paragraph    = null;
            string? subParagraph = null;

            if (parts.Length > 2)
            {
                var part2 = parts[2].Trim();
                if (int.TryParse(part2, out int para))
                    paragraph = para;
                else if (!string.IsNullOrEmpty(part2))
                    subParagraph = part2;
            }

            if (parts.Length > 3 && paragraph.HasValue)
            {
                var part3 = parts[3].Trim();
                if (!string.IsNullOrEmpty(part3))
                    subParagraph = part3;
            }

            return new ParsedNormalizedKey
            {
                LawAlias     = lawAlias,
                Article      = article,
                Paragraph    = paragraph,
                SubParagraph = subParagraph,
            };
        }

        // ── 별표·부칙 참조: part1이 "별표" 또는 "부칙"으로 시작 ──────────────
        if (part1.StartsWith("별표", StringComparison.Ordinal) ||
            part1.StartsWith("부칙", StringComparison.Ordinal))
        {
            string? subParagraph = parts.Length > 2
                ? parts[2].Trim()
                : null;

            return new ParsedNormalizedKey
            {
                LawAlias     = lawAlias,
                AppendixRef  = part1,
                SubParagraph = string.IsNullOrEmpty(subParagraph) ? null : subParagraph,
            };
        }

        // ── 섹션 참조: 고시·조례 플레이스홀더 (API 조회 불가) ─────────────────
        return new ParsedNormalizedKey
        {
            LawAlias  = lawAlias,
            SectionId = part1,
        };
    }
}
