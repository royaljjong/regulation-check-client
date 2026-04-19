using System.Text;
using System.Text.RegularExpressions;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using GeoAPI.CoordinateSystems.Transformations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace AutomationRawCheck.Infrastructure.Spatial;

public sealed class CityPlanFacilityGeometryService : ICityPlanFacilityGeometryService
{
    private const string FeatureCacheKey = "CityPlanFacilityGeometryService.Features.v2";
    private const string MetadataCacheKey = "CityPlanFacilityGeometryService.Metadata.v1";
    private const string Epsg5174Wkt =
        "PROJCS[\"Korean 1985 / Modified Central Belt\"," +
        "GEOGCS[\"Korean 1985\"," +
        "DATUM[\"Korean Datum 1985\"," +
        "SPHEROID[\"Bessel 1841\",6377397.155,299.1528128,AUTHORITY[\"EPSG\",\"7004\"]]," +
        "TOWGS84[-115.8,474.99,674.11,1.16,-2.31,-1.63,6.43]," +
        "AUTHORITY[\"EPSG\",\"6162\"]]," +
        "PRIMEM[\"Greenwich\",0.0,AUTHORITY[\"EPSG\",\"8901\"]]," +
        "UNIT[\"degree\",0.017453292519943295]," +
        "AUTHORITY[\"EPSG\",\"4162\"]]," +
        "PROJECTION[\"Transverse_Mercator\"]," +
        "PARAMETER[\"central_meridian\",127.00289027777775]," +
        "PARAMETER[\"latitude_of_origin\",38.0]," +
        "PARAMETER[\"scale_factor\",1.0]," +
        "PARAMETER[\"false_easting\",200000.0]," +
        "PARAMETER[\"false_northing\",500000.0]," +
        "UNIT[\"m\",1.0]," +
        "AUTHORITY[\"EPSG\",\"5174\"]]";

    private static readonly Regex CityPlanCodeRegex =
        new(@"UQ15\d", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] NameCandidates =
    {
        "DGM_NM",
        "dgm_nm",
        "ALIAS",
        "alias",
        "NAME",
        "name",
    };

    private static readonly string[] CodeCandidates =
    {
        "ATRB_SE",
        "atrb_se",
        ShapefileLoader.UqCodeKey,
    };

    private static readonly CoordinateTransformationFactory CtFactory = new();
    private static readonly CoordinateSystemFactory CsFactory = new();
    private static readonly IMathTransform Wgs84ToEpsg5174 =
        CtFactory.CreateFromCoordinateSystems(
            GeographicCoordinateSystem.WGS84,
            CsFactory.CreateFromWkt(Epsg5174Wkt))
        .MathTransform;

    private readonly ShapefileLoader _loader;
    private readonly IMemoryCache _cache;
    private readonly SpatialDataOptions _options;
    private readonly ILogger<CityPlanFacilityGeometryService> _logger;

    public CityPlanFacilityGeometryService(
        ShapefileLoader loader,
        IMemoryCache cache,
        IOptions<SpatialDataOptions> options,
        ILogger<CityPlanFacilityGeometryService> logger)
    {
        _loader = loader;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<CityPlanFacilityGeometry>> FindContainingAsync(
        CoordinateQuery query,
        CancellationToken ct = default)
    {
        var features = GetOrLoadFeatures();
        if (features.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<CityPlanFacilityGeometry>>(Array.Empty<CityPlanFacilityGeometry>());
        }

        var metadata = GetOrLoadMetadata();
        var transformed = Wgs84ToEpsg5174.Transform(new[] { query.Longitude, query.Latitude });
        var point = new GeometryFactory(new PrecisionModel(), 5174)
            .CreatePoint(new Coordinate(transformed[0], transformed[1]));

        var matches = new List<CityPlanFacilityGeometry>();
        var sequence = 0;

        foreach (var feature in features)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (!feature.Geometry.Contains(point) && !feature.Geometry.Covers(point))
                {
                    continue;
                }

                var geometryShape = BuildGeometryShape(feature.Geometry);
                if (geometryShape is null || geometryShape.Value.Points.Count < 2)
                {
                    continue;
                }

                var shape = geometryShape.Value;
                var code = PickAttributeValue(feature.Attributes, CodeCandidates);
                var rawLabel = PickAttributeValue(feature.Attributes, NameCandidates);
                var metadataLabel = ResolveMetadataLabel(metadata, code);
                var label = ResolveDisplayLabel(rawLabel, metadataLabel, code);
                var category = ResolveCategory(code, label, metadataLabel);

                matches.Add(new CityPlanFacilityGeometry(
                    Key: $"{(code ?? "city_plan").ToLowerInvariant()}_{sequence++}",
                    Label: label,
                    Code: code,
                    CategoryKey: category.Key,
                    CategoryLabel: category.Label,
                    GeometryType: shape.GeometryType,
                    Outline: shape.Points));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to evaluate city-plan facility feature.");
            }
        }

