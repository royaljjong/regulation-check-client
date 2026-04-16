// =============================================================================
// IDevelopmentRestrictionProvider.cs
// 개발제한구역(그린벨트) 프로바이더 인터페이스
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region IDevelopmentRestrictionProvider 인터페이스 정의

/// <summary>
/// 좌표 기반으로 개발제한구역(그린벨트) 포함 여부를 조회하는 프로바이더 인터페이스입니다.
/// <para>
/// 현재 구현체: <c>ShapefileDevRestrictionProvider</c>
/// UQ141 레이어(UPIS/KLIP)의 UDV100 폴리곤으로 판정합니다.
/// </para>
/// </summary>
public interface IDevelopmentRestrictionProvider
{
    /// <summary>
    /// 입력 좌표의 개발제한구역 포함 여부를 반환합니다.
    /// </summary>
    /// <param name="query">WGS84 경도/위도 좌표 쿼리</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>중첩 판정 결과.</returns>
    Task<OverlayZoneResult> GetOverlayAsync(CoordinateQuery query, CancellationToken ct = default);
}

#endregion
