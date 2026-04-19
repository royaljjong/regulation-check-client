namespace AutomationRawCheck.Domain.Models;

/// <summary>
/// Indicates how reliable an overlay match is.
/// </summary>
public enum OverlayConfidenceLevel
{
    /// <summary>
    /// A normal, directly matched overlay result.
    /// </summary>
    Normal,

    /// <summary>
    /// The coordinate is near a boundary and the result should be reviewed manually.
    /// </summary>
    NearBoundary,

    /// <summary>
    /// The source data was missing or unavailable, so the overlay could not be verified.
    /// </summary>
    DataUnavailable,
}

/// <summary>
/// Result of checking whether a coordinate falls inside an overlay zone.
/// </summary>
/// <param name="IsInside">True when the coordinate is inside the overlay zone.</param>
/// <param name="Name">Overlay display name when available.</param>
/// <param name="Code">Overlay code when available.</param>
/// <param name="Source">Source identifier such as <c>shp</c>, <c>api</c>, or <c>none</c>.</param>
/// <param name="Note">Optional diagnostic note for the overlay result.</param>
/// <param name="Confidence">Reliability of the overlay result.</param>
/// <param name="Outline">Optional WGS84 outline geometry for map rendering.</param>
public record OverlayZoneResult(
    bool IsInside,
    string? Name,
    string? Code,
    string Source,
    string? Note = null,
    OverlayConfidenceLevel Confidence = OverlayConfidenceLevel.Normal,
    IReadOnlyList<ZoningGeometryPoint>? Outline = null);
