// =============================================================================
// CoordinateContainmentChecker.cs
// 좌표 포함 판정 전담 클래스 (Point-in-Polygon)
// - 피처 목록에서 입력 좌표를 포함하는 첫 번째 피처를 찾아 반환합니다.
// - ShapefileZoningLayerProvider에서 호출합니다.
//
// [좌표계 변환]
//   SHP 데이터: EPSG:5174 (Korean 1985 / Modified Central Belt TM)
//   API 입력  : EPSG:4326 (WGS84, 경도/위도)
//   → ProjNet4GeoAPI를 사용해 WGS84 입력을 EPSG:5174로 변환 후 판정합니다.
//
// [실제 컬럼 매핑 - 2026년 2월 데이터 기준]
//   DGM_NM  (UPIS) / dgm_nm  (KLIP) : 용도지역명 (예: "제2종일반주거지역")
//   ATRB_SE (UPIS) / atrb_se (KLIP) : 용도속성코드 (예: "UQA122")
//   _UQ_CODE (ShapefileLoader 주입)  : UQ 레이어 코드 (예: "UQ122")
// =============================================================================

using System.Text.Json;
using AutomationRawCheck.Domain.Models;
using GeoAPI.CoordinateSystems.Transformations;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace AutomationRawCheck.Infrastructure.Spatial;

#region CoordinateContainmentChecker 클래스

/// <summary>
/// 피처 목록에서 입력 WGS84 좌표를 포함하는 용도지역 피처를 찾는 전담 클래스입니다.
/// <para>
/// 내부적으로 WGS84 → EPSG:5174(Korean TM) 변환을 수행한 후 NTS Point-in-Polygon 판정합니다.
/// </para>
/// <para>
/// TODO (성능): 피처 수가 수만 건 이상이면 STRtree 공간 인덱스 사용을 고려하세요.
/// </para>
/// </summary>
public sealed class CoordinateContainmentChecker
{
    #region EPSG:5174 WKT (PRJ 파일 기준)

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

    #endregion

    #region 속성명 상수 (실제 DBF 컬럼명)

    // 용도지역명 후보 필드는 ZoneNameNormalizer.ResolveDisplayName()이 처리합니다.
    // (DGM_NM → ALIAS → ATRB_SE 코드 → "알 수 없음" 순서의 fallback 보장)

    /// <summary>
    /// 용도지역 코드 컬럼 후보 (우선순위 순).
    /// ATRB_SE가 UPIS/KLIP 공통 속성구분코드 컬럼입니다.
    /// </summary>
    private static readonly string[] CodeCandidates =
    {
        "ATRB_SE",      // UPIS / KLIP — 속성구분코드 (예: UQA122)
        "atrb_se",
        "SCLAS_CL",     // 소분류 코드 (ATRB_SE 없을 때 fallback)
        ShapefileLoader.UqCodeKey,  // _UQ_CODE — 파일명에서 추출한 UQ 코드 (최후 fallback)
    };

    #endregion

    #region 좌표 변환기 (정적, 스레드 안전)

    /// <summary>WGS84 → EPSG:5174 수학 변환기 (싱글턴으로 재사용)</summary>
    private static readonly IMathTransform Wgs84ToEpsg5174Transform = BuildTransform();
    private static readonly IMathTransform Epsg5174ToWgs84Transform = BuildReverseTransform();

    private static IMathTransform BuildTransform()
    {
        var csFactory = new CoordinateSystemFactory();
        var ctFactory = new CoordinateTransformationFactory();

        var wgs84   = GeographicCoordinateSystem.WGS84;
        var epsg5174 = csFactory.CreateFromWkt(Epsg5174Wkt);

        // WGS84 (경도, 위도) → EPSG:5174 (Easting, Northing)
        return ctFactory
            .CreateFromCoordinateSystems(wgs84, epsg5174)
            .MathTransform;
    }

    private static IMathTransform BuildReverseTransform()
    {
        var csFactory = new CoordinateSystemFactory();
        var ctFactory = new CoordinateTransformationFactory();

        var wgs84 = GeographicCoordinateSystem.WGS84;
        var epsg5174 = csFactory.CreateFromWkt(Epsg5174Wkt);

        return ctFactory
            .CreateFromCoordinateSystems(epsg5174, wgs84)
            .MathTransform;
    }

    #endregion

    #region 필드 및 생성자

    private readonly ILogger<CoordinateContainmentChecker> _logger;

