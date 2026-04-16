// =============================================================================
// RegulationCheckResponseDto.cs
// POST /api/regulation-check/coordinate|parcel 공통 응답 DTO
// Domain 모델(RegulationCheckResult)을 API 응답 형태로 변환합니다.
//
// [응답 구조]
//   input              : 입력 좌표 (WGS84)
//   zoning             : 판정 용도지역 (zoneName, zoneCode, adminArea, attributes)
//   regulationSummary  : 판정 상태 + 안내 메시지 + 추가 검토 항목(caution)
//   extraLayers        : 개발제한구역, 개발행위허가제한지역, 지구단위계획 중첩 판정
//   regulationInfo     : 용도지역별 건축 법규 참고 정보 (국토계획법 기준, 참고용)
//   lawReferences      : 관련 법령 (현재 stub)
//   meta               : 원천 데이터 추적 정보
// =============================================================================

using AutomationRawCheck.Application.Services.Regulations;
using AutomationRawCheck.Domain.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

#region 보조 DTO 클래스들

/// <summary>입력 좌표 응답 DTO</summary>
[SwaggerSchema(Description = "요청에 사용된 입력 좌표 (WGS84 경위도)")]
public sealed class InputDto
{
    [SwaggerSchema(Description = "경도 (WGS84)", Format = "double")]
    public double Longitude { get; init; }

    [SwaggerSchema(Description = "위도 (WGS84)", Format = "double")]
    public double Latitude  { get; init; }
}

/// <summary>행정구역 정보 DTO</summary>
[SwaggerSchema(Description = "SHP 속성에서 추출된 시군구 코드 및 명칭")]
public sealed class AdminAreaDto
{
    [SwaggerSchema(Description = "5자리 시군구 코드 (법정동 코드 앞 5자리)")]
    public string Code { get; init; } = string.Empty;

    [SwaggerSchema(Description = "시도 명칭. 코드 매핑 불가 시 null.")]
    public string? Name { get; init; }
}

/// <summary>용도지역 정보 응답 DTO</summary>
[SwaggerSchema(Description = "판정된 용도지역 정보. 해당 없으면 null.")]
public sealed class ZoningDto
{
    [SwaggerSchema(Description = "DGM_NM 필드 기반 용도지역명 (예: 제2종일반주거지역)")]
    public string ZoneName    { get; init; } = string.Empty;

    [SwaggerSchema(Description = "ATRB_SE 필드 기반 속성구분코드 (예: UQA122)")]
    public string ZoneCode    { get; init; } = string.Empty;

    [SwaggerSchema(Description = "SHP 속성의 시군구 코드 및 시도 명칭")]
    public AdminAreaDto? AdminArea { get; init; }

    [SwaggerSchema(Description = "원천 SHP 레이어 식별자")]
    public string SourceLayer  { get; init; } = string.Empty;

    /// <summary>데이터 출처: 항상 "shp" (로컬 SHP 파일 직접 매칭)</summary>
    [SwaggerSchema(Description = "데이터 출처: 'shp' = 로컬 SHP 파일 직접 매칭")]
    public string Source { get; init; } = "shp";

    /// <summary>
    /// 신뢰도: "high" = 직접 매칭, "medium" = 경계 근접, "low" = 미발견
    /// </summary>
    [SwaggerSchema(Description = "신뢰도: high(직접 매칭) | medium(경계 근접) | low(미발견)")]
    public string Confidence { get; init; } = "low";

    [SwaggerSchema(Description = "원본 DBF 속성 딕셔너리 (raw). 참조 목적으로만 사용하세요.")]
    public Dictionary<string, object?> Attributes { get; init; } = new();
}

/// <summary>규제 판정 요약 DTO</summary>
[SwaggerSchema(Description = "1차 판정 상태, 안내 메시지, 추가 검토 필요 항목")]
public sealed class RegulationSummaryDto
{
    [SwaggerSchema(Description = "판정 상태: Preliminary(1차 판정) | NotFound(용도지역 없음) | Error")]
    public string Status  { get; init; } = string.Empty;

    [SwaggerSchema(Description = "사용자 안내 메시지")]
    public string Message { get; init; } = string.Empty;

