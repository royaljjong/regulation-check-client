using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Rules;
using AutomationRawCheck.Application.UseProfiles;
using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Centralizes /review response composition so the controller remains thin.
/// </summary>
public static class ReviewResponseComposer
{
    public static BuildingReviewResponseDto Compose(
        BuildingReviewRequestDto request,
        ReviewLevel reviewLevel,
        string? zoneName,
        string? zoneCode,
        bool? districtUnitPlan,
        bool? developmentRestriction,
        bool? developmentActionRestriction,
        LocationSummaryDto location,
        ZoningSummaryDto? zoning,
        OverlaySummaryDto overlays,
        IReadOnlyList<ReviewItemDto> reviewItems,
        IReadOnlyList<ReviewItemRuleRecord> allRules,
        InputSummaryDto inputSummary,
        long elapsedMs)
    {
        return Compose(
            request,
            reviewLevel,
            zoneName,
            zoneCode,
            districtUnitPlan,
            developmentRestriction,
            developmentActionRestriction,
            location,
            zoning,
            overlays,
            reviewItems,
            allRules,
            Array.Empty<LawReference>(),
            inputSummary,
            elapsedMs);
    }

    public static BuildingReviewResponseDto Compose(
        BuildingReviewRequestDto request,
        ReviewLevel reviewLevel,
        string? zoneName,
        string? zoneCode,
        bool? districtUnitPlan,
        bool? developmentRestriction,
        bool? developmentActionRestriction,
        LocationSummaryDto location,
        ZoningSummaryDto? zoning,
        OverlaySummaryDto overlays,
        IReadOnlyList<ReviewItemDto> reviewItems,
        IReadOnlyList<ReviewItemRuleRecord> allRules,
        IReadOnlyList<LawReference> zoningLawReferences,
        InputSummaryDto inputSummary,
        long elapsedMs)
    {
        var frame = ReviewEngineFrameFactory.Create(
            request,
            zoneName,
            zoneCode,
            districtUnitPlan,
            developmentRestriction,
            developmentActionRestriction,
            overlays.DevelopmentActionRestrictionDetail);

        UseProfileRegistry.TryGet(request.SelectedUse, out var useProfile);
        var ruleCoverage = useProfile is null
            ? null
            : UseProfileRuleCoverageAnalyzer.Analyze(request.SelectedUse);

        var reviewItemList = reviewItems.ToList();
        var tasks = TaskLayerMapper.Map(reviewItemList).ToList();
        AppendCoverageFallbackTask(tasks, useProfile, ruleCoverage, reviewItemList.Count);
        AppendDevelopmentActionFollowUpTask(tasks, overlays.DevelopmentActionRestrictionDetail);

        var checklist = TaskLayerMapper.Summarize(tasks);
        var activeRuleBundles = RuleBundleResolver.Summarize(useProfile, reviewItemList);
        var nextLevelHint = NextLevelHintBuilder.Build(
            request.SelectedUse,
            reviewLevel,
            request.BuildingInputs,
            allRules);

        var manualReviews = ManualReviewLayerBuilder.Build(
            useProfile,
            reviewItemList,
            request.BuildingInputs,
            districtUnitPlan,
            developmentActionRestriction,
            overlays.DevelopmentActionRestrictionDetail).ToList();
        AppendCoverageFallbackManualReview(manualReviews, useProfile, ruleCoverage, reviewItemList.Count);

        var ordinanceReviews = OrdinanceLayerBuilder.Build(
            useProfile,
            location.ResolvedAddress ?? location.InputAddress,
            zoneName,
            districtUnitPlan,
            developmentActionRestriction,
            urbanPlanningFacility: frame.PlanningContext.IsInUrbanPlanningFacility,
            developmentActionDetail: overlays.DevelopmentActionRestrictionDetail);

        var (applicableLaws, reviewTriggers) = ApplicableLawCatalogBuilder.Build(
            request.SelectedUse,
            useProfile,
            zoning,
            overlays,
            reviewItemList,
            tasks.Select(MapTaskDto).ToList(),
            manualReviews,
            ordinanceReviews.ToList(),
            zoningLawReferences,
            request.BuildingInputs);

        var reportPreview = ReportPreviewBuilder.Build(
            request,
            useProfile,
            location,
            zoning,
            overlays,
            reviewItemList,
            tasks,
            manualReviews,
            ordinanceReviews);

        return new BuildingReviewResponseDto
        {
            ReviewLevel = ReviewLevelDetector.LevelToString(reviewLevel),
            SelectedUse = request.SelectedUse,
            UseProfile = MapUseProfileDto(useProfile, ruleCoverage),
            Location = location,
            Zoning = zoning,
            Overlays = overlays,
            ReviewItems = reviewItemList,
            ApplicableLaws = applicableLaws,
            ReviewTriggers = reviewTriggers.ToList(),
            Tasks = tasks.Select(MapTaskDto).ToList(),
            ActiveRuleBundles = activeRuleBundles.ToList(),
            Checklist = MapChecklistDto(checklist),
            ManualReviews = manualReviews,
            OrdinanceReviews = ordinanceReviews.ToList(),
            ReportPreview = reportPreview,
            DevelopmentActionApiStatus = MapApiVerificationStatus(overlays.DevelopmentActionRestrictionDetail),
            InputSummary = inputSummary,
            NextLevelHint = nextLevelHint,
            ElapsedMs = elapsedMs,
        };
    }

