// =============================================================================
// ZoningFeatureCache.cs
// 용도지역 피처 목록 + 메타데이터 캐싱 전담 클래스
// - IMemoryCache를 래핑합니다.
// - 피처 목록(List<LoadedFeature>)과 메타데이터(CacheEntry)를 함께 보관합니다.
// =============================================================================

using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.Spatial;

#region CacheEntry 내부 레코드

/// <summary>
/// 캐시에 저장되는 피처 목록 + 메타데이터 컨테이너입니다.
/// </summary>
public sealed record CacheEntry(
    List<LoadedFeature> Features,
    SpatialLayerMeta Meta);

#endregion

#region ZoningFeatureCache 클래스

/// <summary>
/// 로드된 용도지역 피처 목록과 레이어 메타데이터를 메모리 캐시에 보관하는 전담 클래스입니다.
/// <para>
/// 파일 로드는 앱 시작 후 첫 요청 시 한 번만 수행되며,
/// 이후 요청은 캐시에서 직접 반환합니다.
/// </para>
/// TODO (캐시 갱신):
/// 데이터 파일 교체 시 <see cref="Invalidate"/>를 호출하면 다음 요청 때 다시 로드합니다.
/// 향후 FileSystemWatcher 기반 자동 갱신도 가능합니다.
/// </summary>
public sealed class ZoningFeatureCache
{
    #region 상수 및 필드

    private const string CacheKey = "ZoningFeatures_v2";
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ZoningFeatureCache> _logger;

    #endregion

    #region 생성자

    /// <summary>ZoningFeatureCache를 초기화합니다.</summary>
    public ZoningFeatureCache(IMemoryCache memoryCache, ILogger<ZoningFeatureCache> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger      = logger      ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region 캐시 조회 메서드

    /// <summary>
    /// 캐시에서 피처 목록과 메타데이터를 함께 조회합니다.
    /// </summary>
    /// <returns>캐시 히트 시 <see cref="CacheEntry"/>, 미스 시 <c>null</c>.</returns>
    public CacheEntry? TryGet()
    {
        if (_memoryCache.TryGetValue(CacheKey, out CacheEntry? entry) && entry is not null)
        {
            _logger.LogDebug(
                "캐시 히트. 피처 수={Count}, 로드 시각={At}",
                entry.Features.Count, entry.Meta.LoadedAt);
            return entry;
        }

        _logger.LogDebug("캐시 미스. 파일 로드가 필요합니다.");
        return null;
    }

    #endregion

    #region 캐시 저장 메서드

    /// <summary>
    /// 피처 목록과 메타데이터를 캐시에 저장합니다.
    /// </summary>
    /// <param name="features">로드된 피처 목록</param>
    /// <param name="meta">레이어 메타데이터 (파일명, 로드시각, 피처수)</param>
    /// <param name="expiry">캐시 만료 시간. null이면 기본값(24시간).</param>
    public void Set(List<LoadedFeature> features, SpatialLayerMeta meta, TimeSpan? expiry = null)
    {
        var entry = new CacheEntry(features, meta);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry
        };

        _memoryCache.Set(CacheKey, entry, options);

        _logger.LogInformation(
            "피처 캐시 저장 완료. LayerName={Layer}, 피처 수={Count}, 만료={Expiry}",
            meta.LayerName, features.Count, expiry ?? DefaultExpiry);
    }

    #endregion

    #region 캐시 무효화 메서드

    /// <summary>
    /// 캐시를 무효화합니다. 다음 요청 시 파일을 다시 로드합니다.
    /// </summary>
    public void Invalidate()
    {
        _memoryCache.Remove(CacheKey);
        _logger.LogInformation("용도지역 피처 캐시 무효화됨.");
    }

    #endregion
}

#endregion
