// =============================================================================
// BuildingReviewDtos.cs
// POST /api/regulation-check/review 통합 엔드포인트 요청/응답 DTO 모음
//
// [설계 원칙]
//   - reviewLevel: "quick"|"standard"|"detailed"|"expert" (생략 시 buildingInputs 기반 자동 추론)
//   - buildingInputs: 단계별 선택 입력 — 없는 필드는 기준치 제시 모드
//   - 응답 ReviewItemDto에 ruleId + judgeStatus + judgeNote 포함
//   - 기존 /review-items, /law-layers 엔드포인트는 변경 없음
// =============================================================================

using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

// ─────────────────────────────────────────────────────────────────────────────
// 요청 DTO
// ─────────────────────────────────────────────────────────────────────────────

#region 요청

/// <summary>
/// POST /api/regulation-check/review 통합 요청 DTO.
/// 위치(address 또는 좌표)와 계획 용도를 필수로 받고,
/// buildingInputs를 통해 단계적으로 상세 입력을 추가합니다.
/// </summary>
public sealed class BuildingReviewRequestDto
{
    // ── 위치 입력 (address 또는 longitude/latitude 중 하나 필수) ───────────────

    /// <summary>도로명 또는 지번 주소 (V-World 지오코딩 사용).</summary>
    [SwaggerSchema(Description = "도로명/지번 주소. longitude/latitude와 둘 중 하나 필수.")]
    public string? Address { get; init; }

    /// <summary>WGS84 경도. Address 없을 때 latitude와 함께 필수.</summary>
    [SwaggerSchema(Description = "WGS84 경도. Address 없을 때 사용.")]
    public double? Longitude { get; init; }

    /// <summary>WGS84 위도. Address 없을 때 longitude와 함께 필수.</summary>
    [SwaggerSchema(Description = "WGS84 위도. Address 없을 때 사용.")]
    public double? Latitude { get; init; }

    // ── 검토 단계 제어 ────────────────────────────────────────────────────────

    /// <summary>
    /// 검토 단계.
    /// "quick" | "standard" | "detailed" | "expert"
    /// 생략 시 buildingInputs에서 자동 추론합니다.
    /// </summary>
    [SwaggerSchema(Description = "검토 단계. 생략 시 buildingInputs 기반 자동 추론.")]
    public string? ReviewLevel { get; init; }

    // ── 계획 용도 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 계획 용도 (필수).
    /// "공동주택" | "제1종근린생활시설" | "제2종근린생활시설" | "업무시설"
    /// </summary>
    [SwaggerSchema(Description = "계획 용도. 필수.")]
    public string SelectedUse { get; init; } = string.Empty;

    // ── 건축 규모 입력 (선택) ─────────────────────────────────────────────────

    /// <summary>건축 규모 상세 입력 (선택). 없으면 기준치 제시 모드로 동작.</summary>
    [SwaggerSchema(Description = "건축 규모 상세 입력. 없으면 기준치 제시 모드.")]
    public BuildingInputsDto? BuildingInputs { get; init; }

    // ── 확장 옵션 ─────────────────────────────────────────────────────────────

    /// <summary>법제처 조문 텍스트 포함 여부 (기본 false).</summary>
    [SwaggerSchema(Description = "true이면 법제처 DRF API 조문 텍스트 포함.")]
    public bool IncludeLegalBasis { get; init; } = false;
    public ProjectContextDto? ProjectContext { get; init; }
    public GeometryInputDto? GeometryInput { get; init; }
    public string? CsvUploadToken { get; init; }
}

/// <summary>
/// 건축 규모 상세 입력 DTO.
/// Standard / Detailed / Expert 단계별로 필드를 추가합니다.
/// </summary>
public sealed class ProjectContextDto
{
    public string? ProjectId { get; init; }
    public string? ScenarioId { get; init; }
    public List<string> SiteIds { get; init; } = new();
    public string? VersionTag { get; init; }
}

