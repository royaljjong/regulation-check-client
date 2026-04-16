using System.Collections.Concurrent;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;

namespace AutomationRawCheck.Application.Services;

public sealed class InMemoryReviewSnapshotStore : IReviewSnapshotStore
{
    private readonly ConcurrentDictionary<string, ReviewSnapshotDto> _snapshots = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ProjectReviewBaselineDto> _baselines = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ReviewSnapshotCompareArchiveDto> _compareReports = new(StringComparer.Ordinal);

    public ReviewSnapshotDto Save(
        BuildingReviewRequestDto request,
        BuildingReviewResponseDto review,
        BuildingReviewReportPackageDto package)
    {
        var projectId = request.ProjectContext?.ProjectId;
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("projectId is required to save a review snapshot.");

        var snapshot = new ReviewSnapshotDto
        {
            SnapshotId = $"snapshot-{Guid.NewGuid():N}",
            ProjectId = projectId,
            ScenarioId = request.ProjectContext?.ScenarioId,
            VersionTag = request.ProjectContext?.VersionTag,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            SelectedUse = review.SelectedUse,
            ReviewLevel = review.ReviewLevel,
            Address = review.Location.ResolvedAddress ?? review.Location.InputAddress,
            Request = request,
            DevelopmentActionApiStatus = review.DevelopmentActionApiStatus,
            Checklist = review.Checklist,
            TaskCount = package.Tasks.Count,
            HighPriorityTaskCount = package.Tasks.Count(task => string.Equals(task.Priority, "high", StringComparison.OrdinalIgnoreCase)),
            ManualReviewCount = package.ManualReviews.Count,
            ReportPackage = package,
        };

        _snapshots[snapshot.SnapshotId] = snapshot;
        return snapshot;
    }

    public ReviewSnapshotDto? Get(string snapshotId)
    {
        return _snapshots.TryGetValue(snapshotId, out var snapshot)
            ? snapshot
            : null;
    }

    public IReadOnlyList<ReviewSnapshotSummaryDto> ListByProject(string projectId)
    {
        return _snapshots.Values
            .Where(snapshot => string.Equals(snapshot.ProjectId, projectId, StringComparison.Ordinal))
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .Select(ReviewSnapshotComparer.MapSummary)
            .ToList();
    }

    public ReviewSnapshotSummaryDto? GetLatestByProject(string projectId, string? scenarioId = null)
    {
        var query = _snapshots.Values
            .Where(snapshot => string.Equals(snapshot.ProjectId, projectId, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(scenarioId))
        {
            query = query.Where(snapshot => string.Equals(snapshot.ScenarioId, scenarioId, StringComparison.Ordinal));
        }

        var latest = query
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .FirstOrDefault();

        return latest is null ? null : ReviewSnapshotComparer.MapSummary(latest);
    }

    public ProjectReviewBaselineDto? GetBaselineByProject(string projectId, string? scenarioId = null)
    {
        var key = ReviewSnapshotBaselineHelper.BuildKey(projectId, scenarioId);
        return _baselines.TryGetValue(key, out var baseline)
            ? baseline
            : null;
    }

    public ProjectReviewBaselineDto? SetBaseline(string projectId, string snapshotId, string? scenarioId = null)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
            return null;

        if (!string.Equals(snapshot.ProjectId, projectId, StringComparison.Ordinal))
            return null;

        if (!string.IsNullOrWhiteSpace(scenarioId) &&
            !string.Equals(snapshot.ScenarioId, scenarioId, StringComparison.Ordinal))
            return null;

        var baseline = ReviewSnapshotBaselineHelper.Create(projectId, scenarioId, snapshot);
        _baselines[ReviewSnapshotBaselineHelper.BuildKey(projectId, scenarioId)] = baseline;
        return baseline;
    }

    public ReviewSnapshotCompareResponseDto? Compare(string leftSnapshotId, string rightSnapshotId)
    {
        if (!_snapshots.TryGetValue(leftSnapshotId, out var left) ||
            !_snapshots.TryGetValue(rightSnapshotId, out var right))
            return null;

        return ReviewSnapshotComparer.Compare(left, right);
    }

    public ReviewSnapshotCompareArchiveDto SaveCompareReport(string projectId, string? scenarioId, ReviewSnapshotCompareReportPackageDto package)
    {
        var archive = new ReviewSnapshotCompareArchiveDto
        {
            CompareReportId = $"compare-{Guid.NewGuid():N}",
            ProjectId = projectId,
            ScenarioId = scenarioId,
            BaselineSnapshotId = package.Comparison.Left.SnapshotId,
            TargetSnapshotId = package.Comparison.Right.SnapshotId,
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            ReportPackage = package,
        };

        _compareReports[archive.CompareReportId] = archive;
        return archive;
    }

    public ReviewSnapshotCompareArchiveDto? GetCompareReport(string compareReportId)
    {
        return _compareReports.TryGetValue(compareReportId, out var archive)
            ? archive
            : null;
    }

    public ReviewSnapshotCompareArchiveDto? FindCompareReport(string projectId, string baselineSnapshotId, string targetSnapshotId, string? scenarioId = null)
    {
        return _compareReports.Values
            .Where(archive => string.Equals(archive.ProjectId, projectId, StringComparison.Ordinal))
            .Where(archive => string.Equals(archive.BaselineSnapshotId, baselineSnapshotId, StringComparison.Ordinal))
            .Where(archive => string.Equals(archive.TargetSnapshotId, targetSnapshotId, StringComparison.Ordinal))
            .Where(archive => string.IsNullOrWhiteSpace(scenarioId) || string.Equals(archive.ScenarioId, scenarioId, StringComparison.Ordinal))
            .OrderByDescending(archive => archive.CreatedAt)
            .FirstOrDefault();
    }

    public IReadOnlyList<ReviewSnapshotCompareArchiveSummaryDto> ListCompareReportsByProject(string projectId, string? scenarioId = null)
    {
        var query = _compareReports.Values
            .Where(archive => string.Equals(archive.ProjectId, projectId, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(scenarioId))
            query = query.Where(archive => string.Equals(archive.ScenarioId, scenarioId, StringComparison.Ordinal));

        return query
            .OrderByDescending(archive => archive.CreatedAt)
            .Select(ReviewSnapshotCompareArchiveMapper.MapSummary)
            .ToList();
    }
}
