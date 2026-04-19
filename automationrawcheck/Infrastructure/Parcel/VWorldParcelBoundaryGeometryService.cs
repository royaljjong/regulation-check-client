using System.Text.Json;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Parcel;

public sealed class VWorldParcelBoundaryGeometryService : IParcelBoundaryGeometryService
{
    private const string ClientName = "VWorldData";
    private const string ParcelLayer = "LP_PA_CBND_BUBUN";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VWorldOptions _options;
    private readonly ILogger<VWorldParcelBoundaryGeometryService> _logger;

    public VWorldParcelBoundaryGeometryService(
        IHttpClientFactory httpClientFactory,
        IOptions<VWorldOptions> options,
        ILogger<VWorldParcelBoundaryGeometryService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ParcelBoundaryGeometry?> FindContainingAsync(CoordinateQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(ClientName);
            var requestUri = BuildRequestUri(query);
            using var response = await client.GetAsync(requestUri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("V-World parcel boundary query failed. Status={StatusCode}", (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return ParseParcelGeometry(document.RootElement);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load parcel boundary geometry from V-World.");
            return null;
        }
    }

    private string BuildRequestUri(CoordinateQuery query)
    {
        var lon = query.Longitude.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var lat = query.Latitude.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        var geomFilter = Uri.EscapeDataString($"POINT({lon} {lat})");

        return $"{_options.DataBaseUrl}" +
               $"?service=data" +
               $"&request=GetFeature" +
               $"&data={ParcelLayer}" +
               $"&key={Uri.EscapeDataString(_options.ApiKey)}" +
               $"&geomfilter={geomFilter}" +
               $"&geometry=true" +
               $"&size=1" +
               $"&format=json";
    }

    private static ParcelBoundaryGeometry? ParseParcelGeometry(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var response))
        {
            return null;
        }

        var status = response.TryGetProperty("status", out var statusProperty)
            ? statusProperty.GetString()
            : null;

        if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!response.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("featureCollection", out var featureCollection) ||
            !featureCollection.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var firstFeature = features.EnumerateArray().FirstOrDefault();
        if (firstFeature.ValueKind != JsonValueKind.Object ||
            !firstFeature.TryGetProperty("geometry", out var geometry))
        {
            return null;
        }

        var outline = ParseOutline(geometry);
        if (outline.Count < 4)
        {
            return null;
        }

        var properties = firstFeature.TryGetProperty("properties", out var propertiesValue)
            ? propertiesValue
            : default;

        var pnu = properties.TryGetProperty("pnu", out var pnuValue) ? pnuValue.GetString() ?? string.Empty : string.Empty;
        var jibun = properties.TryGetProperty("jibun", out var jibunValue) ? jibunValue.GetString() ?? string.Empty : string.Empty;
        var address = properties.TryGetProperty("addr", out var addressValue) ? addressValue.GetString() ?? string.Empty : string.Empty;

        return new ParcelBoundaryGeometry(pnu, jibun, address, outline);
    }

    private static IReadOnlyList<ZoningGeometryPoint> ParseOutline(JsonElement geometry)
    {
        if (!geometry.TryGetProperty("type", out var typeValue) ||
            !geometry.TryGetProperty("coordinates", out var coordinatesValue))
        {
            return Array.Empty<ZoningGeometryPoint>();
        }

        var type = typeValue.GetString();
        var points = type switch
        {
            "Polygon" => ExtractLinearRing(coordinatesValue),
            "MultiPolygon" => ExtractLargestMultiPolygonRing(coordinatesValue),
            _ => Array.Empty<ZoningGeometryPoint>()
        };

        return points;
    }

    private static IReadOnlyList<ZoningGeometryPoint> ExtractLargestMultiPolygonRing(JsonElement coordinatesValue)
    {
        List<ZoningGeometryPoint>? best = null;
        var bestArea = double.MinValue;

        foreach (var polygon in coordinatesValue.EnumerateArray())
        {
            var ring = ExtractLinearRing(polygon);
            if (ring.Count < 4)
            {
                continue;
            }

            var area = Math.Abs(ComputeArea(ring));
            if (area > bestArea)
            {
                bestArea = area;
                best = ring;
            }
        }

        return best ?? new List<ZoningGeometryPoint>();
    }

    private static List<ZoningGeometryPoint> ExtractLinearRing(JsonElement polygonCoordinates)
    {
        var ringSource = polygonCoordinates.ValueKind == JsonValueKind.Array &&
                         polygonCoordinates.GetArrayLength() > 0 &&
                         polygonCoordinates[0].ValueKind == JsonValueKind.Array &&
                         polygonCoordinates[0].GetArrayLength() > 0 &&
                         polygonCoordinates[0][0].ValueKind == JsonValueKind.Array
            ? polygonCoordinates[0]
            : polygonCoordinates;

        var result = new List<ZoningGeometryPoint>();
        foreach (var coordinate in ringSource.EnumerateArray())
        {
            if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
            {
                continue;
            }

            var longitude = coordinate[0].GetDouble();
            var latitude = coordinate[1].GetDouble();

            if (result.Count > 0)
            {
                var previous = result[^1];
                if (Math.Abs(previous.Latitude - latitude) < 0.0000001 &&
                    Math.Abs(previous.Longitude - longitude) < 0.0000001)
                {
                    continue;
                }
            }

            result.Add(new ZoningGeometryPoint(latitude, longitude));
        }

        return result;
    }

    private static double ComputeArea(IReadOnlyList<ZoningGeometryPoint> ring)
    {
        if (ring.Count < 4)
        {
            return 0;
        }

        double area = 0;
        for (var i = 0; i < ring.Count - 1; i++)
        {
            var current = ring[i];
            var next = ring[i + 1];
            area += (current.Longitude * next.Latitude) - (next.Longitude * current.Latitude);
        }

        return area / 2d;
    }
}
