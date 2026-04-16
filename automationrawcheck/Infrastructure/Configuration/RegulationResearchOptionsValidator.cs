using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class RegulationResearchOptionsValidator : IValidateOptions<RegulationResearchOptions>
{
    public ValidateOptionsResult Validate(string? name, RegulationResearchOptions options)
    {
        var failures = new List<string>();

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
