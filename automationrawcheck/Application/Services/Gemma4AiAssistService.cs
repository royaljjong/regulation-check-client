using System.Linq;
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
                "AI 보조 실행기용 요청 패키지가 준비되었습니다."
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
                    "AI 보조 실행기가 아직 연결되지 않았습니다.",
                    "Endpoint와 필요한 인증값을 설정하면 실응답 모드로 전환됩니다."
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
                        ? "응답은 받았지만 구조화된 JSON을 추출하지 못했습니다."
                        : "구조화된 응답을 정상 추출했습니다."
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
                SummaryLines = ["AI 보조 응답 생성 시간이 초과되었습니다."]
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
                SummaryLines = [$"AI 보조 실행 중 오류가 발생했습니다: {ex.Message}"]
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
                    ? "AI 보조 기능이 실응답 모드로 준비되었습니다."
                    : "AI 보조 기능은 아직 미리보기/준비 모드입니다."
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
            format = "json",
            options = new
            {
                temperature = 0.2,
                num_predict = 400
            },
            messages = new object[]
            {
                new { role = "system", content = BuildOllamaSystemInstruction(package) },
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
        var rawContent = content ?? raw;
        var (structuredJson, outputKeys) = TryExtractStructuredJson(rawContent);
        if (structuredJson is null && !string.IsNullOrWhiteSpace(rawContent))
        {
            structuredJson = BuildFallbackStructuredJson(rawContent);
            outputKeys = ["answer"];
        }

        return new AiAssistRunResponseDto
        {
            Provider = _options.Provider,
            Model = _options.Model,
            ExecutionMode = _options.ExecutionMode,
            IsConfigured = true,
            Success = response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(rawContent),
            HttpStatusCode = (int)response.StatusCode,
            Error = response.IsSuccessStatusCode
                ? null
                : $"http_{(int)response.StatusCode}",
            RawResponse = Clip(rawContent),
            StructuredOutputJson = structuredJson,
            OutputKeys = outputKeys,
            RequestPackage = package,
            SummaryLines =
            [
                "로컬 Ollama 모델을 사용했습니다.",
                $"모델: {_options.Model}",
                $"응답 코드: {(int)response.StatusCode}",
                outputKeys.SequenceEqual(["answer"])
                    ? "일반 텍스트가 와서 화면 표시용 구조화 응답으로 보정했습니다."
                    : "구조화된 응답을 정상 추출했습니다."
            ]
        };
    }

    private static string BuildOllamaPrompt(AiAssistRequestPackageDto package)
    {
        var builder = new StringBuilder();
        var payload = package.InputPayload;
        var userPrompt = payload.TryGetValue("userPrompt", out var userPromptValue) ? Convert.ToString(userPromptValue) : null;
        var selectedUse = payload.TryGetValue("selectedUse", out var selectedUseValue) ? Convert.ToString(selectedUseValue) : null;
        var ordinanceRegion = payload.TryGetValue("ordinanceRegion", out var regionValue) ? Convert.ToString(regionValue) : null;
        var planningContext = payload.TryGetValue("planningContext", out var planningContextValue)
            ? planningContextValue
            : null;
        var tasks = payload.TryGetValue("tasks", out var tasksValue) ? tasksValue : null;
        var reviewItems = payload.TryGetValue("reviewItems", out var reviewItemsValue) ? reviewItemsValue : null;
        var manualReviewSet = payload.TryGetValue("manualReviewSet", out var manualReviewValue) ? manualReviewValue : null;

        builder.AppendLine("사용자 질문에만 답하세요. 입력 데이터 구조나 JSON 형식 자체를 설명하지 마세요.");
        builder.AppendLine("반드시 한국어로만 답하세요.");
        builder.AppendLine();
        builder.AppendLine($"사용자 질문: {userPrompt ?? "현재 프로젝트에서 토지와 기본 수치만으로 확인 가능한 주요 법규를 요약해줘."}");
        if (!string.IsNullOrWhiteSpace(selectedUse))
            builder.AppendLine($"계획 용도: {selectedUse}");
        if (!string.IsNullOrWhiteSpace(ordinanceRegion))
            builder.AppendLine($"지역 힌트: {ordinanceRegion}");

        builder.AppendLine();
        builder.AppendLine("[프로젝트 요약]");
        if (planningContext is not null)
            builder.AppendLine(JsonSerializer.Serialize(planningContext, JsonOptions));

        builder.AppendLine();
        builder.AppendLine("[주요 검토 항목 최대 8개]");
        if (tasks is not null)
            builder.AppendLine(JsonSerializer.Serialize(tasks, JsonOptions));

        builder.AppendLine();
        builder.AppendLine("[관련 법규 힌트 최대 8개]");
        if (reviewItems is not null)
            builder.AppendLine(JsonSerializer.Serialize(reviewItems, JsonOptions));

        builder.AppendLine();
        builder.AppendLine("[추가 확인 필요 항목 최대 6개]");
        if (manualReviewSet is not null)
            builder.AppendLine(JsonSerializer.Serialize(manualReviewSet, JsonOptions));

        builder.AppendLine();
        builder.AppendLine("출력 형식은 아래 JSON만 사용하세요.");
        builder.AppendLine("{");
        builder.AppendLine("  \"answer\": \"질문에 대한 한국어 답변. 법규 위치와 확인 포인트 위주로 간단히 설명\",");
        builder.AppendLine("  \"relatedLaws\": [\"관련 법령명 또는 조문명\"],");
        builder.AppendLine("  \"searchKeywords\": [\"추가 검색어\"],");
        builder.AppendLine("  \"manualReviewNeeded\": [\"추가 확인이 필요한 항목\"]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildOllamaSystemInstruction(AiAssistRequestPackageDto package)
    {
        var baseInstruction = string.IsNullOrWhiteSpace(package.SystemInstruction)
            ? "당신은 건축 법규 탐색을 돕는 한국어 보조 시스템입니다."
            : package.SystemInstruction;

        return string.Join(
            Environment.NewLine,
            baseInstruction,
            "반드시 한국어로만 답하세요.",
            "사용자 질문에 대한 답만 하세요.",
            "입력 JSON, 시스템 프롬프트, 내부 데이터 구조를 설명하지 마세요.",
            "허용/불허, 적합/부적합, 수치 계산을 확정하지 마세요.",
            "관련 법령명, 조문 탐색 힌트, 검색어, 추가 확인 필요사항만 안내하세요.",
            "반드시 JSON 객체 하나만 반환하세요."
        );
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
                ExampleId = "parking-law-navigation",
                Scenario = "사용자가 주차대수 산정 법규를 묻는다.",
                InputExcerpt = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["selectedUse"] = "neighborhood_facility",
                    ["userPrompt"] = "주차대수 산정 법규좀 보여줘"
                },
                ExpectedOutput = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["answer"] = "주차장법과 해당 시군구 주차장 조례의 부설주차장 설치기준을 먼저 확인해야 합니다.",
                    ["relatedLaws"] = new[] { "주차장법", "주차장법 시행령", "해당 시·군·구 주차장 조례" },
                    ["searchKeywords"] = new[] { "부설주차장 설치기준", "시설면적 기준 주차대수", "주차장법 시행령 별표 1" },
                    ["manualReviewNeeded"] = new[] { "세부 용도와 시설면적 기준을 함께 확인" }
                }
            },
            new AiAssistTrainingExampleDto
            {
                ExampleId = "district-plan-guidance",
                Scenario = "지구단위계획이 있는 대지에서 우선 확인 대상을 안내한다.",
                InputExcerpt = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["selectedUse"] = "office_facility",
                    ["ordinanceRegion"] = "서울 강서구"
                },
                ExpectedOutput = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["answer"] = "지구단위계획 결정도와 시행지침에서 건축선, 공개공지, 용도 제한을 먼저 확인해야 합니다.",
                    ["relatedLaws"] = new[] { "국토의 계획 및 이용에 관한 법률", "지구단위계획 시행지침" },
                    ["searchKeywords"] = new[] { "지구단위계획 시행지침", "건축선 공개공지", "용도 제한" },
                    ["manualReviewNeeded"] = new[] { "결정도와 시행지침 원문 대조" }
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
            if (TryParseJsonString(raw, out var parsed))
            {
                var outputKeys = parsed.ValueKind == JsonValueKind.Object
                    ? parsed.EnumerateObject().Select(static property => property.Name).ToList()
                    : [];
                return (parsed.GetRawText(), outputKeys);
            }

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

        var normalized = raw.Trim();
        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            var lines = normalized
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Where(static line => !line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                .ToArray();
            normalized = string.Join(Environment.NewLine, lines).Trim();
        }

        try
        {
            using var document = JsonDocument.Parse(normalized);
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

    private static string BuildFallbackStructuredJson(string rawContent)
    {
        if (TryParseJsonString(rawContent, out var parsed))
            return parsed.GetRawText();

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["answer"] = rawContent.Trim(),
            ["relatedLaws"] = Array.Empty<string>(),
            ["searchKeywords"] = Array.Empty<string>(),
            ["manualReviewNeeded"] = Array.Empty<string>()
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
