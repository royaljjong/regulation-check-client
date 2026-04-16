// =============================================================================
// ReviewItemRuleTable.cs
// 계획 용도 기반 건축 검토 항목 조립기 (thin mapper)
//
// 규칙 문자열/조건은 모두 Application/Rules/Data/review_item_rules.json 에서 로드.
// 이 클래스는 RuleStore 데이터를 DTO로 변환하고 오버레이 항목 추가 순서를 관리합니다.
//
// [필터링 우선순위]
//   1. 용도 매칭: selectedUse 일치 OR applicableUses 목록에 포함
//   2. 용도지역 매칭: applicableZones=["*"] 이거나 zoneName이 목록에 포함
//      → zoneName 미판정(null/empty)이면 zone 필터 생략 (전부 포함)
//   3. trigger.alwaysInclude = true
//
// [오버레이 항목 (selectedUse="*")]
//   zone/use 필터와 독립적으로 작동. 공간 판정 조건(districtUnitPlan/developmentRestriction)으로만 제어.
//   - RI-OVL-DUP-001: districtUnitPlanIsInside=true 시 결과 끝에 append
//   - RI-OVL-DAR-001: developmentRestrictionIsInside=true 시 결과 끝에 append
// =============================================================================

using AutomationRawCheck.Application.Rules;
using AutomationRawCheck.Application.UseProfiles;
using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// 계획 용도별 건축 검토 항목을 반환하는 매핑 레이어입니다.
/// </summary>
public static class ReviewItemRuleTable
{
    /// <summary>
    /// 공간 판정 결과와 선택 용도를 조합해 검토 항목 목록을 반환합니다.
    /// </summary>
    public static List<ReviewItemDto> GetReviewItems(
        string  selectedUse,
        string? zoneName,
        bool?   districtUnitPlanIsInside,
        bool?   developmentRestrictionIsInside)
    {
        var rules = GetRuleSet(selectedUse);

        // ── 1. 용도별 기본 항목 (use + zone 필터 적용) ────────────────────────
        var items = rules
            .Where(r => MatchesUse(r, selectedUse)
                     && r.Trigger.AlwaysInclude
                     && MatchesZone(r, zoneName))
            .Select(ToDto)
            .ToList();

        // ── 2. 지구단위계획구역 포함 시 오버레이 항목 ─────────────────────────
        //    오버레이는 zone/use 필터 없이 trigger 조건만 적용
        if (districtUnitPlanIsInside == true)
        {
            items.AddRange(
                rules
                    .Where(r => r.SelectedUse == "*"
                             && r.Trigger.WhenDistrictUnitPlan == true)
                    .Select(ToDto));
        }

        // ── 3. 개발제한구역 포함 시 오버레이 항목 ────────────────────────────
        if (developmentRestrictionIsInside == true)
        {
            items.AddRange(
                rules
                    .Where(r => r.SelectedUse == "*"
                             && r.Trigger.WhenDevelopmentRestriction == true)
                    .Select(ToDto));
        }

        return items;
    }

    /// <summary>
    /// 공간 판정 결과와 선택 용도를 조합해 검토 항목 규칙 원본 목록을 반환합니다.
    /// <para>
    /// legalBasis 조문 조회가 필요한 경우 (includeLegalBasis=true) 이 메서드를 사용하고,
    /// 컨트롤러에서 직접 DTO 매핑 + 조문 데이터 붙이기를 수행합니다.
    /// </para>
    /// </summary>
    public static List<ReviewItemRuleRecord> GetReviewItemsRaw(
        string  selectedUse,
        string? zoneName,
        bool?   districtUnitPlanIsInside,
        bool?   developmentRestrictionIsInside)
    {
        var rules = GetRuleSet(selectedUse);

        var items = rules
            .Where(r => MatchesUse(r, selectedUse)
                     && r.Trigger.AlwaysInclude
                     && MatchesZone(r, zoneName))
            .ToList();

        if (districtUnitPlanIsInside == true)
        {
            items.AddRange(
                rules.Where(r => r.SelectedUse == "*"
                              && r.Trigger.WhenDistrictUnitPlan == true));
        }

        if (developmentRestrictionIsInside == true)
        {
            items.AddRange(
                rules.Where(r => r.SelectedUse == "*"
                              && r.Trigger.WhenDevelopmentRestriction == true));
        }

        return items;
    }

    private static IReadOnlyList<ReviewItemRuleRecord> GetRuleSet(string selectedUse)
    {
        var rules = RuleStore.ReviewItemRules;
        var hasExplicitUseRules = rules.Any(r => MatchesUse(r, selectedUse));
        var seedRules = RuleStore.ReviewItemSeedRules
            .Where(r => MatchesUse(r, selectedUse))
            .ToList();

        if (hasExplicitUseRules)
            return rules;

        if (seedRules.Count > 0)
            return rules.Concat(seedRules).ToList();

        if (!UseProfileRegistry.TryGet(selectedUse, out var profile))
            return rules;

        return rules.Concat(UseProfileFallbackRuleFactory.BuildReviewItemRules(profile)).ToList();
    }

    // ── 매칭 헬퍼 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 규칙이 요청 용도와 일치하는지 확인합니다.
    /// selectedUse 단일 값 또는 applicableUses 목록 중 하나라도 일치하면 참입니다.
    /// </summary>
    private static bool MatchesUse(ReviewItemRuleRecord r, string selectedUse) =>
        r.SelectedUse == selectedUse ||
        (r.ApplicableUses.Count > 0 &&
         r.ApplicableUses.Contains(selectedUse, StringComparer.Ordinal));

    /// <summary>
    /// 규칙이 요청 용도지역과 일치하는지 확인합니다.
    /// <list type="bullet">
    ///   <item>zoneName 미판정(null/empty) → 필터 생략 (모든 규칙 포함)</item>
    ///   <item>applicableZones 비어 있거나 "*" 포함 → 전체 일치</item>
    ///   <item>그 외 → 공백 제거 후 동등 비교 (한국어 명칭 공백 편차 보정)</item>
    /// </list>
    /// </summary>
    private static bool MatchesZone(ReviewItemRuleRecord r, string? zoneName)
    {
        if (string.IsNullOrWhiteSpace(zoneName)) return true;
        if (r.ApplicableZones.Count == 0 || r.ApplicableZones.Contains("*")) return true;

        // 공백 제거 후 비교 (예: "제 2종 일반주거지역" == "제2종일반주거지역")
        var normalizedZone = zoneName.Replace(" ", string.Empty);
        return r.ApplicableZones.Any(z =>
            z.Replace(" ", string.Empty).Equals(normalizedZone, StringComparison.Ordinal));
    }

    // ── 변환 헬퍼 ─────────────────────────────────────────────────────────────

    private static ReviewItemDto ToDto(ReviewItemRuleRecord r) => new()
    {
        RuleId          = r.Id,
        Category        = r.Category,
        Title           = r.Title,
        Description     = r.Description,
        RequiredInputs  = r.RequiredInputs,
        RelatedLaws     = r.RelatedLaws,
        IsAutoCheckable = r.IsAutoCheckable,
        Priority        = r.Priority,
    };
}
