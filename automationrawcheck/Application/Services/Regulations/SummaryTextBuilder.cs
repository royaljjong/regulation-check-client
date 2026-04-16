// =============================================================================
// SummaryTextBuilder.cs
// 규칙 기반 summaryText 생성기 (LLM 없음)
//
// [책임]
//   RegulationCheckResult(용도지역 정보 + 오버레이 판정 + 법령 참조)를 받아
//   사람이 읽을 수 있는 1차 검토 요약 문장을 생성합니다.
//
// [원칙]
//   - LLM 없이 규칙 기반 문자열 조합만 사용합니다.
//   - "확정 판정" 문구를 생성하지 않습니다.
//   - 항상 "기본 기준 + 추가 검토 필요" 구조를 유지합니다.
// =============================================================================

using System.Text;
using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Services.Regulations;

/// <summary>
/// 규칙 기반 요약 텍스트 생성기입니다.
/// <para>
/// LLM 없이 용도지역명·건폐율/용적률 참고값·오버레이 판정·법령 참조를 조합해
/// 사람이 읽을 수 있는 1차 검토 요약 문장을 반환합니다.
/// </para>
/// </summary>
public static class SummaryTextBuilder
{
    #region 상수

    private const string NotFoundText =
        "선택하신 좌표에서 판정된 용도지역 정보가 없습니다. " +
        "해당 지역이 용도지역 미지정 구역이거나, 데이터 연동 범위 밖일 수 있습니다.";

    private const string DisclaimerPrefix = "\n\n[안내] ";
    private const string Disclaimer =
        "본 결과는 관련 SHP/CSV 데이터를 바탕으로 한 1차 참고용 판정입니다. " +
        "지구단위계획, 자치법규(조례) 및 개별 법령에 따라 실제 규제 내용이 달라질 수 있으므로, " +
        "반드시 관계 기관이나 토지이음(eum.go.kr)을 통해 확정 정보를 확인하시기 바랍니다.";

    #endregion

    /// <summary>
    /// 검토 결과를 바탕으로 요약 텍스트를 생성합니다.
    /// </summary>
    /// <param name="r">규제 검토 결과 도메인 모델</param>
    /// <returns>사람이 읽을 수 있는 1차 검토 요약 문장</returns>
    public static string Build(RegulationCheckResult r)
    {
        var sb = new StringBuilder();

        // ── 1. 용도지역 확인 ──────────────────────────────────────────────────
        if (r.RegulationInfo is not null && r.Zoning is not null)
        {
            var info = r.RegulationInfo;
            sb.Append($"본 필지는 [{info.ZoneName}]으로 확인됩니다.");
            sb.Append($"\n건축 시 법정 건폐율 {info.BuildingCoverageRatioRef},");
            sb.Append($" 용적률 {info.FloorAreaRatioRef} 범위를 참고할 수 있습니다.");
        }
        else
        {
            sb.Append(NotFoundText);
            if (r.NearestDistance.HasValue && r.NearestDistance.Value < 100)
            {
                 sb.Append($" (인접 용도지역 경계까지 약 {r.NearestDistance.Value:F1}m)");
            }
        }

        // ── 2. 주요 규제 (오버레이) 요약 ──────────────────────────────────────
        var restrictions = new List<string>();
        
        if (r.ExtraLayers.DevelopmentRestriction?.IsInside == true)
            restrictions.Add("개발제한구역(그린벨트)");
            
        if (r.ExtraLayers.DevelopmentActionRestriction?.IsInside == true)
            restrictions.Add("개발행위허가제한지역");
            
        if (r.ExtraLayers.DistrictUnitPlan?.IsInside == true)
            restrictions.Add("지구단위계획구역");

        if (restrictions.Count > 0)
        {
            sb.Append("\n\n[주요 규제 확인] ");
            sb.Append(string.Join(", ", restrictions));
            sb.Append("에 포함된 상태입니다. 해당 구역의 개별 법령 및 지침에 따른 행위 제한을 우선적으로 검토해야 합니다.");
        }
        else if (r.Zoning is not null)
        {
            sb.Append("\n\n[주요 규제 확인] 현재 연동된 주요 공간 데이터(그린벨트 등) 범위 내에서 특별한 중첩 규제는 탐지되지 않았습니다.");
        }

        // ── 3. 면책 안내 ──────────────────────────────────────────────────────
        sb.Append(DisclaimerPrefix);
        sb.Append(Disclaimer);

        return sb.ToString();
    }

