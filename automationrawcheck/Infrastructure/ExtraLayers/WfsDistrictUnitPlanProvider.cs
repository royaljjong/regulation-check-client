using System.Text.Json;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.ExtraLayers;

public sealed class WfsDistrictUnitPlanProvider : IDistrictUnitPlanProvider
{
    private const string ClientName = "VWorldData";
    private static readonly TimeSpan NormalTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan UnavailableTtl = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpFactory;
    private readonly VWorldOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WfsDistrictUnitPlanProvider> _logger;

    public WfsDistrictUnitPlanProvider(
        IHttpClientFactory httpFactory,
        IOptions<VWorldOptions> options,
        IMemoryCache cache,
        ILogger<WfsDistrictUnitPlanProvider> logger)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OverlayZoneResult> GetOverlayAsync(
        CoordinateQuery query,
        CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(query);
        if (_cache.TryGetValue(cacheKey, out OverlayZoneResult? cached) && cached is not null)
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Unavailable("V-World API 키가 설정되지 않았습니다.");
        }

        var url = BuildUrl(query);

        try
        {
            var client = _httpFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "V-World district-unit-plan request failed. Status={StatusCode}, Lon={Lon}, Lat={Lat}",
                    (int)response.StatusCode,
                    query.Longitude,
                    query.Latitude);

                return Unavailable($"V-World API HTTP {(int)response.StatusCode} 오류");
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var result = ParseResponse(body, query);
            _cache.Set(
                cacheKey,
                result,
                result.Confidence == OverlayConfidenceLevel.DataUnavailable ? UnavailableTtl : NormalTtl);

