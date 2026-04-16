// =============================================================================
// ShapefileZoningLayerProvider.cs
// IZoningLayerProvider 구현체 - 로컬 SHP 디렉토리 기반 용도지역 프로바이더
//
// [역할 분리]
//   ShapefileLoader             : SHP 파일 읽기 (EUC-KR 인코딩, UQ 코드 추출)
//   ZoningFeatureCache          : 피처 목록 + 메타데이터 캐싱
//   CoordinateContainmentChecker: WGS84→EPSG:5174 변환 + Point-in-Polygon 판정
//   이 클래스                    : 디렉토리 재귀 탐색 + 위 3개 조합 오케스트레이터
//
// [디렉토리 구조 예시]
//   ZoningShapefileDirectory/
//     UPIS_004_20260201_11000/
//       UPIS_C_UQ111.shp   ← 제1종일반주거지역 등
//       UPIS_C_UQ122.shp
//       ...
//     KLIP_004_20260201_41000/
//       KLIP_C_UQ111.shp
//       ...
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Spatial;

#region ShapefileZoningLayerProvider 클래스

/// <summary>
/// 로컬 SHP 디렉토리 기반 <see cref="IZoningLayerProvider"/> 구현체입니다.
/// <para>
/// 처리 흐름:
/// <list type="number">
///   <item>캐시 조회 → 히트 시 즉시 반환</item>
///   <item>미스 → ZoningShapefileDirectory 재귀 탐색으로 모든 .shp 파일 로드</item>
///   <item>로드 성공 → <see cref="SpatialLayerMeta"/> 생성 후 캐시 저장</item>
///   <item>WGS84→EPSG:5174 변환 후 Point-in-Polygon 판정</item>
/// </list>
/// </para>
/// </summary>
public sealed class ShapefileZoningLayerProvider : IZoningLayerProvider
{
    #region 필드 및 생성자

    private readonly SpatialDataOptions             _options;
    private readonly ShapefileLoader                _shapefileLoader;
    private readonly ZoningFeatureCache             _cache;
    private readonly CoordinateContainmentChecker   _checker;
    private readonly ILogger<ShapefileZoningLayerProvider> _logger;

    /// <summary>ShapefileZoningLayerProvider를 초기화합니다.</summary>
    public ShapefileZoningLayerProvider(
        IOptions<SpatialDataOptions>         options,
        ShapefileLoader                      shapefileLoader,
        ZoningFeatureCache                   cache,
        CoordinateContainmentChecker         checker,
        ILogger<ShapefileZoningLayerProvider> logger)
    {
        _options         = options?.Value    ?? throw new ArgumentNullException(nameof(options));
        _shapefileLoader = shapefileLoader   ?? throw new ArgumentNullException(nameof(shapefileLoader));
        _cache           = cache             ?? throw new ArgumentNullException(nameof(cache));
        _checker         = checker           ?? throw new ArgumentNullException(nameof(checker));
        _logger          = logger            ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IZoningLayerProvider 구현

    /// <inheritdoc/>
    public async Task<ZoningFeature?> GetZoningAsync(CoordinateQuery query, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "용도지역 조회 요청: Lon={Lon}, Lat={Lat}",
            query.Longitude, query.Latitude);

        // ── 1. 피처 목록 + 메타데이터 로드 (캐시 우선) ─────────────────────
        var (features, meta) = await LoadAsync(ct);

        if (features.Count == 0)
        {
            _logger.LogWarning(
                "로드된 피처 없음. 디렉토리: {Dir}",
                ToAbsolutePath(_options.ZoningShapefileDirectory));
            return null;
        }

        // ── 2. WGS84→EPSG:5174 변환 + Point-in-Polygon 판정 ─────────────
        return _checker.Find(features, query, meta.LayerName, ct);
    }

