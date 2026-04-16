// =============================================================================
// LawReference.cs
// 법령 참조 도메인 모델 - 법제처 API 연동 결과 또는 참고 법령 정보
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region LawReference 레코드 정의

/// <summary>
/// 관련 법령 참조 정보를 나타냅니다.
/// 현재는 stub이며, 향후 법제처 국가법령정보 API와 연동됩니다.
/// </summary>
public record LawReference
{
    /// <summary>법령명 (예: 국토의 계획 및 이용에 관한 법률)</summary>
    public string LawName { get; init; }

    /// <summary>조항 참조 (예: 제76조 제1항)</summary>
    public string ArticleRef { get; init; }

    /// <summary>법령 원문 URL (선택, null 허용)</summary>
    public string? Url { get; init; }

    /// <summary>추가 참고사항 (선택, null 허용)</summary>
    public string? Note { get; init; }

    /// <summary>LawReference를 초기화합니다.</summary>
    public LawReference(string lawName, string articleRef, string? url = null, string? note = null)
    {
        LawName = lawName;
        ArticleRef = articleRef;
        Url = url;
        Note = note;
    }
}

#endregion
