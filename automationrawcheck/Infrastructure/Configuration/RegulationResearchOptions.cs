namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class RegulationResearchOptions
{
    public const string SectionName = "RegulationResearch";

    public bool OfficialLawApiEnabled { get; set; }
    public string OfficialLawApiBaseUrl { get; set; } = "http://www.law.go.kr/DRF";
    public string OfficialLawApiOc { get; set; } = string.Empty;
    public string OfficialLawPublicDataBaseUrl { get; set; } = "http://apis.data.go.kr/1170000/law";
    public string OfficialLawPublicDataServiceKey { get; set; } = string.Empty;
    public string LiveLawSourceEndpoint { get; set; } = string.Empty;
    public string MunicipalOrdinanceEndpoint { get; set; } = string.Empty;
    public string PdfHubEndpoint { get; set; } = string.Empty;
    public string ChangeTrackingMode { get; set; } = "manual_compare";
    public string SearchHubMode { get; set; } = "metadata_composition";
}
