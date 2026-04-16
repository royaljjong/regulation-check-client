// =============================================================================
// IDistrictUnitPlanProvider.cs
// 지구단위계획구역 프로바이더 인터페이스
//
// [현재 상태 — DataUnavailable]
//   KLIP_004_20260201 데이터셋에 지구단위계획구역 레이어가 포함되어 있지 않습니다.
//   (UQ171은 개발행위허가제한지역 데이터 — IDevActRestrictionProvider 참고)
//   현재 NullDistrictUnitPlanProvider가 등록되어 항상 DataUnavailable 을 반환합니다.
//
// [향후 연동 방법]
//   1. 지구단위계획구역 SHP 데이터 확보 (국가공간정보포털 nsdi.go.kr 또는 토지이음 API)
//   2. ShapefileDistrictUnitPlanProvider 구현 후 Program.cs DI 교체
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region IDistrictUnitPlanProvider 인터페이스 정의

/// <summary>
/// 좌표 기반으로 지구단위계획구역 포함 여부를 조회하는 프로바이더 인터페이스입니다.
/// <para>
/// <b>현재 구현체</b>: <c>NullDistrictUnitPlanProvider</c> (DataUnavailable 반환)<br/>
/// KLIP_004_20260201 데이터셋에 지구단위계획구역 레이어가 없어 미연동 상태입니다.
/// 지구단위계획 SHP 데이터 확보 후 실제 구현체로 교체하세요.
/// </para>
/// </summary>
public interface IDistrictUnitPlanProvider
{
    /// <summary>
    /// 입력 좌표의 지구단위계획구역 중첩 여부를 반환합니다.
    /// </summary>
    /// <param name="query">WGS84 경도/위도 좌표 쿼리</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>중첩 판정 결과. 데이터 미보유 시 IsInside=false 결과 반환.</returns>
    Task<OverlayZoneResult> GetOverlayAsync(CoordinateQuery query, CancellationToken ct = default);
}

#endregion
