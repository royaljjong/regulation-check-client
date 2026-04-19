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

        if (!IsAnonymousLocalEndpoint(options.Endpoint) && string.IsNullOrWhiteSpace(options.ApiKey))
            failures.Add("AiAssist:ApiKey is required when Enabled=true.");

        if (string.IsNullOrWhiteSpace(options.ApiKeyHeaderName))
            failures.Add("AiAssist:ApiKeyHeaderName is required when Enabled=true.");

        if (options.TimeoutSeconds <= 0)
            failures.Add("AiAssist:TimeoutSeconds must be greater than 0 when Enabled=true.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsAnonymousLocalEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return false;

        return uri.IsLoopback && uri.Port == 11434;
    }
}
