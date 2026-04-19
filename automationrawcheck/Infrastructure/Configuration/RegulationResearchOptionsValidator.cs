using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class RegulationResearchOptionsValidator : IValidateOptions<RegulationResearchOptions>
{
    public ValidateOptionsResult Validate(string? name, RegulationResearchOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.OfficialLawApiBaseUrl) &&
            !Uri.TryCreate(options.OfficialLawApiBaseUrl, UriKind.Absolute, out _))
            failures.Add("RegulationResearch:OfficialLawApiBaseUrl must be an absolute URI.");

        if (!string.IsNullOrWhiteSpace(options.OfficialLawPublicDataBaseUrl) &&
            !Uri.TryCreate(options.OfficialLawPublicDataBaseUrl, UriKind.Absolute, out _))
            failures.Add("RegulationResearch:OfficialLawPublicDataBaseUrl must be an absolute URI.");

        if (options.OfficialLawApiEnabled &&
            string.IsNullOrWhiteSpace(options.OfficialLawApiOc) &&
            string.IsNullOrWhiteSpace(options.OfficialLawPublicDataServiceKey))
        {
            failures.Add("RegulationResearch:OfficialLawApiOc or OfficialLawPublicDataServiceKey is required when OfficialLawApiEnabled=true.");
        }

        if (!string.IsNullOrWhiteSpace(options.LiveLawSourceEndpoint) &&
            !Uri.TryCreate(options.LiveLawSourceEndpoint, UriKind.Absolute, out _))
            failures.Add("RegulationResearch:LiveLawSourceEndpoint must be an absolute URI.");

        if (!string.IsNullOrWhiteSpace(options.MunicipalOrdinanceEndpoint) &&
            !Uri.TryCreate(options.MunicipalOrdinanceEndpoint, UriKind.Absolute, out _))
            failures.Add("RegulationResearch:MunicipalOrdinanceEndpoint must be an absolute URI.");

        if (!string.IsNullOrWhiteSpace(options.PdfHubEndpoint) &&
            !Uri.TryCreate(options.PdfHubEndpoint, UriKind.Absolute, out _))
            failures.Add("RegulationResearch:PdfHubEndpoint must be an absolute URI.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