public sealed class GeometryInputDto
{
    public string? SourceType { get; init; }
    public string? GeometryRef { get; init; }
    public string? CoordinateSystem { get; init; }
}

public sealed class BuildingInputsDto
{
    // ── Standard 입력 ─────────────────────────────────────────────────────────

    /// <summary>대지면적 (m²). 건폐율·용적률 계산에 사용.</summary>
    public double? SiteArea { get; init; }
    public double? BuildingArea { get; init; }

    /// <summary>지상 연면적 합계 (m²). 주차·방화구획·교통영향평가 기준.</summary>
    public double? FloorArea { get; init; }

    /// <summary>지상 층수. 피난계단·승강기 의무 기준.</summary>
    public int? FloorCount { get; init; }

    /// <summary>건물 높이 (m). 비상용 승강기 31m 기준 등 확인용. 선택.</summary>
    public double? BuildingHeight { get; init; }

    /// <summary>도로 접면 폭 (m). 접도 요건(4m 이상) 확인용. 선택.</summary>
    public double? RoadFrontageWidth { get; init; }

    // ── Detailed — 공동주택 전용 ─────────────────────────────────────────────

    /// <summary>세대수. 주차대수 정밀 산정에 필요.</summary>
    public int? UnitCount { get; init; }
    public int? RoomCount { get; init; }
    public int? GuestRoomCount { get; init; }
    public int? BedCount { get; init; }
    public int? StudentCount { get; init; }

    /// <summary>세대별 전용면적 (m²). 주차대수 기준값 결정.</summary>
    public double? UnitArea { get; init; }

    /// <summary>
    /// 공동주택 세부 유형.
    /// "아파트" | "연립" | "다세대"
    /// </summary>
    public string? HousingSubtype { get; init; }

    /// <summary>
    /// 주차 방식.
    /// "underground" | "ground" | "mechanical"
    /// </summary>
    public string? ParkingType { get; init; }
    public string? VehicleIngressType { get; init; }

    // ── Detailed — 근린생활시설 전용 ──────────────────────────────────────────

    /// <summary>계획 업종 세부 (예: "의원", "슈퍼마켓", "고시원"). 바닥면적 상한 확인용.</summary>
    public string? DetailUseSubtype { get; init; }

    /// <summary>해당 업종 계획 바닥면적 (m²). 면적 상한 초과 여부 판정.</summary>
    public double? DetailUseFloorArea { get; init; }

    /// <summary>다중이용업 해당 여부. true이면 강화 기준 적용.</summary>
    public bool? IsMultipleOccupancy { get; init; }

    /// <summary>화재위험 업종 해당 여부 (2종 근린생활시설). 방화 강화 기준 트리거.</summary>
    public bool? IsHighRiskOccupancy { get; init; }

    /// <summary>장애인·노인 이용 시설 해당 여부. 편의시설 설치 의무 기준.</summary>
    public bool? HasDisabilityUsers { get; init; }

    // ── Detailed — 업무시설 전용 ──────────────────────────────────────────────

    /// <summary>
    /// 업무시설 세부 유형.
    /// "오피스텔" | "일반업무"
    /// </summary>
    public string? OfficeSubtype { get; init; }

    /// <summary>복합 용도 비율 (0.0~1.0). 공공기여·방화구획 분리 검토.</summary>
    public double? MixedUseRatio { get; init; }

    /// <summary>예상 상주 인원. 승강기 대수 기준 산정.</summary>
    public int? OccupantCount { get; init; }

    /// <summary>공개공지 계획 여부. 연면적 5,000m² 이상 상업지역 의무 확인.</summary>
    public bool? HasPublicSpace { get; init; }

    /// <summary>하역 공간 계획 여부. 업무시설 진출입 동선 확인.</summary>
    public bool? HasLoadingBay { get; init; }
    public string? MedicalSpecialCriteria { get; init; }
    public string? EducationSpecialCriteria { get; init; }
    public string? HazardousMaterialProfile { get; init; }
    public string? LogisticsOperationProfile { get; init; }
    public string? AccommodationSpecialCriteria { get; init; }

