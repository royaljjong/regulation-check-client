using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class AiAssistOptionsValidator : IValidateOptions<AiAssistOptions>
{
    public ValidateOptionsResult Validate(string? name, AiAssistOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Provider))
            failures.Add("AiAssist:Provider is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.Model))
            failures.Add("AiAssist:Model is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.Endpoint))
            failures.Add("AiAssist:Endpoint is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.ApiKeyHeaderName))
            failures.Add("AiAssist:ApiKeyHeaderName is required when Enabled=true.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
