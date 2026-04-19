// =============================================================================
// ShapefileDevActRestrictionProvider.cs
// 개발행위허가제한지역 프로바이더 — UQ171 SHP 레이어 기반 실구현체
//
// [데이터 근거]
//   KLIP_004_20260201 데이터셋의 UQ171 레이어는 ATRB_SE=UQQ900 코드를 가진
//   개발행위허가제한지역(국토계획법 제63조) 폴리곤으로 확인됩니다.
//   ※ UQ171 파일명은 UPIS 표준상 지구단위계획구역 코드이나,
//     이 데이터셋에서는 개발행위허가제한지역 데이터가 수록되어 있습니다.
//
// [좌표계]
//   SHP PRJ: EPSG:5174 (Korean TM) → ProjNet4GeoAPI로 WGS84 입력 변환 후 판정.
//
// [로딩 전략]
//   Singleton으로 등록하며, 첫 쿼리 시 SHP 디렉토리 내 **/UQ171*.shp 파일을 모두 로드합니다.
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Spatial;
using GeoAPI.CoordinateSystems.Transformations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace AutomationRawCheck.Infrastructure.ExtraLayers;

/// <summary>UQ171 피처에서 추출한 검증용 샘플 좌표 (WGS84)</summary>
public sealed record DevActRestrictionFeatureSample(
    int      FeatureIndex,
    string?  Name,
    string?  Code,
    string?  DgmNm,
    string?  Alias,
    string?  AtrbSe,
    string?  UqCode,
    double   CentroidLon,
    double   CentroidLat,
    double?  NearOutsideLon,
    double?  NearOutsideLat,
    double   AreaSqM);

#region ShapefileDevActRestrictionProvider 클래스

/// <summary>
/// UQ171 레이어(ATRB_SE=UQQ900) SHP 파일에서 개발행위허가제한지역 포함 여부를 판정하는 구현체입니다.
/// <para>
/// 개발행위허가제한지역(국토계획법 제63조)은 무분별한 개발행위를 막기 위해 지정하는 구역으로,
/// 포함 시 개발행위 허가가 제한되거나 불허될 수 있습니다.
/// </para>
/// <para>
/// 확정 판정이 아닌, UQ171 레이어 기준의 보수적 안내용 overlay 결과를 반환합니다.
/// </para>
/// </summary>
public sealed class ShapefileDevActRestrictionProvider : IDevActRestrictionProvider
{
    #region 상수

    private const string SourceDescription = "UQ171 레이어 (개발행위허가제한지역, UQQ900)";

    #endregion

    #region 필드 및 생성자

    private readonly SpatialDataOptions                           _options;
    private readonly ShapefileLoader                             _loader;
    private readonly CoordinateContainmentChecker                _checker;
    private readonly ILogger<ShapefileDevActRestrictionProvider> _logger;

