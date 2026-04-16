using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Api.Dtos;

public sealed class ReviewSnapshotDto
{
    public string SnapshotId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public string? VersionTag { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string SelectedUse { get; init; } = string.Empty;
    public string ReviewLevel { get; init; } = string.Empty;
    public string? Address { get; init; }
    public BuildingReviewRequestDto Request { get; init; } = new();
    public ApiVerificationStatusDto DevelopmentActionApiStatus { get; init; } = new();
    public ProjectChecklistDto Checklist { get; init; } = new();
    public int TaskCount { get; init; }
    public int HighPriorityTaskCount { get; init; }
    public int ManualReviewCount { get; init; }
    public BuildingReviewReportPackageDto ReportPackage { get; init; } = new();
}

public sealed class ReviewSnapshotSummaryDto
{
    public string SnapshotId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public string? VersionTag { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string SelectedUse { get; init; } = string.Empty;
    public string ReviewLevel { get; init; } = string.Empty;
    public string? Address { get; init; }
    public ApiVerificationStatusDto DevelopmentActionApiStatus { get; init; } = new();
    public ProjectChecklistDto Checklist { get; init; } = new();
    public int TaskCount { get; init; }
    public int HighPriorityTaskCount { get; init; }
    public int ManualReviewCount { get; init; }
}

public sealed class ReviewSnapshotCompareRequestDto
{
    public string LeftSnapshotId { get; init; } = string.Empty;
    public string RightSnapshotId { get; init; } = string.Empty;
}

public sealed class ReviewSnapshotCompareLatestRequestDto
{
    public string? BaselineSnapshotId { get; init; }
    public string? ScenarioId { get; init; }
}

public sealed class ReviewSnapshotCompareLatestExportRequestDto
{
    public string Format { get; init; } = "pdf";
    public ReviewSnapshotCompareLatestRequestDto CompareLatestRequest { get; init; } = new();
}

public sealed class ProjectReviewBaselineDto
{
    public string ProjectId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public string SnapshotId { get; init; } = string.Empty;
    public string SetAt { get; init; } = string.Empty;
    public ReviewSnapshotSummaryDto Snapshot { get; init; } = new();
}

public sealed class SetProjectReviewBaselineRequestDto
{
    public string SnapshotId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
}

public sealed class ReviewSnapshotChecklistDiffDto
{
    public int FailDelta { get; init; }
    public int WarningDelta { get; init; }
    public int OkDelta { get; init; }
    public int InfoDelta { get; init; }
    public int ManualReviewDelta { get; init; }
}

public sealed class ReviewTaskCategoryDiffDto
{
    public string Category { get; init; } = string.Empty;
    public int LeftCount { get; init; }
    public int RightCount { get; init; }
    public int Delta { get; init; }
}

public sealed class ReviewSnapshotCompareResponseDto
{
    public ReviewSnapshotSummaryDto Left { get; init; } = new();
    public ReviewSnapshotSummaryDto Right { get; init; } = new();
    public ReviewSnapshotChecklistDiffDto ChecklistDiff { get; init; } = new();
    public bool SameSelectedUse { get; init; }
    public bool SameReviewLevel { get; init; }
    public bool SameDevelopmentActionStatus { get; init; }
    public List<string> SummaryLines { get; init; } = new();
    public List<ReviewTaskCategoryDiffDto> TaskCategoryDiffs { get; init; } = new();
    public List<string> AddedHighPriorityTasks { get; init; } = new();
    public List<string> RemovedHighPriorityTasks { get; init; } = new();
    public List<string> AddedManualReviews { get; init; } = new();
    public List<string> RemovedManualReviews { get; init; } = new();
}

public sealed class ReviewSnapshotCompareReportPackageDto
{
    public ReportPackageMetadataDto Metadata { get; init; } = new();
    public ReportPreviewDto Preview { get; init; } = new();
    public ReviewSnapshotCompareResponseDto Comparison { get; init; } = new();
}

public sealed class ReviewSnapshotCompareReportExportRequestDto
{
    public string Format { get; init; } = "pdf";
    public ReviewSnapshotCompareRequestDto CompareRequest { get; init; } = new();
}

public sealed class ReviewSnapshotCompareReportExportPlanDto
{
    public string Status { get; init; } = "ready";
    public string Format { get; init; } = "pdf";
    public string MimeType { get; init; } = "application/pdf";
    public string RendererKey { get; init; } = "report_renderer_v1";
    public string TemplateKey { get; init; } = string.Empty;
    public string SuggestedFileName { get; init; } = string.Empty;
    public ReviewSnapshotCompareReportPackageDto Package { get; init; } = new();
}

public sealed class ReviewSnapshotCompareReportRenderResultDto
{
    public string Status { get; init; } = "render_ready";
    public string Format { get; init; } = "pdf";
    public string TargetMimeType { get; init; } = "application/pdf";
    public string PayloadMimeType { get; init; } = "text/markdown";
    public string RendererKey { get; init; } = "report_renderer_v1";
    public string TemplateKey { get; init; } = string.Empty;
    public string SuggestedFileName { get; init; } = string.Empty;
    public string PayloadType { get; init; } = "markdown_document";
    public string PayloadText { get; init; } = string.Empty;
    public ReviewSnapshotCompareReportExportPlanDto ExportPlan { get; init; } = new();
}

public sealed class ReviewSnapshotCompareArchiveDto
{
    public string CompareReportId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public string BaselineSnapshotId { get; init; } = string.Empty;
    public string TargetSnapshotId { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public ReviewSnapshotCompareReportPackageDto ReportPackage { get; init; } = new();
}

public sealed class ReviewSnapshotCompareArchiveSummaryDto
{
    public string CompareReportId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public string BaselineSnapshotId { get; init; } = string.Empty;
    public string TargetSnapshotId { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public List<string> SummaryLines { get; init; } = new();
}

public sealed class ReviewProjectWorkspaceSummaryDto
{
    public string ProjectId { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public ProjectReviewBaselineDto? Baseline { get; init; }
    public ReviewSnapshotSummaryDto? Latest { get; init; }
    public int SnapshotCount { get; init; }
    public int CompareArchiveCount { get; init; }
    public List<ReviewSnapshotSummaryDto> RecentSnapshots { get; init; } = new();
    public List<ReviewSnapshotCompareArchiveSummaryDto> RecentCompareArchives { get; init; } = new();
    public List<string> SummaryLines { get; init; } = new();
}