    private static UseProfileSummaryDto? MapUseProfileDto(
        UseProfileDefinition? profile,
        UseProfileRuleCoverageSummary? ruleCoverage)
    {
        if (profile is null)
            return null;

        return new UseProfileSummaryDto
        {
            Key = profile.Identity.Key,
            DisplayName = profile.Identity.DisplayName,
            FunctionalGroup = profile.Identity.FunctionalGroup,
            AutomationCoverage = profile.Coverage.ToString().ToLowerInvariant(),
            RuleDataStatus = ruleCoverage?.RuleDataStatus ?? "profile_only",
            ReviewRuleCount = ruleCoverage?.ReviewRuleCount ?? 0,
            LawLayerRuleCount = ruleCoverage?.LawLayerRuleCount ?? 0,
            ExplicitReviewRuleCount = ruleCoverage?.ExplicitReviewRuleCount ?? 0,
            ExplicitLawLayerRuleCount = ruleCoverage?.ExplicitLawLayerRuleCount ?? 0,
            SeedReviewRuleCount = ruleCoverage?.SeedReviewRuleCount ?? 0,
            SeedLawLayerRuleCount = ruleCoverage?.SeedLawLayerRuleCount ?? 0,
            FallbackReviewRuleCount = ruleCoverage?.FallbackReviewRuleCount ?? 0,
            FallbackLawLayerRuleCount = ruleCoverage?.FallbackLawLayerRuleCount ?? 0,
            UsesSeed = ruleCoverage?.UsesSeed ?? false,
            UsesFallback = ruleCoverage?.UsesFallback ?? false,
            CoverageNote = ruleCoverage?.CoverageNote,
            LegalSearchHints = profile.LegalSearchHints.ToList(),
            RuleBundles = profile.RuleBundles.ToList(),
        };
    }

    private static void AppendCoverageFallbackTask(
        ICollection<ReviewTask> tasks,
        UseProfileDefinition? profile,
        UseProfileRuleCoverageSummary? ruleCoverage,
        int reviewItemCount)
    {
        if (!NeedsCoverageFallback(profile, ruleCoverage, reviewItemCount))
            return;

        tasks.Add(new ReviewTask
        {
            TaskId = $"coverage-{profile!.Identity.Key}",
            Category = "엔진연결",
            Title = $"{profile.Identity.DisplayName} 규칙 데이터 연결 보강 필요",
            Action = "현재 단계에서는 자동판정 범위가 제한되므로 수동검토 카드와 조례 확인을 우선 진행하세요.",
            Status = AutomationRawCheck.Domain.Models.TaskStatus.ManualReview,
            Reason = ruleCoverage!.CoverageNote,
            RelatedRuleIds = [],
            Priority = "high",
        });
    }

