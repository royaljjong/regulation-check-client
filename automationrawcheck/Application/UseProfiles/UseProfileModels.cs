namespace AutomationRawCheck.Application.UseProfiles;

public enum AutomationCoverage
{
    High,
    Medium,
    Low,
}

public enum UseInputGroup
{
    Common,
    SemiCommon,
    Specialized,
}

public sealed record UseProfileIdentity
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FunctionalGroup { get; init; } = string.Empty;
    public string SampleSet { get; init; } = "generalized";
}

public sealed record UseProfileDefinition
{
    public UseProfileIdentity Identity { get; init; } = new();
    public Dictionary<string, List<string>> RequiredInputsByLevel { get; init; } = new(StringComparer.Ordinal);
    public List<string> DerivedMetrics { get; init; } = new();
    public List<string> RuleBundles { get; init; } = new();
    public List<string> TaskTemplates { get; init; } = new();
    public List<string> ManualCheckTemplates { get; init; } = new();
    public List<string> LegalSearchHints { get; init; } = new();
    public Dictionary<UseInputGroup, List<string>> InputGroups { get; init; } = new();
    public AutomationCoverage Coverage { get; init; } = AutomationCoverage.Medium;
}