    [SwaggerSchema(Description = "추가 검토 필요 항목 목록. 확정 판정이 아닙니다.")]
    public List<string> Caution { get; init; } = new();
}

/// <summary>중첩 레이어 판정 결과 DTO</summary>
[SwaggerSchema(Description = "특정 공간 레이어(개발제한구역 등)와의 중첩 판정 결과")]
public sealed class OverlayZoneResultDto
{
    [SwaggerSchema(Description = "해당 구역 내부에 포함되면 true")]
    public bool    IsInside { get; init; }

    [SwaggerSchema(Description = "발견된 구역명. 미포함 또는 데이터 없음 시 null.")]
    public string? Name     { get; init; }

    [SwaggerSchema(Description = "발견된 구역 코드 (예: UDV100). 미포함 시 null.")]
    public string? Code     { get; init; }

    /// <summary>표준화된 데이터 출처: "shp" | "none"</summary>
    [SwaggerSchema(Description = "데이터 출처: 'shp' = 로컬 SHP, 'none' = 데이터 없음")]
    public string  Source   { get; init; } = "shp";

    [SwaggerSchema(Description = "추가 안내 메시지. 해당 없으면 null.")]
    public string? Note       { get; init; }

    /// <summary>
    /// [표준] 신뢰도: "high" | "medium" | "low".
    /// </summary>
    [SwaggerSchema(Description = "[표준] 신뢰도: high(직접 매칭) | medium(경계 근접) | low(데이터 없음)")]
    public string  Confidence { get; init; } = "low";

    /// <summary>
    /// [Deprecated] 내부 열거형 이름 원값: Normal | NearBoundary | DataUnavailable.
    /// confidence 필드로 이전 예정.
    /// </summary>
    [SwaggerSchema(Description = "[Deprecated] 내부 열거형 원값. → confidence 사용 권장.")]
    public string  ConfidenceRaw { get; init; } = string.Empty;

    /// <summary>[Deprecated] ConfidenceLevel → confidence 로 이전 예정. 동일 값.</summary>
    [SwaggerSchema(Description = "[Deprecated] → confidence 사용 권장. 동일 값.")]
    public string  ConfidenceLevel { get; init; } = "low";
}

/// <summary>
/// 개발제한구역 판정 결과 DTO (표준화 스키마).
/// <para>
/// source: "api" = VWorld API 확인, "shp" = SHP 로컬 fallback, "none" = 데이터 없음<br/>
/// status: "confirmed" = API 성공, "fallback" = API 실패·SHP 사용, "unavailable" = 둘 다 없음
/// </para>
/// </summary>
[SwaggerSchema(Description = "개발제한구역 판정 결과. source/status로 데이터 출처와 신뢰도를 구분합니다.")]
public sealed class DevRestrictionDto
{
    [SwaggerSchema(Description = "개발제한구역 포함 여부. unavailable 상태에서는 null.")]
    public bool?  IsInside { get; init; }

    [SwaggerSchema(Description = "데이터 출처: api(VWorld) | shp(로컬 SHP) | none(없음)")]
    public string Source   { get; init; } = "none";

    [SwaggerSchema(Description = "판정 상태: confirmed(API 확인) | fallback(SHP 대체) | unavailable(판정 불가)")]
    public string Status   { get; init; } = "unavailable";

    [SwaggerSchema(Description = "판정 근거 메시지 (디버깅/감사용)")]
    public string? Note    { get; init; }

    /// <summary>
    /// [표준] 신뢰도: "high" | "medium" | "low".
    /// ConfidenceLevel과 동일한 값이며 표준 필드명.
    /// </summary>
    [SwaggerSchema(Description = "[표준] 신뢰도: high(API 확인) | medium(SHP fallback) | low(미연동/불가)")]
    public string Confidence { get; init; } = "low";

    /// <summary>[Deprecated] ConfidenceLevel → Confidence 로 이전 예정. 동일 값.</summary>
    [SwaggerSchema(Description = "[Deprecated] → confidence 사용 권장. 동일 값.")]
    public string ConfidenceLevel { get; init; } = "low";
}

