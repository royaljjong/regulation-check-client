// =============================================================================
// RegulationCautionBuilder.cs
// 동적 cautionNotes 목록 생성기
//
// [책임]
//   오버레이 판정 결과(개발제한구역, 개발행위허가제한지역, 지구단위계획)와
//   법령 참조 유무를 바탕으로 추가 검토 필요 항목 목록을 동적으로 조합합니다.
//
// [원칙]
//   - 오버레이 IsInside = true 시 확인된 사실로 안내하되 "확정 판정" 금지.
//   - 오버레이 IsInside = false 시 "추가 확인 필요" 형태로 안내.
//   - 지구단위계획은 현재 데이터 미연동 → 항상 DataUnavailable 안내.
//   - 기본 면책 항목(조례, 개별 법령 등)은 항상 포함.
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Services.Regulations;

/// <summary>
/// 규제 검토 결과에 따른 동적 cautionNotes 목록 생성기입니다.
/// </summary>
public static class RegulationCautionBuilder
{
    #region 고정 caution 항목 (NotFound 전용)

    /// <summary>용도지역 미발견 시 반환하는 고정 caution 목록입니다.</summary>
    private static readonly IReadOnlyList<string> NotFoundCautions = new[]
    {
        "입력 좌표가 용도지역 지정 범위 밖일 수 있습니다",
        "국토 외 좌표(해상, 국경 밖)이거나 좌표 오입력 가능성을 확인하세요",
    };

    #endregion

    /// <summary>
    /// Preliminary(1차 판정) 상태에 대한 동적 caution 목록을 생성합니다.
    /// <para>
    /// 개발제한구역, 개발행위허가제한지역, 지구단위계획 오버레이 결과에 따라 항목을 조정합니다.
    /// </para>
    /// </summary>
    public static List<string> BuildPreliminary(RegulationCheckResult r)
    {
        var list = new List<string>(9);

        // ── 개발제한구역 (UQ141) ─────────────────────────────────────────────
        list.Add(BuildGreenBeltCaution(r.ExtraLayers.DevelopmentRestriction));

        // ── 개발행위허가제한지역 (UQ171 UQQ900) ──────────────────────────────
        list.Add(BuildDevActRestrictionCaution(r.ExtraLayers.DevelopmentActionRestriction));

        // ── 지구단위계획 (현재 미연동) ────────────────────────────────────────
        list.Add(BuildDistrictUnitPlanCaution(r.ExtraLayers.DistrictUnitPlan));

        // ── 항상 포함하는 기본 면책 항목 ─────────────────────────────────────
        list.Add("자치법규 및 조례 검토 필요 (지역별 건폐율·용적률 조례)");
        list.Add("건폐율·용적률은 법정 참고값이며 지자체 조례로 변경 가능합니다");
        list.Add("농지·산지·문화재보호구역 등 개별 법령 추가 검토 필요");

        // ── 법령 미조회 시 토지이음 안내 추가 ────────────────────────────────
        if (r.LawReferences.Count == 0)
        {
            list.Add("법령 참조가 조회되지 않았습니다 — 토지이음(eum.go.kr) 직접 확인 권장");
        }

        return list;
    }

    /// <summary>NotFound 상태에 대한 고정 caution 목록을 반환합니다.</summary>
    public static IReadOnlyList<string> BuildNotFound() => NotFoundCautions;

    #region 구문 조각 생성 메서드

    /// <summary>
    /// 개발제한구역 판정 결과와 데이터 신뢰도를 기반으로 caution 문구를 결정합니다.
    /// <list type="bullet">
    ///   <item><b>IsInside=true</b>: 포함 확인 경고</item>
    ///   <item><b>NearBoundary</b>: 경계 근접 — 정밀도 한계 경고</item>
    ///   <item><b>DataUnavailable</b>: 데이터 없음 — 직접 확인 필수</item>
    ///   <item><b>Normal (미포함)</b>: 기본 추가 확인 필요 안내</item>
    /// </list>
    /// </summary>
    private static string BuildGreenBeltCaution(OverlayZoneResult? drp)
    {
        if (drp is { IsInside: true })
            return "⚠ 개발제한구역(그린벨트) 내 위치 확인 — 건축 행위 원칙적 제한 (개발제한구역법 적용)";

        return drp?.Confidence switch
        {
            OverlayConfidenceLevel.DataUnavailable =>
                "⚠ 개발제한구역 데이터 미확인 — 해당 권역 SHP 데이터 부재 가능성. " +
                "토지이음(eum.go.kr) 직접 확인 필수",

            OverlayConfidenceLevel.NearBoundary =>
                "⚠ 개발제한구역 경계 근접 — 데이터 정밀도 한계로 현장 직접 확인 필요 " +
                "(토지이음 재확인 권장)",

            _ =>
                "개발제한구역(그린벨트) 해당 여부 추가 확인 필요"
        };
    }

    /// <summary>
    /// 개발행위허가제한지역(UQ171) 판정 결과를 기반으로 caution 문구를 결정합니다.
    /// </summary>
    private static string BuildDevActRestrictionCaution(OverlayZoneResult? dar)
    {
        if (dar is { IsInside: true })
            return "⚠ 개발행위허가제한지역 내 위치 확인 — 개발행위 및 인허가 제한 여부 추가 검토 필요 " +
                   "(국토계획법 제63조)";

        return dar?.Confidence switch
        {
            OverlayConfidenceLevel.DataUnavailable =>
                "⚠ 개발행위허가제한지역 데이터 미확인 — 해당 권역 UQ171 SHP 데이터 부재 가능성. " +
                "토지이음(eum.go.kr) 직접 확인 필수",

            OverlayConfidenceLevel.NearBoundary =>
                "⚠ 개발행위허가제한지역 경계 근접 — 데이터 정밀도 한계로 현장 직접 확인 필요",

            _ =>
                "개발행위허가제한지역 해당 여부 추가 확인 필요"
        };
    }

    /// <summary>
    /// 지구단위계획 항목은 현재 데이터셋 미연동으로 항상 DataUnavailable 안내를 반환합니다.
    /// </summary>
    private static string BuildDistrictUnitPlanCaution(OverlayZoneResult? dup) =>
        dup is { IsInside: true }
            ? "⚠ 지구단위계획구역 내 위치 — 별도 기준 적용 (지구단위계획서 확인 필요)"
            : "지구단위계획 데이터는 현재 연동 범위에 포함되지 않아 추가 확인이 필요합니다 " +
              "(토지이음 eum.go.kr 직접 확인 권장)";

    #endregion
}
