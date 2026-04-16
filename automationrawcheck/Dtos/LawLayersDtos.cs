// =============================================================================
// LawLayersDtos.cs
// POST /api/regulation-check/law-layers 요청/응답 DTO
//
// [요청]
//   selectedUse                   : 계획 용도 (필수)
//   districtUnitPlanIsInside      : 지구단위계획구역 포함 여부 (선택)
//   developmentRestrictionIsInside: 개발제한구역 포함 여부 (선택)
//
// [응답]
//   selectedUse      : 요청 용도
//   coreLaws[]       : Core Layer  — 건축 기본 법규 (law, scope)
//   extendedCoreLaws[]: Extended Core — 건축 필수 연계 법규 (law, scope)
//   mepLaws[]        : MEP Layer  — 협력사 법규 (title, teamTag)
//                      항상 "연계 검토 필요" 성격 / 자동 판정 금지
// =============================================================================

using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region LawClauseDto — 조문 텍스트 DTO (includeLegalBasis=true 시 포함)

/// <summary>
/// normalizedKey 하나에 대응하는 법제처 조문 텍스트 DTO.
/// <para>
/// <c>includeLegalBasis=true</c> 쿼리 파라미터 사용 시에만 응답에 포함됩니다.
/// </para>
/// </summary>
[SwaggerSchema(Description = "법제처 DRF API 조회 결과 조문 텍스트. includeLegalBasis=true 시에만 포함.")]
public sealed class LawClauseDto
{
    /// <summary>조회 키 (예: "건축법/11")</summary>
    [SwaggerSchema(Description = "normalizedKey (예: '건축법/11')")]
    public string NormalizedKey { get; init; } = string.Empty;

    /// <summary>법령 표시명 (예: "건축법")</summary>
    [SwaggerSchema(Description = "법령 표시명")]
    public string LawName { get; init; } = string.Empty;

    /// <summary>
    /// 조문 참조 (예: "제11조 (건축허가)", "별표4").
    /// </summary>
    [SwaggerSchema(Description = "조문 참조 문자열 (예: '제11조 (건축허가)')")]
    public string ArticleRef { get; init; } = string.Empty;

    /// <summary>
    /// 조문 원문 텍스트 (최대 500자).
    /// 별표·부칙·고시 등 텍스트를 가져올 수 없으면 null.
    /// </summary>
    [SwaggerSchema(Description = "조문 원문 텍스트 (최대 500자). 가져올 수 없으면 null.")]
    public string? ClauseText { get; init; }

    /// <summary>법제처 원문 링크 (HTML 뷰어)</summary>
    [SwaggerSchema(Description = "법제처 원문 링크 URL")]
    public string? Url { get; init; }

    /// <summary>
    /// 데이터 출처.
    /// "openlaw_api": 법제처 DRF API 조회 성공 /
    /// "rule_meta": API 실패 — JSON 메타데이터 기반 참조 정보
    /// </summary>
    [SwaggerSchema(Description = "'openlaw_api' = API 성공 | 'rule_meta' = API 실패, 규칙 메타 기반")]
    public string Source { get; init; } = "openlaw_api";
}

#endregion

#region 요청

/// <summary>법규 레이어 조회 요청 DTO</summary>
public sealed class LawLayersRequestDto
{
    /// <summary>계획 용도 (공동주택 | 제1종근린생활시설 | 제2종근린생활시설 | 업무시설)</summary>
    public string SelectedUse { get; init; } = string.Empty;

    /// <summary>지구단위계획구역 포함 여부 (null이면 미확인)</summary>
    public bool? DistrictUnitPlanIsInside { get; init; }

    /// <summary>개발제한구역 포함 여부 (null이면 미확인)</summary>
    public bool? DevelopmentRestrictionIsInside { get; init; }
}

#endregion

#region 응답 항목 DTO

/// <summary>Core / Extended Core 레이어 단일 항목 DTO</summary>
[SwaggerSchema(Description = "법령명과 적용 범위로 구성된 단일 법규 항목")]
public sealed class CoreLawItemDto
{
    /// <summary>법령명 (예: "건축법 제11조·제4조")</summary>
    [SwaggerSchema(Description = "관련 법령 또는 조문 명칭")]
    public string Law { get; init; } = string.Empty;

