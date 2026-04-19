using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

public sealed class RegulationMapGeometryRequestDto
{
    [SwaggerSchema(Description = "Latitude in WGS84", Format = "double")]
    public double Latitude { get; init; }

    [SwaggerSchema(Description = "Longitude in WGS84", Format = "double")]
    public double Longitude { get; init; }
}

public sealed class RegulationMapGeometryPointDto
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }
}

public sealed class RegulationMapPolygonDto
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string GeometryType { get; init; } = "polygon";

    public string LegendGroupKey { get; init; } = string.Empty;

    public string LegendGroupLabel { get; init; } = string.Empty;

    public int LegendSortOrder { get; init; }

    public string StrokeColor { get; init; } = string.Empty;

    public string FillColor { get; init; } = string.Empty;

    public double FillOpacity { get; init; }

    public List<RegulationMapGeometryPointDto> Outline { get; init; } = new();
}

public sealed class RegulationMapLegendGroupDto
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public int ItemCount { get; init; }
}

public sealed class RegulationMapMatchedItemDto
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string GeometryType { get; init; } = "polygon";

    public string LegendGroupKey { get; init; } = string.Empty;

    public string LegendGroupLabel { get; init; } = string.Empty;

    public int LegendSortOrder { get; init; }
}

public sealed class RegulationMapGeometryResponseDto
{
    public List<RegulationMapPolygonDto> Polygons { get; init; } = new();

    public List<RegulationMapLegendGroupDto> LegendGroups { get; init; } = new();

    public List<RegulationMapMatchedItemDto> MatchedItems { get; init; } = new();
}