    private List<LoadedFeature>? _features;
    private List<string>         _loadedFiles = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    /// <summary>ShapefileDevActRestrictionProvider를 초기화합니다.</summary>
    public ShapefileDevActRestrictionProvider(
        IOptions<SpatialDataOptions>                        options,
        ShapefileLoader                                     loader,
        CoordinateContainmentChecker                        checker,
        ILogger<ShapefileDevActRestrictionProvider>         logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loader  = loader         ?? throw new ArgumentNullException(nameof(loader));
        _checker = checker        ?? throw new ArgumentNullException(nameof(checker));
        _logger  = logger         ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IDevActRestrictionProvider 구현

    /// <inheritdoc/>
    public async Task<OverlayZoneResult> GetOverlayAsync(
        CoordinateQuery query,
        CancellationToken ct = default)
    {
        var features = await EnsureLoadedAsync(ct);

        if (features.Count == 0)
        {
            return new OverlayZoneResult(
                IsInside:   false,
                Name:       null,
                Code:       null,
                Source:     SourceDescription,
                Note:       "UQ171 파일을 찾을 수 없거나 피처가 없습니다. " +
                            "현재 연동 데이터 범위상 개발행위허가제한지역 여부를 확인할 수 없습니다.",
                Confidence: OverlayConfidenceLevel.DataUnavailable);
        }

        var match = _checker.Find(features, query, SourceDescription, ct);

        if (match is null)
        {
            var nearestMeters = _checker.FindNearestDistanceMeters(features, query);
            var confidence = nearestMeters.HasValue && nearestMeters.Value <= 200.0
                ? OverlayConfidenceLevel.NearBoundary
                : OverlayConfidenceLevel.Normal;

            _logger.LogDebug(
                "개발행위허가제한지역 미포함: Confidence={Confidence}, 최근접={Dist}m, Point=({Lon},{Lat})",
                confidence,
                nearestMeters.HasValue ? $"{nearestMeters:F0}" : "측정불가",
                query.Longitude, query.Latitude);

            return new OverlayZoneResult(
                IsInside:   false,
                Name:       null,
                Code:       null,
                Source:     SourceDescription,
                Note:       BuildNotFoundNote(nearestMeters, features.Count),
                Confidence: confidence);
        }

        var safeName = string.IsNullOrWhiteSpace(match.Name)
                       || match.Name.Equals("알수없음", StringComparison.OrdinalIgnoreCase)
                       || match.Name.Equals("알 수 없음", StringComparison.OrdinalIgnoreCase)
            ? null
            : match.Name;

        _logger.LogInformation(
            "개발행위허가제한지역 포함 확인: Name={Name}, Code={Code}",
            safeName ?? "(이름 없음)", match.Code);

        return new OverlayZoneResult(
            IsInside: true,
            Name:     safeName,
            Code:     match.Code,
            Source:   SourceDescription,
            Note:     "개발행위허가제한지역 내로 확인됩니다. " +
                      "개발행위 및 인허가 제한 여부를 관련 법령(국토계획법 제63조) 기준으로 추가 검토하세요.",
            Outline:  match.Outline);
    }

    #endregion

    #region UQ171 파일 지연 로딩

    private async Task<List<LoadedFeature>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_features is not null)
            return _features;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_features is not null)
                return _features;

            var dirPath = ToAbsolutePath(_options.ZoningShapefileDirectory);

            if (!Directory.Exists(dirPath))
            {
                _logger.LogWarning("SHP 디렉토리 없음 (개발행위허가제한지역): {Dir}", dirPath);
                _features = new List<LoadedFeature>();
                return _features;
            }

            var uq171Files = Directory
                .EnumerateFiles(dirPath, "*UQ171*.shp", SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();

            _loadedFiles = uq171Files
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList();

            _logger.LogInformation(
                "개발행위허가제한지역 UQ171 파일 로드 시작: {Count}개 [{Files}]",
                uq171Files.Count,
                string.Join(", ", _loadedFiles));

            var all = new List<LoadedFeature>();
            foreach (var path in uq171Files)
            {
                ct.ThrowIfCancellationRequested();
                all.AddRange(_loader.Load(path));
            }

            _features = all;
            _logger.LogInformation(
                "개발행위허가제한지역 UQ171 로드 완료: 파일={Files}, 피처={Features}",
                uq171Files.Count, all.Count);

            return _features;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static string ToAbsolutePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    #endregion

    #region 샘플 좌표 추출 (검증 전용)

    // EPSG:5174 → WGS84 역변환기
    private static readonly IMathTransform Epsg5174ToWgs84Transform = BuildReverseTransform();

    private static IMathTransform BuildReverseTransform()
    {
        const string epsg5174Wkt =
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

        var csFactory = new CoordinateSystemFactory();
        var ctFactory = new CoordinateTransformationFactory();
        var wgs84    = GeographicCoordinateSystem.WGS84;
        var epsg5174 = csFactory.CreateFromWkt(epsg5174Wkt);
        return ctFactory.CreateFromCoordinateSystems(epsg5174, wgs84).MathTransform;
    }

    /// <summary>
    /// 로드된 UQ171 피처에서 WGS84 centroid 좌표와 경계 인접 좌표를 추출합니다.
    /// 검증 전용 — 메인 API 경로에서 호출하지 마세요.
    /// </summary>
    public async Task<IReadOnlyList<DevActRestrictionFeatureSample>> GetSamplePointsAsync(
        int maxSamples = 10,
        CancellationToken ct = default)
    {
        var features = await EnsureLoadedAsync(ct);
        if (features.Count == 0)
            return Array.Empty<DevActRestrictionFeatureSample>();

        var results = new List<DevActRestrictionFeatureSample>();
        var step    = Math.Max(1, features.Count / maxSamples);

        for (var i = 0; i < features.Count && results.Count < maxSamples; i += step)
        {
            ct.ThrowIfCancellationRequested();

            var f = features[i];

            try
            {
                var geom     = f.Geometry;
                var centroid = geom.Centroid;
                var centWgs  = ToWgs84(centroid.X, centroid.Y);

                var bCoord    = geom.Boundary?.Coordinates?.FirstOrDefault();
                double? nearOutLon = null, nearOutLat = null;
                if (bCoord is not null)
                {
                    var dx  = bCoord.X - centroid.X;
                    var dy  = bCoord.Y - centroid.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 0)
                    {
                        var outsideX   = bCoord.X + dx / len * 5;
                        var outsideY   = bCoord.Y + dy / len * 5;
                        var outsideWgs = ToWgs84(outsideX, outsideY);
                        nearOutLon = outsideWgs[0];
                        nearOutLat = outsideWgs[1];
                    }
                }

                var dgmNm  = PickAttr(f, "DGM_NM",  "dgm_nm");
                var alias  = PickAttr(f, "ALIAS",   "alias");
                var atrbSe = PickAttr(f, "ATRB_SE", "atrb_se");
                var uqCode = PickAttr(f, ShapefileLoader.UqCodeKey);
                var name   = ZoneNameNormalizer.ResolveDisplayName(f.Attributes);

                results.Add(new DevActRestrictionFeatureSample(
                    FeatureIndex:   i,
                    Name:           name,
                    Code:           atrbSe ?? uqCode,
                    DgmNm:          dgmNm,
                    Alias:          alias,
                    AtrbSe:         atrbSe,
                    UqCode:         uqCode,
                    CentroidLon:    Math.Round(centWgs[0], 6),
                    CentroidLat:    Math.Round(centWgs[1], 6),
                    NearOutsideLon: nearOutLon.HasValue ? Math.Round(nearOutLon.Value, 6) : null,
                    NearOutsideLat: nearOutLat.HasValue ? Math.Round(nearOutLat.Value, 6) : null,
                    AreaSqM:        Math.Round(geom.Area, 1)));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "샘플 추출 실패 (index={Index})", i);
            }
        }

        return results;
    }

    private static double[] ToWgs84(double x, double y) =>
        Epsg5174ToWgs84Transform.Transform(new[] { x, y });

    private static string? PickAttr(LoadedFeature f, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (f.Attributes.TryGetValue(k, out var v))
            {
                var s = v?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        return null;
    }

    #endregion

    #region 진단 유틸리티

    private static string BuildNotFoundNote(double? nearestMeters, int featureCount)
    {
        if (nearestMeters is null)
            return $"개발행위허가제한지역 비포함으로 보이나 거리 측정 불가 (피처={featureCount}건). 추가 확인 필요.";

        var dist = nearestMeters.Value;

        if (dist <= 200)
            return $"개발행위허가제한지역 경계 근접 (약 {dist:F0}m). " +
                   "데이터 정밀도 한계상 계획도서 기준 추가 확인이 필요합니다.";

        if (dist <= 5_000)
            return $"개발행위허가제한지역 비포함. 최근접 경계까지 약 {dist:F0}m (피처={featureCount}건 기준).";

        return $"개발행위허가제한지역 비포함. 최근접 경계까지 약 {dist / 1000:F1}km — " +
               "해당 UQ171 데이터 커버리지 외부이거나 비지정 지역.";
    }

    #endregion
}

#endregion
