// =============================================================================
// IZoningLayerProvider.cs
// 용도지역 공간 레이어 프로바이더 인터페이스
// 구현체: Infrastructure/Spatial/ShapefileZoningLayerProvider.cs
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region IZoningLayerProvider 인터페이스

/// <summary>
/// 좌표 기반으로 용도지역 정보를 조회하는 공간 레이어 프로바이더 인터페이스입니다.
/// <para>
/// 현재 구현체: <c>ShapefileZoningLayerProvider</c> — 로컬 SHP/CSV 파일 기반.
/// </para>
/// <para>
/// TODO (확장 포인트):
/// 향후 외부 공간 API(토지이음, VWorld GIS API 등) 연동 시
/// 이 인터페이스를 구현하는 새 클래스를 만들고 DI만 교체하면 됩니다.
/// </para>
/// </summary>
public interface IZoningLayerProvider
{
    #region 메서드

    /// <summary>
    /// 입력 좌표가 포함되는 용도지역 피처를 반환합니다.
    /// </summary>
    /// <param name="query">WGS84 경도/위도 좌표 쿼리</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>
    /// 좌표가 속하는 용도지역 피처.
    /// 해당 용도지역이 없으면 <c>null</c>.
    /// </returns>
    Task<ZoningFeature?> GetZoningAsync(CoordinateQuery query, CancellationToken ct = default);

    /// <summary>
    /// 진단 정보를 포함하여 용도지역 정보를 조회합니다.
    /// </summary>
    Task<(ZoningFeature? Feature, string? DebugReason, double? NearestDistance, string? ZoningRaw)> 
        GetDebugZoningAsync(CoordinateQuery query, CancellationToken ct = default);

    #endregion
}

#endregion
