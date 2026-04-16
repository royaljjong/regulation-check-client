using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

internal static class ReviewSnapshotBaselineHelper
{
    public static string BuildKey(string projectId, string? scenarioId)
    {
        return string.IsNullOrWhiteSpace(scenarioId)
            ? projectId
            : $"{projectId}::{scenarioId}";
    }

    public static ProjectReviewBaselineDto Create(string projectId, string? scenarioId, ReviewSnapshotDto snapshot)
    {
        return new ProjectReviewBaselineDto
        {
            ProjectId = projectId,
            ScenarioId = scenarioId,
            SnapshotId = snapshot.SnapshotId,
            SetAt = DateTimeOffset.UtcNow.ToString("O"),
            Snapshot = ReviewSnapshotComparer.MapSummary(snapshot),
        };
    }
}
