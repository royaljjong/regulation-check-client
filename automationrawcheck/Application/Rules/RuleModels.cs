// =============================================================================
// RuleModels.cs
// 법규 규칙 데이터 사전 레코드 타입
//
// [설계 원칙]
//   - 규칙 데이터(문자열/조건)와 조립 로직을 완전 분리
//   - 모든 규칙 항목에 관리 메타(id, version, isActive, sortOrder) 포함
//   - 표시 문구(law/scope/title/description)와 판정 조건(trigger)을 별도 필드로 구분
//   - 법규 근거(legalBasis)를 조문 단위 객체 배열로 구조화 — 외부 법령 API 연동 기반
//
// [ID 체계]
//   LawLayer  : LL-{USE}-{LAYER}-{SEQ}
//               USE: APT / NH1 / NH2 / OFF
//               LAYER: CORE / EXCL / MEP
//               오버레이 공통: LL-OVL-DUP-001 / LL-OVL-DAR-001
//   ReviewItem: RI-{USE}-{SEQ}
//               오버레이 공통: RI-OVL-DUP-001 / RI-OVL-DAR-001
//
// [표시 문구 vs 판정 조건 분리]
//   표시 문구 (UI 렌더링용): law, scope, title, teamTag, category, description
//   판정 조건 (로직 제어용): trigger, applicableUses, applicableZones
//   관리용(사전 관리자용): legalBasis, practicalNote, isActive, sortOrder, version
//
// [legalBasis 구조]
//   List<LegalReferenceRecord> — 조문별 객체 배열
//   각 항목은 referenceType + lawName + (article 또는 appendixRef) + normalizedKey 필수
//   normalizedKey 형식:
//     조문: {lawAlias}/{article}[/{paragraph}][/{subParagraph}]
//     별표: {lawAlias}/{appendixRef}[/{subParagraph}]
//     고시: {noticeAlias}/{section}
//
// [isActive 운영]
//   isActive=false → RuleStore 로드 시 컬렉션에서 제외 (무중단 비활성화)
//
// [sortOrder 운영]
//   같은 레이어·용도 그룹 내 오름차순 정렬 (10, 20, 30...)
//   오버레이 항목은 sortOrder=1 (항상 그룹 맨 앞)
//   미지정 시 int.MaxValue (목록 맨 끝)
// =============================================================================

namespace AutomationRawCheck.Application.Rules;

// ─────────────────────────────────────────────────────────────────────────────
// 공통 서브 레코드
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>규칙 항목의 트리거(포함) 조건</summary>
public sealed record RuleTrigger
{
    /// <summary>true → 용도 선택 시 항상 포함. false → 오버레이 조건 필요.</summary>
    public bool AlwaysInclude { get; init; } = true;

    /// <summary>지구단위계획구역 포함 여부가 true일 때만 포함. null = 해당 없음.</summary>
    public bool? WhenDistrictUnitPlan { get; init; }

    /// <summary>개발제한구역 포함 여부가 true일 때만 포함. null = 해당 없음.</summary>
    public bool? WhenDevelopmentRestriction { get; init; }
}

/// <summary>
/// 단일 법령 조문 참조 레코드.
/// 외부 국가법령정보 API 연동 시 normalizedKey를 조회 키로 사용합니다.
/// legalBasis 필드는 관리용 전용이며 API 응답에 포함되지 않습니다.
/// </summary>
public sealed record LegalReferenceRecord
{
    // ── 분류 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 참조 유형.
    /// "statute" | "enforcementDecree" | "enforcementRule" | "notice" | "ordinance" | "guideline"
    /// </summary>
    public string ReferenceType { get; init; } = string.Empty;

    // ── 법령 식별 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 법령 표시명 (필수).
    /// 예: "건축법", "국토의 계획 및 이용에 관한 법률", "소방시설 설치 및 관리에 관한 법률"
    /// </summary>
    public string LawName { get; init; } = string.Empty;

    // ── 조문 위치 — 조문 참조 시 ─────────────────────────────────────────────

    /// <summary>
    /// 조문 번호 (양수). 별표·부칙 참조 시 null. AppendixRef와 둘 중 하나 필수.
    /// 예: 11 → "제11조"
    /// </summary>
    public int? Article { get; init; }