    /// <summary>적용 범위 또는 주요 내용 요약</summary>
    [SwaggerSchema(Description = "해당 법규의 적용 범위 및 핵심 내용")]
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// [Optional] 법제처 조문 텍스트 목록.
    /// <c>includeLegalBasis=true</c> 쿼리 파라미터 사용 시에만 포함됩니다.
    /// 기본 응답에서는 null이며 직렬화 시 생략됩니다.
    /// </summary>
    [SwaggerSchema(Description = "[Optional] 법제처 조문 텍스트. includeLegalBasis=true 시에만 포함.")]
    public List<LawClauseDto>? LegalBasisClauses { get; init; }
}

/// <summary>MEP Layer 단일 항목 DTO</summary>
[SwaggerSchema(Description = "협력사 검토 항목 (자동 판정 대상 아님)")]
public sealed class MepLawItemDto
{
    /// <summary>검토 항목 제목</summary>
    [SwaggerSchema(Description = "협력사 검토 항목 제목")]
    public string Title { get; init; } = string.Empty;

    /// <summary>담당 팀 태그 (예: "전기팀", "소방팀")</summary>
    [SwaggerSchema(Description = "담당 협력사 또는 팀 구분")]
    public string TeamTag { get; init; } = string.Empty;

    /// <summary>
    /// [Optional] 법제처 조문 텍스트 목록.
    /// <c>includeLegalBasis=true</c> 쿼리 파라미터 사용 시에만 포함됩니다.
    /// </summary>
    [SwaggerSchema(Description = "[Optional] 법제처 조문 텍스트. includeLegalBasis=true 시에만 포함.")]
    public List<LawClauseDto>? LegalBasisClauses { get; init; }
}

#endregion

#region 응답

/// <summary>용도별 법규 레이어 응답 DTO</summary>
[SwaggerSchema(Description = "Core / Extended Core / MEP 3개 레이어로 구성된 용도별 법규 목록. " +
                             "MEP 레이어는 항상 '연계 검토 필요' 성격이며 자동 판정을 포함하지 않습니다.")]
public sealed class LawLayersResponseDto
{
    /// <summary>요청 시 전달한 계획 용도</summary>
    public string SelectedUse { get; init; } = string.Empty;

    /// <summary>
    /// [표준] 데이터 출처: 항상 "rule" (내부 규칙 테이블).
    /// </summary>
    [SwaggerSchema(Description = "[표준] 데이터 출처: 'rule' = 내부 규칙 테이블 (참고용)")]
    public string Source { get; init; } = "rule";

    /// <summary>
    /// [표준] 신뢰도: 항상 "low" (규칙 테이블 기반, 공간 데이터 자동 판정 아님).
    /// </summary>
    [SwaggerSchema(Description = "[표준] 신뢰도: 'low' = 규칙 테이블 (참고용)")]
    public string Confidence { get; init; } = "low";

    /// <summary>[Deprecated] DataSource → Source 로 이전 예정. 동일 값.</summary>
    [SwaggerSchema(Description = "[Deprecated] → source 사용 권장. 동일 값.")]
    public string DataSource { get; init; } = "rule";

    /// <summary>
    /// [3] Core Layer — 건축 기본 법규.
    /// 지구단위계획구역 포함 시 개별 지침 우선 검토 항목이 앞에 추가됩니다.
    /// </summary>
    [SwaggerSchema(Description = "건축 기본 법규 목록 (Core Layer)")]
    public List<CoreLawItemDto> CoreLaws { get; init; } = new();

    /// <summary>
    /// [4] Extended Core Layer — 건축 필수 연계 법규.
    /// 개발제한구역 포함 시 공통 경고 항목이 앞에 추가됩니다.
    /// </summary>
    [SwaggerSchema(Description = "건축 필수 연계 법규 목록 (Extended Core)")]
    public List<CoreLawItemDto> ExtendedCoreLaws { get; init; } = new();

    /// <summary>[5] MEP Layer — 협력사 법규 (연계 검토 필요 고정)</summary>
    [SwaggerSchema(Description = "협력사 법규 목록 (MEP Layer). 자동 판정 불가.")]
    public List<MepLawItemDto> MepLaws { get; init; } = new();
}

#endregion
