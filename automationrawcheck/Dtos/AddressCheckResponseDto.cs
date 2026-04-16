// =============================================================================
// AddressCheckResponseDto.cs
// POST /api/regulation-check/address 응답 DTO
//
// [응답 구조]
//   inputQuery        : 원본 입력 주소 텍스트
//   candidates        : Geocoding 후보 전체 목록 (최소 1건)
//   selected          : 법규 검토에 사용된 후보 (candidates[0], V-World 최우선)
//   geocodingProvider : 사용된 Geocoding 서비스명 (예: "VWorld")
//   candidateCount    : 전체 후보 수
//   candidateNote     : 복수 후보 시 안내 메시지 (단일이면 null)
//   regulationResult  : 기존 좌표 기반 법규 검토 결과 (selected 좌표 기준)
//
// [복수 후보 처리 정책]
//   - candidates[0] = V-World 최우선 후보 → selected로 자동 선택
//   - 나머지 candidates는 프론트엔드에서 선택 UI 구현용
//   - candidateNote로 "복수 후보 존재" 명시
// =============================================================================

using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region AddressCandidateDto

/// <summary>주소 Geocoding 후보 한 건을 나타내는 DTO</summary>
[SwaggerSchema(Description = "Geocoding으로 변환된 주소 후보 한 건 (V-World 제공 순서 유지)")]
public sealed class AddressCandidateDto
{
    /// <summary>V-World가 정규화한 주소 문자열</summary>
    [SwaggerSchema(Description = "V-World가 정규화한 표준 주소 문자열")]
    public string Address { get; init; } = string.Empty;

    /// <summary>위도 (WGS84)</summary>
    [SwaggerSchema(Description = "위도 (WGS84)")]
    public double Latitude { get; init; }

    /// <summary>경도 (WGS84)</summary>
    [SwaggerSchema(Description = "경도 (WGS84)")]
    public double Longitude { get; init; }

    /// <summary>주소 유형: "road" (도로명) 또는 "parcel" (지번)</summary>
    [SwaggerSchema(Description = "주소 유형: road(도로명) | parcel(지번)")]
    public string AddressType { get; init; } = string.Empty;
}

#endregion

#region AddressCheckResponseDto 클래스

/// <summary>
/// 주소 기반 법규 검토 API 응답 DTO입니다.
/// <para>
/// Geocoding 후보 목록(<c>candidates</c>), 선택된 후보(<c>selected</c>),
/// 해당 좌표 기반 법규 검토 결과(<c>regulationResult</c>)를 함께 반환합니다.
/// </para>
/// <para>
/// 모든 법규 판정 결과는 <b>참고용 1차 판정</b>이며 확정 근거가 아닙니다.
/// </para>
/// </summary>
[SwaggerSchema(Description = "주소 기반 법규 검토 응답. Geocoding 후보 목록과 법규 판정 결과 포함.")]
public sealed class AddressCheckResponseDto
{
    /// <summary>원본 입력 주소 텍스트</summary>
    [SwaggerSchema(Description = "요청에 사용된 원본 입력 주소 텍스트")]
    public string InputQuery { get; init; } = string.Empty;

    /// <summary>
    /// Geocoding 후보 전체 목록. V-World 반환 순서를 유지합니다.
    /// 항상 1건 이상입니다 (0건이면 404 응답).
    /// </summary>
    [SwaggerSchema(Description = "Geocoding 후보 목록 (V-World 정렬 순서 유지). 1건 이상.")]
    public List<AddressCandidateDto> Candidates { get; init; } = new();

    /// <summary>
    /// 법규 검토에 사용된 후보 (candidates[0], V-World 최우선).
    /// regulationResult는 이 좌표를 기준으로 산출됩니다.
    /// </summary>
    [SwaggerSchema(Description = "법규 검토에 사용된 최우선 후보 (candidates[0])")]
    public AddressCandidateDto Selected { get; init; } = new();

    /// <summary>사용된 Geocoding 서비스명 (예: "VWorld")</summary>
    [SwaggerSchema(Description = "좌표 변환에 사용된 외부 Geocoding 서비스명")]
    public string? GeocodingProvider { get; init; }

    /// <summary>전체 후보 수</summary>
    [SwaggerSchema(Description = "Geocoding 전체 후보 수")]
    public int CandidateCount { get; init; }

    /// <summary>
    /// 복수 후보 시 안내 메시지. 단일 결과면 null.
    /// </summary>
    [SwaggerSchema(Description = "복수 후보 안내. candidates가 2건 이상일 때만 존재.")]
    public string? CandidateNote { get; init; }

    /// <summary>
    /// selected 좌표 기반 법규 1차 검토 결과.
    /// POST /coordinate 응답과 동일한 구조입니다.
    /// </summary>
    [SwaggerSchema(Description = "selected 좌표 기반 법규 검토 결과 (POST /coordinate와 동일 구조)")]
    public RegulationCheckResponseDto RegulationResult { get; init; } = null!;
}

#endregion
