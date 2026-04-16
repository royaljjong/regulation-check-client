using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.UseProfiles;
using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Projects review results into a report-friendly structure with fixed sections and keyed fields.
/// </summary>
public static class ReportPreviewBuilder
{
    public static ReportPreviewDto Build(
        BuildingReviewRequestDto request,
        UseProfileDefinition? profile,
        LocationSummaryDto location,
        ZoningSummaryDto? zoning,
        OverlaySummaryDto overlays,
        IReadOnlyList<ReviewItemDto> reviewItems,
        IReadOnlyList<ReviewTask> tasks,
        IReadOnlyList<ManualReviewCardDto> manualReviews,
        IReadOnlyList<OrdinanceReviewCardDto> ordinanceReviews)
    {
        return new ReportPreviewDto
        {
            SchemaVersion = "report_preview_v1",
            Title = $"{request.SelectedUse} 검토 보고서",
            Sections =
            [
                BuildSiteOverview(location, zoning, overlays),
                BuildLawOverview(profile, zoning),
                BuildAutoReview(reviewItems),
                BuildTaskSection(tasks),
                BuildManualReviewSection(manualReviews),
                BuildOrdinanceSection(ordinanceReviews),
                BuildRecommendationSection(tasks, manualReviews),
                BuildLegalBasisSection(reviewItems),
            ],
            LegalBasisEntries = BuildLegalBasisEntries(reviewItems),
        };
    }

    private static ReportSectionDto BuildSiteOverview(
        LocationSummaryDto location,
        ZoningSummaryDto? zoning,
        OverlaySummaryDto overlays)
    {
        return new ReportSectionDto
        {
            Order = 1,
            SectionId = "site_overview",
            Title = "대지개요",
            Fields =
            [
                Field("input_address", "입력 주소", location.InputAddress ?? "좌표 입력"),
                Field("resolved_address", "해석 주소", location.ResolvedAddress ?? "미확인"),
                Field("zone_name", "용도지역", zoning?.ZoneName ?? "미확인"),
                Field("district_unit_plan", "지구단위계획", BoolLabel(overlays.DistrictUnitPlan)),
                Field("development_restriction", "개발제한구역", BoolLabel(overlays.DevelopmentRestriction)),
                Field("development_action", "개발행위허가 검토", BoolLabel(overlays.DevelopmentActionRestriction)),
                Field("development_action_source", "개발행위허가 출처", overlays.DevelopmentActionRestrictionDetail?.Source ?? "none"),
                Field("development_action_status", "개발행위허가 상태", overlays.DevelopmentActionRestrictionDetail?.Status ?? "unavailable"),
                Field("development_action_confidence", "개발행위허가 신뢰도", overlays.DevelopmentActionRestrictionDetail?.Confidence ?? "low"),
                Field("development_action_note", "개발행위허가 메모", overlays.DevelopmentActionRestrictionDetail?.Note ?? "미확인"),
            ],
            Highlights =
            [
                $"용도지역 {zoning?.ZoneName ?? "미확인"}",
                $"지구단위계획 {BoolLabel(overlays.DistrictUnitPlan)}",
                $"개발행위허가 검토 {BoolLabel(overlays.DevelopmentActionRestriction)}",
                $"개발행위허가 판정: {overlays.DevelopmentActionRestrictionDetail?.Source ?? "none"} / {overlays.DevelopmentActionRestrictionDetail?.Status ?? "unavailable"}",
            ],
        };
    }

    private static ReportSectionDto BuildLawOverview(
        UseProfileDefinition? profile,
        ZoningSummaryDto? zoning)
    {
        var ruleBundles = profile?.RuleBundles.ToList() ?? [];

        return new ReportSectionDto
        {
            Order = 2,
            SectionId = "law_overview",
            Title = "법규",
            Fields =
            [
                Field("use_key", "UseProfile", profile?.Identity.Key ?? "미확인"),
                Field("functional_group", "기능형 그룹", profile?.Identity.FunctionalGroup ?? "미확인"),
                Field("automation_coverage", "자동화 범위", profile is null ? "unknown" : profile.Coverage.ToString().ToLowerInvariant()),
                Field("rule_bundles", "적용 번들", JoinOrNone(ruleBundles)),
                Field("bcr_limit_pct", "건폐율 상한", Pct(zoning?.BcRatioLimitPct)),
                Field("far_limit_pct", "용적률 상한", Pct(zoning?.FarLimitPct)),
                Field("zoning_note", "법규 메모", zoning?.Note ?? "미확인"),
            ],
            Highlights =
            [
                $"기능형 그룹: {profile?.Identity.FunctionalGroup ?? "미확인"}",
                $"적용 번들: {JoinOrNone(ruleBundles)}",
                $"건폐율/용적률 {Pct(zoning?.BcRatioLimitPct)} / {Pct(zoning?.FarLimitPct)}",
            ],
        };
    }

