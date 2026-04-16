using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Application.Services;

public sealed class Gemma4AiAssistService : IAiAssistService
{
    private readonly AiAssistOptions _options;

    public Gemma4AiAssistService(IOptions<AiAssistOptions> options)
    {
        _options = options.Value;
    }

    public AiAssistResponseDto BuildPreview(AiAssistRequestDto request)
    {
        var response = AiAssistContractBuilder.Build(request);
        response.Provider = _options.Provider;
        response.Model = _options.Model;
        response.ExecutionMode = _options.ExecutionMode;
        response.IsConfigured = _options.Enabled && !string.IsNullOrWhiteSpace(_options.Endpoint);
        return response;
    }

    public AiAssistRequestPackageDto BuildRequestPackage(AiAssistRequestDto request)
    {
        var preview = BuildPreview(request);
        return new AiAssistRequestPackageDto
        {
            Provider = _options.Provider,
            Model = _options.Model,
            ExecutionMode = _options.ExecutionMode,
            InputPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["selectedUse"] = request.SelectedUse,
                ["useProfile"] = request.UseProfile,
                ["planningContext"] = request.PlanningContext,
                ["reviewItems"] = request.ReviewItems,
                ["tasks"] = request.Tasks,
                ["manualReviewSet"] = request.ManualReviewSet,
                ["ordinanceRegion"] = request.OrdinanceRegion
            },
            Guardrails = preview.Guardrails.ToList(),
            SummaryLines =
            [
                $"provider={_options.Provider}",
                $"model={_options.Model}",
                $"executionMode={_options.ExecutionMode}",
                "Payload is ready for a Gemma4 runner and expects structured JSON only."
            ]
        };
    }

    public AiAssistServiceStatusDto GetStatus()
    {
        return new AiAssistServiceStatusDto
        {
            Provider = _options.Provider,
            Model = _options.Model,
            ExecutionMode = _options.ExecutionMode,
            Enabled = _options.Enabled,
            IsConfigured = _options.Enabled && !string.IsNullOrWhiteSpace(_options.Endpoint),
            EndpointConfigured = !string.IsNullOrWhiteSpace(_options.Endpoint),
            ApiKeyConfigured = !string.IsNullOrWhiteSpace(_options.ApiKey),
            Endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) ? null : _options.Endpoint,
            ApiKeyHeaderName = string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName) ? null : _options.ApiKeyHeaderName,
            SummaryLines =
            [
                $"provider={_options.Provider}",
                $"model={_options.Model}",
                $"executionMode={_options.ExecutionMode}",
                _options.Enabled
                    ? "AI Assist service is enabled for Gemma4-ready integration."
                    : "AI Assist service is disabled and remains in preview mode."
            ]
        };
    }
}
