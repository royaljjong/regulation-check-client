using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

public static class ReviewSnapshotCompareReportBuilder
{
    public static ReviewSnapshotCompareReportPackageDto Build(ReviewSnapshotCompareResponseDto comparison)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var title = $"{comparison.Left.ProjectId} 대안 비교 보고서";
        var preview = new ReportPreviewDto
        {
            SchemaVersion = "review_compare_preview_v1",
            Title = title,
            Sections =
            [
                new ReportSectionDto
                {
                    Order = 1,
                    SectionId = "compare_overview",
                    Title = "비교 개요",
                    Fields =
                    [
                        new ReportFieldDto { Key = "left_snapshot_id", Label = "기준안 Snapshot", Value = comparison.Left.SnapshotId },
                        new ReportFieldDto { Key = "right_snapshot_id", Label = "비교안 Snapshot", Value = comparison.Right.SnapshotId },
                        new ReportFieldDto { Key = "left_version_tag", Label = "기준안 버전", Value = comparison.Left.VersionTag ?? "-" },
                        new ReportFieldDto { Key = "right_version_tag", Label = "비교안 버전", Value = comparison.Right.VersionTag ?? "-" },
                        new ReportFieldDto { Key = "left_created_at", Label = "기준안 생성시각", Value = comparison.Left.CreatedAt },
                        new ReportFieldDto { Key = "right_created_at", Label = "비교안 생성시각", Value = comparison.Right.CreatedAt },
                    ],
                    Highlights = comparison.SummaryLines.ToList(),
                },
                new ReportSectionDto
                {
                    Order = 2,
                    SectionId = "checklist_delta",
                    Title = "체크리스트 변화",
                    Fields =
                    [
                        new ReportFieldDto { Key = "fail_delta", Label = "Fail 변화", Value = FormatDelta(comparison.ChecklistDiff.FailDelta) },
                        new ReportFieldDto { Key = "warning_delta", Label = "Warning 변화", Value = FormatDelta(comparison.ChecklistDiff.WarningDelta) },
                        new ReportFieldDto { Key = "ok_delta", Label = "Ok 변화", Value = FormatDelta(comparison.ChecklistDiff.OkDelta) },
                        new ReportFieldDto { Key = "info_delta", Label = "Info 변화", Value = FormatDelta(comparison.ChecklistDiff.InfoDelta) },
                        new ReportFieldDto { Key = "manual_review_delta", Label = "Manual Review 변화", Value = FormatDelta(comparison.ChecklistDiff.ManualReviewDelta) },
                    ],
                },
                new ReportSectionDto
                {
                    Order = 3,
                    SectionId = "task_delta",
                    Title = "Task 변화",
                    Fields = comparison.TaskCategoryDiffs
                        .Select(diff => new ReportFieldDto
                        {
                            Key = $"task_delta_{SanitizeKey(diff.Category)}",
                            Label = diff.Category,
                            Value = $"{diff.LeftCount} -> {diff.RightCount} ({FormatDelta(diff.Delta)})",
                        })
                        .ToList(),
                    Highlights = BuildTaskHighlights(comparison),
                },
                new ReportSectionDto
                {
                    Order = 4,
                    SectionId = "manual_review_delta",
                    Title = "수동검토 변화",
                    Fields =
                    [
                        new ReportFieldDto { Key = "added_manual_reviews", Label = "추가 수동검토", Value = comparison.AddedManualReviews.Count.ToString() },
                        new ReportFieldDto { Key = "removed_manual_reviews", Label = "제거 수동검토", Value = comparison.RemovedManualReviews.Count.ToString() },
                    ],
                    Highlights = comparison.AddedManualReviews
                        .Select(item => $"추가: {item}")
                        .Concat(comparison.RemovedManualReviews.Select(item => $"제거: {item}"))
                        .ToList(),
                },
            ],
        };

        return new ReviewSnapshotCompareReportPackageDto
        {
            Metadata = new ReportPackageMetadataDto
            {
                PackageVersion = "review_compare_report_package_v1",
                PreviewSchemaVersion = preview.SchemaVersion,
                Title = title,
                SuggestedFileNameBase = $"{SanitizeKey(comparison.Left.ProjectId)}_compare_{timestamp:yyyyMMdd}",
                GeneratedAt = timestamp.ToString("O"),
                SupportedFormats = ["md", "pdf", "docx"],
                IntermediateFormats = ["md"],
                DevelopmentActionApiStatus = comparison.Right.DevelopmentActionApiStatus,
            },
            Preview = preview,
            Comparison = comparison,
        };
    }

    private static List<string> BuildTaskHighlights(ReviewSnapshotCompareResponseDto comparison)
    {
        return comparison.AddedHighPriorityTasks
            .Select(item => $"고우선 추가: {item}")
            .Concat(comparison.RemovedHighPriorityTasks.Select(item => $"고우선 제거: {item}"))
            .ToList();
    }

    private static string FormatDelta(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string SanitizeKey(string value)
    {
        return string.Concat(value
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_'))
            .Trim('_');
    }
}