/// <summary>추가 레이어 정보 DTO</summary>
[SwaggerSchema(Description = "용도지역 외 추가 공간 레이어 중첩 판정 결과")]
public sealed class ExtraLayersDto
{
    [SwaggerSchema(Description = "개발제한구역(그린벨트) 판정 결과. source/status로 출처 구분.")]
    public DevRestrictionDto?   DevelopmentRestriction { get; init; }

    [SwaggerSchema(Description = "개발행위허가제한지역 중첩 여부 (UQ171 UQQ900 실데이터, 국토계획법 제63조)")]
    public OverlayZoneResultDto? DevelopmentActionRestriction { get; init; }

    [SwaggerSchema(Description = "지구단위계획구역 판정 결과. source/status로 출처와 신뢰도를 구분합니다 (VWorld LT_C_UPISUQ161 API).")]
    public DevRestrictionDto? DistrictUnitPlan { get; init; }
}

/// <summary>용도지역 건축 법규 참고 정보 DTO</summary>
[SwaggerSchema(Description = "용도지역별 건축 법규 참고 정보. 국토계획법 기준 참고값이며 확정 수치가 아닙니다.")]
public sealed class RegulationInfoDto
{
    [SwaggerSchema(Description = "건폐율 법정 상한 참고값 (예: '60% 이하'). 조례로 달라질 수 있음.")]
    public string BuildingCoverageRatioRef { get; init; } = string.Empty;

    [SwaggerSchema(Description = "용적률 법정 상한 참고값 범위 (예: '150~250% (조례)'). 조례로 달라질 수 있음.")]
    public string FloorAreaRatioRef        { get; init; } = string.Empty;

    [SwaggerSchema(Description = "허용 용도 카테고리 요약 (참고용). 예: '단독주택·저밀 공동주택 중심'")]
    public string UseCategorySummary       { get; init; } = string.Empty;

    [SwaggerSchema(Description = "주요 제한 및 주의사항 목록 (ZoningRuleTable 기반)")]
    public List<string> Restrictions       { get; init; } = new();

    [SwaggerSchema(Description = "추가 검토 필요 주의사항 목록 (오버레이 판정 결과 포함)")]
    public List<string> CautionNotes       { get; init; } = new();

    [SwaggerSchema(Description = "추가 검토 필요 여부. Preliminary 판정은 항상 true.")]
    public bool NeedsAdditionalReview      { get; init; }
}

/// <summary>
/// 지도 마커 표시용 오버레이 플래그 요약 DTO.
/// <para>
/// 각 필드는 <c>bool?</c>입니다:
/// <c>true</c> = 해당 구역 내 확인, <c>false</c> = 해당 없음 확인, <c>null</c> = 데이터 미확인(DataUnavailable).
/// </para>
/// </summary>
[SwaggerSchema(Description = "오버레이 구역 포함 여부 compact 요약. null = 데이터 미확인(DataUnavailable).")]
public sealed class OverlayFlagsDto
{
    [SwaggerSchema(Description = "개발제한구역(그린벨트) 포함 여부. null = 데이터 미확인.")]
    public bool? IsDevelopmentRestricted { get; init; }

    [SwaggerSchema(Description = "개발행위허가제한지역(국토계획법 제63조) 포함 여부. null = 데이터 미확인.")]
    public bool? IsDevelopmentActionRestricted { get; init; }

    [SwaggerSchema(Description = "지구단위계획구역 포함 여부. null = 데이터 미확인.")]
    public bool? IsInDistrictUnitPlan { get; init; }

    /// <summary>
    /// 하나라도 명확히 true인 오버레이가 있으면 true.
    /// 마커 색상(주의/일반) 결정에 사용합니다.
    /// DataUnavailable 항목은 보수적으로 false로 처리합니다.
    /// </summary>
    [SwaggerSchema(Description = "하나 이상의 오버레이가 명확히 포함되면 true. 마커 색상 결정용.")]
    public bool HasAnyRestriction { get; init; }
}

