namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class RegulationResearchOptions
{
    public const string SectionName = "RegulationResearch";

    public string LiveLawSourceEndpoint { get; set; } = string.Empty;
    public string MunicipalOrdinanceEndpoint { get; set; } = string.Empty;
    public string PdfHubEndpoint { get; set; } = string.Empty;
    public string ChangeTrackingMode { get; set; } = "manual_compare";
    public string SearchHubMode { get; set; } = "metadata_composition";
}
