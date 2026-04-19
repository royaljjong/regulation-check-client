using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace AutomationRawCheck.Api.Controllers;

[ApiController]
[Route("api/regulation-check")]
public sealed class RegulationMapGeometryController : ControllerBase
{
    private readonly IRegulationCheckService _regulationCheckService;
    private readonly ICityPlanFacilityGeometryService _cityPlanFacilityGeometryService;

    public RegulationMapGeometryController(
        IRegulationCheckService regulationCheckService,
        ICityPlanFacilityGeometryService cityPlanFacilityGeometryService)
    {
        _regulationCheckService = regulationCheckService;
        _cityPlanFacilityGeometryService = cityPlanFacilityGeometryService;
    }

    [HttpPost("map-geometries")]
    public async Task<ActionResult<RegulationMapGeometryResponseDto>> PostMapGeometries(
        [FromBody] RegulationMapGeometryRequestDto request,
        CancellationToken ct)
    {
        var query = new CoordinateQuery(request.Longitude, request.Latitude);
        var result = await _regulationCheckService.CheckAsync(query, ct);

        var geometries = new List<RegulationMapPolygonDto>();

        AddGeometry(
            geometries,
            "zoning",
            result.Zoning?.Name ?? "\uC6A9\uB3C4\uC9C0\uC5ED",
            "polygon",
            "base_zoning",
            "\uAE30\uBCF8 \uB808\uC774\uC5B4",
            10,
            "#2563eb",
            "#60a5fa",
            0.20,
            result.Zoning?.Outline);

        AddGeometry(
            geometries,
            "development_restriction",
            result.ExtraLayers.DevelopmentRestriction?.Name ?? "\uAC1C\uBC1C\uC81C\uD55C\uAD6C\uC5ED",
            "polygon",
            "overlay_restriction",
            "\uADDC\uC81C\u00B7\uACC4\uD68D \uB808\uC774\uC5B4",
            20,
            "#15803d",
            "#4ade80",
            0.16,
            result.ExtraLayers.DevelopmentRestriction?.Outline);

        AddGeometry(
            geometries,
            "district_unit_plan",
            result.ExtraLayers.DistrictUnitPlan?.Name ?? "\uC9C0\uAD6C\uB2E8\uC704\uACC4\uD68D\uAD6C\uC5ED",
            "polygon",
            "overlay_restriction",
            "\uADDC\uC81C\u00B7\uACC4\uD68D \uB808\uC774\uC5B4",
            20,
            "#7c3aed",
            "#c4b5fd",
            0.18,
            result.ExtraLayers.DistrictUnitPlan?.Outline);

        AddGeometry(
            geometries,
            "development_action_restriction",
            result.ExtraLayers.DevelopmentActionRestriction?.Name ?? "\uAC1C\uBC1C\uD589\uC704\uD5C8\uAC00 \uC81C\uD55C",
            "polygon",
            "overlay_restriction",
            "\uADDC\uC81C\u00B7\uACC4\uD68D \uB808\uC774\uC5B4",
            20,
            "#dc2626",
            "#f87171",
            0.14,
            result.ExtraLayers.DevelopmentActionRestriction?.Outline);

        var cityPlanFacilities = await _cityPlanFacilityGeometryService.FindContainingAsync(query, ct);
        foreach (var facility in cityPlanFacilities)
        {
            var style = ResolveCityPlanStyle(facility.CategoryKey, facility.GeometryType);

            AddGeometry(
                geometries,
                facility.Key,
                facility.Code is { Length: > 0 }
                    ? $"{facility.Label} ({facility.Code})"
                    : facility.Label,
                facility.GeometryType,
                facility.CategoryKey,
                facility.CategoryLabel,
                30 + style.SortOffset,
                style.StrokeColor,
                style.FillColor,
                style.FillOpacity,
                facility.Outline);
        }

        var matchedItems = geometries
            .Select(geometry => new RegulationMapMatchedItemDto
            {
                Key = geometry.Key,
                Label = geometry.Label,
                GeometryType = geometry.GeometryType,
                LegendGroupKey = geometry.LegendGroupKey,
                LegendGroupLabel = geometry.LegendGroupLabel,
                LegendSortOrder = geometry.LegendSortOrder
            })
            .OrderBy(item => item.LegendSortOrder)
            .ThenBy(item => item.Label, StringComparer.CurrentCulture)
            .ToList();

        var legendGroups = matchedItems
            .GroupBy(item => new { item.LegendGroupKey, item.LegendGroupLabel, item.LegendSortOrder })
            .OrderBy(group => group.Key.LegendSortOrder)
            .ThenBy(group => group.Key.LegendGroupLabel, StringComparer.CurrentCulture)
            .Select(group => new RegulationMapLegendGroupDto
            {
                Key = group.Key.LegendGroupKey,
                Label = group.Key.LegendGroupLabel,
                SortOrder = group.Key.LegendSortOrder,
                ItemCount = group.Count()
            })
            .ToList();

        return Ok(new RegulationMapGeometryResponseDto
        {
            Polygons = geometries,
            LegendGroups = legendGroups,
            MatchedItems = matchedItems
        });
    }

    private static void AddGeometry(
        List<RegulationMapPolygonDto> geometries,
        string key,
        string label,
        string geometryType,
        string legendGroupKey,
        string legendGroupLabel,
        int legendSortOrder,
        string strokeColor,
        string fillColor,
        double fillOpacity,
        IReadOnlyList<ZoningGeometryPoint>? outline)
    {
        if (outline is null || outline.Count < 2)
        {
            return;
        }

        if (geometryType == "polygon" && outline.Count < 3)
        {
            return;
        }

        geometries.Add(new RegulationMapPolygonDto
        {
            Key = key,
            Label = label,
            GeometryType = geometryType,
            LegendGroupKey = legendGroupKey,
            LegendGroupLabel = legendGroupLabel,
            LegendSortOrder = legendSortOrder,
            StrokeColor = strokeColor,
            FillColor = fillColor,
            FillOpacity = fillOpacity,
            Outline = outline
                .Select(point => new RegulationMapGeometryPointDto
                {
                    Latitude = point.Latitude,
                    Longitude = point.Longitude
                })
                .ToList()
        });
    }

    private static (string StrokeColor, string FillColor, double FillOpacity, int SortOffset) ResolveCityPlanStyle(
        string categoryKey,
        string geometryType)
    {
        var isLine = string.Equals(geometryType, "line", StringComparison.OrdinalIgnoreCase);

        return categoryKey switch
        {
            "city_plan_transport" => ("#b45309", isLine ? "#fde68a" : "#f59e0b", isLine ? 0.08 : 0.20, 1),
            "city_plan_park_green" => ("#15803d", "#86efac", 0.20, 2),
            "city_plan_public" => ("#7c3aed", "#c4b5fd", 0.20, 3),
            "city_plan_distribution" => ("#0f766e", "#5eead4", 0.20, 4),
            "city_plan_water" => ("#0369a1", "#7dd3fc", 0.20, 5),
            "city_plan_utility" => ("#475569", "#cbd5e1", 0.20, 6),
            _ => ("#be123c", "#fda4af", 0.18, 7),
        };
    }
}
