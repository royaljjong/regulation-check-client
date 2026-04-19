using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

public interface ICityPlanFacilityGeometryService
{
    Task<IReadOnlyList<CityPlanFacilityGeometry>> FindContainingAsync(
        CoordinateQuery query,
        CancellationToken ct = default);
}