    // ── Expert — 서류 보유 여부 ───────────────────────────────────────────────

    /// <summary>지구단위계획 결정도·지침서 보유 여부.</summary>
    public bool? HasDistrictUnitPlanDocument { get; init; }

    /// <summary>개발행위허가제한 지자체 협의 여부.</summary>
    public bool? HasDevActRestrictionConsult { get; init; }
}

#endregion

// ─────────────────────────────────────────────────────────────────────────────
// 응답 DTO
// ─────────────────────────────────────────────────────────────────────────────

#region 응답 서브 DTO

/// <summary>위치 해석 결과 요약 DTO</summary>
public sealed class LocationSummaryDto
{
    /// <summary>요청 시 전달한 주소 (좌표 직접 입력이면 null)</summary>
    public string? InputAddress { get; init; }

    /// <summary>V-World 지오코딩이 반환한 정규화 주소 (주소 입력 시)</summary>
    public string? ResolvedAddress { get; init; }

    /// <summary>판정에 사용된 경도</summary>
    public double? Longitude { get; init; }

    /// <summary>판정에 사용된 위도</summary>
    public double? Latitude { get; init; }

    /// <summary>지오코딩 제공사 (예: "VWorld"). 좌표 직접 입력 시 null.</summary>
    public string? GeocodingProvider { get; init; }
}

/// <summary>용도지역 판정 요약 DTO</summary>
public sealed class ZoningSummaryDto
{
    /// <summary>판정된 용도지역명 (예: 제2종일반주거지역)</summary>
    public string? ZoneName { get; init; }

    /// <summary>법정 건폐율 상한 (%). null이면 해당 용도지역 기준 없음.</summary>
    public double? BcRatioLimitPct { get; init; }

    /// <summary>법정 용적률 상한 (%). null이면 해당 용도지역 기준 없음.</summary>
    public double? FarLimitPct { get; init; }

    /// <summary>상한 출처 안내 메시지</summary>
    public string Note { get; init; } = "국토계획법 법정 상한 (조례에서 하향될 수 있음)";
}

/// <summary>오버레이 레이어 판정 결과 요약 DTO</summary>
public sealed class OverlayDecisionDto
{
    public bool? IsInside { get; init; }
    public string Source { get; init; } = "none";
    public string Status { get; init; } = "unavailable";
    public string Confidence { get; init; } = "low";
    public string? Name { get; init; }
    public string? Code { get; init; }
    public string? Note { get; init; }
}

public sealed class ApiVerificationStatusDto
{
    public string Source { get; init; } = "none";
    public string Status { get; init; } = "unavailable";
    public string Confidence { get; init; } = "low";
    public string? Note { get; init; }
}

public sealed class OverlaySummaryDto
{
    /// <summary>지구단위계획구역 포함 여부. null=데이터 미보유.</summary>
    public bool? DistrictUnitPlan { get; init; }

    /// <summary>개발제한구역 포함 여부. null=데이터 미보유.</summary>
    public bool? DevelopmentRestriction { get; init; }

    /// <summary>개발행위허가제한지역 포함 여부. null=데이터 미보유.</summary>
    public bool? DevelopmentActionRestriction { get; init; }

    /// <summary>개발행위허가제한지역 상세 판정 요약. source/status/confidence 포함.</summary>
    public OverlayDecisionDto? DevelopmentActionRestrictionDetail { get; init; }
}

/// <summary>입력값 제공 현황 요약 DTO</summary>
public sealed class InputSummaryDto
{
    /// <summary>제공된 입력 필드명 목록</summary>
    public List<string> Provided { get; init; } = new();

    /// <summary>제공되지 않은 핵심 입력 필드명 목록</summary>
    public List<string> Missing { get; init; } = new();

    /// <summary>추가 입력 시 판정 향상 안내 메시지. 없으면 null.</summary>
    public string? MissingNote { get; init; }
}

