// =============================================================================
// AddressSelectRequestDto.cs
// POST /api/regulation-check/address/select 요청 DTO
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region AddressSelectRequestDto 클래스

/// <summary>
/// 주소 후보 중 특정 인덱스를 선택해 법규 검토를 요청하는 DTO입니다.
/// <para>
/// 일반적인 사용 흐름: POST /address → candidates 목록 확인 → POST /address/select
/// </para>
/// </summary>
[SwaggerSchema(Description = "주소 후보 선택 기반 법규 검토 요청. /address에서 얻은 candidateIndex를 지정합니다.")]
public sealed class AddressSelectRequestDto
{
    /// <summary>검색할 주소 또는 지번 텍스트 (/address 요청과 동일한 값)</summary>
    [Required(ErrorMessage = "query는 필수입니다.")]
    [MinLength(2, ErrorMessage = "주소는 2자 이상 입력하세요.")]
    [MaxLength(300, ErrorMessage = "주소는 300자 이하로 입력하세요.")]
    [SwaggerSchema(Description = "도로명 또는 지번 주소 텍스트 (2~300자). /address 요청과 동일한 값 사용.")]
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 선택할 후보의 0-기반 인덱스.
    /// 0 = candidates[0] (최우선 후보), 1 = candidates[1], ...
    /// 유효 범위: 0 이상, candidates.Count 미만.
    /// </summary>
    [Range(0, 99, ErrorMessage = "candidateIndex는 0~99 범위여야 합니다.")]
    [SwaggerSchema(Description = "선택할 후보의 0-기반 인덱스. 유효 범위: 0 ~ (candidateCount - 1).")]
    public int CandidateIndex { get; init; } = 0;
}

#endregion
