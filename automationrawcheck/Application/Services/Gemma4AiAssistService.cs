using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Application.Services;

public sealed class Gemma4AiAssistService : IAiAssistService
{
    private const string HttpClientName = "ai-assist-runner";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiAssistOptions _options;

    public Gemma4AiAssistService(
        IHttpClientFactory httpClientFactory,
        IOptions<AiAssistOptions> options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public AiAssistResponseDto BuildPreview(AiAssistRequestDto request)
    {
        var response = AiAssistContractBuilder.Build(request);
        response.Provider = _options.Provider;
        response.Model = _options.Model;
        response.ExecutionMode = _options.ExecutionMode;
        response.IsConfigured = IsConfigured();
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
            SystemInstruction = _options.SystemInstruction,
            InputPayload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["userPrompt"] = request.UserPrompt,
                ["selectedUse"] = request.SelectedUse,
                ["useProfile"] = request.UseProfile,
                ["planningContext"] = request.PlanningContext,
                ["reviewItems"] = request.ReviewItems,
                ["tasks"] = request.Tasks,
                ["manualReviewSet"] = request.ManualReviewSet,
                ["ordinanceRegion"] = request.OrdinanceRegion
            },
            TrainingExamples = BuildTrainingExamples(),
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

    public async Task<AiAssistRunResponseDto> RunAsync(AiAssistRequestDto request, CancellationToken ct = default)
    {
        var package = BuildRequestPackage(request);
        if (!IsConfigured())
        {
            return new AiAssistRunResponseDto
            {
                Provider = _options.Provider,
                Model = _options.Model,
                ExecutionMode = _options.ExecutionMode,
                IsConfigured = false,
                Success = false,
                Error = "ai_assist_not_configured",
                RequestPackage = package,
                SummaryLines =
                [
                    "Gemma4 runner endpoint or API key is not configured.",
                    "Set AiAssist:Enabled, Endpoint, ApiKey, and ApiKeyHeaderName to enable live execution."
                ]
            };
        }

        try
        {
            if (IsOllamaEndpoint())
                return await RunOllamaAsync(package, ct).ConfigureAwait(false);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var message = new HttpRequestMessage(HttpMethod.Post, string.Empty)
            {
                Content = new StringContent(JsonSerializer.Serialize(package, JsonOptions), Encoding.UTF8, "application/json")
            };

            ApplyApiKey(message);

            using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var (structuredJson, outputKeys) = TryExtractStructuredJson(raw);

            return new AiAssistRunResponseDto
            {
                Provider = _options.Provider,
                Model = _options.Model,
                ExecutionMode = _options.ExecutionMode,
                IsConfigured = true,
                Success = response.IsSuccessStatusCode && structuredJson is not null,
                HttpStatusCode = (int)response.StatusCode,
                Error = response.IsSuccessStatusCode
                    ? structuredJson is null ? "structured_json_not_found" : null
                    : $"http_{(int)response.StatusCode}",
                RawResponse = Clip(raw),
                StructuredOutputJson = structuredJson,
                OutputKeys = outputKeys,
                RequestPackage = package,
                SummaryLines =
                [
                    $"provider={_options.Provider}",
                    $"model={_options.Model}",
                    $"httpStatus={(int)response.StatusCode}",
                    structuredJson is null
                        ? "Runner responded but no structured JSON payload could be extracted."
                        : "Structured JSON payload extracted successfully."
                ]
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new AiAssistRunResponseDto
            {
                Provider = _options.Provider,
                Model = _options.Model,
                ExecutionMode = _options.ExecutionMode,
                IsConfigured = true,
                Success = false,
                Error = "timeout",
                RequestPackage = package,
                SummaryLines = ["Gemma4 runner timed out before returning a structured response."]
            };
        }
        catch (Exception ex)
        {
            return new AiAssistRunResponseDto
            {
                Provider = _options.Provider,
                Model = _options.Model,
                ExecutionMode = _options.ExecutionMode,
                IsConfigured = true,
                Success = false,
                Error = ex.GetType().Name,
                RequestPackage = package,
                SummaryLines = [$"Gemma4 runner call failed: {ex.Message}"]
            };
        }
    }

    public AiAssistServiceStatusDto GetStatus()
    {
        return new AiAssistServiceStatusDto
        {
            Provider = _options.Provider,
            Model = _options.Model,
            ExecutionMode = _options.ExecutionMode,
            Enabled = _options.Enabled,
            IsConfigured = IsConfigured(),
            EndpointConfigured = !string.IsNullOrWhiteSpace(_options.Endpoint),
            ApiKeyConfigured = !string.IsNullOrWhiteSpace(_options.ApiKey),
            Endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) ? null : _options.Endpoint,
            ApiKeyHeaderName = string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName) ? null : _options.ApiKeyHeaderName,
            SummaryLines =
            [
                $"provider={_options.Provider}",
                $"model={_options.Model}",
                $"executionMode={_options.ExecutionMode}",
                IsConfigured()
                    ? "AI Assist service is ready for live Gemma4 execution."
                    : "AI Assist service is still in preview-only mode until endpoint and API key are configured."
            ]
        };
    }

    private bool IsConfigured()
        => _options.Enabled &&
           !string.IsNullOrWhiteSpace(_options.Endpoint) &&
           (IsOllamaEndpoint() || !string.IsNullOrWhiteSpace(_options.ApiKey));

