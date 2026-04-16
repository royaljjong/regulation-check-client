// =============================================================================
// LawClauseResult.cs
// normalizedKey 기반 법제처 조문 조회 결과 도메인 모델
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

/// <summary>
/// normalizedKey 하나에 대응하는 법제처 조문 조회 결과입니다.
/// <para>
/// API 조회 성공 시 ClauseText에 조문 원문(500자 이하)이 채워집니다.
/// 실패(고시/조례 플레이스홀더, 타임아웃 등)는 null로 반환되며 시스템은 계속 동작합니다.
/// </para>
/// </summary>
public sealed record LawClauseResult
{
    /// <summary>조회에 사용된 normalizedKey (예: "건축법/11")</summary>
    public string NormalizedKey { get; init; } = string.Empty;

    /// <summary>법령 표시명 (예: "건축법")</summary>
    public string LawName { get; init; } = string.Empty;

    /// <summary>
    /// 조문 참조 표시 문자열 (예: "제11조 (건축허가)", "별표4").
    /// 항·호가 있으면 "제11조 제1항 제2호" 형태로 이어 붙입니다.
    /// </summary>
    public string ArticleRef { get; init; } = string.Empty;

    /// <summary>
    /// 조문 원문 텍스트 (최대 500자, 초과 시 "…" 붙임).
    /// 별표·부칙·고시 등 텍스트를 가져올 수 없는 경우 null.
    /// </summary>
    public string? ClauseText { get; init; }

    /// <summary>법제처 원문 링크 URL (HTML 뷰어)</summary>
    public string? Url { get; init; }

    /// <summary>true이면 메모리 캐시에서 읽은 결과입니다.</summary>
    public bool FromCache { get; init; }
}
