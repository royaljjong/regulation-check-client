using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class DevelopmentActionPermitApiOptionsValidator : IValidateOptions<DevelopmentActionPermitApiOptions>
{
    public ValidateOptionsResult Validate(string? name, DevelopmentActionPermitApiOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            failures.Add("DevelopmentActionPermitApi:BaseUrl is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.UserId))
            failures.Add("DevelopmentActionPermitApi:UserId is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.ServiceKey))
            failures.Add("DevelopmentActionPermitApi:ServiceKey is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.InsideFieldPath))
            failures.Add("DevelopmentActionPermitApi:InsideFieldPath is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.LongitudeParameterName))
            failures.Add("DevelopmentActionPermitApi:LongitudeParameterName is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.LatitudeParameterName))
            failures.Add("DevelopmentActionPermitApi:LatitudeParameterName is required when Enabled=true.");

        foreach (var entry in options.StaticQueryParameters)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
                failures.Add("DevelopmentActionPermitApi:StaticQueryParameters key must not be empty.");
        }

        foreach (var entry in options.RequestHeaders)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
                failures.Add("DevelopmentActionPermitApi:RequestHeaders key must not be empty.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
