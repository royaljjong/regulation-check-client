// =============================================================================
// ExtraLayerInfo.cs
// 추가 공간 레이어 정보 도메인 모델 - 개발제한구역, 개발행위허가제한지역, 지구단위계획 등
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region ExtraLayerInfo 레코드 정의

/// <summary>
/// 용도지역 외 추가 공간 레이어 정보를 나타냅니다.
/// <para>
/// 각 레이어는 <see cref="OverlayZoneResult"/>로 표현되며,
/// IsInside / Name / Code / Source / Confidence를 포함합니다.
/// </para>
/// </summary>
public record ExtraLayerInfo
{
    /// <summary>
    /// 지구단위계획구역 중첩 판정 결과.
    /// <para>
    /// <b>현재 미연동</b> — KLIP_004_20260201 데이터셋에 지구단위계획구역 레이어 없음.<br/>
    /// Confidence=DataUnavailable, IsInside=false 상태로 반환됩니다.<br/>
    /// 지구단위계획 SHP 데이터 확보 후 NullDistrictUnitPlanProvider를 실구현체로 교체하세요.
    /// </para>
    /// </summary>
    public OverlayZoneResult? DistrictUnitPlan { get; init; }

    /// <summary>
    /// 개발제한구역(그린벨트) 중첩 판정 결과.
    /// <para>
    /// UQ141 레이어(KLIP/UPIS)에서 UDV100 코드 폴리곤과의 중첩 여부를 판정합니다.
    /// </para>
    /// </summary>
    public OverlayZoneResult? DevelopmentRestriction { get; init; }

    /// <summary>
    /// 개발행위허가제한지역 중첩 판정 결과.
    /// <para>
    /// UQ171 레이어(KLIP/UPIS)에서 UQQ900 코드 폴리곤과의 중첩 여부를 판정합니다.<br/>
    /// 포함 시 국토계획법 제63조에 따른 개발행위 허가 제한 여부 추가 검토가 필요합니다.
    /// </para>
    /// </summary>
    public OverlayZoneResult? DevelopmentActionRestriction { get; init; }

    /// <summary>ExtraLayerInfo를 초기화합니다.</summary>
    public ExtraLayerInfo(
        OverlayZoneResult? districtUnitPlan,
        OverlayZoneResult? developmentRestriction,
        OverlayZoneResult? developmentActionRestriction = null)
    {
        DistrictUnitPlan             = districtUnitPlan;
        DevelopmentRestriction       = developmentRestriction;
        DevelopmentActionRestriction = developmentActionRestriction;
    }
}

#endregion
