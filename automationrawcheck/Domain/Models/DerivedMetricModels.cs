namespace AutomationRawCheck.Domain.Models;

public enum DerivedMetricStatus
{
    Calculated,
    Triggered,
    Pending,
    NotApplicable,
}

public sealed record DerivedMetricResult
{
    public string MetricId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public object? Value { get; init; }
    public DerivedMetricStatus Status { get; init; }
    public string? Note { get; init; }
}