/// <summary>법령 참조 응답 DTO</summary>
[SwaggerSchema(Description = "관련 법령 참조 정보. 현재 stub이므로 빈 배열.")]
public sealed class LawReferenceDto
{
    public string  LawName    { get; init; } = string.Empty;
    public string  ArticleRef { get; init; } = string.Empty;
    public string? Url        { get; init; }
    public string? Note       { get; init; }
}

/// <summary>진단용 디버그 정보 DTO</summary>
[SwaggerSchema(Description = "검토 실패 시 또는 결과 분석용 내부 진단 정보")]
public sealed class DebugInfoDto
{
    [SwaggerSchema(Description = "판정 실패 또는 결과 요약 사유 (내부용)")]
    public string? Reason { get; init; }

    [SwaggerSchema(Description = "매칭된 피처의 원본 속성 수")]
    public int? AttributeCount { get; init; }

    [SwaggerSchema(Description = "탐색 대상 전체 피처 수")]
    public int? TotalFeaturesSearched { get; init; }

    [SwaggerSchema(Description = "가장 가까운 피처까지의 거리 (m). NotFound 시에만 제공.")]
    public double? NearestDistanceMeters { get; init; }

    [SwaggerSchema(Description = "규제 검토 서비스 처리 시간 (ms). 성능 모니터링용.")]
    public long? ResponseTimeMs { get; init; }
}

/// <summary>
/// 데이터 출처 및 신뢰도 통합 요약 DTO.
/// <para>
/// 각 레이어의 source("api"/"shp"/"rule"/"calculated"/"none")와
/// 신뢰도("high"/"medium"/"low")를 한 곳에서 확인할 수 있습니다.
/// </para>
/// </summary>
[SwaggerSchema(Description = "응답 전체의 데이터 출처 및 신뢰도 통합 요약")]
public sealed class DataTrustDto
{
    [SwaggerSchema(Description = "용도지역 데이터 출처. 항상 'shp' (로컬 SHP 파일).")]
    public string ZoningSource { get; init; } = "shp";

    [SwaggerSchema(Description = "용도지역 신뢰도: high(직접 매칭) | medium(경계 근접) | low(미발견)")]
    public string ZoningConfidence { get; init; } = "low";

    [SwaggerSchema(Description = "개발제한구역 신뢰도: high(VWorld API) | medium(SHP fallback) | low(미연동)")]
    public string DevRestrictionConfidence { get; init; } = "low";

    [SwaggerSchema(Description = "지구단위계획 신뢰도: high(API 확인) | medium(SHP fallback) | low(미연동)")]
    public string DistrictUnitPlanConfidence { get; init; } = "low";

    [SwaggerSchema(Description = "전체 응답 종합 신뢰도. 개별 레이어 중 최저 신뢰도 기준.")]
    public string OverallConfidence { get; init; } = "low";

    [SwaggerSchema(Description = "종합 신뢰도 한줄 안내 메시지")]
    public string OverallNote { get; init; } = string.Empty;
}

/// <summary>공간 레이어 메타데이터 응답 DTO</summary>
[SwaggerSchema(Description = "원천 데이터 추적 정보 (레이어명, 로드 시각, 피처 수, 좌표계)")]
public sealed class LayerMetaDto
{
    [SwaggerSchema(Description = "로드된 공간 데이터 레이어 식별자")]
    public string LayerName            { get; init; } = string.Empty;

    [SwaggerSchema(Description = "데이터 로드 시각 (UTC)")]
    public DateTimeOffset LoadedAt     { get; init; }

    [SwaggerSchema(Description = "로드된 전체 폴리곤 피처 수")]
    public int FeatureCount            { get; init; }

    [SwaggerSchema(Description = "원천 데이터 좌표계 및 변환 방식 안내")]
    public string CoordinateSystemNote { get; init; } = string.Empty;
}

#endregion

#region RegulationCheckResponseDto 클래스

/// <summary>
/// 법규 검토 API 공통 응답 DTO입니다.
/// <para>
/// 모든 결과는 <b>참고용 1차 판정</b>이며 확정 건축 허가 판단의 근거로 사용해서는 안 됩니다.
/// </para>
/// </summary>
[SwaggerSchema(Description = "법규 1차 검토 응답. 모든 결과는 참고용이며 확정 판단의 근거가 아닙니다.")]
public sealed class RegulationCheckResponseDto
{
    #region 시군구 코드 → 시도명 매핑

