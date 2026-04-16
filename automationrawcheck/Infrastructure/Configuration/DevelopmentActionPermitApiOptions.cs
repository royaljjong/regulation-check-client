namespace AutomationRawCheck.Infrastructure.Configuration;

/// <summary>
/// Generic configuration for a development-action permit restriction API.
/// The actual request/response schema is defined by settings so credentials are not hardcoded.
/// </summary>
public sealed class DevelopmentActionPermitApiOptions
{
    public const string SectionName = "DevelopmentActionPermitApi";

    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ServiceKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;

    public string LongitudeParameterName { get; set; } = "lon";
    public string LatitudeParameterName { get; set; } = "lat";
    public string UserIdParameterName { get; set; } = "id";
    public string ServiceKeyParameterName { get; set; } = "key";
    public Dictionary<string, string> StaticQueryParameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> RequestHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ResponseRootPath { get; set; } = string.Empty;
    public string InsideFieldPath { get; set; } = string.Empty;
    public string NameFieldPath { get; set; } = string.Empty;
    public string CodeFieldPath { get; set; } = string.Empty;
}
