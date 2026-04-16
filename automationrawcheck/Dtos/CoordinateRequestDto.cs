// =============================================================================
// CoordinateRequestDto.cs
// POST /api/regulation-check/coordinate 요청 DTO
// =============================================================================

using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AutomationRawCheck.Api.Dtos;

#region CoordinateRequestDto 클래스

/// <summary>
/// 좌표 기반 법규 검토 요청 DTO입니다.
/// WGS84(EPSG:4326) 기준 경도/위도를 입력합니다.
/// </summary>
[SwaggerSchema(Description = "WGS84 경도/위도 좌표 입력 DTO")]
public sealed class CoordinateRequestDto
{
    /// <summary>
    /// 경도 (WGS84, -180 ~ 180).
    /// 한국 본토 범위: 약 124 ~ 132.
    /// </summary>
    [Required]
    [Range(-180.0, 180.0, ErrorMessage = "경도는 -180 ~ 180 범위여야 합니다.")]
    [DefaultValue(127.0276)]
    [SwaggerSchema(Description = "경도 (WGS84, -180 ~ 180). 예: 127.0276 (서울 종로구)")]
    public double Longitude { get; set; } = 127.0276;

    /// <summary>
    /// 위도 (WGS84, -90 ~ 90).
    /// 한국 본토 범위: 약 33 ~ 39.
    /// </summary>
    [Required]
    [Range(-90.0, 90.0, ErrorMessage = "위도는 -90 ~ 90 범위여야 합니다.")]
    [DefaultValue(37.5796)]
    [SwaggerSchema(Description = "위도 (WGS84, -90 ~ 90). 예: 37.5796 (서울 종로구)")]
    public double Latitude { get; set; } = 37.5796;
}

#endregion
