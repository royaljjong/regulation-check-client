// =============================================================================
// AddressCheckRequestDto.cs
// POST /api/regulation-check/address 요청 DTO
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region AddressCheckRequestDto 클래스

/// <summary>
/// 주소 또는 지번 텍스트 기반 법규 검토 요청 DTO입니다.
/// </summary>
[SwaggerSchema(Description = "주소/지번 텍스트 기반 법규 검토 요청. 도로명 또는 지번 주소 모두 가능합니다.")]
public sealed class AddressCheckRequestDto
{
    /// <summary>
    /// 검색할 주소 또는 지번 텍스트.
    /// <para>예: "서울특별시 강남구 영동대로 513" 또는 "서울 강남구 삼성동 159"</para>
    /// </summary>
    [Required(ErrorMessage = "query는 필수입니다.")]
    [MinLength(2, ErrorMessage = "주소는 2자 이상 입력하세요.")]
    [MaxLength(300, ErrorMessage = "주소는 300자 이하로 입력하세요.")]
    [SwaggerSchema(Description = "도로명 주소 또는 지번 주소 텍스트 (2~300자)")]
    public string Query { get; init; } = string.Empty;
}

#endregion
