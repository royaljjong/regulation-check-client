using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

internal static class ReviewSnapshotCompareArchiveMapper
{
    public static ReviewSnapshotCompareArchiveSummaryDto MapSummary(ReviewSnapshotCompareArchiveDto archive) => new()
    {
        CompareReportId = archive.CompareReportId,
        ProjectId = archive.ProjectId,
        ScenarioId = archive.ScenarioId,
        BaselineSnapshotId = archive.BaselineSnapshotId,
        TargetSnapshotId = archive.TargetSnapshotId,
        CreatedAt = archive.CreatedAt,
        SummaryLines = archive.ReportPackage.Comparison.SummaryLines.ToList(),
    };
}
