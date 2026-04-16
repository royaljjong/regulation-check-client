// =============================================================================
// IDevActRestrictionProvider.cs
// 개발행위허가제한지역 프로바이더 인터페이스 (UQ171 레이어, ATRB_SE=UQQ900)
//
// [데이터 근거]
//   KLIP_004_20260201 데이터셋의 UQ171 레이어는 ATRB_SE=UQQ900 기반의
//   개발행위허가제한지역 폴리곤을 포함합니다 (지구단위계획구역 아님).
//
// [현재 구현체]
//   ShapefileDevActRestrictionProvider (Singleton, 지연 로딩)
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region IDevActRestrictionProvider 인터페이스 정의

/// <summary>
/// 좌표 기반으로 개발행위허가제한지역 포함 여부를 조회하는 프로바이더 인터페이스입니다.
/// <para>
/// 현재 구현체: <c>ShapefileDevActRestrictionProvider</c><br/>
/// UQ171 레이어(KLIP/UPIS)의 UQQ900 폴리곤으로 판정합니다.
/// 개발행위허가제한지역은 국토계획법 제63조 기반으로 개발행위를 제한하는 구역입니다.
/// </para>
/// </summary>
public interface IDevActRestrictionProvider
{
    /// <summary>
    /// 입력 좌표의 개발행위허가제한지역 포함 여부를 반환합니다.
    /// </summary>
    /// <param name="query">WGS84 경도/위도 좌표 쿼리</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>중첩 판정 결과.</returns>
    Task<OverlayZoneResult> GetOverlayAsync(CoordinateQuery query, CancellationToken ct = default);
}

#endregion
