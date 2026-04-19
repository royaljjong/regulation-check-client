using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.UseProfiles;
using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Services;

public static class ApplicableLawCatalogBuilder
{
    private sealed record SectionSpec(string Id, string Title);

    private static readonly IReadOnlyList<SectionSpec> SectionOrder =
    [
        new("land_use", "토지이용·용도지역"),
        new("building_code", "건축 기본 법규"),
        new("safety_mep", "피난·방화·설비 트리거"),
        new("committee", "심의·협의·인허가"),
        new("ordinance", "조례·지구단위계획"),
    ];

    public static (ApplicableLawCatalogDto Catalog, IReadOnlyList<ReviewTriggerDto> Triggers) Build(
        string selectedUse,
        UseProfileDefinition? useProfile,
        ZoningSummaryDto? zoning,
        OverlaySummaryDto overlays,
        IReadOnlyList<ReviewItemDto> reviewItems,
        IReadOnlyList<ReviewTaskDto> tasks,
        IReadOnlyList<ManualReviewCardDto> manualReviews,
        IReadOnlyList<OrdinanceReviewCardDto> ordinanceReviews,
        IReadOnlyList<LawReference> zoningLawReferences,
        BuildingInputsDto? inputs)
    {
        var sections = SectionOrder.ToDictionary(
            static section => section.Id,
            static section => new ApplicableLawSectionDto
            {
                SectionId = section.Id,
                Title = section.Title,
            },
            StringComparer.Ordinal);

        var itemsBySection = sections.Keys.ToDictionary(
            static sectionId => sectionId,
            static _ => new List<ApplicableLawItemDto>(),
            StringComparer.Ordinal);

        foreach (var lawRef in zoningLawReferences)
        {
            itemsBySection["land_use"].Add(new ApplicableLawItemDto
            {
                ItemId = $"law-ref-{Sanitize(lawRef.LawName)}-{Sanitize(lawRef.ArticleRef)}",
                SourceType = "law_reference",
                Category = "land_use",
                Title = lawRef.LawName,
                Summary = string.IsNullOrWhiteSpace(lawRef.Note) ? lawRef.ArticleRef : $"{lawRef.ArticleRef} · {lawRef.Note}",
                Status = "applicable",
                LawName = lawRef.LawName,
                ArticleRef = lawRef.ArticleRef,
                Link = lawRef.Url,
            });
        }

        foreach (var reviewItem in reviewItems)
        {
            var sectionId = MapSectionId(reviewItem.Category);
            itemsBySection[sectionId].Add(new ApplicableLawItemDto
            {
                ItemId = reviewItem.RuleId ?? $"review-item-{Sanitize(reviewItem.Title)}",
                SourceType = "review_item",
                Category = reviewItem.Category,
                Title = reviewItem.Title,
                Summary = reviewItem.JudgeNote ?? reviewItem.Description,
                Status = MapReviewItemStatus(reviewItem.JudgeStatus ?? "pending"),
                RelatedLaws = reviewItem.RelatedLaws.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RelatedRuleIds = reviewItem.RuleId is null ? [] : [reviewItem.RuleId],
                RequiredInputs = reviewItem.RequiredInputs.ToList(),
            });
        }

        foreach (var manualReview in manualReviews)
        {
            itemsBySection[MapManualSectionId(manualReview)].Add(new ApplicableLawItemDto
            {
                ItemId = manualReview.ManualReviewId,
                SourceType = manualReview.SourceType,
                Category = manualReview.Category,
                Title = manualReview.Title,
                Summary = manualReview.Prompt,
                Status = manualReview.Status,
                RelatedRuleIds = manualReview.RelatedRuleIds.ToList(),
                RequiredInputs = manualReview.RequiredInputs.ToList(),
            });
        }

        foreach (var ordinance in ordinanceReviews)
        {
            itemsBySection["ordinance"].Add(new ApplicableLawItemDto
            {
                ItemId = ordinance.OrdinanceId,
                SourceType = ordinance.SourceType,
                Category = "ordinance",
                Title = ordinance.Title,
                Summary = string.Join(" / ", ordinance.CheckItems.Where(static item => !string.IsNullOrWhiteSpace(item))),
                Status = "manual_review",
                Link = ordinance.Link,
                RelatedLaws = ordinance.Keywords.ToList(),
            });
        }

        foreach (var section in SectionOrder)
        {
            sections[section.Id].Items.AddRange(
                itemsBySection[section.Id]
                    .GroupBy(static item => item.ItemId, StringComparer.Ordinal)
                    .Select(static group => group.First())
                    .OrderBy(static item => item.Title, StringComparer.Ordinal));
        }

        var triggers = BuildTriggers(selectedUse, zoning, overlays, tasks, inputs);
        var summaryLines = new List<string>
        {
            $"Selected use: {selectedUse}",
            $"Applicable law items: {sections.Values.Sum(static section => section.Items.Count)}",
            $"Review triggers: {triggers.Count}",
        };

        if (!string.IsNullOrWhiteSpace(zoning?.ZoneName))
            summaryLines.Add($"Zoning: {zoning.ZoneName}");

        if (useProfile is not null)
            summaryLines.Add($"Rule bundles: {useProfile.RuleBundles.Count}");

        return (new ApplicableLawCatalogDto
        {
            Sections = SectionOrder
                .Select(section => sections[section.Id])
                .Where(static section => section.Items.Count > 0)
                .ToList(),
            SummaryLines = summaryLines,
        }, triggers);
    }