/// <summary>다음 단계 진행 시 필요한 입력 필드 DTO</summary>
public sealed class NextLevelInputFieldDto
{
    /// <summary>입력 필드 키 (예: "floorArea")</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>사용자 표시용 레이블 (예: "계획 연면적 (m²)")</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>이 필드가 필요한 이유 (예: "건폐율·용적률 판정")</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>다음 단계 진행 힌트 DTO</summary>
public sealed class NextLevelHintDto
{
    /// <summary>다음 검토 단계 ("standard" | "detailed" | "expert")</summary>
    public string NextLevel { get; init; } = string.Empty;

    /// <summary>다음 단계 진행 시 추가로 필요한 입력 필드 목록</summary>
    public List<NextLevelInputFieldDto> AdditionalInputsNeeded { get; init; } = new();

    /// <summary>다음 단계에서 새로 판정 가능한 항목 목록 (제목 + ruleId)</summary>
    public List<string> WillUnlock { get; init; } = new();

    /// <summary>
    /// 힌트 보조 안내 메시지.
    /// 힌트가 있는 경우 null (생략). 더 이상 진행할 단계가 없거나 이미 모든 입력이 제공된 경우 이유 문자열.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>다음 단계에서 새로 활성화될 Task 카테고리 목록</summary>
    public List<string> WillAddTaskCategories { get; init; } = new();

    /// <summary>다음 단계 체크리스트 변화 요약</summary>
    public List<string> WillChangeChecklist { get; init; } = new();
}

/// <summary>선택된 용도의 내부 UseProfile 요약 DTO</summary>
public sealed class UseProfileSummaryDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FunctionalGroup { get; init; } = string.Empty;
    public string AutomationCoverage { get; init; } = string.Empty;
    public string RuleDataStatus { get; init; } = string.Empty;
    public int ReviewRuleCount { get; init; }
    public int LawLayerRuleCount { get; init; }
    public int ExplicitReviewRuleCount { get; init; }
    public int ExplicitLawLayerRuleCount { get; init; }
    public int SeedReviewRuleCount { get; init; }
    public int SeedLawLayerRuleCount { get; init; }
    public int FallbackReviewRuleCount { get; init; }
    public int FallbackLawLayerRuleCount { get; init; }
    public bool UsesSeed { get; init; }
    public bool UsesFallback { get; init; }
    public string? CoverageNote { get; init; }
    public List<string> LegalSearchHints { get; init; } = new();
    public List<string> RuleBundles { get; init; } = new();
}

/// <summary>계획 용도 선택 UI용 UseProfile 목록 항목</summary>
public sealed class UseProfileListItemDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string FunctionalGroup { get; init; } = string.Empty;
    public string AutomationCoverage { get; init; } = string.Empty;
    public List<string> RequiredStandardInputs { get; init; } = new();
    public List<string> RequiredDetailedInputs { get; init; } = new();
    public List<string> RuleBundles { get; init; } = new();
    public List<string> LegalSearchHints { get; init; } = new();
}

/// <summary>활성 RuleBundle 요약 DTO</summary>
public sealed class RuleBundleSummaryDto
{
    public string BundleId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public List<string> Categories { get; init; } = new();
    public List<string> RelatedRuleIds { get; init; } = new();
}

/// <summary>사용자 작업 단위 Task DTO</summary>
public sealed class ReviewTaskDto
{
    public string TaskId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public List<string> RelatedRuleIds { get; init; } = new();
    public string Priority { get; init; } = "medium";
}

/// <summary>프로젝트 체크리스트 집계 DTO</summary>
public sealed class ProjectChecklistDto
{
    public int Fail { get; init; }
    public int Warning { get; init; }
    public int Ok { get; init; }
    public int Info { get; init; }
    public int ManualReview { get; init; }
}

/// <summary>수동 검토 레이어 카드 DTO</summary>
public sealed class ManualReviewCardDto
{
    public string ManualReviewId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string Status { get; init; } = "manual_review";
    public string SourceType { get; init; } = "rule_pending";
    public List<string> RelatedRuleIds { get; init; } = new();
    public List<string> RequiredInputs { get; init; } = new();
    public List<string> SuggestedChecks { get; init; } = new();
    public List<string> SearchHints { get; init; } = new();
}

