// =============================================================================
// ReviewItemsRequestDto.cs
// POST /api/regulation-check/review-items 요청 DTO
//
// 공간 판정 결과 + 계획 용도를 입력받아 검토 항목 목록을 반환합니다.
// 지원 용도: "공동주택" | "제1종근린생활시설" | "제2종근린생활시설" | "업무시설"
// =============================================================================

namespace AutomationRawCheck.Api.Dtos;

/// <summary>
/// 계획 용도 기반 검토 항목 조회 요청 DTO입니다.
/// </summary>
public sealed class ReviewItemsRequestDto
{
    /// <summary>판정된 용도지역명 (예: 제2종일반주거지역). 없으면 null.</summary>
    public string? ZoneName { get; init; }

    /// <summary>지구단위계획구역 포함 여부. null = 판정 불가.</summary>
    public bool? DistrictUnitPlanIsInside { get; init; }

    /// <summary>개발제한구역 포함 여부. null = 판정 불가.</summary>
    public bool? DevelopmentRestrictionIsInside { get; init; }

    /// <summary>
    /// 계획 용도.
    /// 지원값: "공동주택" | "제1종근린생활시설" | "제2종근린생활시설" | "업무시설"
    /// </summary>
    public string SelectedUse { get; init; } = string.Empty;
}
