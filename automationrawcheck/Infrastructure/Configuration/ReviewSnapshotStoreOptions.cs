namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class ReviewSnapshotStoreOptions
{
    public const string SectionName = "ReviewSnapshotStore";

    public bool Enabled { get; init; } = true;
    public string StoragePath { get; init; } = "App_Data/review-snapshots.json";
    public bool WriteIndented { get; init; } = true;
}