/// <summary>조례/행정 확인 카드 DTO</summary>
public sealed class OrdinanceReviewCardDto
{
    public string OrdinanceId { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string SourceType { get; init; } = "region_metadata";
    public string? Link { get; init; }
    public string? Department { get; init; }
    public List<string> Keywords { get; init; } = new();
    public List<string> CheckItems { get; init; } = new();
}

public sealed class ReportSectionDto
{
    public int Order { get; init; }
    public string SectionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<ReportFieldDto> Fields { get; init; } = new();
    public List<string> Highlights { get; init; } = new();
}

public sealed class ReportFieldDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class ReportLegalBasisEntryDto
{
    public string RuleId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<LawClauseDto> Clauses { get; init; } = new();
}

public sealed class ReportPreviewDto
{
    public string SchemaVersion { get; init; } = "report_preview_v1";
    public string Title { get; init; } = string.Empty;
    public List<ReportSectionDto> Sections { get; init; } = new();
    public List<ReportLegalBasisEntryDto> LegalBasisEntries { get; init; } = new();
}

public sealed class ReportPackageMetadataDto
{
    public string PackageVersion { get; init; } = "report_package_v1";
    public string PreviewSchemaVersion { get; init; } = "report_preview_v1";
    public string Title { get; init; } = string.Empty;
    public string SuggestedFileNameBase { get; init; } = string.Empty;
    public string GeneratedAt { get; init; } = string.Empty;
    public List<string> SupportedFormats { get; init; } = new();
    public List<string> IntermediateFormats { get; init; } = new();
    public ApiVerificationStatusDto DevelopmentActionApiStatus { get; init; } = new();
}

public sealed class BuildingReviewReportPackageDto
{
    public ReportPackageMetadataDto Metadata { get; init; } = new();
    public ReportPreviewDto Preview { get; init; } = new();
    public ProjectChecklistDto Checklist { get; init; } = new();
    public List<ReviewTaskDto> Tasks { get; init; } = new();
    public List<ManualReviewCardDto> ManualReviews { get; init; } = new();
    public List<OrdinanceReviewCardDto> OrdinanceReviews { get; init; } = new();
    public BuildingReviewResponseDto SourceReview { get; init; } = new();
}

public sealed class ReviewReportExportRequestDto
{
    public string Format { get; init; } = "pdf";
    public BuildingReviewRequestDto ReviewRequest { get; init; } = new();
}

public sealed class ReviewReportExportPlanDto
{
    public string Status { get; init; } = "ready";
    public string Format { get; init; } = "pdf";
    public string MimeType { get; init; } = "application/pdf";
    public string RendererKey { get; init; } = "report_renderer_v1";
    public string TemplateKey { get; init; } = string.Empty;
    public string SuggestedFileName { get; init; } = string.Empty;
    public BuildingReviewReportPackageDto Package { get; init; } = new();
}

public sealed class ReviewReportRenderResultDto
{
    public string Status { get; init; } = "render_ready";
    public string Format { get; init; } = "pdf";
    public string TargetMimeType { get; init; } = "application/pdf";
    public string PayloadMimeType { get; init; } = "text/markdown";
    public string RendererKey { get; init; } = "report_renderer_v1";
    public string TemplateKey { get; init; } = string.Empty;
    public string SuggestedFileName { get; init; } = string.Empty;
    public string PayloadType { get; init; } = "markdown_document";
    public string PayloadText { get; init; } = string.Empty;
    public ReviewReportExportPlanDto ExportPlan { get; init; } = new();
}

public sealed class ReviewReportArtifactDto
{
    public string Status { get; init; } = "artifact_ready";
    public string Format { get; init; } = "md";
    public string MimeType { get; init; } = "text/markdown";
    public string Encoding { get; init; } = "utf-8";
    public string SuggestedFileName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

#endregion

#region 응답 루트 DTO

/// <summary>
/// POST /api/regulation-check/review 통합 응답 DTO.
/// <para>
/// 위치 판정 + 용도지역 자동 판정 + 단계별 검토 항목 판정 + 다음 단계 힌트를 통합 제공합니다.
/// </para>
/// </summary>
[SwaggerSchema(Description = "통합 건축 법규 검토 응답. 모든 결과는 참고용 1차 판정입니다.")]
public sealed class BuildingReviewResponseDto
{
    /// <summary>실제 적용된 검토 단계 ("quick" | "standard" | "detailed" | "expert")</summary>
    [SwaggerSchema(Description = "실제 적용된 검토 단계 (생략 시 자동 추론된 값)")]
    public string ReviewLevel { get; init; } = string.Empty;

