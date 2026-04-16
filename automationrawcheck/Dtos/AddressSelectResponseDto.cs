// =============================================================================
// AddressSelectResponseDto.cs
// POST /api/regulation-check/address/select 응답 DTO
//
// [응답 구조]
//   inputQuery        : 원본 입력 주소 텍스트
//   candidateIndex    : 선택된 후보 인덱스 (요청값 에코)
//   candidateCount    : 전체 후보 수 (인덱스 범위 검증용)
//   selectedCandidate : 선택된 후보 상세 (address, latitude, longitude, addressType)
//   geocodingProvider : 사용된 Geocoding 서비스명
//   regulationResult  : selectedCandidate 좌표 기준 법규 검토 결과
//
// [역할 분리]
//   POST /address        → 후보 탐색 (전체 candidates 반환, selected 자동 결정)
//   POST /address/select → 확정 조회 (사용자가 candidateIndex 명시, 해당 후보 기준 판정)
// =============================================================================

using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region AddressSelectResponseDto 클래스

/// <summary>
/// 주소 후보 선택 기반 법규 검토 응답 DTO입니다.
/// <para>
/// 사용자가 명시한 <c>candidateIndex</c>에 해당하는 후보의 좌표를 기준으로
/// 법규 1차 검토를 수행한 결과를 반환합니다.
/// 모든 법규 판정 결과는 <b>참고용 1차 판정</b>이며 확정 근거가 아닙니다.
/// </para>
/// </summary>
[SwaggerSchema(Description = "주소 후보 선택 법규 검토 응답. 선택된 후보 정보와 해당 좌표 기반 판정 결과 포함.")]
public sealed class AddressSelectResponseDto
{
    /// <summary>원본 입력 주소 텍스트</summary>
    [SwaggerSchema(Description = "요청에 사용된 원본 입력 주소 텍스트")]
    public string InputQuery { get; init; } = string.Empty;

    /// <summary>선택된 후보의 0-기반 인덱스 (요청값 에코)</summary>
    [SwaggerSchema(Description = "선택된 후보 인덱스 (0-기반, 요청값 에코)")]
    public int CandidateIndex { get; init; }

    /// <summary>전체 후보 수</summary>
    [SwaggerSchema(Description = "Geocoding 전체 후보 수 (인덱스 유효 범위 참고용)")]
    public int CandidateCount { get; init; }

    /// <summary>선택된 후보의 상세 정보 (주소, 좌표, 주소 유형)</summary>
    [SwaggerSchema(Description = "candidateIndex로 선택된 후보의 상세 정보. regulationResult는 이 좌표 기준.")]
    public AddressCandidateDto SelectedCandidate { get; init; } = new();

    /// <summary>사용된 Geocoding 서비스명 (예: "VWorld")</summary>
    [SwaggerSchema(Description = "좌표 변환에 사용된 외부 Geocoding 서비스명")]
    public string? GeocodingProvider { get; init; }

    /// <summary>
    /// selectedCandidate 좌표 기준 법규 1차 검토 결과.
    /// POST /coordinate 응답과 동일한 구조입니다.
    /// </summary>
    [SwaggerSchema(Description = "선택된 후보 좌표 기반 법규 검토 결과 (POST /coordinate와 동일 구조)")]
    public RegulationCheckResponseDto RegulationResult { get; init; } = null!;
}

#endregion