    private static IReadOnlyList<ReviewTriggerDto> BuildTriggers(
        string selectedUse,
        ZoningSummaryDto? zoning,
        OverlaySummaryDto overlays,
        IReadOnlyList<ReviewTaskDto> tasks,
        BuildingInputsDto? inputs)
    {
        var triggers = new List<ReviewTriggerDto>();

        triggers.AddRange(tasks
            .Where(static task => task.Status is "manual_review" or "warning" or "fail")
            .Select(task => new ReviewTriggerDto
            {
                TriggerId = task.TaskId,
                Category = task.Category,
                Title = task.Title,
                Status = task.Status,
                Basis = task.Reason,
            }));

        if (overlays.DistrictUnitPlan == true)
        {
            triggers.Add(new ReviewTriggerDto
            {
                TriggerId = "trigger-district-unit-plan",
                Category = "district_plan",
                Title = "지구단위계획 개별 지침 검토",
                Status = "manual_review",
                Basis = "대지가 지구단위계획구역에 포함되어 결정도·시행지침 우선 검토가 필요합니다.",
                RequiredInputs = ["hasDistrictUnitPlanDocument"],
            });
        }

        if (overlays.DevelopmentActionRestrictionDetail?.Source is "shp" or "none")
        {
            triggers.Add(new ReviewTriggerDto
            {
                TriggerId = "trigger-development-action",
                Category = "permit",
                Title = "개발행위허가 사전 검토",
                Status = "manual_review",
                Basis = overlays.DevelopmentActionRestrictionDetail?.Note ?? "개발행위허가 API 확인 전 단계입니다.",
                RequiredInputs = ["hasDevActRestrictionConsult"],
            });
        }

        if (inputs?.FloorArea is >= 500)
        {
            triggers.Add(new ReviewTriggerDto
            {
                TriggerId = "trigger-energy-plan",
                Category = "energy",
                Title = "에너지 절약계획서 대상 검토",
                Status = "candidate",
                Basis = $"연면적 {inputs.FloorArea:0.#}㎡ 기준으로 에너지 관련 제출 대상 여부를 우선 검토합니다.",
            });
        }

        if (inputs?.FloorCount is >= 3)
        {
            triggers.Add(new ReviewTriggerDto
            {
                TriggerId = "trigger-egress",
                Category = "egress",
                Title = "직통계단·피난 동선 검토",
                Status = "candidate",
                Basis = $"{inputs.FloorCount}층 계획으로 계단 및 피난 체계 검토가 필요합니다.",
            });
        }

        if ((inputs?.FloorCount ?? 0) >= 6 || (inputs?.BuildingHeight ?? 0) > 31)
        {
            triggers.Add(new ReviewTriggerDto
            {
                TriggerId = "trigger-elevator",
                Category = "elevator",
                Title = "승강기·비상용승강기 검토",
                Status = "candidate",
                Basis = "층수 또는 높이 기준으로 승강기 계획 검토가 필요합니다.",
            });
        }

        if (selectedUse is "물류시설" or "창고시설" or "공장" && inputs?.FloorArea is >= 55000)
        {
            triggers.Add(new ReviewTriggerDto
            {
                TriggerId = "trigger-traffic-impact",
                Category = "committee",
                Title = "교통·물류 영향 검토",
                Status = "candidate",
                Basis = $"연면적 {inputs.FloorArea:0.#}㎡ 규모의 {selectedUse} 계획으로 교통·물류 영향 검토 후보입니다.",
            });
        }

        if (!string.IsNullOrWhiteSpace(zoning?.ZoneName) &&
            zoning.ZoneName.Contains("준공업", StringComparison.OrdinalIgnoreCase))
        {
            triggers.Add(new ReviewTriggerDto
            {
                TriggerId = "trigger-semi-industrial-ordinance",
                Category = "ordinance",
                Title = "준공업지역 조례·지침 확인",
                Status = "manual_review",
                Basis = $"{zoning.ZoneName}에서는 조례 및 지구단위계획에 따라 상한·허용용도 실적용이 달라질 수 있습니다.",
            });
        }

        return triggers
            .GroupBy(static trigger => trigger.TriggerId, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToList();
    }

    private static string MapSectionId(string? category) => category switch
    {
        "허용용도" => "land_use",
        "지구단위계획" => "ordinance",
        "중첩규제" => "committee",
        "밀도" => "building_code",
        "도로/건축선" => "building_code",
        "주차" => "building_code",
        "피난/계단" => "safety_mep",
        "승강기" => "safety_mep",
        "방화" => "safety_mep",
        _ => "building_code",
    };

    private static string MapManualSectionId(ManualReviewCardDto manualReview) => manualReview.Category switch
    {
        "엔진연결" => "committee",
        "조례" => "ordinance",
        "인허가" => "committee",
        _ => manualReview.Title.Contains("지구단위", StringComparison.OrdinalIgnoreCase)
            ? "ordinance"
            : "committee",
    };

    private static string MapReviewItemStatus(string judgeStatus) => judgeStatus switch
    {
        "active" => "auto_check",
        "pending" => "needs_input",
        _ => "reference",
    };

    private static string Sanitize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "item" : new string(chars).ToLowerInvariant();
    }
}
