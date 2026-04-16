namespace AutomationRawCheck.Domain.Models;

public enum ReviewArchitectureLayer
{
    SpatialLayer,
    ProfileLayer,
    CalculationLayer,
    RuleLayer,
    TaskLayer,
    ChecklistLayer,
    ManualReviewLayer,
    OrdinanceLayer,
    AiAssistLayer,
}

public sealed record ReviewQueryContext
{
    public string? Address { get; init; }
    public string? ParcelAddress { get; init; }
    public double? Longitude { get; init; }
    public double? Latitude { get; init; }
    public string? ParcelId { get; init; }
    public string? SelectedUse { get; init; }
    public string? ReviewLevel { get; init; }
}

public sealed record ReviewRawInputs
{
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.Ordinal);
}

public sealed record ReviewDerivedInputs
{
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.Ordinal);
}

public sealed record ReviewApiVerificationStatus
{
    public string Source { get; init; } = "none";
    public string Status { get; init; } = "unavailable";
    public string Confidence { get; init; } = "low";
    public string? Note { get; init; }
}

public sealed record ReviewPlanningContext
{
    public string? ZoneName { get; init; }
    public string? ZoneCode { get; init; }
    public bool? IsInDistrictUnitPlan { get; init; }
    public bool? IsInUrbanPlanningFacility { get; init; }
    public bool? IsDevelopmentRestriction { get; init; }
    public bool? NeedsDevelopmentActPermission { get; init; }
    public ReviewApiVerificationStatus DevelopmentActionApiStatus { get; init; } = new();
    public Dictionary<string, object?> Spatial { get; init; } = new(StringComparer.Ordinal);
}

public sealed record ReviewOutputEnvelope
{
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.Ordinal);
}

public sealed record ReviewEngineFrame
{
    public ReviewQueryContext Query { get; init; } = new();
    public ReviewRawInputs RawInputs { get; init; } = new();
    public ReviewDerivedInputs DerivedInputs { get; init; } = new();
    public ReviewPlanningContext PlanningContext { get; init; } = new();
    public ReviewOutputEnvelope Output { get; init; } = new();
}