    private static readonly Dictionary<string, string> SidoCodeMap = new()
    {
        { "11", "서울특별시" }, { "26", "부산광역시" }, { "27", "대구광역시" },
        { "28", "인천광역시" }, { "29", "광주광역시" }, { "30", "대전광역시" },
        { "31", "울산광역시" }, { "36", "세종특별자치시" }, { "41", "경기도" },
        { "42", "강원특별자치도" }, { "43", "충청북도" }, { "44", "충청남도" },
        { "45", "전북특별자치도" }, { "46", "전라남도" }, { "47", "경상북도" },
        { "48", "경상남도" }, { "50", "제주특별자치도" },
        { "51", "강원특별자치도" }, { "52", "전북특별자치도" },
    };

    #endregion

    #region 프로퍼티

    /// <summary>요청 입력값 (WGS84 경위도)</summary>
    public InputDto              Input             { get; init; } = new();

    /// <summary>프론트엔드 호환용 좌표 객체 (Input과 동일)</summary>
    [SwaggerSchema(Description = "프론트엔드 호환용 {lat, lon} 객체")]
    public object Coordinate => new { lat = Input.Latitude, lon = Input.Longitude };

    /// <summary>
    /// 지도 마커 팝업용 대표 표시 이름.
    /// 용도지역이 확인되면 zoneName (예: "제2종일반주거지역"),
    /// 미확인이면 null입니다.
    /// </summary>
    [SwaggerSchema(Description = "지도 마커 팝업용 대표 이름. 용도지역 미확인 시 null.")]
    public string?               DisplayName       { get; init; }

    /// <summary>
    /// 오버레이 구역 포함 여부 compact 요약.
    /// 지도 마커 색상(주의 여부) 결정에 사용합니다.
    /// </summary>
    [SwaggerSchema(Description = "오버레이 포함 여부 compact 요약. 마커 색상 결정용.")]
    public OverlayFlagsDto       OverlayFlags      { get; init; } = new();

    /// <summary>판정된 용도지역 정보 (없으면 null)</summary>
    public ZoningDto?            Zoning            { get; init; }

    /// <summary>매칭된 피처의 원본 속성 문자열 (디버깅용)</summary>
    [SwaggerSchema(Description = "매칭된 피처의 원본 속성 문자열. NotFound 시 null.")]
    public string?               ZoningRaw         { get; init; }

    /// <summary>1차 판정 요약 (상태 + 안내 메시지 + 추가 검토 항목)</summary>
    public RegulationSummaryDto  RegulationSummary { get; init; } = new();

    /// <summary>추가 레이어 중첩 판정 (지구단위계획, 개발제한구역)</summary>
    public ExtraLayersDto        ExtraLayers       { get; init; } = new();

    /// <summary>
    /// 용도지역별 건축 법규 참고 정보 (국토계획법 기준, 참고용).
    /// 확정 수치가 아니며 조례·지구단위계획 추가 확인 필요.
    /// 용도지역 미발견 시 null.
    /// </summary>
    public RegulationInfoDto?    RegulationInfo    { get; init; }

    /// <summary>관련 법령 참조 (현재 stub — 빈 배열)</summary>
    public List<LawReferenceDto> LawReferences     { get; init; } = new();

    /// <summary>추가 확인이 필요한 관련 규제/절차 목록</summary>
    [SwaggerSchema(Description = "해당 토지에서 확인이 필요한 추가 규제 및 절차 리스트")]
    public List<string>          RelatedRegulations { get; init; } = new();

    /// <summary>
    /// 규칙 기반 자동 생성 요약 텍스트.
    /// 용도지역명, 건폐율/용적률 참고값, 오버레이 판정, 관련 법령을 조합해 생성됩니다.
    /// LLM 없이 규칙 기반으로 생성되며 확정 판단의 근거가 아닙니다.
    /// </summary>
    public string                SummaryText       { get; init; } = string.Empty;

    /// <summary>원천 데이터 메타데이터 (레이어명, 로드 시각, 피처 수)</summary>
    public LayerMetaDto          Meta              { get; init; } = new();

