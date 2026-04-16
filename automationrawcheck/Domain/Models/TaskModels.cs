namespace AutomationRawCheck.Domain.Models;

public enum TaskStatus
{
    Ok,
    Fail,
    Warning,
    Info,
    ManualReview,
}

public sealed record ReviewTask
{
    public string TaskId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public TaskStatus Status { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<string> RelatedRuleIds { get; init; } = [];
    public string Priority { get; init; } = "medium";
}

public sealed record ProjectChecklistSummary
{
    public int Fail { get; init; }
    public int Warning { get; init; }
    public int Ok { get; init; }
    public int Info { get; init; }
    public int ManualReview { get; init; }
}
