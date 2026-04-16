// =============================================================================
// ReviewItemsResponseDto.cs
// POST /api/regulation-check/review-items 응답 DTO
//
// [응답 구조]
//   selectedUse  : 입력 계획 용도
//   zoneName     : 판정 용도지역명 (없으면 null)
//   reviewItems  : 검토 항목 목록
//     - category        : 항목 분류 (허용용도, 밀도, 도로/건축선, 주차, 피난/계단, 승강기, 방화, 지구단위계획, 중첩규제)
//     - title           : 행동형 항목명 (예: "건폐율·용적률 상한 확인")
//     - description     : 리스크/사업성 영향 중심 설명
//     - requiredInputs  : 판정에 필요한 추가 입력값 목록
//     - relatedLaws     : 관련 법령 문자열 목록 (참고용)
//     - isAutoCheckable : true = 공간 데이터로 자동 판정, false = 수동 검토 필요
//     - priority        : 우선순위 (high / medium / low)
// =============================================================================

using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region ReviewItemDto

/// <summary>단일 건축 검토 항목 DTO</summary>
[SwaggerSchema(Description = "건축 인허가 준비 시 검토해야 할 단일 항목")]
public sealed class ReviewItemDto
{
    /// <summary>
    /// 검토 항목 분류.
    /// 예: 허용용도, 밀도, 주차, 피난/계단, 승강기, 방화, 지구단위계획, 중첩규제
    /// </summary>
    [SwaggerSchema(Description = "검토 항목 분류 (예: 허용용도, 밀도, 주차)")]
    public string Category { get; init; } = string.Empty;

    /// <summary>항목명</summary>
    [SwaggerSchema(Description = "검토 항목명")]
    public string Title { get; init; } = string.Empty;

    /// <summary>한 줄 설명 (검토 포인트 요약)</summary>
    [SwaggerSchema(Description = "검토 포인트 한 줄 설명")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 이 항목을 판정하기 위해 추가로 필요한 입력값 목록.
    /// 예: ["계획 층수", "세대수", "건물 연면적"]
    /// </summary>
    [SwaggerSchema(Description = "판정에 필요한 추가 입력값 목록 (없으면 빈 배열)")]
    public List<string> RequiredInputs { get; init; } = new();

    /// <summary>관련 법령 문자열 목록 (참고용, 빈 배열 가능)</summary>
    [SwaggerSchema(Description = "관련 법령 참고 목록. 빈 배열이면 개별 확인 필요.")]
    public List<string> RelatedLaws { get; init; } = new();

    /// <summary>
    /// 자동 판정 가능 여부.
    /// <c>true</c>: 공간 데이터로 이미 확인된 항목 (자동 감지).
    /// <c>false</c>: 면적·층수·조례 등 추가 정보가 있어야 판단 가능한 수동 검토 항목.
    /// </summary>
    [SwaggerSchema(Description = "false이면 수동 검토 필요 항목")]
    public bool IsAutoCheckable { get; init; }

    /// <summary>
    /// 검토 우선순위.
    /// high: 사업 가부를 결정하는 최우선 항목 / medium: 설계 변경이 필요할 수 있는 항목 / low: 실시설계 단계에서 확정 가능한 항목
    /// </summary>
    [SwaggerSchema(Description = "우선순위: high / medium / low")]
    public string Priority { get; init; } = "medium";

    /// <summary>
    /// [Optional] 법제처 조문 텍스트 목록.
    /// <c>includeLegalBasis=true</c> 쿼리 파라미터 사용 시에만 포함됩니다.
    /// 기본 응답에서는 null이며 직렬화 시 생략됩니다.
    /// </summary>
    [SwaggerSchema(Description = "[Optional] 법제처 조문 텍스트. includeLegalBasis=true 시에만 포함.")]
    public List<LawClauseDto>? LegalBasisClauses { get; init; }

    // ── /review 통합 엔드포인트 전용 확장 필드 ───────────────────────────────
    // 기존 /review-items 응답에서는 모두 null (WhenWritingNull 직렬화 생략)

    /// <summary>
    /// 규칙 고유 ID (예: "RI-APT-002").
    /// /review 엔드포인트 응답에 포함됩니다. /review-items에서는 포함.
    /// </summary>
    [SwaggerSchema(Description = "규칙 고유 ID. /review 엔드포인트에서 판정 추적용.")]
    public string? RuleId { get; init; }

    /// <summary>
    /// 판정 상태.
    /// <list type="bullet">
    ///   <item><term>active</term><description>입력값 기반 계산 판정 완료</description></item>
    ///   <item><term>reference</term><description>용도지역 법정 기준치 제시 (입력 부족)</description></item>
    ///   <item><term>pending</term><description>판정에 필요한 입력값 없음 — 기준치도 표시 불가</description></item>
    /// </list>
    /// /review-items 엔드포인트에서는 null.
    /// </summary>
    [SwaggerSchema(Description = "'active'=계산완료 | 'reference'=기준치제시 | 'pending'=입력부족")]
    public string? JudgeStatus { get; init; }

    /// <summary>
    /// 판정 결과 메모 (계산값 또는 기준치 안내).
    /// 예: "계획 용적률 600% > 법정 상한 250% — 초과 ⚠️"
    /// /review-items 엔드포인트에서는 null.
    /// </summary>
    [SwaggerSchema(Description = "판정 결과 메모. /review 엔드포인트 전용.")]
    public string? JudgeNote { get; init; }
}

#endregion

#region ReviewItemsResponseDto

/// <summary>계획 용도 기반 검토 항목 응답 DTO</summary>
[SwaggerSchema(Description = "선택 용도 기반 건축 검토 항목 목록. 모든 항목은 참고용이며 확정 판단의 근거가 아닙니다.")]
public sealed class ReviewItemsResponseDto
{
    /// <summary>선택된 계획 용도</summary>
    [SwaggerSchema(Description = "요청 시 전달한 계획 용도")]
    public string SelectedUse { get; init; } = string.Empty;

    /// <summary>판정 용도지역명. 없으면 null.</summary>
    [SwaggerSchema(Description = "판정된 용도지역명. 미발견 시 null.")]
    public string? ZoneName { get; init; }

    /// <summary>검토 항목 목록 (카테고리 순 정렬)</summary>
    [SwaggerSchema(Description = "검토 항목 목록")]
    public List<ReviewItemDto> ReviewItems { get; init; } = new();

    /// <summary>[표준] 데이터 출처: 항상 "rule" (내부 규칙 테이블).</summary>
    [SwaggerSchema(Description = "[표준] 데이터 출처: 'rule' = 내부 규칙 테이블 (참고용)")]
    public string Source { get; init; } = "rule";

    /// <summary>[표준] 신뢰도: 항상 "low" (규칙 테이블 기반).</summary>
    [SwaggerSchema(Description = "[표준] 신뢰도: 'low' = 규칙 테이블 (참고용)")]
    public string Confidence { get; init; } = "low";

    /// <summary>[Deprecated] DataSource → Source 로 이전 예정. 동일 값.</summary>
    [SwaggerSchema(Description = "[Deprecated] → source 사용 권장. 동일 값.")]
    public string DataSource { get; init; } = "rule";
}

#endregion