    #region 구문 조각 생성 메서드

    private static void AppendGreenBeltNote(StringBuilder sb, OverlayZoneResult? drp)
    {
        if (drp is { IsInside: true })
        {
            sb.Append(" 단, 해당 토지는 개발제한구역(그린벨트)으로 확인되어" +
                      " 건축 행위가 원칙적으로 제한됩니다 (개별 허가 필요).");
            return;
        }

        // IsInside=false 이지만 데이터 신뢰도가 낮은 경우 보수적 문구 추가
        // (확정 부정 표현 방지)
        switch (drp?.Confidence)
        {
            case OverlayConfidenceLevel.DataUnavailable:
                sb.Append(" 개발제한구역 여부는 현재 연동 데이터 범위상 확인이 어려우며," +
                          " 토지이음(eum.go.kr)을 통한 직접 확인이 필요합니다.");
                break;

            case OverlayConfidenceLevel.NearBoundary:
                sb.Append(" 개발제한구역 경계 근접으로, 데이터 정밀도 한계상" +
                          " 현장 직접 확인이 권장됩니다.");
                break;

            // Normal (명확히 외부) 또는 null → summaryText에 별도 문구 없이 면책 안내로 처리
        }
    }

    private static void AppendDevActRestrictionNote(StringBuilder sb, OverlayZoneResult? dar)
    {
        if (dar is null)
            return;

        if (dar.IsInside)
        {
            sb.Append(" 해당 토지는 개발행위허가제한지역으로 확인되어" +
                      " 개발행위 및 인허가에 제한이 적용될 수 있습니다 (국토계획법 제63조 추가 검토 필요).");
            return;
        }

        switch (dar.Confidence)
        {
            case OverlayConfidenceLevel.NearBoundary:
                sb.Append(" 개발행위허가제한지역 경계 근접으로, 데이터 정밀도 한계상 현장 직접 확인이 권장됩니다.");
                break;

            case OverlayConfidenceLevel.DataUnavailable:
                sb.Append(" 현재 연동 데이터 범위상 개발행위허가제한지역 여부를 확인할 수 없어" +
                          " 토지이음(eum.go.kr) 직접 확인이 필요합니다.");
                break;
            // Normal → 면책 안내로만 처리
        }
    }

    private static void AppendDistrictUnitPlanNote(StringBuilder sb, OverlayZoneResult? dup)
    {
        if (dup is null)
            return;

        if (dup.IsInside)
        {
            sb.Append(" 지구단위계획구역 내로 확인되어 개별 계획 기준을 우선 검토할 필요가 있습니다.");
            return;
        }

        // 현재 데이터셋 미연동 → DataUnavailable 상태가 정상이므로 항상 추가 확인 안내
        sb.Append(" 지구단위계획 데이터는 현재 연동 범위에 포함되지 않아 추가 확인이 필요합니다.");
    }

    private static void AppendLawReferenceNote(
        StringBuilder sb, IReadOnlyList<LawReference> refs)
    {
        if (refs.Count == 0) return;

        var names  = string.Join(", ", refs.Take(2).Select(l => l.LawName));
        var suffix = refs.Count > 2 ? " 등" : string.Empty;
        sb.Append($" 관련 법령으로 {names}{suffix}이 확인됩니다.");
    }

    #endregion
}