            return result;
        }
        catch (TaskCanceledException)
        {
            return Unavailable($"V-World API 응답 시간 초과 ({_options.TimeoutSeconds}초)");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "V-World district-unit-plan request threw an exception. Lon={Lon}, Lat={Lat}",
                query.Longitude,
                query.Latitude);

            return Unavailable("V-World API 연결에 실패했습니다.");
        }
    }

    private string BuildUrl(CoordinateQuery query)
    {
        var point = Uri.EscapeDataString(
            FormattableString.Invariant($"POINT({query.Longitude:F6} {query.Latitude:F6})"));

        return $"{_options.DataBaseUrl}" +
               $"?service=data" +
               $"&request=GetFeature" +
               $"&data={Uri.EscapeDataString(_options.DistrictUnitPlanLayer)}" +
               $"&geomFilter={point}" +
               $"&geometry=true" +
               $"&attribute=true" +
               $"&crs=EPSG%3A4326" +
               $"&size=1" +
               $"&format=json" +
               $"&key={Uri.EscapeDataString(_options.ApiKey)}";
    }

    private OverlayZoneResult ParseResponse(string body, CoordinateQuery query)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var response = doc.RootElement.GetProperty("response");
            var status = response.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(status, "NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return new OverlayZoneResult(
                    IsInside: false,
                    Name: null,
                    Code: null,
                    Source: "api",
                    Note: "지구단위계획구역 비포함으로 확인됩니다.",
                    Confidence: OverlayConfidenceLevel.Normal);
            }

            if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                var errorText = response.TryGetProperty("error", out var errorElement) &&
                                errorElement.TryGetProperty("text", out var textElement)
                    ? textElement.GetString()
                    : null;

                return Unavailable($"V-World API 오류(status={status}): {errorText ?? "메시지 없음"}");
            }

            if (!response.TryGetProperty("result", out var resultElement) ||
                !resultElement.TryGetProperty("featureCollection", out var featureCollectionElement) ||
                !featureCollectionElement.TryGetProperty("features", out var featuresElement) ||
                featuresElement.GetArrayLength() == 0)
            {
                return new OverlayZoneResult(
                    IsInside: false,
                    Name: null,
                    Code: null,
                    Source: "api",
                    Note: "지구단위계획구역 비포함으로 확인됩니다.",
                    Confidence: OverlayConfidenceLevel.Normal);
            }

            var feature = featuresElement[0];
            var properties = feature.TryGetProperty("properties", out var propertiesElement)
                ? propertiesElement
                : default;
            var geometry = feature.TryGetProperty("geometry", out var geometryElement)
                ? geometryElement
                : default;

            var name = PickString(properties, "dgm_nm");
            var code = PickString(properties, "atrb_se");
            var area = PickString(properties, "sig_nam");
            var outline = TryBuildOutline(geometry);

            _logger.LogInformation(
                "District unit plan hit. Name={Name}, Code={Code}, Area={Area}, Lon={Lon}, Lat={Lat}, OutlineCount={OutlineCount}",
                name ?? "(unknown)",
                code ?? "-",
                area ?? "-",
                query.Longitude,
                query.Latitude,
                outline?.Count ?? 0);

            return new OverlayZoneResult(
                IsInside: true,
                Name: name,
                Code: code,
                Source: "api",
                Note: "지구단위계획구역으로 확인되었습니다. 시행지침과 건축선 조건을 함께 확인해야 합니다.",
                Confidence: OverlayConfidenceLevel.Normal,
                Outline: outline);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse V-World district-unit-plan response JSON.");
            return Unavailable("V-World API 응답 파싱에 실패했습니다.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unexpected V-World district-unit-plan response structure.");
            return Unavailable("V-World API 응답 구조가 예상과 다릅니다.");
        }
    }

    private static string BuildCacheKey(CoordinateQuery query) =>
        FormattableString.Invariant($"dup_{Math.Round(query.Latitude, 5):F5}_{Math.Round(query.Longitude, 5):F5}");

    private static OverlayZoneResult Unavailable(string note) =>
        new(
            IsInside: false,
            Name: null,
            Code: null,
            Source: "none",
            Note: note,
            Confidence: OverlayConfidenceLevel.DataUnavailable);

    private static string? PickString(JsonElement element, string key)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static IReadOnlyList<ZoningGeometryPoint>? TryBuildOutline(JsonElement geometry)
    {
        if (geometry.ValueKind != JsonValueKind.Object ||
            !geometry.TryGetProperty("type", out var typeElement) ||
            !geometry.TryGetProperty("coordinates", out var coordinatesElement))
        {
            return null;
        }

        var geometryType = typeElement.GetString();
        if (string.Equals(geometryType, "Polygon", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPolygonOutline(coordinatesElement);
        }

        if (string.Equals(geometryType, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
        {
            return BuildMultiPolygonOutline(coordinatesElement);
        }

        return null;
    }

    private static IReadOnlyList<ZoningGeometryPoint>? BuildPolygonOutline(JsonElement coordinates)
    {
        if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() == 0)
        {
            return null;
        }

        return BuildRingOutline(coordinates[0]);
    }

    private static IReadOnlyList<ZoningGeometryPoint>? BuildMultiPolygonOutline(JsonElement coordinates)
    {
        if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() == 0)
        {
            return null;
        }

        List<ZoningGeometryPoint>? best = null;

        foreach (var polygon in coordinates.EnumerateArray())
        {
            if (polygon.ValueKind != JsonValueKind.Array || polygon.GetArrayLength() == 0)
            {
                continue;
            }

            var candidate = BuildRingOutline(polygon[0]);
            if (candidate is null)
            {
                continue;
            }

            if (best is null || candidate.Count > best.Count)
            {
                best = candidate.ToList();
            }
        }

        return best;
    }

    private static IReadOnlyList<ZoningGeometryPoint>? BuildRingOutline(JsonElement ring)
    {
        if (ring.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var points = new List<ZoningGeometryPoint>();

        foreach (var point in ring.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2)
            {
                continue;
            }

            var longitude = point[0].GetDouble();
            var latitude = point[1].GetDouble();

            if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
            {
                continue;
            }

            points.Add(new ZoningGeometryPoint(latitude, longitude));
        }

        return points.Count >= 3 ? points : null;
    }
}
