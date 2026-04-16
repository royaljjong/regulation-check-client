using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

internal static class ReviewSnapshotComparer
{
    public static ReviewSnapshotCompareResponseDto Compare(ReviewSnapshotDto left, ReviewSnapshotDto right)
    {
        var leftHighPriorityTasks = left.ReportPackage.Tasks
            .Where(static task => string.Equals(task.Priority, "high", StringComparison.OrdinalIgnoreCase))
            .Select(static task => task.Title)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var rightHighPriorityTasks = right.ReportPackage.Tasks
            .Where(static task => string.Equals(task.Priority, "high", StringComparison.OrdinalIgnoreCase))
            .Select(static task => task.Title)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var leftManualReviews = left.ReportPackage.ManualReviews
            .Select(static card => card.Title)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var rightManualReviews = right.ReportPackage.ManualReviews
            .Select(static card => card.Title)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var categoryDiffs = left.ReportPackage.Tasks
            .Select(static task => task.Category)
            .Concat(right.ReportPackage.Tasks.Select(static task => task.Category))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static category => category, StringComparer.Ordinal)
            .Select(category =>
            {
                var leftCount = left.ReportPackage.Tasks.Count(task => string.Equals(task.Category, category, StringComparison.Ordinal));
                var rightCount = right.ReportPackage.Tasks.Count(task => string.Equals(task.Category, category, StringComparison.Ordinal));
                return new ReviewTaskCategoryDiffDto
                {
                    Category = category,
                    LeftCount = leftCount,
                    RightCount = rightCount,
                    Delta = rightCount - leftCount,
                };
            })
            .ToList();

        var checklistDiff = new ReviewSnapshotChecklistDiffDto
        {
            FailDelta = right.Checklist.Fail - left.Checklist.Fail,
            WarningDelta = right.Checklist.Warning - left.Checklist.Warning,
            OkDelta = right.Checklist.Ok - left.Checklist.Ok,
            InfoDelta = right.Checklist.Info - left.Checklist.Info,
            ManualReviewDelta = right.Checklist.ManualReview - left.Checklist.ManualReview,
        };

        var sameSelectedUse = string.Equals(left.SelectedUse, right.SelectedUse, StringComparison.Ordinal);
        var sameReviewLevel = string.Equals(left.ReviewLevel, right.ReviewLevel, StringComparison.Ordinal);
        var sameDevelopmentActionStatus =
            string.Equals(left.DevelopmentActionApiStatus.Source, right.DevelopmentActionApiStatus.Source, StringComparison.Ordinal) &&
            string.Equals(left.DevelopmentActionApiStatus.Status, right.DevelopmentActionApiStatus.Status, StringComparison.Ordinal);

        return new ReviewSnapshotCompareResponseDto
        {
            Left = MapSummary(left),
            Right = MapSummary(right),
            ChecklistDiff = checklistDiff,
            SameSelectedUse = sameSelectedUse,
            SameReviewLevel = sameReviewLevel,
            SameDevelopmentActionStatus = sameDevelopmentActionStatus,
            SummaryLines = BuildSummaryLines(checklistDiff, left, right, sameSelectedUse, sameReviewLevel, sameDevelopmentActionStatus, categoryDiffs),
            TaskCategoryDiffs = categoryDiffs,
            AddedHighPriorityTasks = rightHighPriorityTasks.Except(leftHighPriorityTasks, StringComparer.Ordinal).OrderBy(static x => x).ToList(),
            RemovedHighPriorityTasks = leftHighPriorityTasks.Except(rightHighPriorityTasks, StringComparer.Ordinal).OrderBy(static x => x).ToList(),
            AddedManualReviews = rightManualReviews.Except(leftManualReviews, StringComparer.Ordinal).OrderBy(static x => x).ToList(),
            RemovedManualReviews = leftManualReviews.Except(rightManualReviews, StringComparer.Ordinal).OrderBy(static x => x).ToList(),
        };
    }

    public static ReviewSnapshotSummaryDto MapSummary(ReviewSnapshotDto snapshot) => new()
    {
        SnapshotId = snapshot.SnapshotId,
        ProjectId = snapshot.ProjectId,
        ScenarioId = snapshot.ScenarioId,
        VersionTag = snapshot.VersionTag,
        CreatedAt = snapshot.CreatedAt,
        SelectedUse = snapshot.SelectedUse,
        ReviewLevel = snapshot.ReviewLevel,
        Address = snapshot.Address,
        DevelopmentActionApiStatus = snapshot.DevelopmentActionApiStatus,
        Checklist = snapshot.Checklist,
        TaskCount = snapshot.TaskCount,
        HighPriorityTaskCount = snapshot.HighPriorityTaskCount,
        ManualReviewCount = snapshot.ManualReviewCount,
    };

    private static List<string> BuildSummaryLines(
        ReviewSnapshotChecklistDiffDto checklistDiff,
        ReviewSnapshotDto left,
        ReviewSnapshotDto right,
        bool sameSelectedUse,
        bool sameReviewLevel,
        bool sameDevelopmentActionStatus,
        IReadOnlyList<ReviewTaskCategoryDiffDto> taskCategoryDiffs)
    {
        var lines = new List<string>
        {
            $"fail {checklistDiff.FailDelta:+#;-#;0}, warning {checklistDiff.WarningDelta:+#;-#;0}, manual_review {checklistDiff.ManualReviewDelta:+#;-#;0}",
        };

        if (!sameSelectedUse)
            lines.Add($"용도 변경: {left.SelectedUse} -> {right.SelectedUse}");

        if (!sameReviewLevel)
            lines.Add($"검토 단계 변경: {left.ReviewLevel} -> {right.ReviewLevel}");

        if (!sameDevelopmentActionStatus)
            lines.Add($"개발행위 API 상태 변경: {left.DevelopmentActionApiStatus.Source}/{left.DevelopmentActionApiStatus.Status} -> {right.DevelopmentActionApiStatus.Source}/{right.DevelopmentActionApiStatus.Status}");

        foreach (var diff in taskCategoryDiffs.Where(static diff => diff.Delta != 0))
            lines.Add($"{diff.Category}: {diff.LeftCount} -> {diff.RightCount} ({diff.Delta:+#;-#;0})");

        return lines;
    }
}
