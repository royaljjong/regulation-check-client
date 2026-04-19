namespace AutomationRawCheck.Domain.Models;

public readonly record struct ZoningGeometryPoint(double Latitude, double Longitude);

/// <summary>
/// Spatial zoning feature selected from SHP/CSV sources.
/// </summary>
public record ZoningFeature
{
    public string Name { get; init; }

    public string Code { get; init; }

    public string SourceLayer { get; init; }

    public IReadOnlyDictionary<string, object?> Attributes { get; init; }

    /// <summary>
    /// WGS84 outline for the matched zoning polygon.
    /// This is a representative zoning boundary, not a cadastral parcel boundary.
    /// </summary>
    public IReadOnlyList<ZoningGeometryPoint> Outline { get; init; }

    public ZoningFeature(
        string name,
        string code,
        string sourceLayer,
        IReadOnlyDictionary<string, object?> attributes,
        IReadOnlyList<ZoningGeometryPoint>? outline = null)
    {
        Name = name;
        Code = code;
        SourceLayer = sourceLayer;
        Attributes = attributes;
        Outline = outline ?? Array.Empty<ZoningGeometryPoint>();
    }
}