    /// <summary>진단용 내부 정보 (개발 단계 노출)</summary>
    public DebugInfoDto          Debug             { get; init; } = new();

    /// <summary>데이터 출처 및 신뢰도 통합 요약</summary>
    [SwaggerSchema(Description = "각 레이어의 데이터 출처(source)와 신뢰도(confidence) 통합 요약")]
    public DataTrustDto          DataTrust         { get; init; } = new();

    #endregion

    #region MapFrom 정적 팩토리

    /// <summary>
    /// 도메인 모델 <see cref="RegulationCheckResult"/>를 응답 DTO로 변환합니다.
    /// </summary>
    /// <param name="r">도메인 결과</param>
    /// <param name="responseTimeMs">서비스 처리 시간 (ms). 측정한 경우 전달.</param>
    public static RegulationCheckResponseDto MapFrom(RegulationCheckResult r, long? responseTimeMs = null)
    {
        var isPreliminary = r.RegulationSummary.Status == RegulationStatus.Preliminary;
        var cautions      = isPreliminary
            ? RegulationCautionBuilder.BuildPreliminary(r)
            : RegulationCautionBuilder.BuildNotFound().ToList();

        var darFlag  = OverlayFlag(r.ExtraLayers.DevelopmentRestriction);
        var daarFlag = OverlayFlag(r.ExtraLayers.DevelopmentActionRestriction);
        var dupFlag  = OverlayFlag(r.ExtraLayers.DistrictUnitPlan);

        // 신뢰도 계산 (DataTrust 구성용)
        var drDto  = MapDevRestriction(r.ExtraLayers.DevelopmentRestriction);
        var dupDto = MapDevRestriction(r.ExtraLayers.DistrictUnitPlan);

        var zoningConf = r.Zoning is null ? "low"
            : (r.NearestDistance ?? 0) > 0.1 ? "medium"
            : "high";
        var overall = WorstConfidence(zoningConf, drDto.ConfidenceLevel, dupDto.ConfidenceLevel);

        return new RegulationCheckResponseDto
        {
            Input = new InputDto
            {
                Longitude = r.Input.Longitude,
                Latitude  = r.Input.Latitude
            },

            DisplayName = r.Zoning?.Name,

            OverlayFlags = new OverlayFlagsDto
            {
                IsDevelopmentRestricted       = darFlag,
                IsDevelopmentActionRestricted = daarFlag,
                IsInDistrictUnitPlan          = dupFlag,
                HasAnyRestriction             = darFlag == true || daarFlag == true || dupFlag == true
            },

            Zoning = r.Zoning is null ? null : new ZoningDto
            {
                ZoneName    = r.Zoning.Name,
                ZoneCode    = r.Zoning.Code,
                AdminArea   = ExtractAdminArea(r.Zoning.Attributes),
                SourceLayer = r.Zoning.SourceLayer,
                Source      = "shp",
                Confidence  = zoningConf,
                Attributes  = new Dictionary<string, object?>(r.Zoning.Attributes)
            },

            RegulationSummary = new RegulationSummaryDto
            {
                Status  = r.RegulationSummary.Status.ToString(),
                Message = r.RegulationSummary.Message,
                Caution = cautions
            },

            ExtraLayers = new ExtraLayersDto
            {
                DevelopmentRestriction       = drDto,
                DevelopmentActionRestriction = MapOverlay(r.ExtraLayers.DevelopmentActionRestriction),
                DistrictUnitPlan             = dupDto,
            },

            DataTrust = new DataTrustDto
            {
                ZoningSource               = "shp",
                ZoningConfidence           = zoningConf,
                DevRestrictionConfidence   = drDto.ConfidenceLevel,
                DistrictUnitPlanConfidence = dupDto.ConfidenceLevel,
                OverallConfidence          = overall,
                OverallNote                = OverallConfidenceNote(overall, dupDto.ConfidenceLevel),
            },

            RegulationInfo = r.RegulationInfo is null ? null : new RegulationInfoDto
            {
                BuildingCoverageRatioRef = r.RegulationInfo.BuildingCoverageRatioRef,
                FloorAreaRatioRef        = r.RegulationInfo.FloorAreaRatioRef,
                UseCategorySummary       = r.RegulationInfo.AllowedUseSummary,
                Restrictions             = r.RegulationInfo.Restrictions,
                CautionNotes             = cautions,
                NeedsAdditionalReview    = isPreliminary
            },

            SummaryText = SummaryTextBuilder.Build(r),

            LawReferences = r.LawReferences
                .Select(l => new LawReferenceDto
                {
                    LawName    = l.LawName,
                    ArticleRef = l.ArticleRef,
                    Url        = l.Url,
                    Note       = l.Note
                })
                .ToList(),

            RelatedRegulations = BuildRelatedRegulations(r),

            ZoningRaw = r.ZoningRaw,

            Meta = new LayerMetaDto
            {
                LayerName            = r.LayerMeta.LayerName,
                LoadedAt             = r.LayerMeta.LoadedAt,
                FeatureCount         = r.LayerMeta.FeatureCount,
                CoordinateSystemNote = r.LayerMeta.CoordinateSystemNote
            },

            Debug = new DebugInfoDto
            {
                Reason                = r.DebugReason,
                AttributeCount        = r.Zoning?.Attributes.Count,
                TotalFeaturesSearched = r.LayerMeta.FeatureCount,
                NearestDistanceMeters = r.NearestDistance,
                ResponseTimeMs        = responseTimeMs,
            }
        };
    }

