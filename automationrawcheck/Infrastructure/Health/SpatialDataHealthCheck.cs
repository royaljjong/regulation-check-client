// =============================================================================
// SpatialDataHealthCheck.cs
// 공간 데이터 디렉토리 존재 여부 / 캐시 상태를 확인하는 헬스체크
// GET /health 엔드포인트에서 사용됩니다.
// =============================================================================

using AutomationRawCheck.Infrastructure.Spatial;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Infrastructure.Health;

#region SpatialDataHealthCheck 클래스

/// <summary>
/// 공간 데이터 디렉토리 존재 여부와 캐시 로드 상태를 확인하는 헬스체크입니다.
/// <para>
/// 체크 항목:
/// <list type="bullet">
///   <item>ZoningShapefileDirectory 디렉토리 존재 여부</item>
///   <item>SHP 파일 수</item>
///   <item>캐시에 피처 로드 여부 및 피처 수</item>
/// </list>
/// </para>
/// </summary>
public sealed class SpatialDataHealthCheck : IHealthCheck
{
    #region 필드 및 생성자

    private readonly SpatialDataOptions _options;
    private readonly ZoningFeatureCache _cache;

    /// <summary>SpatialDataHealthCheck를 초기화합니다.</summary>
    public SpatialDataHealthCheck(
        IOptions<SpatialDataOptions> options,
        ZoningFeatureCache cache)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _cache   = cache          ?? throw new ArgumentNullException(nameof(cache));
    }

    #endregion

    #region IHealthCheck 구현

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();

        #region 디렉토리 / 파일 존재 여부 확인

        var dirPath   = ToAbsolutePath(_options.ZoningShapefileDirectory);
        bool dirExists = Directory.Exists(dirPath);

        data["shapefileDirectory"] = dirPath;
        data["directoryExists"]    = dirExists;

        if (dirExists)
        {
            var shpCount = Directory
                .EnumerateFiles(dirPath, "*.shp", SearchOption.AllDirectories)
                .Count();
            data["shpFileCount"] = shpCount;
        }

        #endregion

        #region 캐시 상태 확인

        var cached = _cache.TryGet();
        if (cached is not null)
        {
            data["cacheLoaded"]  = true;
            data["featureCount"] = cached.Meta.FeatureCount;
            data["loadedAt"]     = cached.Meta.LoadedAt.ToString("O");
            data["layerName"]    = cached.Meta.LayerName;
        }
        else
        {
            data["cacheLoaded"]  = false;
            data["featureCount"] = 0;
        }

        #endregion

        #region 상태 판정

        if (!dirExists)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "SHP 디렉토리가 존재하지 않습니다. " +
                "appsettings.json의 SpatialData.ZoningShapefileDirectory를 확인하세요.",
                data: data));
        }

        if (cached is null)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "디렉토리는 존재하지만 아직 캐시에 로드되지 않았습니다. " +
                "첫 API 요청 시 자동으로 로드됩니다.",
                data: data));
        }

        if (cached.Meta.FeatureCount == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "SHP 파일이 로드되었지만 피처 수가 0입니다. 파일 내용을 확인하세요.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"공간 데이터 정상 로드. 피처 수: {cached.Meta.FeatureCount:N0}",
            data: data));

        #endregion
    }

    #endregion

    #region 유틸리티

    private static string ToAbsolutePath(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

    #endregion
}

#endregion