    /// <summary>CoordinateContainmentChecker를 초기화합니다.</summary>
    public CoordinateContainmentChecker(ILogger<CoordinateContainmentChecker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region 포함 판정 메서드

    /// <summary>
    /// 피처 목록에서 입력 WGS84 좌표를 포함하는 첫 번째 <see cref="ZoningFeature"/>를 반환합니다.
    /// </summary>
    /// <param name="features">검색 대상 피처 목록 (EPSG:5174 좌표계)</param>
    /// <param name="query">WGS84 경도/위도 입력 좌표</param>
    /// <param name="sourceLayerName">원천 레이어명 (응답 DTO에 포함)</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>해당 좌표를 포함하는 용도지역 피처. 없으면 <c>null</c>.</returns>
    public ZoningFeature? Find(
        List<LoadedFeature> features,
        CoordinateQuery query,
        string sourceLayerName,
        CancellationToken ct = default)
    {
        #region WGS84 → EPSG:5174 좌표 변환

        double tmX, tmY;
        try
        {
            // ProjNet: Transform(double[] { lon, lat }) → { easting, northing }
            var transformed = Wgs84ToEpsg5174Transform.Transform(
                new[] { query.Longitude, query.Latitude });
            tmX = transformed[0];
            tmY = transformed[1];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "WGS84→EPSG:5174 좌표 변환 실패: Lon={Lon}, Lat={Lat}",
                query.Longitude, query.Latitude);
            return null;
        }

        // NTS Point (EPSG:5174 좌표계)
        var factory = new GeometryFactory(new NetTopologySuite.Geometries.PrecisionModel(), 5174);
        var point   = factory.CreatePoint(new Coordinate(tmX, tmY));

        _logger.LogDebug(
            "좌표 변환 완료: WGS84({Lon},{Lat}) → EPSG:5174({X},{Y})",
            query.Longitude, query.Latitude, tmX, tmY);

        _logger.LogDebug(
            "Point-in-Polygon 탐색 시작. 피처 수={Count}",
            features.Count);

        #endregion

        #region 선형 탐색 (Point-in-Polygon)

        foreach (var feature in features)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (!feature.Geometry.Contains(point) && !feature.Geometry.Covers(point))
                    continue;

                // 매칭 피처에서 이름/코드 추출
                // ZoneNameNormalizer: DGM_NM → ALIAS → ATRB_SE 코드 조회 순으로 fallback
                var rawDgmNm = feature.Attributes.TryGetValue("DGM_NM", out var r) ? r?.ToString() : null;
                var name     = ZoneNameNormalizer.ResolveDisplayName(feature.Attributes);
                var code     = PickAttributeValue(feature.Attributes, CodeCandidates) ?? string.Empty;

                _logger.LogInformation(
                    "포함 피처 발견: Name={Name} (rawDGM_NM={Raw}), Code={Code}, Layer={Layer}",
                    name, rawDgmNm, code, sourceLayerName);

                return new ZoningFeature(
                    name:        name,
                    code:        code,
                    sourceLayer: sourceLayerName,
                    attributes:  feature.Attributes,
                    outline:     BuildOutline(feature.Geometry));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "지오메트리 포함 판정 오류 (해당 피처 건너뜀)");
            }
        }

        #endregion

        _logger.LogDebug(
            "포함 피처 없음. EPSG:5174({X},{Y})", tmX, tmY);
        return null;
    }

    /// <summary>
    /// 진단 정보를 포함하여 포함 피처를 탐색합니다. (Fail-soft 핵심)
    /// </summary>
    public (ZoningFeature? Feature, string DebugReason, double? NearestDistance, string? ZoningRaw) 
        FindWithDiagnostics(
            List<LoadedFeature> features,
            CoordinateQuery query,
            string sourceLayerName,
            CancellationToken ct = default)
    {
        double tmX, tmY;
        try
        {
            var transformed = Wgs84ToEpsg5174Transform.Transform(
                new[] { query.Longitude, query.Latitude });
            tmX = transformed[0];
            tmY = transformed[1];
        }
        catch (Exception ex)
        {
            return (null, $"Coordinate transform failed: {ex.Message}", null, null);
        }

        var factory = new GeometryFactory(new NetTopologySuite.Geometries.PrecisionModel(), 5174);
        var point   = factory.CreatePoint(new Coordinate(tmX, tmY));

        double minDist = double.MaxValue;
        LoadedFeature? nearestFeature = null;

        foreach (var feature in features)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (feature.Geometry.Contains(point) || feature.Geometry.Covers(point))
                {
                    var name = ZoneNameNormalizer.ResolveDisplayName(feature.Attributes);
                    var code = PickAttributeValue(feature.Attributes, CodeCandidates) ?? string.Empty;
                    var zoning = new ZoningFeature(
                        name,
                        code,
                        sourceLayerName,
                        feature.Attributes,
                        BuildOutline(feature.Geometry));
                    return (zoning, "Successful zoning hit.", 0, StringifyAttributes(feature.Attributes));
                }

                // Miss case: Track nearest for diagnostics
                var d = feature.Geometry.Distance(point);
                if (d < minDist)
                {
                    minDist = d;
                    nearestFeature = feature;
                }
            }
            catch { continue; }
        }

        // Final Miss: Provide nearest info
        string reason = "No spatial hit (Point-in-Polygon miss)";
        string? raw = null;
        if (nearestFeature != null)
        {
            raw = StringifyAttributes(nearestFeature.Attributes);
            if (minDist < 1.0) reason = $"Boundary precision miss (Dist: {minDist:F3}m)";
            else if (minDist < 50.0) reason = $"Outside but near boundary (Dist: {minDist:F1}m)";
            else reason = $"Outside coverage (Nearest: {minDist:F1}m)";
        }

        return (null, reason, minDist < double.MaxValue ? minDist : null, raw);
    }

    #endregion

    #region 진단 유틸리티

    /// <summary>
    /// 피처 목록에서 입력 WGS84 좌표와 가장 가까운 폴리곤 경계까지의 거리(미터)를 반환합니다.
    /// <para>
    /// 진단 전용: Point-in-Polygon miss 시 "경계 근접 미스" vs "완전 커버리지 외부"를 구분합니다.
    /// EPSG:5174 미터 단위 거리를 그대로 반환합니다 (근사치).
    /// </para>
    /// </summary>
    /// <param name="features">검색 대상 피처 목록</param>
    /// <param name="query">WGS84 경도/위도 입력 좌표</param>
    /// <returns>가장 가까운 폴리곤까지의 거리(m). 변환 실패 또는 목록 비어있으면 null.</returns>
    public double? FindNearestDistanceMeters(
        List<LoadedFeature> features,
        CoordinateQuery     query)
    {
        if (features.Count == 0) return null;

        double tmX, tmY;
        try
        {
            var transformed = Wgs84ToEpsg5174Transform.Transform(
                new[] { query.Longitude, query.Latitude });
            tmX = transformed[0];
            tmY = transformed[1];
        }
        catch
        {
            return null;
        }

        var factory = new GeometryFactory(new NetTopologySuite.Geometries.PrecisionModel(), 5174);
        var point   = factory.CreatePoint(new Coordinate(tmX, tmY));

        var minDist = double.MaxValue;
        foreach (var feature in features)
        {
            try
            {
                var d = feature.Geometry.Distance(point);
                if (d < minDist) minDist = d;
            }
            catch { /* 지오메트리 오류 피처 건너뜀 */ }
        }

        return minDist < double.MaxValue ? minDist : null;
    }

    #endregion

    #region 속성값 추출 유틸리티

    /// <summary>
    /// 속성 딕셔너리에서 후보 키 순서대로 첫 번째 유효한 값을 반환합니다.
    /// </summary>
    private static string? PickAttributeValue(
        Dictionary<string, object?> attributes,
        string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (!attributes.TryGetValue(key, out var value))
                continue;

            var str = value?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(str))
                return str;
        }

        return null;
    }

    /// <summary>
    /// 속성 딕셔너리를 JSON 문자열로 변환합니다.
    /// </summary>
    public string StringifyAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        try
        {
            return JsonSerializer.Serialize(attributes, new JsonSerializerOptions { 
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch
        {
            return string.Join(", ", attributes.Select(kv => $"{kv.Key}={kv.Value}"));
        }
    }

    private static IReadOnlyList<ZoningGeometryPoint> BuildOutline(Geometry geometry)
    {
        var polygon = GetRepresentativePolygon(geometry);
        if (polygon is null)
        {
            return Array.Empty<ZoningGeometryPoint>();
        }

        var coordinates = polygon.ExteriorRing?.Coordinates;
        if (coordinates is null || coordinates.Length < 4)
        {
            return Array.Empty<ZoningGeometryPoint>();
        }

        var outline = new List<ZoningGeometryPoint>(coordinates.Length);
        foreach (var coordinate in coordinates)
        {
            var transformed = Epsg5174ToWgs84Transform.Transform(new[] { coordinate.X, coordinate.Y });
            var longitude = transformed[0];
            var latitude = transformed[1];

            if (outline.Count > 0)
            {
                var previous = outline[^1];
                if (Math.Abs(previous.Latitude - latitude) < 0.0000001 &&
                    Math.Abs(previous.Longitude - longitude) < 0.0000001)
                {
                    continue;
                }
            }

            outline.Add(new ZoningGeometryPoint(latitude, longitude));
        }

        return outline;
    }

    private static Polygon? GetRepresentativePolygon(Geometry geometry)
    {
        return geometry switch
        {
            Polygon polygon => polygon,
            MultiPolygon multiPolygon when multiPolygon.NumGeometries > 0 =>
                Enumerable.Range(0, multiPolygon.NumGeometries)
                    .Select(index => multiPolygon.GetGeometryN(index))
                    .OfType<Polygon>()
                    .OrderByDescending(item => item.Area)
                    .FirstOrDefault(),
            _ => null
        };
    }

    #endregion
}

#endregion