    /// <inheritdoc/>
    public async Task<(ZoningFeature? Feature, string? DebugReason, double? NearestDistance, string? ZoningRaw)> 
        GetDebugZoningAsync(CoordinateQuery query, CancellationToken ct = default)
    {
        var (features, meta) = await LoadAsync(ct);
        if (features.Count == 0)
            return (null, "No spatial data loaded in cache.", null, null);

        // CoordinateContainmentChecker.FindWithDiagnostics() 사용
        return _checker.FindWithDiagnostics(features, query, meta.LayerName, ct);
    }

    /// <summary>
    /// 현재 캐시에 저장된 레이어 메타데이터를 반환합니다.
    /// 아직 로드되지 않았으면 <c>null</c>입니다.
    /// </summary>
    public SpatialLayerMeta? GetCurrentMeta() => _cache.TryGet()?.Meta;

    #endregion

    #region 디렉토리 탐색 + 피처 로드 오케스트레이션

    /// <summary>
    /// 캐시 또는 디렉토리 탐색으로 피처 목록과 메타데이터를 로드합니다.
    /// </summary>
    private async Task<(List<LoadedFeature> Features, SpatialLayerMeta Meta)> LoadAsync(
        CancellationToken ct)
    {
        // ── 캐시 히트 ────────────────────────────────────────────────────────
        var cached = _cache.TryGet();
        if (cached is not null)
            return (cached.Features, cached.Meta);

        // ── 디렉토리 탐색 ────────────────────────────────────────────────────
        var dirPath  = ToAbsolutePath(_options.ZoningShapefileDirectory);
        var loadedAt = DateTimeOffset.UtcNow;

        if (!Directory.Exists(dirPath))
        {
            _logger.LogError(
                "SHP 디렉토리를 찾을 수 없습니다. " +
                "경로: {Dir}\n" +
                "해결: appsettings.json의 SpatialData.ZoningShapefileDirectory를 " +
                "실제 데이터 폴더 경로로 설정하세요.",
                dirPath);

            var emptyMeta = new SpatialLayerMeta("데이터 없음", loadedAt, 0);
            _cache.Set(new List<LoadedFeature>(), emptyMeta);
            return (new List<LoadedFeature>(), emptyMeta);
        }

        // 디렉토리 하위의 모든 .shp 파일을 재귀 탐색
        var shpFiles = Directory
            .EnumerateFiles(dirPath, "*.shp", SearchOption.AllDirectories)
            .OrderBy(p => p)
            .ToList();

        if (shpFiles.Count == 0)
        {
            _logger.LogError(
                "디렉토리에 SHP 파일이 없습니다. " +
                "경로: {Dir}",
                dirPath);

            var emptyMeta = new SpatialLayerMeta("데이터 없음", loadedAt, 0);
            _cache.Set(new List<LoadedFeature>(), emptyMeta);
            return (new List<LoadedFeature>(), emptyMeta);
        }

        _logger.LogInformation(
            "SHP 파일 탐색 완료: {Count}개 파일 로드 시작. 경로: {Dir}",
            shpFiles.Count, dirPath);

        // ── 병렬 또는 순차 로드 (파일 수가 많아 Task.Run으로 스레드풀 사용) ──
        var allFeatures = await Task.Run(() =>
        {
            var list = new List<LoadedFeature>();
            foreach (var shpPath in shpFiles)
            {
                ct.ThrowIfCancellationRequested();
                var features = _shapefileLoader.Load(shpPath);
                list.AddRange(features);
            }
            return list;
        }, ct);

        // ── 메타데이터 생성 ───────────────────────────────────────────────────
        var layerName = $"전국 용도지역 ({shpFiles.Count}개 레이어, {dirPath})";
        var meta = new SpatialLayerMeta(
            layerName    : layerName,
            loadedAt     : loadedAt,
            featureCount : allFeatures.Count);

        _cache.Set(allFeatures, meta);

        _logger.LogInformation(
            "전체 SHP 로드 완료: 파일={FileCount}, 총 피처={FeatureCount}",
            shpFiles.Count, allFeatures.Count);

        return (allFeatures, meta);
    }

    #endregion

    #region 경로 유틸리티

    private static string ToAbsolutePath(string path) =>
        Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

    #endregion
}

#endregion
