// =============================================================================
// BasicCalculatorDtos.cs
// POST /api/calculator/basic 요청/응답 DTO
//
// [요청]
//   siteArea       : 대지면적 (㎡, 양수)
//   buildingArea   : 건축면적 (㎡, 양수)
//   totalFloorArea : 연면적 (㎡, 양수)
//   zoneName       : 용도지역명 (선택, 법정 한도 조회용)
//
// [응답]
//   results[]
//     type        : "BCR" | "FAR"
//     label       : "건폐율" | "용적률"
//     value       : 계산값 (%)
//     limit       : 법정 상한값 (%, zoneName 미입력 시 null)
//     isExceeded  : 법정 상한 초과 여부 (limit null이면 false)
//     note        : 보조 안내 문구
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region 요청

/// <summary>건폐율·용적률 간이 계산 요청 DTO</summary>
public sealed class BasicCalculatorRequestDto
{
    /// <summary>대지면적 (㎡). 양수여야 합니다.</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "대지면적은 0보다 커야 합니다.")]
    public double SiteArea { get; init; }

    /// <summary>건축면적 (㎡). 양수여야 합니다.</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "건축면적은 0보다 커야 합니다.")]
    public double BuildingArea { get; init; }

    /// <summary>연면적 (㎡). 양수여야 합니다.</summary>
    [Range(0.01, double.MaxValue, ErrorMessage = "연면적은 0보다 커야 합니다.")]
    public double TotalFloorArea { get; init; }

    /// <summary>
    /// 용도지역명 (선택). 입력 시 법정 상한값(limit)을 함께 반환합니다.
    /// 예: "제2종일반주거지역"
    /// </summary>
    public string? ZoneName { get; init; }
}

#endregion

#region 응답

/// <summary>단일 항목 계산 결과 DTO</summary>
[SwaggerSchema(Description = "건폐율 또는 용적률 단일 계산 결과")]
public sealed class CalculatorResultItemDto
{
    /// <summary>항목 구분: "BCR" | "FAR"</summary>
    [SwaggerSchema(Description = "BCR = 건폐율, FAR = 용적률")]
    public string Type { get; init; } = string.Empty;

    /// <summary>항목 한글명: "건폐율" | "용적률"</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>계산값 (%)</summary>
    [SwaggerSchema(Description = "계산된 비율 (%)")]
    public double Value { get; init; }

    /// <summary>
    /// 법정 상한값 (%). zoneName 미입력 또는 알 수 없는 용도지역이면 null.
    /// </summary>
    [SwaggerSchema(Description = "법정 상한 (%). null이면 한도 미확인.")]
    public double? Limit { get; init; }

    /// <summary>
    /// 법정 상한 초과 여부. limit이 null이면 항상 false.
    /// </summary>
    [SwaggerSchema(Description = "법정 상한 초과 여부")]
    public bool IsExceeded { get; init; }

    /// <summary>보조 안내 문구</summary>
    [SwaggerSchema(Description = "추가 안내 또는 조례 확인 안내")]
    public string Note { get; init; } = string.Empty;
}

/// <summary>건폐율·용적률 간이 계산 응답 DTO</summary>
[SwaggerSchema(Description = "건폐율(BCR)·용적률(FAR) 간이 계산 결과. 법정 상한은 참고용이며 조례 기준이 다를 수 있습니다.")]
public sealed class BasicCalculatorResponseDto
{
    /// <summary>계산 결과 목록 (BCR, FAR 순서)</summary>
    public List<CalculatorResultItemDto> Results { get; init; } = new();

    /// <summary>[표준] 데이터 출처: 항상 "calculated" (입력값 기반 산술 계산).</summary>
    [SwaggerSchema(Description = "[표준] 데이터 출처: 'calculated' = 입력 면적 기반 산술 계산")]
    public string Source { get; init; } = "calculated";

    /// <summary>[표준] 신뢰도: 항상 "low" (입력값 정확도에 의존).</summary>
    [SwaggerSchema(Description = "[표준] 신뢰도: 'low' = 입력값 의존 계산")]
    public string Confidence { get; init; } = "low";

    /// <summary>[Deprecated] DataSource → Source 로 이전 예정. 동일 값.</summary>
    [SwaggerSchema(Description = "[Deprecated] → source 사용 권장. 동일 값.")]
    public string DataSource { get; init; } = "calculated";
}

#endregion
