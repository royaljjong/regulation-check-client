// =============================================================================
// LoadedFeature.cs
// SHP/CSV 로더가 반환하는 내부 피처 모델
// - Infrastructure 레이어 내부에서만 사용하는 구현 세부사항
// - 이 타입은 Domain 모델(ZoningFeature)로 변환되기 전 중간 단계입니다.
// =============================================================================

using NetTopologySuite.Geometries;

namespace AutomationRawCheck.Infrastructure.Spatial;

#region LoadedFeature 내부 모델

/// <summary>
/// SHP 또는 CSV 파일에서 읽어온 지오메트리와 속성을 함께 보관하는 내부 모델입니다.
/// <para>
/// 이 타입은 Infrastructure 계층 내부에서만 사용합니다.
/// 외부(Application/Domain)에 노출하지 마세요.
/// </para>
/// </summary>
public sealed record LoadedFeature
{
    #region 프로퍼티

    /// <summary>NTS 지오메트리 (Polygon, MultiPolygon 등)</summary>
    public Geometry Geometry { get; init; }

    /// <summary>
    /// 원본 SHP/CSV 속성 딕셔너리.
    /// 키는 필드명(대소문자 무시), 값은 문자열 또는 null.
    /// </summary>
    public Dictionary<string, object?> Attributes { get; init; }

    #endregion

    #region 생성자

    /// <summary>LoadedFeature를 초기화합니다.</summary>
    public LoadedFeature(Geometry geometry, Dictionary<string, object?> attributes)
    {
        Geometry = geometry;
        Attributes = attributes;
    }

    #endregion
}

#endregion
