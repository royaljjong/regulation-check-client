// =============================================================================
// IAddressResolver.cs
// 주소 → 좌표 변환 서비스 인터페이스
//
// [반환 설계]
//   IReadOnlyList<AddressResolveResult>
//     - 빈 리스트  : 주소를 찾지 못함
//     - 1건        : 단일 후보 (자동 선택)
//     - 2건 이상   : 복수 후보 (0번 = 최우선, 전체 반환하여 호출자가 선택 가능)
//
// [구현체]
//   실연동: VWorldAddressResolver (V-World 주소 좌표 검색 API)
//   Stub  : StubAddressResolver (항상 빈 리스트 반환)
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region IAddressResolver 인터페이스

/// <summary>
/// 주소 문자열을 WGS84 좌표 후보 목록으로 변환하는 주소 해석기 인터페이스입니다.
/// </summary>
public interface IAddressResolver
{
    #region 메서드

    /// <summary>
    /// 주소 문자열을 WGS84 좌표 후보 목록으로 변환합니다.
    /// </summary>
    /// <param name="addressText">지번 또는 도로명 주소 텍스트</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>
    /// 후보 목록. 빈 리스트이면 주소를 찾지 못한 것입니다.
    /// 2건 이상이면 0번 인덱스가 최우선 후보입니다.
    /// </returns>
    Task<IReadOnlyList<AddressResolveResult>> ResolveAsync(
        string addressText,
        CancellationToken ct = default);

    #endregion
}

#endregion