    private static ReportSectionDto BuildAutoReview(IReadOnlyList<ReviewItemDto> reviewItems)
    {
        var activeCount = reviewItems.Count(static item => string.Equals(item.JudgeStatus, "active", StringComparison.Ordinal));
        var referenceCount = reviewItems.Count(static item => string.Equals(item.JudgeStatus, "reference", StringComparison.Ordinal));
        var pendingCount = reviewItems.Count(static item => string.Equals(item.JudgeStatus, "pending", StringComparison.Ordinal));

        return new ReportSectionDto
        {
            Order = 3,
            SectionId = "auto_review",
            Title = "자동판정",
            Fields =
            [
                Field("review_item_count", "검토항목 수", reviewItems.Count.ToString()),
                Field("active_count", "active 수", activeCount.ToString()),
                Field("reference_count", "reference 수", referenceCount.ToString()),
                Field("pending_count", "pending 수", pendingCount.ToString()),
            ],
            Highlights =
            [
                $"검토항목 {reviewItems.Count}건",
                $"active {activeCount} / reference {referenceCount} / pending {pendingCount}",
            ],
        };
    }

    private static ReportSectionDto BuildTaskSection(IReadOnlyList<ReviewTask> tasks)
    {
        var failCount = tasks.Count(static task => task.Status == AutomationRawCheck.Domain.Models.TaskStatus.Fail);
        var warningCount = tasks.Count(static task => task.Status == AutomationRawCheck.Domain.Models.TaskStatus.Warning);
        var okCount = tasks.Count(static task => task.Status == AutomationRawCheck.Domain.Models.TaskStatus.Ok);
        var infoCount = tasks.Count(static task => task.Status == AutomationRawCheck.Domain.Models.TaskStatus.Info);
        var manualReviewCount = tasks.Count(static task => task.Status == AutomationRawCheck.Domain.Models.TaskStatus.ManualReview);
        var highPriorityCount = tasks.Count(static task => string.Equals(task.Priority, "high", StringComparison.OrdinalIgnoreCase));

        return new ReportSectionDto
        {
            Order = 4,
            SectionId = "tasks",
            Title = "Task",
            Fields =
            [
                Field("task_count", "Task 수", tasks.Count.ToString()),
                Field("fail_count", "fail 수", failCount.ToString()),
                Field("warning_count", "warning 수", warningCount.ToString()),
                Field("ok_count", "ok 수", okCount.ToString()),
                Field("info_count", "info 수", infoCount.ToString()),
                Field("manual_review_count", "manual_review 수", manualReviewCount.ToString()),
                Field("high_priority_count", "high 우선순위", highPriorityCount.ToString()),
            ],
            Highlights =
            [
                $"fail {failCount} / warning {warningCount} / manual_review {manualReviewCount}",
                $"high 우선순위 Task {highPriorityCount}건",
            ],
        };
    }