    private static void AppendCoverageFallbackManualReview(
        ICollection<ManualReviewCardDto> manualReviews,
        UseProfileDefinition? profile,
        UseProfileRuleCoverageSummary? ruleCoverage,
        int reviewItemCount)
    {
        if (!NeedsCoverageFallback(profile, ruleCoverage, reviewItemCount))
            return;

        var requiredInputs = profile!.RequiredInputsByLevel
            .Where(entry => entry.Key is "standard" or "detailed")
            .SelectMany(entry => entry.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        manualReviews.Add(new ManualReviewCardDto
        {
            ManualReviewId = $"coverage-{profile.Identity.Key}",
            Category = "엔진연결",
            Title = $"{profile.Identity.DisplayName} 자동화 연결 범위 수동 보강",
            Prompt = ruleCoverage!.CoverageNote,
            Status = "manual_review",
            SourceType = ruleCoverage.UsesFallback ? "coverage_fallback" : "coverage_seed",
            RelatedRuleIds = [],
            RequiredInputs = requiredInputs,
            SuggestedChecks =
            [
                "review_item_rules 연결 여부 확인",
                "law_layer_rules 연결 여부 확인",
                "관련 조례와 상위 법령 검토 결과를 별도 기록",
            ],
            SearchHints = profile.LegalSearchHints.ToList(),
        });
    }

    private static void AppendDevelopmentActionFollowUpTask(
        ICollection<ReviewTask> tasks,
        OverlayDecisionDto? developmentActionDetail)
    {
        if (developmentActionDetail is null)
        {
            tasks.Add(new ReviewTask
            {
                TaskId = "development-action-api-readiness",
                Category = "인허가",
                Title = "개발행위허가 API 설정 확인",
                Action = "debug config/parse-sample/probe 경로로 API 설정과 응답 path를 우선 검증",
                Status = AutomationRawCheck.Domain.Models.TaskStatus.ManualReview,
                Reason = "개발행위허가 상세 결과가 응답에 존재하지 않습니다.",
                RelatedRuleIds = [],
                Priority = "high",
            });
            return;
        }

        if (string.Equals(developmentActionDetail.Source, "shp", StringComparison.OrdinalIgnoreCase))
        {
            tasks.Add(new ReviewTask
            {
                TaskId = "development-action-api-fallback",
                Category = "인허가",
                Title = "개발행위허가 API fallback 결과 대조",
                Action = "동일 좌표로 API probe 결과를 확인하고 SHP fallback과 차이를 비교",
                Status = AutomationRawCheck.Domain.Models.TaskStatus.ManualReview,
                Reason = developmentActionDetail.Note ?? "현재 개발행위허가 결과가 SHP fallback 기준입니다.",
                RelatedRuleIds = [],
                Priority = "medium",
            });
        }
        else if (string.Equals(developmentActionDetail.Source, "none", StringComparison.OrdinalIgnoreCase))
        {
            tasks.Add(new ReviewTask
            {
                TaskId = "development-action-api-unverified",
                Category = "인허가",
                Title = "개발행위허가 1차 확인 필요",
                Action = "API 또는 담당 부서 회신으로 개발행위허가 필요 여부를 먼저 확인",
                Status = AutomationRawCheck.Domain.Models.TaskStatus.ManualReview,
                Reason = developmentActionDetail.Note ?? "개발행위허가 결과를 자동 확인하지 못했습니다.",
                RelatedRuleIds = [],
                Priority = "high",
            });
        }
    }

    private static bool NeedsCoverageFallback(
        UseProfileDefinition? profile,
        UseProfileRuleCoverageSummary? ruleCoverage,
        int reviewItemCount)
    {
        return profile is not null &&
               ruleCoverage is not null &&
               reviewItemCount == 0 &&
               !string.Equals(ruleCoverage.RuleDataStatus, "connected", StringComparison.Ordinal);
    }

    private static ReviewTaskDto MapTaskDto(ReviewTask task) => new()
    {
        TaskId = task.TaskId,
        Category = task.Category,
        Title = task.Title,
        Action = task.Action,
        Status = task.Status.ToString().ToLowerInvariant(),
        Reason = task.Reason,
        RelatedRuleIds = task.RelatedRuleIds.ToList(),
        Priority = task.Priority,
    };

    private static ProjectChecklistDto MapChecklistDto(ProjectChecklistSummary summary) => new()
    {
        Fail = summary.Fail,
        Warning = summary.Warning,
        Ok = summary.Ok,
        Info = summary.Info,
        ManualReview = summary.ManualReview,
    };

    private static ApiVerificationStatusDto MapApiVerificationStatus(OverlayDecisionDto? overlay) => new()
    {
        Source = overlay?.Source ?? "none",
        Status = overlay?.Status ?? "unavailable",
        Confidence = overlay?.Confidence ?? "low",
        Note = overlay?.Note,
    };
}
