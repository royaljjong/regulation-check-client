namespace AutomationRawCheck.Domain.Models;

public sealed record CityPlanFacilityGeometry(
    string Key,
    string Label,
    string? Code,
    string CategoryKey,
    string CategoryLabel,
    string GeometryType,
    IReadOnlyList<ZoningGeometryPoint> Outline);