    /// <summary>
    /// 항 번호 (null = 미지정).
    /// 예: 1 → "제1항"
    /// </summary>
    public int? Paragraph { get; init; }

    /// <summary>
    /// 호·목 (null = 미지정).
    /// 예: "1" → "제1호", "가" → "가목"
    /// </summary>
    public string? SubParagraph { get; init; }

    // ── 조문 위치 — 별표·부칙 참조 시 ──────────────────────────────────────────

    /// <summary>
    /// 별표·부칙 참조 식별자 (null = 해당 없음). Article null 시 필수.
    /// 예: "별표4", "부칙1", "별표1"
    /// </summary>
    public string? AppendixRef { get; init; }

    // ── 내용 요약 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 해당 조문의 내용 요약 (관리자용, UI 미표시).
    /// </summary>
    public string? ClauseTextSummary { get; init; }

    // ── API 연동 키 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 정규화된 참조 키 (필수, 규칙 내 유일).
    /// 형식:
    ///   조문: {lawAlias}/{article}[/{paragraph}][/{subParagraph}]
    ///   별표: {lawAlias}/{appendixRef}[/{subParagraph}]
    ///   고시: {noticeAlias}/{section}
    /// 예: "건축법/11", "건축법시행령/5/1/2", "국토계획법시행령/별표4", "건축법/별표1/2"
    /// 향후 국가법령정보 API 조회 시 이 키로 매핑합니다.
    /// </summary>
    public string NormalizedKey { get; init; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// LawLayerRuleRecord
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 법규 레이어(Core / ExtendedCore / MEP) 단일 규칙 레코드.
/// JSON 키는 camelCase, C# 프로퍼티는 PascalCase (PropertyNameCaseInsensitive=true).
/// </summary>
public sealed record LawLayerRuleRecord
{
    // ── 관리 메타 ─────────────────────────────────────────────────────────────

    /// <summary>규칙 고유 ID (필수). 예: "LL-APT-CORE-001"</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 규칙 버전 문자열. 규칙 내용 변경 시 올립니다.
    /// 예: "1.0", "1.1"
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// 활성 여부. false면 RuleStore 로드 시 제외됩니다.
    /// 삭제하지 않고 비활성화할 때 사용합니다.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// 같은 레이어·용도 그룹 내 표시 순서 (오름차순).
    /// 오버레이 항목은 1, 일반 항목은 10 단위 증가.
    /// 미지정 시 int.MaxValue (목록 맨 끝).
    /// </summary>
    public int SortOrder { get; init; } = int.MaxValue;

    // ── 분류 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 단일 적용 용도 (backward compat). "*" = 전체 용도 공통 (오버레이 항목).
    /// 복수 용도 규칙은 ApplicableUses 사용.
    /// </summary>
    public string SelectedUse { get; init; } = string.Empty;

    /// <summary>
    /// 적용 대상 용도 목록. 비어 있으면 SelectedUse 단독 적용.
    /// 향후 복수 용도 공유 규칙 지원 시 사용합니다.
    /// </summary>
    public List<string> ApplicableUses { get; init; } = new();

    /// <summary>레이어 구분: "core" | "extendedCore" | "mep"</summary>
    public string LayerType { get; init; } = string.Empty;

    // ── 표시 문구 (UI 렌더링용) ───────────────────────────────────────────────

    /// <summary>법령명 표시 문구 (Core/ExtendedCore 항목용)</summary>
    public string? Law { get; init; }

    /// <summary>적용 범위 요약 표시 문구 (Core/ExtendedCore 항목용)</summary>
    public string? Scope { get; init; }

    /// <summary>검토 항목 제목 (MEP 항목용)</summary>
    public string? Title { get; init; }

    /// <summary>담당 팀 태그 (MEP 항목용). 예: "전기팀", "소방팀"</summary>
    public string? TeamTag { get; init; }

    // ── 법규 근거 (관리용, 조문 단위 분리) ───────────────────────────────────

    /// <summary>
    /// 조문 단위 법규 근거 목록 (관리용, API 응답 미포함).
    /// 빈 목록이면 Law/Scope 표시 문구만 사용합니다.
    /// 각 항목의 normalizedKey가 향후 국가법령정보 API 연동 키입니다.
    /// </summary>
    public List<LegalReferenceRecord> LegalBasis { get; init; } = new();

    /// <summary>
    /// 실무 적용 시 유의사항 (관리자용 메모).
    /// UI에 표시하지 않으며, 규칙 관리자를 위한 실무 참고 내용입니다.
    /// </summary>
    public string? PracticalNote { get; init; }

    // ── 표준 메타 ─────────────────────────────────────────────────────────────

    /// <summary>[표준] 데이터 출처: 항상 "rule"</summary>
    public string Source { get; init; } = "rule";

    /// <summary>[표준] 신뢰도: 항상 "low" (내부 규칙 테이블 기반)</summary>
    public string Confidence { get; init; } = "low";

    // ── 판정 조건 (로직 제어용) ───────────────────────────────────────────────

    /// <summary>포함 트리거 조건</summary>
    public RuleTrigger Trigger { get; init; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// ReviewItemRuleRecord
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 건축 검토 항목(ReviewItem) 단일 규칙 레코드.
/// </summary>
public sealed record ReviewItemRuleRecord
{
    // ── 관리 메타 ─────────────────────────────────────────────────────────────

    /// <summary>규칙 고유 ID (필수). 예: "RI-APT-001"</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>규칙 버전 문자열.</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>활성 여부. false면 RuleStore 로드 시 제외됩니다.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>같은 용도 그룹 내 표시 순서 (오름차순). 미지정 시 int.MaxValue.</summary>
    public int SortOrder { get; init; } = int.MaxValue;

    // ── 분류 ──────────────────────────────────────────────────────────────────

    /// <summary>단일 적용 용도. "*" = 전체 용도 공통 (오버레이 항목).</summary>
    public string SelectedUse { get; init; } = string.Empty;

    /// <summary>적용 대상 용도 목록 (복수 용도 공유 규칙 지원용).</summary>
    public List<string> ApplicableUses { get; init; } = new();

    /// <summary>
    /// 적용 대상 용도지역 목록.
    /// ["*"] = 모든 용도지역. 특정 용도지역만 해당하는 규칙은 구체적 지역명 목록 사용.
    /// 현재는 필터링 로직 미구현 (관리·문서화 목적).
    /// </summary>
    public List<string> ApplicableZones { get; init; } = new() { "*" };

    // ── 표시 문구 (UI 렌더링용) ───────────────────────────────────────────────

    /// <summary>검토 항목 분류</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>항목명 (행동형 제목)</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>리스크·사업성 영향 중심 설명</summary>
    public string Description { get; init; } = string.Empty;

    // ── 판정 조건 (로직 제어용) ───────────────────────────────────────────────

    /// <summary>판정에 필요한 추가 입력값 목록</summary>
    public List<string> RequiredInputs { get; init; } = new();

    /// <summary>관련 법령 참고 목록 (표시용)</summary>
    public List<string> RelatedLaws { get; init; } = new();

    /// <summary>공간 데이터로 자동 판정 가능 여부</summary>
    public bool IsAutoCheckable { get; init; }

    /// <summary>우선순위: "high" | "medium" | "low"</summary>
    public string Priority { get; init; } = "medium";

    // ── 법규 근거 (관리용) ────────────────────────────────────────────────────

    /// <summary>
    /// 조문 단위 법규 근거 목록 (관리용, API 응답 미포함).
    /// RelatedLaws는 UI 표시용, LegalBasis는 조문 추적·검색용.
    /// 각 항목의 normalizedKey가 향후 국가법령정보 API 연동 키입니다.
    /// </summary>
    public List<LegalReferenceRecord> LegalBasis { get; init; } = new();

    /// <summary>실무 적용 유의사항 (관리자용 메모, UI 미표시).</summary>
    public string? PracticalNote { get; init; }

    // ── 표준 메타 ─────────────────────────────────────────────────────────────

    /// <summary>[표준] 데이터 출처: 항상 "rule"</summary>
    public string Source { get; init; } = "rule";

    /// <summary>[표준] 신뢰도: 항상 "low"</summary>
    public string Confidence { get; init; } = "low";

    // ── 포함 트리거 조건 ──────────────────────────────────────────────────────

    /// <summary>포함 트리거 조건</summary>
    public RuleTrigger Trigger { get; init; } = new();
}
