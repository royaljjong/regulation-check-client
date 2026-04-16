using System.Globalization;
using System.Net;
using System.Text.Json;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.ExtraLayers;

/// <summary>
/// Generic API adapter for development-action permit restriction checks.
/// The concrete request and response contract is driven by configuration.
/// </summary>
public sealed class DevelopmentActionPermitApiProvider
{
    private const string HttpClientName = "development-action-permit-api";
    private const int ResponseSnippetMaxLength = 400;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DevelopmentActionPermitApiOptions _options;
    private readonly ILogger<DevelopmentActionPermitApiProvider> _logger;

    public DevelopmentActionPermitApiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<DevelopmentActionPermitApiOptions> options,
        ILogger<DevelopmentActionPermitApiProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public DevelopmentActionPermitApiDiagnostics GetDiagnostics(CoordinateQuery? sampleQuery = null)
    {
        var missingFields = GetMissingConfigurationFields();
        var canCall = missingFields.Count == 0;
        var effectiveQuery = sampleQuery ?? new CoordinateQuery(127.3845, 36.3504);

        return new DevelopmentActionPermitApiDiagnostics(
            Enabled: _options.Enabled,
            CanCall: canCall,
            MissingFields: missingFields,
            MaskedRequestUri: canCall ? BuildRequestUri(effectiveQuery, maskSecrets: true) : null,
            EffectiveLongitudeParameterName: _options.LongitudeParameterName,
            EffectiveLatitudeParameterName: _options.LatitudeParameterName,
            EffectiveUserIdParameterName: _options.UserIdParameterName,
            EffectiveServiceKeyParameterName: _options.ServiceKeyParameterName,
            StaticQueryParameterKeys: _options.StaticQueryParameters.Keys.OrderBy(x => x).ToArray(),
            RequestHeaderKeys: _options.RequestHeaders.Keys.OrderBy(x => x).ToArray(),
            EffectiveResponseRootPath: _options.ResponseRootPath,
            EffectiveInsideFieldPath: _options.InsideFieldPath,
            EffectiveNameFieldPath: _options.NameFieldPath,
            EffectiveCodeFieldPath: _options.CodeFieldPath);
    }