    #endregion

    #region 유틸리티

    private static List<string> BuildRelatedRegulations(RegulationCheckResult r)
    {
        var list = new List<string>();

        // ── 1. 용도 및 밀도 검토 ──────────────────────────────────────────
        if (r.Zoning is not null)
        {
            var zoneName = r.Zoning.Name;
            if (zoneName.Contains("주거"))
                list.Add("[용도] 주거계 용도지역 내 허용 건축물 종류 확인");
            else if (zoneName.Contains("상업"))
                list.Add("[용도] 상업계 용도지역 내 허용 건축물 및 주상복합 비율 확인");
            else if (zoneName.Contains("공업"))
                list.Add("[용도] 공업계 용도지역 내 허용 건축물 및 업종 제한 확인");
            else if (zoneName.Contains("녹지"))
                list.Add("[용도] 녹지계 용도지역 내 건축 가능 규모 및 용도 제약 확인");
            else
                list.Add("[용도] 해당 용도지역 내 건축 가능 용도 확인");

            list.Add("[밀도] 지자체 조례에 따른 건폐율·용적률 상한 및 인센티브 확인");
        }
        else
        {
            list.Add("[미확인] 용도지역 미지정: 관할 시군구청을 통한 관리지역 세분화 여부 확인 필수");
        }

        // ── 2. 중첩 규제 검토 ────────────────────────────────────────────
        if (r.ExtraLayers.DevelopmentRestriction.IsInside)
            list.Add("[제한] 개발제한구역 행위허가 및 예외 가능 여부 우선 검토 (개특법)");
        
        if (r.ExtraLayers.DevelopmentActionRestriction.IsInside)
            list.Add("[제한] 개발행위허가 제한 내용 및 기간 확인 (국토계획법 제63조)");

        if (r.ExtraLayers.DistrictUnitPlan.IsInside)
            list.Add("[계획] 지구단위계획 결정도 및 시행지침 상세 확인 (건축선, 형태 제한 등)");
        else if (r.ExtraLayers.DistrictUnitPlan.Confidence == OverlayConfidenceLevel.DataUnavailable)
            list.Add("[주의] 지구단위계획구역 여부 별도 확인 권장 (토지이음)");

        // ── 3. 공통 인허가 검토 ────────────────────────────────────────────
        list.Add("[공통] 도로 접면 조건 및 건축선 후퇴(setback)에 따른 가용 면적 검토");
        list.Add("[공통] 대지의 조경, 공개공지, 주차장법 등 개별 법령에 따른 추가 면적 검토");

        return list.Distinct().ToList();
    }