        return Task.FromResult<IReadOnlyList<CityPlanFacilityGeometry>>(matches);
    }

    private List<LoadedFeature> GetOrLoadFeatures()
    {
        if (_cache.TryGetValue(FeatureCacheKey, out List<LoadedFeature>? cached) && cached is not null)
        {
            return cached;
        }

        var result = new List<LoadedFeature>();
        var root = _options.CityPlanShapefileDirectory;

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            _logger.LogInformation("City-plan SHP directory not available: {Directory}", root);
            _cache.Set(FeatureCacheKey, result, TimeSpan.FromHours(1));
            return result;
        }

        var shpFiles = Directory.EnumerateFiles(root, "*.shp", SearchOption.AllDirectories)
            .Where(path => CityPlanCodeRegex.IsMatch(Path.GetFileNameWithoutExtension(path)))
            .ToList();

        foreach (var shpFile in shpFiles)
        {
            result.AddRange(_loader.Load(shpFile));
        }

        _logger.LogInformation(
            "Loaded city-plan facility features. Directory={Directory}, Files={Files}, Features={Features}",
            root,
            shpFiles.Count,
            result.Count);

        _cache.Set(FeatureCacheKey, result, TimeSpan.FromHours(12));
        return result;
    }

    private Dictionary<string, string> GetOrLoadMetadata()
    {
        if (_cache.TryGetValue(MetadataCacheKey, out Dictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var root = ResolveCityPlanCsvDirectory();

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            _logger.LogInformation("City-plan CSV directory not available: {Directory}", root);
            _cache.Set(MetadataCacheKey, metadata, TimeSpan.FromHours(1));
            return metadata;
        }

        var csvFiles = Directory.EnumerateFiles(root, "*.csv", SearchOption.AllDirectories).ToList();
        foreach (var csvFile in csvFiles)
        {
            try
            {
                LoadMetadataFile(csvFile, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to load city-plan metadata csv: {CsvFile}", csvFile);
            }
        }

        _logger.LogInformation(
            "Loaded city-plan metadata. Directory={Directory}, Files={Files}, Entries={Entries}",
            root,
            csvFiles.Count,
            metadata.Count);

        _cache.Set(MetadataCacheKey, metadata, TimeSpan.FromHours(12));
        return metadata;
    }

    private string? ResolveCityPlanCsvDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.CityPlanCsvDirectory))
        {
            return _options.CityPlanCsvDirectory;
        }

        if (string.IsNullOrWhiteSpace(_options.CityPlanShapefileDirectory))
        {
            return null;
        }

        var shapeRoot = _options.CityPlanShapefileDirectory;
        var parent = Directory.GetParent(shapeRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }

        return Path.Combine(parent, "csv");
    }

    private static void LoadMetadataFile(string csvFile, IDictionary<string, string> metadata)
    {
        using var reader = new StreamReader(csvFile, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return;
        }

        var headers = ParseCsvLine(headerLine);
        var codeIndex = FindHeaderIndex(headers, "scls_cd");
        if (codeIndex < 0)
        {
            return;
        }

        var sclsNameIndex = FindHeaderIndex(headers, "scls_nm");
        var gradeIndex = FindHeaderIndex(headers, "scl_grd_nm");
        var kindIndex = FindHeaderIndex(headers, "scl_knd_nm");
        var lcNameIndex = FindHeaderIndex(headers, "lc_nm");
        var areaNameIndex = FindHeaderIndex(headers, "area_nm");

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = ParseCsvLine(line);
            var code = GetCell(cells, codeIndex)?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var label = FirstNonEmpty(
                GetCell(cells, sclsNameIndex),
                CombineRoadGrade(GetCell(cells, gradeIndex), GetCell(cells, kindIndex)),
                GetCell(cells, lcNameIndex),
                GetCell(cells, areaNameIndex));

            if (!string.IsNullOrWhiteSpace(label))
            {
                metadata[code] = label.Trim();
            }
        }
    }

    private static int FindHeaderIndex(IReadOnlyList<string> headers, string name)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (string.Equals(headers[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string? GetCell(IReadOnlyList<string> cells, int index)
    {
        return index >= 0 && index < cells.Count ? cells[index] : null;
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? CombineRoadGrade(string? grade, string? kind)
    {
        if (string.IsNullOrWhiteSpace(grade))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(kind) ? grade.Trim() : $"{grade.Trim()}{kind.Trim()}";
    }

    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];

            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static string? PickAttributeValue(
        IDictionary<string, object?> attributes,
        IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            var text = raw?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string? ResolveMetadataLabel(
        IReadOnlyDictionary<string, string> metadata,
        string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return metadata.TryGetValue(code.Trim(), out var label) ? label : null;
    }

    private static string ResolveDisplayLabel(string? rawLabel, string? metadataLabel, string? code)
    {
        if (!string.IsNullOrWhiteSpace(rawLabel) &&
            !string.Equals(rawLabel, code, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(rawLabel, "\uB3C4\uC2DC\uACC4\uD68D\uC2DC\uC124", StringComparison.Ordinal))
        {
            return rawLabel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(metadataLabel))
        {
            return metadataLabel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(rawLabel))
        {
            return rawLabel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            return code.Trim();
        }

        return "\uB3C4\uC2DC\uACC4\uD68D\uC2DC\uC124";
    }

    private static (string GeometryType, IReadOnlyList<ZoningGeometryPoint> Points)? BuildGeometryShape(Geometry geometry)
    {
        if (geometry is LineString lineString)
        {
            var linePoints = ToPoints(lineString.Coordinates);
            return linePoints.Count >= 2 ? ("line", linePoints) : null;
        }

        if (geometry is MultiLineString multiLineString && multiLineString.NumGeometries > 0)
        {
            var line = multiLineString.Geometries
                .OfType<LineString>()
                .OrderByDescending(candidate => candidate.Length)
                .FirstOrDefault();

            var linePoints = line is null ? [] : ToPoints(line.Coordinates);
            return linePoints.Count >= 2 ? ("line", linePoints) : null;
        }

        var polygon = geometry switch
        {
            Polygon singlePolygon => singlePolygon,
            MultiPolygon multiPolygon when multiPolygon.NumGeometries > 0 => multiPolygon.Geometries
                .OfType<Polygon>()
                .OrderByDescending(candidate => candidate.Area)
                .FirstOrDefault(),
            _ => null
        };

        var polygonPoints = polygon?.ExteriorRing is null ? [] : ToPoints(polygon.ExteriorRing.Coordinates);
        return polygonPoints.Count >= 3 ? ("polygon", polygonPoints) : null;
    }

    private static IReadOnlyList<ZoningGeometryPoint> ToPoints(Coordinate[] coordinates)
    {
        return coordinates
            .Select(coordinate => new ZoningGeometryPoint(coordinate.Y, coordinate.X))
            .ToList();
    }

    private static (string Key, string Label) ResolveCategory(string? code, string label, string? metadataLabel)
    {
        var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedLabel = $"{label} {metadataLabel}".Trim();

        if (normalizedCode.StartsWith("UQS", StringComparison.Ordinal))
        {
            return ("city_plan_transport", "\uB3C4\uB85C\u00B7\uAD50\uD1B5\uC2DC\uC124");
        }

        if (normalizedCode.StartsWith("UQT", StringComparison.Ordinal))
        {
            return ("city_plan_park_green", "\uACF5\uC6D0\u00B7\uB179\uC9C0\u00B7\uC720\uC6D0\uC9C0");
        }

        if (normalizedCode.StartsWith("UQU", StringComparison.Ordinal))
        {
            return ("city_plan_distribution", "\uC720\uD1B5\u00B7\uACF5\uAE09\uC2DC\uC124");
        }

        if (normalizedCode.StartsWith("UQV", StringComparison.Ordinal))
        {
            return ("city_plan_public", "\uACF5\uACF5\u00B7\uAD50\uC721\uC2DC\uC124");
        }

        if (normalizedCode.StartsWith("UQW", StringComparison.Ordinal))
        {
            return ("city_plan_water", "\uBC29\uC7AC\u00B7\uC218\uACF5\uAC04\uC2DC\uC124");
        }

        if (normalizedCode.StartsWith("UQX", StringComparison.Ordinal) ||
            normalizedCode.StartsWith("UQY", StringComparison.Ordinal))
        {
            return ("city_plan_utility", "\uD658\uACBD\u00B7\uC720\uD2F8\uB9AC\uD2F0\uC2DC\uC124");
        }

        if (normalizedLabel.Contains("\uD559\uAD50", StringComparison.Ordinal) ||
            normalizedLabel.Contains("\uAD50\uC721", StringComparison.Ordinal) ||
            normalizedLabel.Contains("\uACF5\uACF5\uCCAD\uC0AC", StringComparison.Ordinal))
        {
            return ("city_plan_public", "\uACF5\uACF5\u00B7\uAD50\uC721\uC2DC\uC124");
        }

        if (normalizedLabel.Contains("\uACF5\uC6D0", StringComparison.Ordinal) ||
            normalizedLabel.Contains("\uB179\uC9C0", StringComparison.Ordinal) ||
            normalizedLabel.Contains("\uC720\uC6D0\uC9C0", StringComparison.Ordinal))
        {
            return ("city_plan_park_green", "\uACF5\uC6D0\u00B7\uB179\uC9C0\u00B7\uC720\uC6D0\uC9C0");
        }

        if (normalizedLabel.Contains("\uC2DC\uC7A5", StringComparison.Ordinal) ||
            normalizedLabel.Contains("\uC8FC\uCC28\uC7A5", StringComparison.Ordinal))
        {
            return ("city_plan_distribution", "\uC720\uD1B5\u00B7\uACF5\uAE09\uC2DC\uC124");
        }

        if (normalizedLabel.Contains("\uCCA0\uB3C4", StringComparison.Ordinal) ||
            normalizedLabel.Contains("\uB3C4\uB85C", StringComparison.Ordinal))
        {
            return ("city_plan_transport", "\uB3C4\uB85C\u00B7\uAD50\uD1B5\uC2DC\uC124");
        }

        if (normalizedLabel.Contains("\uD558\uC218", StringComparison.Ordinal) ||
            normalizedLabel.Contains("\uCC98\uB9AC\uC2DC\uC124", StringComparison.Ordinal))
        {
            return ("city_plan_utility", "\uD658\uACBD\u00B7\uC720\uD2F8\uB9AC\uD2F0\uC2DC\uC124");
        }

        return ("city_plan_other", "\uAE30\uD0C0 \uB3C4\uC2DC\uACC4\uD68D\uC2DC\uC124");
    }
}
