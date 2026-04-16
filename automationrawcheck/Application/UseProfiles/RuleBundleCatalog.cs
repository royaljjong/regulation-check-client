namespace AutomationRawCheck.Application.UseProfiles;

public static class RuleBundleCatalog
{
    public const string Density = "Density";
    public const string Access = "Access";
    public const string Parking = "Parking";
    public const string Egress = "Egress";
    public const string FireCompartment = "FireCompartment";
    public const string Accessibility = "Accessibility";
    public const string Energy = "Energy";
    public const string StructureReview = "StructureReview";
    public const string MEPReview = "MEPReview";
    public const string OrdinanceReview = "OrdinanceReview";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Density,
        Access,
        Parking,
        Egress,
        FireCompartment,
        Accessibility,
        Energy,
        StructureReview,
        MEPReview,
        OrdinanceReview,
    };
}
