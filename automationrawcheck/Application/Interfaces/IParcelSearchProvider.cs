// =============================================================================
// IParcelSearchProvider.cs
// 지번/주소 → 좌표 변환 프로바이더 인터페이스
// - 1차 MVP에서는 stub 구현만 존재.
// - 향후 VWorld / 카카오맵 / 공공 주소 API 연동 시 이 인터페이스를 구현하세요.
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region IParcelSearchProvider 인터페이스 정의

/// <summary>
/// 지번 또는 도로명 주소 문자열을 WGS84 좌표로 변환하는 프로바이더 인터페이스입니다.
/// <para>
/// 구현 예시 (향후):
/// - VWorld 주소 검색 API (행정안전부)
/// - 카카오맵 주소 검색 API
/// - 공공데이터포털 도로명주소 API
/// </para>
/// TODO: 도메인 인증이 확보된 후 실제 외부 API 구현체로 교체하세요.
/// </summary>
public interface IParcelSearchProvider
{
    #region 메서드 정의

    /// <summary>
    /// 주소 문자열을 WGS84 좌표로 변환합니다.
    /// </summary>
    /// <param name="addressText">지번 또는 도로명 주소 텍스트</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>
    /// 변환된 좌표; 주소를 찾을 수 없거나 아직 미구현이면 null.
    /// </returns>
    Task<CoordinateQuery?> ResolveAddressAsync(string addressText, CancellationToken ct = default);

    #endregion
}

#endregion