    /// <summary>
    /// OverlayZoneResult를 지도 표시용 bool?로 변환합니다.
    /// DataUnavailable이거나 null이면 null(미확인), 그 외에는 IsInside 값을 반환합니다.
    /// </summary>
    private static bool? OverlayFlag(OverlayZoneResult? overlay) =>
        overlay is null || overlay.Confidence == OverlayConfidenceLevel.DataUnavailable
            ? null
            : overlay.IsInside;

    /// <summary>
    /// 개발제한구역 전용 매핑: Source 태그("api"/"shp"/"none")를 읽어 표준 스키마로 변환합니다.
    /// </summary>
    private static DevRestrictionDto MapDevRestriction(OverlayZoneResult? overlay)
    {
        if (overlay is null)
            return new DevRestrictionDto { IsInside = null, Source = "none", Status = "unavailable" };

        var source = overlay.Source switch
        {
            "api"  => "api",
            "shp"  => "shp",
            "none" => "none",
            _      => "shp"   // 기존 SHP 파일명 등 레거시 값 → shp 처리
        };

        var isUnavailable = source == "none" ||
                            overlay.Confidence == OverlayConfidenceLevel.DataUnavailable;

        var status = source switch
        {
            "api"  => "confirmed",
            "shp"  => isUnavailable ? "unavailable" : "fallback",
            _      => "unavailable"
        };

        var confidence = status switch
        {
            "confirmed" => "high",
            "fallback"  => "medium",
            _           => "low",
        };

        return new DevRestrictionDto
        {
            IsInside        = isUnavailable ? null : overlay.IsInside,
            Source          = source,
            Status          = status,
            Note            = overlay.Note,
            Confidence      = confidence,      // [표준]
            ConfidenceLevel = confidence,      // [Deprecated] 동일 값 유지
        };
    }

    private static string WorstConfidence(params string[] levels)
    {
        if (levels.Contains("low"))    return "low";
        if (levels.Contains("medium")) return "medium";
        return "high";
    }

    private static string OverallConfidenceNote(string overall, string dupConfidence)
    {
        if (dupConfidence == "low")
            return "지구단위계획 데이터 미연동 — 해당 여부는 토지이음에서 별도 확인하세요.";
        return overall switch
        {
            "high"   => "모든 공간 데이터 직접 매칭됨 (법적 확정 아님)",
            "medium" => "일부 데이터 SHP fallback 또는 경계 근접 — 정밀 확인 권장",
            _        => "일부 레이어 미연동 — 참고용으로만 사용하세요",
        };
    }

    private static OverlayZoneResultDto? MapOverlay(OverlayZoneResult? overlay)
    {
        if (overlay is null) return null;

        var normalizedSource = overlay.Source switch
        {
            "api"  => "api",
            "shp"  => "shp",
            "none" => "none",
            _      => "shp"   // SHP 파일명 등 레거시 값 → "shp"
        };

        var confLevel = overlay.Confidence switch
        {
            OverlayConfidenceLevel.Normal        => "high",
            OverlayConfidenceLevel.NearBoundary  => "medium",
            _                                    => "low",
        };

        return new OverlayZoneResultDto
        {
            IsInside        = overlay.IsInside,
            Name            = overlay.Name,
            Code            = overlay.Code,
            Source          = normalizedSource,
            Note            = overlay.Note,
            Confidence      = confLevel,                         // [표준] "high"/"medium"/"low"
            ConfidenceRaw   = overlay.Confidence.ToString(),    // [Deprecated] 원래 열거형 이름
            ConfidenceLevel = confLevel,                         // [Deprecated] 동일 값 유지
        };
    }

    private static AdminAreaDto? ExtractAdminArea(IReadOnlyDictionary<string, object?> attributes)
    {
        string? code = null;
        foreach (var key in new[] { "SIGNGU_SE", "signgu_se", "sgg_cd", "SGG_CD" })
        {
            if (attributes.TryGetValue(key, out var val))
            {
                var str = val?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(str)) { code = str; break; }
            }
        }
        if (code is null) return null;

        var prefix = code.Length >= 2 ? code[..2] : code;
        SidoCodeMap.TryGetValue(prefix, out var sidoName);
        return new AdminAreaDto { Code = code, Name = sidoName };
    }

    #endregion
}

#endregion
