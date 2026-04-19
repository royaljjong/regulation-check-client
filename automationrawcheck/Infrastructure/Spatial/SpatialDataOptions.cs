namespace AutomationRawCheck.Infrastructure.Spatial;

public sealed class SpatialDataOptions
{
    public const string SectionName = "SpatialData";

    public string ZoningShapefileDirectory { get; set; } = "Data/shp";

    public string CityPlanShapefileDirectory { get; set; } = "city plan data/shp";

    public string CityPlanCsvDirectory { get; set; } = "city plan data/csv";
}
