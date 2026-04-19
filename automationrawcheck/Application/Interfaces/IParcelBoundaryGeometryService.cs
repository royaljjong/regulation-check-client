using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

public interface IParcelBoundaryGeometryService
{
    Task<ParcelBoundaryGeometry?> FindContainingAsync(CoordinateQuery query, CancellationToken ct = default);
}
