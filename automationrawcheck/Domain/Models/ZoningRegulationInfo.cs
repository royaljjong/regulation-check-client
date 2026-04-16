// =============================================================================
// ZoningRegulationInfo.cs
// 용도지역별 건축 법규 참고 정보 도메인 모델
//
// [중요한 원칙]
//   이 모델의 모든 수치는 국토의 계획 및 이용에 관한 법률(국토계획법) 기준
//   '법정 최대치 참고값'이며 확정 판정이 아닙니다.
//   실제 건폐율/용적률은 지자체 조례, 지구단위계획에 따라 다를 수 있습니다.
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region ZoningRegulationInfo 레코드

/// <summary>
/// 용도지역별 건축 법규 기본 참고 정보입니다.
/// <para>
/// <b>주의</b>: 국토계획법 기준 참고값이며 확정 수치가 아닙니다.
/// 건폐율/용적률 확정값은 반드시 지자체 조례 및 지구단위계획을 추가 확인하세요.
/// </para>
/// </summary>
public record ZoningRegulationInfo
{
    /// <summary>용도지역 속성구분코드 (예: UQA122)</summary>
    public string ZoneCode { get; init; } = string.Empty;

    /// <summary>용도지역명 (예: 제2종일반주거지역)</summary>
    public string ZoneName { get; init; } = string.Empty;

    /// <summary>
    /// 건폐율 법정 상한 참고값 (%).
    /// 예: "60%" — 실제 조례에 따라 낮을 수 있음.
    /// </summary>
    public string BuildingCoverageRatioRef { get; init; } = string.Empty;

    /// <summary>
    /// 용적률 법정 상한 참고값 범위.
    /// 예: "150~250%" — 지역 조례로 달라짐.
    /// </summary>
    public string FloorAreaRatioRef { get; init; } = string.Empty;

    /// <summary>
    /// 허용 용도 카테고리 요약 (참고용).
    /// 예: "저밀 주거 중심 (단독주택, 제1~2종 근린생활시설)"
    /// </summary>
    public string AllowedUseSummary { get; init; } = string.Empty;

    /// <summary>
    /// 주요 제한 및 특이사항 목록.
    /// 사용자에게 반드시 안내해야 할 조건을 나열합니다.
    /// </summary>
    public List<string> Restrictions { get; init; } = new();
}

#endregion