    private bool IsOllamaEndpoint()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || !Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var uri))
            return false;

        return uri.IsLoopback && uri.Port == 11434;
    }

    private async Task<AiAssistRunResponseDto> RunOllamaAsync(AiAssistRequestPackageDto package, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var prompt = BuildOllamaPrompt(package);
        var payload = new
        {
            model = _options.Model,
            stream = false,
            messages = new object[]
            {
                new { role = "system", content = package.SystemInstruction },
                new { role = "user", content = prompt }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var content = TryExtractOllamaContent(raw);
        var (structuredJson, outputKeys) = TryExtractStructuredJson(content ?? raw);

        return new AiAssistRunResponseDto
        {
            Provider = _options.Provider,
            Model = _options.Model,
            ExecutionMode = _options.ExecutionMode,
            IsConfigured = true,
            Success = response.IsSuccessStatusCode && structuredJson is not null,
            HttpStatusCode = (int)response.StatusCode,
            Error = response.IsSuccessStatusCode
                ? structuredJson is null ? "structured_json_not_found" : null
                : $"http_{(int)response.StatusCode}",
            RawResponse = Clip(content ?? raw),
            StructuredOutputJson = structuredJson,
            OutputKeys = outputKeys,
            RequestPackage = package,
            SummaryLines =
            [
                "Local Ollama endpoint used.",
                $"model={_options.Model}",
                $"httpStatus={(int)response.StatusCode}",
                structuredJson is null
                    ? "Ollama responded but structured JSON could not be extracted."
                    : "Structured JSON payload extracted successfully."
            ]
        };
    }

    private static string BuildOllamaPrompt(AiAssistRequestPackageDto package)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Return structured JSON only.");
        builder.AppendLine("Use this request package:");
        builder.AppendLine(JsonSerializer.Serialize(package, JsonOptions));
        return builder.ToString();
    }

    private static string? TryExtractOllamaContent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }

            if (root.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.String)
                return response.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private void ApplyApiKey(HttpRequestMessage message)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName) || string.IsNullOrWhiteSpace(_options.ApiKey))
            return;

        var value = _options.ApiKey;
        if (string.Equals(_options.ApiKeyHeaderName, "Authorization", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(_options.ApiKeyPrefix) &&
            !value.StartsWith(_options.ApiKeyPrefix + " ", StringComparison.OrdinalIgnoreCase))
        {
            value = $"{_options.ApiKeyPrefix} {value}";
        }

        if (string.Equals(_options.ApiKeyHeaderName, "Authorization", StringComparison.OrdinalIgnoreCase))
            message.Headers.Authorization = AuthenticationHeaderValue.Parse(value);
        else
            message.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, value);
    }

    private static List<AiAssistTrainingExampleDto> BuildTrainingExamples()
    {
        return
        [
            new AiAssistTrainingExampleDto
            {
                ExampleId = "manual-review-reminder",
                Scenario = "A hospital project has multiple manual-review items and ordinance lookup tasks.",
                InputExcerpt = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["selectedUse"] = "medical_facility",
                    ["taskCategories"] = new[] { "egress", "fire", "ordinance" },
                    ["ordinanceRegion"] = "서울특별시"
                },
                ExpectedOutput = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["hints"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["hintType"] = "ordinance_navigation",
                            ["title"] = "서울시 의료시설 피난 관련 조례 확인",
                            ["keywords"] = new[] { "서울시 의료시설 피난", "병상수 피난 조례" }
                        }
                    }
                }
            },
            new AiAssistTrainingExampleDto
            {
                ExampleId = "law-navigation-only",
                Scenario = "An education facility review needs article references but must not produce compliance decisions.",
                InputExcerpt = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["selectedUse"] = "education_facility",
                    ["guardrail"] = "no pass/fail judgement"
                },
                ExpectedOutput = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["hints"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["hintType"] = "article_navigation",
                            ["title"] = "학생수 기반 피난 기준 탐색",
                            ["message"] = "건축법령과 교육시설 관련 기준에서 학생수·피난 키워드 중심으로 탐색"
                        }
                    }
                }
            }
        ];
    }

    private static (string? StructuredJson, List<string> OutputKeys) TryExtractStructuredJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, []);

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var candidate = ExtractPayload(root);
            if (candidate is null)
                return (null, []);

            var outputKeys = candidate.Value.ValueKind == JsonValueKind.Object
                ? candidate.Value.EnumerateObject().Select(static property => property.Name).ToList()
                : [];
            return (candidate.Value.GetRawText(), outputKeys);
        }
        catch (JsonException)
        {
            return (null, []);
        }
    }

    private static JsonElement? ExtractPayload(JsonElement root)
    {
        if (root.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertyName in new[] { "output", "result", "data" })
                {
                    if (root.TryGetProperty(propertyName, out var nested) &&
                        nested.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        return nested;
                    }
                }

                if (root.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String &&
                    TryParseJsonString(content.GetString(), out var parsedContent))
                {
                    return parsedContent;
                }
            }

            return root;
        }

        return null;
    }

    private static bool TryParseJsonString(string? raw, out JsonElement parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using var document = JsonDocument.Parse(raw);
            parsed = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Clip(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        return raw.Length <= 4000 ? raw : raw[..4000] + "...";
    }
}
