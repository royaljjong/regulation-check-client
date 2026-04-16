// =============================================================================
// ParcelSearchRequestDto.cs
// 지번/주소 기반 법규 검토 요청 DTO
// - 1차 MVP: 좌표가 없으면 주소 변환 stub이 null을 반환하므로 판정 불가 메시지 반환.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Api.Dtos;

#region ParcelSearchRequestDto 클래스

/// <summary>
/// 지번 또는 도로명 주소 기반 법규 검토 요청 DTO입니다.
/// <para>
/// 1차 MVP 단계에서는 좌표 기반 검토가 우선이며,
/// 주소 기반 검토는 placeholder 응답을 반환합니다.
/// </para>
/// </summary>
public sealed class ParcelSearchRequestDto
{
    #region 요청 필드

    /// <summary>
    /// 검색 타입 문자열. "Coordinate" | "JibunAddress" | "RoadAddress"
    /// 기본값: "JibunAddress"
    /// </summary>
    [Required(ErrorMessage = "searchType은 필수입니다.")]
    public string SearchType { get; set; } = "JibunAddress";

    /// <summary>
    /// 지번 또는 도로명 주소 텍스트.
    /// SearchType이 JibunAddress 또는 RoadAddress일 때 필수입니다.
    /// 예: "경기도 성남시 분당구 정자동 1-1" 또는 "경기도 성남시 분당구 판교로 1"
    /// </summary>
    public string? AddressText { get; set; }

    /// <summary>
    /// WGS84 경도. SearchType이 Coordinate일 때 사용됩니다.
    /// </summary>
    [Range(-180.0, 180.0, ErrorMessage = "경도는 -180 ~ 180 범위여야 합니다.")]
    public double? Longitude { get; set; }

    /// <summary>
    /// WGS84 위도. SearchType이 Coordinate일 때 사용됩니다.
    /// </summary>
    [Range(-90.0, 90.0, ErrorMessage = "위도는 -90 ~ 90 범위여야 합니다.")]
    public double? Latitude { get; set; }

    #endregion

    #region 도메인 모델 변환

    /// <summary>
    /// DTO를 도메인 모델 <see cref="ParcelSearchRequest"/>로 변환합니다.
    /// </summary>
    public ParcelSearchRequest ToDomain()
    {
        // SearchType 파싱 (대소문자 무시)
        var searchType = Enum.TryParse<ParcelSearchType>(SearchType, ignoreCase: true, out var parsed)
            ? parsed
            : ParcelSearchType.JibunAddress;

        // 좌표 생성
        CoordinateQuery? coord = searchType == ParcelSearchType.Coordinate
                                 && Longitude.HasValue && Latitude.HasValue
            ? new CoordinateQuery(Longitude.Value, Latitude.Value)
            : null;

        return new ParcelSearchRequest(searchType, AddressText, coord);
    }

    #endregion
}

#endregion
