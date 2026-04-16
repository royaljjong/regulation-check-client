// =============================================================================
// RegulationSummary.cs
// 규제 판정 요약 도메인 모델 - 1차 판정 상태 및 안내 메시지
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region RegulationStatus 열거형 정의

/// <summary>
/// 규제 검토 결과의 상태를 나타내는 열거형입니다.
/// </summary>
public enum RegulationStatus
{
    /// <summary>1차 예비 판정 완료 (추가 검토 필요)</summary>
    Preliminary,

    /// <summary>해당 좌표에서 용도지역 정보를 찾을 수 없음</summary>
    NotFound,

    /// <summary>처리 중 오류 발생</summary>
    Error
}

#endregion

#region RegulationSummary 레코드 정의

/// <summary>
/// 규제 검토 1차 판정 요약 결과를 나타냅니다.
/// 이 결과는 참고용이며 확정 판정이 아닙니다.
/// </summary>
public record RegulationSummary
{
    /// <summary>판정 상태</summary>
    public RegulationStatus Status { get; init; }

    /// <summary>사용자에게 표시할 안내 메시지 (참고용 경고 포함)</summary>
    public string Message { get; init; }

    /// <summary>RegulationSummary를 초기화합니다.</summary>
    public RegulationSummary(RegulationStatus status, string message)
    {
        Status = status;
        Message = message;
    }
}

#endregion
