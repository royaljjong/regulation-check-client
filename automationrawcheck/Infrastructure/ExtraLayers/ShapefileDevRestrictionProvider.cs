// =============================================================================
// ShapefileDevRestrictionProvider.cs
// 개발제한구역(그린벨트) 프로바이더 - UQ141 SHP 레이어 기반 실구현체
//
// [데이터 근거]
//   UPIS/KLIP UQ141 레이어의 ATRB_SE = "UDV100" 폴리곤이 개발제한구역을 나타냅니다.
//   PRJ 좌표계: EPSG:5174 (Korean TM) → ProjNet4GeoAPI로 WGS84 입력 변환 후 판정.
//
// [로딩 전략]
//   Singleton으로 등록하며, 첫 쿼리 시 SHP 디렉토리 내 **/UQ141*.shp 파일을 모두 로드합니다.
//   전체 724k 피처 캐시와 분리된 소규모 전용 캐시입니다.
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Spatial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.ExtraLayers;

#region ShapefileDevRestrictionProvider 클래스

/// <summary>
/// UQ141 레이어 SHP 파일에서 개발제한구역(그린벨트) 포함 여부를 판정하는 구현체입니다.
/// <para>
/// 개발제한구역 폴리곤: ATRB_SE = <c>UDV100</c>
/// </para>
/// </summary>
public sealed class ShapefileDevRestrictionProvider : IDevelopmentRestrictionProvider
{
    #region 상수

    private const string DevRestrictionCode   = "UDV100";
    private const string SourceDescription    = "UQ141 레이어 (UPIS/KLIP 개발제한구역)";

    #endregion

    #region 필드 및 생성자

    private readonly SpatialDataOptions               _options;
    private readonly ShapefileLoader                  _loader;
    private readonly CoordinateContainmentChecker     _checker;
    private readonly ILogger<ShapefileDevRestrictionProvider> _logger;

