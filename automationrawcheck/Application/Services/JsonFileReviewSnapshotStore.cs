using System.Text.Json;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Application.Services;

public sealed class JsonFileReviewSnapshotStore : IReviewSnapshotStore
{
    private sealed class SnapshotStoreState
    {
        public List<ReviewSnapshotDto> Snapshots { get; init; } = [];
        public Dictionary<string, ProjectReviewBaselineDto> Baselines { get; init; } = new(StringComparer.Ordinal);
        public List<ReviewSnapshotCompareArchiveDto> CompareReports { get; init; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly object _gate = new();
    private readonly Dictionary<string, ReviewSnapshotDto> _snapshots;
    private readonly Dictionary<string, ProjectReviewBaselineDto> _baselines;
    private readonly Dictionary<string, ReviewSnapshotCompareArchiveDto> _compareReports;
    private readonly string _storagePath;
    private readonly bool _enabled;
    private readonly bool _writeIndented;
    private readonly ILogger<JsonFileReviewSnapshotStore> _logger;

    public JsonFileReviewSnapshotStore(
        IOptions<ReviewSnapshotStoreOptions> options,
        IWebHostEnvironment environment,
        ILogger<JsonFileReviewSnapshotStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var storeOptions = options.Value ?? new ReviewSnapshotStoreOptions();
        _enabled = storeOptions.Enabled;
        _writeIndented = storeOptions.WriteIndented;
        _storagePath = ResolveStoragePath(storeOptions.StoragePath, environment.ContentRootPath);
        var state = LoadState();
        _snapshots = state.Snapshots.ToDictionary(static snapshot => snapshot.SnapshotId, StringComparer.Ordinal);
        _baselines = new Dictionary<string, ProjectReviewBaselineDto>(state.Baselines, StringComparer.Ordinal);
        _compareReports = state.CompareReports.ToDictionary(static archive => archive.CompareReportId, StringComparer.Ordinal);
    }

    public ReviewSnapshotDto Save(
        BuildingReviewRequestDto request,
        BuildingReviewResponseDto review,
        BuildingReviewReportPackageDto package)
    {
        var projectId = request.ProjectContext?.ProjectId;
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("projectId is required to save a review snapshot.");

        lock (_gate)
        {
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
            PersistState();
            return snapshot;
        }
    }

    public ReviewSnapshotDto? Get(string snapshotId)
    {
        lock (_gate)
        {
            return _snapshots.TryGetValue(snapshotId, out var snapshot)
                ? snapshot
                : null;
        }
    }

    public IReadOnlyList<ReviewSnapshotSummaryDto> ListByProject(string projectId)
    {
        lock (_gate)
        {
            return _snapshots.Values
                .Where(snapshot => string.Equals(snapshot.ProjectId, projectId, StringComparison.Ordinal))
                .OrderByDescending(snapshot => snapshot.CreatedAt)
                .Select(ReviewSnapshotComparer.MapSummary)
                .ToList();
        }
    }

    public ReviewSnapshotSummaryDto? GetLatestByProject(string projectId, string? scenarioId = null)
    {
        lock (_gate)
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
    }

    public ProjectReviewBaselineDto? GetBaselineByProject(string projectId, string? scenarioId = null)
    {
        lock (_gate)
        {
            return _baselines.TryGetValue(ReviewSnapshotBaselineHelper.BuildKey(projectId, scenarioId), out var baseline)
                ? baseline
                : null;
        }
    }

    public ProjectReviewBaselineDto? SetBaseline(string projectId, string snapshotId, string? scenarioId = null)
    {
        lock (_gate)
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
            PersistState();
            return baseline;
        }
    }

    public ReviewSnapshotCompareResponseDto? Compare(string leftSnapshotId, string rightSnapshotId)
    {
        lock (_gate)
        {
            if (!_snapshots.TryGetValue(leftSnapshotId, out var left) ||
                !_snapshots.TryGetValue(rightSnapshotId, out var right))
                return null;

            return ReviewSnapshotComparer.Compare(left, right);
        }
    }

    public ReviewSnapshotCompareArchiveDto SaveCompareReport(string projectId, string? scenarioId, ReviewSnapshotCompareReportPackageDto package)
    {
        lock (_gate)
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
            PersistState();
            return archive;
        }
    }

    public ReviewSnapshotCompareArchiveDto? GetCompareReport(string compareReportId)
    {
        lock (_gate)
        {
            return _compareReports.TryGetValue(compareReportId, out var archive)
                ? archive
                : null;
        }
    }

    public ReviewSnapshotCompareArchiveDto? FindCompareReport(string projectId, string baselineSnapshotId, string targetSnapshotId, string? scenarioId = null)
    {
        lock (_gate)
        {
            return _compareReports.Values
                .Where(archive => string.Equals(archive.ProjectId, projectId, StringComparison.Ordinal))
                .Where(archive => string.Equals(archive.BaselineSnapshotId, baselineSnapshotId, StringComparison.Ordinal))
                .Where(archive => string.Equals(archive.TargetSnapshotId, targetSnapshotId, StringComparison.Ordinal))
                .Where(archive => string.IsNullOrWhiteSpace(scenarioId) || string.Equals(archive.ScenarioId, scenarioId, StringComparison.Ordinal))
                .OrderByDescending(archive => archive.CreatedAt)
                .FirstOrDefault();
        }
    }

    public IReadOnlyList<ReviewSnapshotCompareArchiveSummaryDto> ListCompareReportsByProject(string projectId, string? scenarioId = null)
    {
        lock (_gate)
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

    private SnapshotStoreState LoadState()
    {
        if (!_enabled || !File.Exists(_storagePath))
            return new SnapshotStoreState();

        try
        {
            var json = File.ReadAllText(_storagePath);
            var state = JsonSerializer.Deserialize<SnapshotStoreState>(json, JsonOptions);
            if (state is not null)
                return state;

            var legacyList = JsonSerializer.Deserialize<List<ReviewSnapshotDto>>(json, JsonOptions) ?? [];
            return new SnapshotStoreState { Snapshots = legacyList };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "리뷰 스냅샷 저장소를 읽지 못해 빈 상태로 시작합니다. path={Path}", _storagePath);
            return new SnapshotStoreState();
        }
    }

    private void PersistState()
    {
        if (!_enabled)
            return;

        try
        {
            var directory = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions(JsonOptions)
            {
                WriteIndented = _writeIndented,
            };

            var payload = new SnapshotStoreState
            {
                Snapshots = _snapshots.Values
                    .OrderBy(static snapshot => snapshot.CreatedAt, StringComparer.Ordinal)
                    .ToList(),
                Baselines = new Dictionary<string, ProjectReviewBaselineDto>(_baselines, StringComparer.Ordinal),
                CompareReports = _compareReports.Values
                    .OrderBy(static archive => archive.CreatedAt, StringComparer.Ordinal)
                    .ToList(),
            };

            var json = JsonSerializer.Serialize(payload, options);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "리뷰 스냅샷 저장소를 기록하지 못했습니다. path={Path}", _storagePath);
        }
    }

    private static string ResolveStoragePath(string configuredPath, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            configuredPath = "App_Data/review-snapshots.json";

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }

}