    /// <summary>요청 계획 용도</summary>
    public string SelectedUse { get; init; } = string.Empty;

    /// <summary>선택 용도의 내부 공통 엔진 프로필 요약</summary>
    public UseProfileSummaryDto? UseProfile { get; init; }

    /// <summary>위치 해석 결과</summary>
    public LocationSummaryDto Location { get; init; } = new();

    /// <summary>
    /// 용도지역 자동 판정 결과.
    /// 공간 데이터 미매칭이면 null.
    /// </summary>
    public ZoningSummaryDto? Zoning { get; init; }

    /// <summary>오버레이 레이어 판정 결과</summary>
    public OverlaySummaryDto Overlays { get; init; } = new();

    /// <summary>
    /// 단계별 필터링 + judgeStatus 판정이 적용된 검토 항목 목록.
    /// <para>각 항목의 judgeStatus: "active" | "reference" | "pending"</para>
    /// </summary>
    [SwaggerSchema(Description = "검토 항목 목록. judgeStatus로 판정 수준 구분.")]
    public List<ReviewItemDto> ReviewItems { get; init; } = new();

    /// <summary>ReviewItems를 사용자 작업 단위로 집계한 Task 레이어</summary>
    public List<ReviewTaskDto> Tasks { get; init; } = new();

    /// <summary>이번 검토에서 활성/휴면 상태가 정리된 RuleBundle 목록</summary>
    public List<RuleBundleSummaryDto> ActiveRuleBundles { get; init; } = new();

    /// <summary>프로젝트 수준 체크리스트 요약</summary>
    public ProjectChecklistDto Checklist { get; init; } = new();

    /// <summary>자동 판정이 어려운 항목을 구조화한 수동 검토 카드</summary>
    public List<ManualReviewCardDto> ManualReviews { get; init; } = new();

    /// <summary>조례/지구단위계획/행정협의 관련 확인 카드</summary>
    public List<OrdinanceReviewCardDto> OrdinanceReviews { get; init; } = new();

    /// <summary>PDF/Word 보고서 생성 전 미리보기용 고정 구조 요약</summary>
    public ReportPreviewDto ReportPreview { get; init; } = new();
    public ApiVerificationStatusDto DevelopmentActionApiStatus { get; init; } = new();

    /// <summary>입력값 제공 현황 요약</summary>
    public InputSummaryDto InputSummary { get; init; } = new();

    /// <summary>
    /// 다음 단계 진행 힌트.
    /// 항상 포함. 더 이상 진행할 단계가 없거나 입력이 완료된 경우 Note 필드에 이유 포함, AdditionalInputsNeeded·WillUnlock은 빈 배열.
    /// </summary>
    public NextLevelHintDto? NextLevelHint { get; init; }

    /// <summary>[표준] 데이터 출처: "rule" (내부 규칙 테이블 기반)</summary>
    public string Source { get; init; } = "rule";

    /// <summary>[표준] 신뢰도: "low" (오프라인 공간데이터 + 규칙 테이블 기반 참고값)</summary>
    public string Confidence { get; init; } = "low";

    /// <summary>서버 처리 시간 (ms)</summary>
    public long ElapsedMs { get; init; }
}

#endregion