    // 지연 로딩: 첫 쿼리 시 초기화
    private List<LoadedFeature>? _features;
    private List<string>         _loadedFiles = new();   // 로드된 UQ141 파일명 목록 (진단용)
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    /// <summary>ShapefileDevRestrictionProvider를 초기화합니다.</summary>
    public ShapefileDevRestrictionProvider(
        IOptions<SpatialDataOptions>                       options,
        ShapefileLoader                                    loader,
        CoordinateContainmentChecker                       checker,
        ILogger<ShapefileDevRestrictionProvider>           logger)
    {
        _options  = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loader   = loader         ?? throw new ArgumentNullException(nameof(loader));
        _checker  = checker        ?? throw new ArgumentNullException(nameof(checker));
        _logger   = logger         ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IDevelopmentRestrictionProvider 구현

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
                Note:       "UQ141 파일을 찾을 수 없거나 피처가 없습니다. " +
                            "개발제한구역 여부를 현재 데이터로 확인할 수 없습니다.",
                Confidence: OverlayConfidenceLevel.DataUnavailable);
        }

        // 개발제한구역(UDV100) 폴리곤만 필터링
        var drzFeatures = features
            .Where(f =>
            {
                var atrb = (f.Attributes.TryGetValue("ATRB_SE", out var v) ? v?.ToString() : null)
                        ?? (f.Attributes.TryGetValue("atrb_se", out var v2) ? v2?.ToString() : null);
                return string.Equals(atrb, DevRestrictionCode, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        _logger.LogDebug(
            "개발제한구역 판정: UQ141 전체={Total}, UDV100={Drz}, Point=({Lon},{Lat})",
            features.Count, drzFeatures.Count, query.Longitude, query.Latitude);

        // ── UDV100 피처 없음: 데이터 구조 또는 파일 커버리지 문제 가능성 ───────
        if (drzFeatures.Count == 0)
        {
            _logger.LogWarning(
                "UDV100 피처 없음 — UQ141 전체 {Total}건 로드됐으나 ATRB_SE=UDV100 매칭 없음. " +
                "필드명({Fields})·코드 값 확인 필요. Point=({Lon},{Lat})",
                features.Count,
                SampleAtrbSeValues(features),
                query.Longitude, query.Latitude);

            return new OverlayZoneResult(
                IsInside:   false,
                Name:       null,
                Code:       null,
                Source:     SourceDescription,
                Note:       $"UDV100 피처 없음 (UQ141 로드={features.Count}건). " +
                            "ATRB_SE 필드명 또는 코드값 불일치 가능성. 데이터 확인 필요.",
                Confidence: OverlayConfidenceLevel.DataUnavailable);
        }

        var match = _checker.Find(drzFeatures, query, SourceDescription, ct);

        if (match is null)
        {
            // ── 경계 근접 미스 여부 확인 (진단용) ─────────────────────────────
            var nearestMeters = _checker.FindNearestDistanceMeters(drzFeatures, query);

            // ≤200m: 경계 근접 — 좌표 정밀도 한계 가능성
            var confidence = nearestMeters.HasValue && nearestMeters.Value <= 200.0
                ? OverlayConfidenceLevel.NearBoundary
                : OverlayConfidenceLevel.Normal;

            var note = BuildNotFoundNote(nearestMeters, drzFeatures.Count);

            _logger.LogDebug(
                "개발제한구역 미포함: Confidence={Confidence}, 최근접={Dist}m, " +
                "UDV100 피처={Count}건, Point=({Lon},{Lat})",
                confidence,
                nearestMeters.HasValue ? $"{nearestMeters:F0}" : "측정불가",
                drzFeatures.Count,
                query.Longitude, query.Latitude);

            return new OverlayZoneResult(
                IsInside:   false,
                Name:       null,
                Code:       null,
                Source:     SourceDescription,
                Note:       note,
                Confidence: confidence);
        }

        _logger.LogInformation(
            "개발제한구역 포함 확인: Name={Name}, Code={Code}",
            match.Name, match.Code);

        return new OverlayZoneResult(
            IsInside: true,
            Name:     match.Name,
            Code:     match.Code,
            Source:   SourceDescription,
            Note:     "개발제한구역 지정지역입니다. 건축 행위에 엄격한 제한이 적용됩니다.");
    }

    #endregion

    #region UQ141 파일 지연 로딩

    /// <summary>
    /// UQ141 SHP 파일 목록을 한 번만 로드합니다 (스레드 안전).
    /// </summary>
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
                _logger.LogWarning("SHP 디렉토리 없음 (개발제한구역): {Dir}", dirPath);
                _features = new List<LoadedFeature>();
                return _features;
            }

            // UQ141 파일만 선별 로드
            var uq141Files = Directory
                .EnumerateFiles(dirPath, "*UQ141*.shp", SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();

            _logger.LogInformation(
                "개발제한구역 UQ141 파일 로드 시작: {Count}개", uq141Files.Count);

            // 로드된 파일 목록 저장 (진단용)
            _loadedFiles = uq141Files
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Select(n => n!)
                .ToList();

            _logger.LogInformation(
                "UQ141 로드 대상 파일: [{Files}]",
                string.Join(", ", _loadedFiles));

            var all = new List<LoadedFeature>();
            foreach (var path in uq141Files)
            {
                ct.ThrowIfCancellationRequested();
                all.AddRange(_loader.Load(path));
            }

            _features = all;
            _logger.LogInformation(
                "개발제한구역 UQ141 로드 완료: 파일={Files}, 피처={Features}",
                uq141Files.Count, all.Count);

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

    #region 진단 공개 메서드

    /// <summary>
    /// 현재 로드된 UQ141 파일 목록 및 피처 수 요약 문자열을 반환합니다.
    /// <para>진단/디버그 전용입니다.</para>
    /// </summary>
    public string GetLoadedFilesInfo()
    {
        if (_features is null)
            return "미로드 (첫 쿼리 전)";

        if (_loadedFiles.Count == 0)
            return $"UQ141 파일 없음 (디렉토리에서 발견된 파일 0건) / 피처={_features.Count}";

        var fileList = string.Join(", ", _loadedFiles.Take(10));
        var suffix   = _loadedFiles.Count > 10 ? $" 외 {_loadedFiles.Count - 10}개" : "";
        return $"UQ141 파일 {_loadedFiles.Count}개 / 전체 피처 {_features.Count}건 / [{fileList}{suffix}]";
    }

    #endregion

    #region 진단 유틸리티

    /// <summary>
    /// 최근접 거리 기반으로 미포함 Note 문자열을 생성합니다.
    /// <list type="bullet">
    ///   <item>≤ 200m  : 경계 근접 미스 — 좌표 정밀도 또는 변환 오차 가능성</item>
    ///   <item>≤ 5000m : 근접 외부 — 인접 지역일 가능성</item>
    ///   <item>&gt; 5000m : 커버리지 외부 — 해당 SHP 데이터 범위 밖</item>
    ///   <item>null    : 거리 측정 불가</item>
    /// </list>
    /// </summary>
    private static string BuildNotFoundNote(double? nearestMeters, int drzFeatureCount)
    {
        if (nearestMeters is null)
            return "개발제한구역에 포함되지 않음. (거리 측정 불가 — 피처 목록 확인 필요)";

        var dist = nearestMeters.Value;

        if (dist <= 200)
            return $"개발제한구역 경계 근접 미스 (약 {dist:F0}m). " +
                   "좌표 정밀도 또는 EPSG:5174 변환 오차 가능성 — 경계 좌표 직접 확인 권장.";

        if (dist <= 5_000)
            return $"개발제한구역 미포함. 최근접 경계까지 약 {dist:F0}m " +
                   $"(UDV100 피처 {drzFeatureCount}건 기준).";

        return $"개발제한구역 미포함. 최근접 경계까지 약 {dist / 1000:F1}km — " +
               "해당 SHP 데이터 커버리지 외부이거나 비지정 지역.";
    }

    /// <summary>
    /// 로드된 피처 샘플에서 ATRB_SE 값 목록을 추출합니다 (진단용).
    /// </summary>
    private static string SampleAtrbSeValues(List<LoadedFeature> features)
    {
        var sample = features
            .Take(10)
            .Select(f =>
                (f.Attributes.TryGetValue("ATRB_SE", out var v) ? v?.ToString() : null)
             ?? (f.Attributes.TryGetValue("atrb_se", out var v2) ? v2?.ToString() : null)
             ?? "null")
            .Distinct()
            .Take(5);

        return string.Join(", ", sample);
    }

    #endregion
}

#endregion