    public async Task<OverlayZoneResult?> TryGetOverlayAsync(CoordinateQuery query, CancellationToken ct = default)
    {
        if (!CanCallApi())
            return null;

        var requestUri = BuildRequestUri(query, maskSecrets: false);

        try
        {
            var client = CreateConfiguredClient();
            using var response = await client.GetAsync(requestUri, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[DevelopmentActionPermitApi] HTTP failure: StatusCode={StatusCode}, Url={Url}",
                    response.StatusCode,
                    requestUri);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return TryParseOverlay(json, out var overlay, out _)
                ? overlay
                : null;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[DevelopmentActionPermitApi] Request timed out.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DevelopmentActionPermitApi] Request failed.");
            return null;
        }
    }

    public DevelopmentActionPermitApiParseDiagnostics ParseSampleResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new DevelopmentActionPermitApiParseDiagnostics(
                Success: false,
                Error: "empty_json",
                Overlay: null);
        }

        return TryParseOverlay(json, out var overlay, out var error)
            ? new DevelopmentActionPermitApiParseDiagnostics(true, null, overlay)
            : new DevelopmentActionPermitApiParseDiagnostics(false, error, null);
    }

    public async Task<IReadOnlyList<DevelopmentActionPermitApiCandidateProbeResult>> ProbeCandidatePathsAsync(
        CoordinateQuery query,
        IEnumerable<string> candidatePaths,
        CancellationToken ct = default)
    {
        var normalizedPaths = candidatePaths
            .Select(NormalizeCandidatePath)
            .Where(static path => path is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPaths.Length == 0)
            normalizedPaths = [string.Empty];

        var missingFields = GetMissingConfigurationFieldsForCandidateProbe();
        if (missingFields.Count > 0)
        {
            return normalizedPaths
                .Select(path => new DevelopmentActionPermitApiCandidateProbeResult(
                    RequestPath: path,
                    MaskedRequestUri: BuildRequestUri(query, maskSecrets: true, requestPathOverride: path),
                    StatusCode: null,
                    ContentType: null,
                    IsJson: false,
                    ParseSuccess: false,
                    ParseError: string.Join(", ", missingFields),
                    Overlay: null,
                    ResponseSnippet: null,
                    Error: "missing_configuration"))
                .ToArray();
        }

        var results = new List<DevelopmentActionPermitApiCandidateProbeResult>(normalizedPaths.Length);
        var client = CreateConfiguredClient();

        foreach (var path in normalizedPaths)
        {
            var requestUri = BuildRequestUri(query, maskSecrets: false, requestPathOverride: path);
            var maskedRequestUri = BuildRequestUri(query, maskSecrets: true, requestPathOverride: path);

            try
            {
                using var response = await client.GetAsync(requestUri, ct);
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var body = await response.Content.ReadAsStringAsync(ct);
                var isJson = IsJsonContentType(contentType) || LooksLikeJson(body);
                var parseSuccess = false;
                string? parseError = null;
                OverlayZoneResult? overlay = null;

                if (isJson)
                {
                    if (string.IsNullOrWhiteSpace(_options.InsideFieldPath))
                    {
                        parseError = "inside_field_path_not_configured";
                    }
                    else if (TryParseOverlay(body, out overlay, out var error))
                    {
                        parseSuccess = true;
                    }
                    else
                    {
                        parseError = error;
                    }
                }

                results.Add(new DevelopmentActionPermitApiCandidateProbeResult(
                    RequestPath: path,
                    MaskedRequestUri: maskedRequestUri,
                    StatusCode: (int)response.StatusCode,
                    ContentType: contentType,
                    IsJson: isJson,
                    ParseSuccess: parseSuccess,
                    ParseError: parseError,
                    Overlay: overlay,
                    ResponseSnippet: CreateSnippet(body),
                    Error: response.IsSuccessStatusCode ? null : $"http_{(int)response.StatusCode}"));
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[DevelopmentActionPermitApi] Candidate path probe timed out: {RequestPath}", path);
                results.Add(new DevelopmentActionPermitApiCandidateProbeResult(
                    RequestPath: path,
                    MaskedRequestUri: maskedRequestUri,
                    StatusCode: null,
                    ContentType: null,
                    IsJson: false,
                    ParseSuccess: false,
                    ParseError: null,
                    Overlay: null,
                    ResponseSnippet: null,
                    Error: "timeout"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DevelopmentActionPermitApi] Candidate path probe failed: {RequestPath}", path);
                results.Add(new DevelopmentActionPermitApiCandidateProbeResult(
                    RequestPath: path,
                    MaskedRequestUri: maskedRequestUri,
                    StatusCode: null,
                    ContentType: null,
                    IsJson: false,
                    ParseSuccess: false,
                    ParseError: null,
                    Overlay: null,
                    ResponseSnippet: null,
                    Error: ex.GetType().Name));
            }
        }

        return results;
    }

    private bool CanCallApi()
    {
        var missingFields = GetMissingConfigurationFields();
        if (missingFields.Count == 0)
            return true;

        if (_options.Enabled)
        {
            _logger.LogWarning(
                "[DevelopmentActionPermitApi] Missing required configuration: {MissingFields}",
                string.Join(", ", missingFields));
        }

        return false;
    }

    private IReadOnlyList<string> GetMissingConfigurationFields()
    {
        var missing = new List<string>();

        if (!_options.Enabled)
            missing.Add("Enabled=false");
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            missing.Add(nameof(_options.BaseUrl));
        if (string.IsNullOrWhiteSpace(_options.UserId))
            missing.Add(nameof(_options.UserId));
        if (string.IsNullOrWhiteSpace(_options.ServiceKey))
            missing.Add(nameof(_options.ServiceKey));
        if (string.IsNullOrWhiteSpace(_options.InsideFieldPath))
            missing.Add(nameof(_options.InsideFieldPath));
        if (string.IsNullOrWhiteSpace(_options.LongitudeParameterName))
            missing.Add(nameof(_options.LongitudeParameterName));
        if (string.IsNullOrWhiteSpace(_options.LatitudeParameterName))
            missing.Add(nameof(_options.LatitudeParameterName));
        if (string.IsNullOrWhiteSpace(_options.UserIdParameterName))
            missing.Add(nameof(_options.UserIdParameterName));
        if (string.IsNullOrWhiteSpace(_options.ServiceKeyParameterName))
            missing.Add(nameof(_options.ServiceKeyParameterName));

        return missing;
    }

    private IReadOnlyList<string> GetMissingConfigurationFieldsForCandidateProbe()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            missing.Add(nameof(_options.BaseUrl));
        if (string.IsNullOrWhiteSpace(_options.UserId))
            missing.Add(nameof(_options.UserId));
        if (string.IsNullOrWhiteSpace(_options.ServiceKey))
            missing.Add(nameof(_options.ServiceKey));
        if (string.IsNullOrWhiteSpace(_options.LongitudeParameterName))
            missing.Add(nameof(_options.LongitudeParameterName));
        if (string.IsNullOrWhiteSpace(_options.LatitudeParameterName))
            missing.Add(nameof(_options.LatitudeParameterName));
        if (string.IsNullOrWhiteSpace(_options.UserIdParameterName))
            missing.Add(nameof(_options.UserIdParameterName));
        if (string.IsNullOrWhiteSpace(_options.ServiceKeyParameterName))
            missing.Add(nameof(_options.ServiceKeyParameterName));

        return missing;
    }

    private HttpClient CreateConfiguredClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        foreach (var header in _options.RequestHeaders)
        {
            client.DefaultRequestHeaders.Remove(header.Key);
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        return client;
    }

    private string BuildRequestUri(CoordinateQuery query, bool maskSecrets)
        => BuildRequestUri(query, maskSecrets, requestPathOverride: null);

    private string BuildRequestUri(CoordinateQuery query, bool maskSecrets, string? requestPathOverride)
    {
        var userId = maskSecrets ? MaskSecret(_options.UserId) : _options.UserId;
        var serviceKey = maskSecrets ? MaskSecret(_options.ServiceKey) : _options.ServiceKey;

        var parameters = new Dictionary<string, string>
        {
            [_options.LongitudeParameterName] = query.Longitude.ToString(CultureInfo.InvariantCulture),
            [_options.LatitudeParameterName] = query.Latitude.ToString(CultureInfo.InvariantCulture),
            [_options.UserIdParameterName] = userId,
            [_options.ServiceKeyParameterName] = serviceKey,
        };

        foreach (var staticParameter in _options.StaticQueryParameters)
            parameters[staticParameter.Key] = staticParameter.Value;

        var queryString = string.Join("&",
            parameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var requestPathSource = requestPathOverride ?? _options.RequestPath;
        var requestPath = string.IsNullOrWhiteSpace(requestPathSource)
            ? string.Empty
            : "/" + requestPathSource.Trim().TrimStart('/');

        return $"{baseUrl}{requestPath}?{queryString}";
    }

    private static string NormalizeCandidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var normalized = path.Trim();
        if (normalized == "/")
            return string.Empty;

        return normalized.TrimStart('/');
    }

    private static string CreateSnippet(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        var trimmed = body.Trim();
        if (trimmed.Length <= ResponseSnippetMaxLength)
            return trimmed;

        return trimmed[..ResponseSnippetMaxLength] + "...";
    }

    private static bool IsJsonContentType(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType) &&
           contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        var trimmed = body.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) ||
               trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        if (value.Length <= 4)
            return new string('*', value.Length);

        return $"{value[..2]}***{value[^2..]}";
    }

    private bool TryParseOverlay(string json, out OverlayZoneResult? overlay, out string? error)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!string.IsNullOrWhiteSpace(_options.ResponseRootPath))
            {
                var nestedRoot = TryReadPath(root, _options.ResponseRootPath);
                if (nestedRoot is null)
                {
                    _logger.LogWarning("[DevelopmentActionPermitApi] ResponseRootPath was not found in response.");
                    overlay = null;
                    error = "response_root_path_not_found";
                    return false;
                }

                root = nestedRoot.Value;
            }

            var insideValue = TryReadPath(root, _options.InsideFieldPath);
            if (insideValue is null)
            {
                _logger.LogWarning("[DevelopmentActionPermitApi] InsideFieldPath was not found in response.");
                overlay = null;
                error = "inside_field_path_not_found";
                return false;
            }

            if (!TryConvertToBool(insideValue.Value, out var isInside))
            {
                _logger.LogWarning("[DevelopmentActionPermitApi] InsideFieldPath value could not be parsed as boolean.");
                overlay = null;
                error = "inside_field_value_invalid";
                return false;
            }

            var name = TryReadPath(root, _options.NameFieldPath)?.ToString();
            var code = TryReadPath(root, _options.CodeFieldPath)?.ToString();

            overlay = new OverlayZoneResult(
                IsInside: isInside,
                Name: isInside ? name : null,
                Code: isInside ? code : null,
                Source: "api",
                Note: isInside
                    ? "Development-action permit API reported an inside match."
                    : "Development-action permit API reported no inside match.",
                Confidence: OverlayConfidenceLevel.Normal);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[DevelopmentActionPermitApi] Response JSON parsing failed.");
            overlay = null;
            error = "invalid_json";
            return false;
        }
    }

    private static JsonElement? TryReadPath(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryReadSegment(current, segment, out current))
                return null;
        }

        return current;
    }

    private static bool TryReadSegment(JsonElement current, string segment, out JsonElement result)
    {
        result = current;
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        var remaining = segment;
        var bracketIndex = remaining.IndexOf('[');
        if (bracketIndex < 0)
            return TryReadProperty(current, remaining, out result);

        if (bracketIndex > 0)
        {
            var propertyName = remaining[..bracketIndex];
            if (!TryReadProperty(current, propertyName, out result))
                return false;
        }

        var cursor = bracketIndex > 0 ? result : current;
        var tail = bracketIndex > 0 ? remaining[bracketIndex..] : remaining;
        while (!string.IsNullOrEmpty(tail))
        {
            if (!tail.StartsWith("[", StringComparison.Ordinal))
                return false;

            var closingIndex = tail.IndexOf(']');
            if (closingIndex <= 1)
                return false;

            var indexToken = tail[1..closingIndex];
            if (!int.TryParse(indexToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var arrayIndex))
                return false;

            if (cursor.ValueKind != JsonValueKind.Array || arrayIndex < 0 || arrayIndex >= cursor.GetArrayLength())
                return false;

            cursor = cursor[arrayIndex];
            tail = tail[(closingIndex + 1)..];
        }

        result = cursor;
        return true;
    }

    private static bool TryReadProperty(JsonElement current, string propertyName, out JsonElement result)
    {
        result = current;
        return current.ValueKind == JsonValueKind.Object &&
               current.TryGetProperty(propertyName, out result);
    }

    private static bool TryConvertToBool(JsonElement element, out bool value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                {
                    value = intValue != 0;
                    return true;
                }
                break;
            case JsonValueKind.String:
                var text = element.GetString()?.Trim();
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "y", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "1", StringComparison.OrdinalIgnoreCase))
                {
                    value = true;
                    return true;
                }

                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "n", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "no", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "0", StringComparison.OrdinalIgnoreCase))
                {
                    value = false;
                    return true;
                }
                break;
        }

        value = false;
        return false;
    }
}

public sealed record DevelopmentActionPermitApiDiagnostics(
    bool Enabled,
    bool CanCall,
    IReadOnlyList<string> MissingFields,
    string? MaskedRequestUri,
    string EffectiveLongitudeParameterName,
    string EffectiveLatitudeParameterName,
    string EffectiveUserIdParameterName,
    string EffectiveServiceKeyParameterName,
    IReadOnlyList<string> StaticQueryParameterKeys,
    IReadOnlyList<string> RequestHeaderKeys,
    string EffectiveResponseRootPath,
    string EffectiveInsideFieldPath,
    string EffectiveNameFieldPath,
    string EffectiveCodeFieldPath);

public sealed record DevelopmentActionPermitApiParseDiagnostics(
    bool Success,
    string? Error,
    OverlayZoneResult? Overlay);

public sealed record DevelopmentActionPermitApiCandidateProbeResult(
    string RequestPath,
    string MaskedRequestUri,
    int? StatusCode,
    string? ContentType,
    bool IsJson,
    bool ParseSuccess,
    string? ParseError,
    OverlayZoneResult? Overlay,
    string? ResponseSnippet,
    string? Error);