    private static ReportSectionDto BuildManualReviewSection(IReadOnlyList<ManualReviewCardDto> manualReviews)
    {
        var coverageCardCount = manualReviews.Count(static card => card.SourceType.StartsWith("coverage_", StringComparison.Ordinal));
        var apiUnavailableCount = manualReviews.Count(static card => string.Equals(card.SourceType, "api_unavailable", StringComparison.Ordinal));
        var apiFallbackCount = manualReviews.Count(static card => string.Equals(card.SourceType, "api_fallback_shp", StringComparison.Ordinal));
        var categories = manualReviews
            .Select(static card => card.Category)
            .Where(static category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ReportSectionDto
        {
            Order = 5,
            SectionId = "manual_review",
            Title = "수동검토",
            Fields =
            [
                Field("manual_review_card_count", "수동검토 카드 수", manualReviews.Count.ToString()),
                Field("coverage_card_count", "coverage 보강 카드 수", coverageCardCount.ToString()),
                Field("api_unavailable_count", "API 미연결 카드 수", apiUnavailableCount.ToString()),
                Field("api_fallback_count", "API fallback 카드 수", apiFallbackCount.ToString()),
                Field("manual_review_categories", "수동검토 카테고리", JoinOrNone(categories)),
            ],
            Highlights =
            [
                $"수동검토 카드 {manualReviews.Count}건",
                $"coverage 보강 카드 {coverageCardCount}건",
                $"개발행위허가 API 미연결 {apiUnavailableCount} / fallback {apiFallbackCount}",
            ],
        };
    }

    private static ReportSectionDto BuildOrdinanceSection(IReadOnlyList<OrdinanceReviewCardDto> ordinanceReviews)
    {
        var districtPlanCount = ordinanceReviews.Count(static card => string.Equals(card.SourceType, "district_unit_plan", StringComparison.Ordinal));
        var developmentActionCount = ordinanceReviews.Count(static card => card.SourceType.StartsWith("development_action", StringComparison.Ordinal));
        var developmentActionApiCount = ordinanceReviews.Count(static card => string.Equals(card.SourceType, "development_action_api", StringComparison.Ordinal));
        var developmentActionFallbackCount = ordinanceReviews.Count(static card => string.Equals(card.SourceType, "development_action_fallback", StringComparison.Ordinal));
        var developmentActionUnverifiedCount = ordinanceReviews.Count(static card => string.Equals(card.SourceType, "development_action_unverified", StringComparison.Ordinal));
        var urbanFacilityCount = ordinanceReviews.Count(static card => string.Equals(card.SourceType, "urban_planning_facility", StringComparison.Ordinal));
        var keywordCount = ordinanceReviews.Sum(static card => card.Keywords.Count);

        return new ReportSectionDto
        {
            Order = 6,
            SectionId = "ordinance",
            Title = "조례",
            Fields =
            [
                Field("ordinance_card_count", "조례 카드 수", ordinanceReviews.Count.ToString()),
                Field("district_unit_plan_count", "지구단위계획 확인 수", districtPlanCount.ToString()),
                Field("development_action_count", "개발행위허가 확인 수", developmentActionCount.ToString()),
                Field("development_action_api_count", "개발행위허가 API 확인 수", developmentActionApiCount.ToString()),
                Field("development_action_fallback_count", "개발행위허가 fallback 수", developmentActionFallbackCount.ToString()),
                Field("development_action_unverified_count", "개발행위허가 미확정 수", developmentActionUnverifiedCount.ToString()),
                Field("urban_planning_facility_count", "도시계획시설 확인 수", urbanFacilityCount.ToString()),
                Field("keyword_count", "탐색 키워드 수", keywordCount.ToString()),
            ],
            Highlights =
            [
                $"조례/행정 확인 카드 {ordinanceReviews.Count}건",
                $"지구단위계획 {districtPlanCount} / 개발행위허가 {developmentActionCount} / 도시계획시설 {urbanFacilityCount}",
                $"개발행위허가 API {developmentActionApiCount} / fallback {developmentActionFallbackCount} / 미확정 {developmentActionUnverifiedCount}",
            ],
        };
    }

    private static ReportSectionDto BuildRecommendationSection(
        IReadOnlyList<ReviewTask> tasks,
        IReadOnlyList<ManualReviewCardDto> manualReviews)
    {
        var prioritizedTasks = tasks
            .Where(static task => string.Equals(task.Priority, "high", StringComparison.OrdinalIgnoreCase))
            .Select(static task => task.Title)
            .Take(3)
            .ToList();

        var prioritizedManualReviews = manualReviews
            .Select(static card => card.Title)
            .Take(2)
            .ToList();

        var fields = new List<ReportFieldDto>();
        var highlights = new List<string>();

        for (var i = 0; i < prioritizedTasks.Count; i++)
        {
            fields.Add(Field($"top_task_{i + 1}", $"우선 조치 {i + 1}", prioritizedTasks[i]));
            highlights.Add($"우선 조치: {prioritizedTasks[i]}");
        }

        for (var i = 0; i < prioritizedManualReviews.Count; i++)
        {
            fields.Add(Field($"top_manual_review_{i + 1}", $"수동 확인 {i + 1}", prioritizedManualReviews[i]));
            highlights.Add($"수동 확인: {prioritizedManualReviews[i]}");
        }

        if (fields.Count == 0)
        {
            fields.Add(Field("summary", "요약", "즉시 수정 권고 없음"));
            highlights.Add("즉시 수정 권고 없음");
        }

        return new ReportSectionDto
        {
            Order = 7,
            SectionId = "recommendation",
            Title = "수정권고",
            Fields = fields,
            Highlights = highlights,
        };
    }

    private static ReportSectionDto BuildLegalBasisSection(IReadOnlyList<ReviewItemDto> reviewItems)
    {
        var itemsWithClauses = reviewItems.Count(static item => item.LegalBasisClauses is { Count: > 0 });
        var totalClauseCount = reviewItems.Sum(static item => item.LegalBasisClauses?.Count ?? 0);

        return new ReportSectionDto
        {
            Order = 8,
            SectionId = "legal_basis",
            Title = "근거조문",
            Fields =
            [
                Field("items_with_clauses", "근거조문 포함 항목 수", itemsWithClauses.ToString()),
                Field("total_clause_count", "총 조문 수", totalClauseCount.ToString()),
            ],
            Highlights =
            [
                $"근거조문 포함 항목 {itemsWithClauses}건",
                $"총 조문 {totalClauseCount}건",
            ],
        };
    }

    private static List<ReportLegalBasisEntryDto> BuildLegalBasisEntries(IReadOnlyList<ReviewItemDto> reviewItems)
    {
        return reviewItems
            .Where(static item => item.LegalBasisClauses is { Count: > 0 })
            .Select(item => new ReportLegalBasisEntryDto
            {
                RuleId = item.RuleId ?? item.Title,
                Title = item.Title,
                Clauses = item.LegalBasisClauses!.ToList(),
            })
            .ToList();
    }

    private static ReportFieldDto Field(string key, string label, string value) => new()
    {
        Key = key,
        Label = label,
        Value = value,
    };

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var materialized = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return materialized.Count == 0 ? "없음" : string.Join(", ", materialized);
    }

    private static string BoolLabel(bool? value) => value switch
    {
        true => "확인 필요",
        false => "없음",
        null => "미확인",
    };

    private static string Pct(double? value) => value.HasValue ? $"{value.Value:0.#}%" : "미확인";
}
