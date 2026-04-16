// =============================================================================
// SpatialLayerMeta.cs
// 공간 레이어 메타데이터 도메인 모델
// - 어떤 파일에서, 언제, 몇 건의 데이터를 로드했는지 추적합니다.
// - RegulationCheckResult에 포함되어 API 응답의 meta 필드로 반환됩니다.
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region SpatialLayerMeta 레코드

/// <summary>
/// 공간 레이어(SHP/CSV) 로드 메타데이터를 나타냅니다.
/// API 응답의 <c>meta</c> 필드로 반환되어 원천 데이터를 추적할 수 있게 합니다.
/// </summary>
public record SpatialLayerMeta
{
    /// <summary>
    /// 로드된 파일명 (예: "용도지역정보.shp").
    /// 파일을 찾지 못한 경우 "데이터 없음".
    /// </summary>
    public string LayerName { get; init; }

    /// <summary>
    /// 데이터가 캐시에 로드된 시각 (UTC).
    /// </summary>
    public DateTimeOffset LoadedAt { get; init; }

    /// <summary>
    /// 로드된 전체 피처(폴리곤) 수.
    /// 데이터가 없으면 0.
    /// </summary>
    public int FeatureCount { get; init; }

    /// <summary>
    /// 좌표계 관련 안내 메시지.
    /// </summary>
    public string CoordinateSystemNote { get; init; }

    /// <summary>SpatialLayerMeta를 초기화합니다.</summary>
    public SpatialLayerMeta(
        string layerName,
        DateTimeOffset loadedAt,
        int featureCount,
        string coordinateSystemNote = "EPSG:5174 (Korean 1985 / Modified Central Belt TM). API 입력(WGS84)을 ProjNet4GeoAPI로 변환 후 판정.")
    {
        LayerName              = layerName;
        LoadedAt               = loadedAt;
        FeatureCount           = featureCount;
        CoordinateSystemNote   = coordinateSystemNote;
    }

    #region 팩토리 메서드

    /// <summary>데이터 파일을 찾지 못한 경우의 메타데이터를 생성합니다.</summary>
    public static SpatialLayerMeta NoData() =>
        new("데이터 없음", DateTimeOffset.UtcNow, 0,
            "공간 데이터 파일이 로드되지 않았습니다. appsettings.json SpatialData 경로를 확인하세요.");

    #endregion
}

#endregion
