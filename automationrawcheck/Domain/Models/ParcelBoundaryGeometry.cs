namespace AutomationRawCheck.Domain.Models;

public sealed record ParcelBoundaryGeometry(
    string Pnu,
    string Jibun,
    string Address,
    IReadOnlyList<ZoningGeometryPoint> Outline);
