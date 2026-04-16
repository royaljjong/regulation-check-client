using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Interfaces;

public interface IReviewSnapshotStore
{
    ReviewSnapshotDto Save(BuildingReviewRequestDto request, BuildingReviewResponseDto review, BuildingReviewReportPackageDto package);
    ReviewSnapshotDto? Get(string snapshotId);
    IReadOnlyList<ReviewSnapshotSummaryDto> ListByProject(string projectId);
    ReviewSnapshotSummaryDto? GetLatestByProject(string projectId, string? scenarioId = null);
    ProjectReviewBaselineDto? GetBaselineByProject(string projectId, string? scenarioId = null);
    ProjectReviewBaselineDto? SetBaseline(string projectId, string snapshotId, string? scenarioId = null);
    ReviewSnapshotCompareResponseDto? Compare(string leftSnapshotId, string rightSnapshotId);
    ReviewSnapshotCompareArchiveDto SaveCompareReport(string projectId, string? scenarioId, ReviewSnapshotCompareReportPackageDto package);
    ReviewSnapshotCompareArchiveDto? GetCompareReport(string compareReportId);
    ReviewSnapshotCompareArchiveDto? FindCompareReport(string projectId, string baselineSnapshotId, string targetSnapshotId, string? scenarioId = null);
    IReadOnlyList<ReviewSnapshotCompareArchiveSummaryDto> ListCompareReportsByProject(string projectId, string? scenarioId = null);
}
