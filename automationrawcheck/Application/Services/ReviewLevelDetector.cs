// =============================================================================
// ReviewLevelDetector.cs
// 검토 단계(ReviewLevel) 판별 + 규칙 필터링 헬퍼
//
// [ReviewLevel 정의]
//   Quick    (0) : 주소 + 계획 용도만 — 허용용도 1차 확인
//   Standard (1) : + 연면적/층수/대지면적 — 밀도/피난/방화 판정
//   Detailed (2) : + 용도별 상세 입력 — 주차/업종/승강기 정밀 판정
//   Expert   (3) : + 지구단위계획 서류 — 전문 검토
//
// [ruleId → 최소 표시 단계 매핑]
//   _ruleLevelMap 정적 딕셔너리에서 관리합니다.
//   미등록 ruleId는 Quick으로 처리(항상 표시).
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Rules;

namespace AutomationRawCheck.Application.Services;

/// <summary>검토 단계 열거형</summary>
public enum ReviewLevel
{
    Quick    = 0,
    Standard = 1,
    Detailed = 2,
    Expert   = 3,
}

/// <summary>
/// ReviewLevel 판별 + 규칙 필터링 정적 헬퍼입니다.
/// </summary>
public static class ReviewLevelDetector
{
    // ── ruleId → 최소 ReviewLevel 매핑 ───────────────────────────────────────
    // 이 단계 이상일 때만 해당 규칙이 응답에 포함됩니다.

    private static readonly Dictionary<string, ReviewLevel> _ruleLevelMap =
        new(StringComparer.Ordinal)
        {
            // ── 공동주택 ──────────────────────────────────────────────────────
            ["RI-APT-001"] = ReviewLevel.Quick,       // 허용용도
            ["RI-APT-002"] = ReviewLevel.Standard,    // 밀도
            ["RI-APT-003"] = ReviewLevel.Standard,    // 도로/건축선
            ["RI-APT-004"] = ReviewLevel.Detailed,    // 주차 (세대수 기준)
            ["RI-APT-005"] = ReviewLevel.Standard,    // 피난/계단
            ["RI-APT-006"] = ReviewLevel.Standard,    // 승강기

            // ── 제1종근린생활시설 ──────────────────────────────────────────────
            ["RI-NH1-001"] = ReviewLevel.Quick,
            ["RI-NH1-002"] = ReviewLevel.Standard,
            ["RI-NH1-003"] = ReviewLevel.Standard,
            ["RI-NH1-004"] = ReviewLevel.Detailed,    // 주차 (연면적 기준)
            ["RI-NH1-005"] = ReviewLevel.Standard,
            ["RI-NH1-006"] = ReviewLevel.Detailed,    // 방화

            // ── 제2종근린생활시설 ──────────────────────────────────────────────
            ["RI-NH2-001"] = ReviewLevel.Quick,
            ["RI-NH2-002"] = ReviewLevel.Standard,
            ["RI-NH2-003"] = ReviewLevel.Standard,
            ["RI-NH2-004"] = ReviewLevel.Detailed,
            ["RI-NH2-005"] = ReviewLevel.Standard,
            ["RI-NH2-006"] = ReviewLevel.Detailed,

            // ── 업무시설 ──────────────────────────────────────────────────────
            ["RI-OFF-001"] = ReviewLevel.Quick,
            ["RI-OFF-002"] = ReviewLevel.Standard,
            ["RI-OFF-003"] = ReviewLevel.Standard,
            ["RI-OFF-004"] = ReviewLevel.Standard,    // 피난계단
            ["RI-OFF-005"] = ReviewLevel.Standard,    // 승강기
            ["RI-OFF-006"] = ReviewLevel.Detailed,    // 주차/교통영향평가
            ["RI-OFF-007"] = ReviewLevel.Detailed,    // 방화

            // ── 오버레이 공통 ─────────────────────────────────────────────────
            ["RI-OVL-DUP-001"] = ReviewLevel.Quick,
            ["RI-OVL-DAR-001"] = ReviewLevel.Quick,
        };

    // ── 공개 API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 문자열 → ReviewLevel 변환.
    /// 인식할 수 없는 값이면 null을 반환합니다.
    /// </summary>
    public static ReviewLevel? Parse(string? levelStr) =>
        levelStr?.ToLowerInvariant() switch
        {
            "quick"    => ReviewLevel.Quick,
            "standard" => ReviewLevel.Standard,
            "detailed" => ReviewLevel.Detailed,
            "expert"   => ReviewLevel.Expert,
            _          => null,
        };

    /// <summary>
    /// buildingInputs의 제공 필드 기반으로 ReviewLevel을 자동 추론합니다.
    /// </summary>
    public static ReviewLevel Detect(BuildingInputsDto? inp)
    {
        if (inp is null) return ReviewLevel.Quick;

        bool hasExpert = inp.HasDistrictUnitPlanDocument.HasValue
                      || inp.HasDevActRestrictionConsult.HasValue;

        bool hasDetailed = inp.UnitCount.HasValue
                         || inp.RoomCount.HasValue
                         || inp.GuestRoomCount.HasValue
                         || inp.BedCount.HasValue
                         || inp.StudentCount.HasValue
                         || inp.DetailUseSubtype is not null
                         || inp.OfficeSubtype is not null
                         || inp.VehicleIngressType is not null
                         || inp.OccupantCount.HasValue
                         || inp.MixedUseRatio.HasValue
                         || inp.UnitArea.HasValue
                         || inp.DetailUseFloorArea.HasValue
                         || inp.MedicalSpecialCriteria is not null
                         || inp.EducationSpecialCriteria is not null
                         || inp.HazardousMaterialProfile is not null
                         || inp.LogisticsOperationProfile is not null
                         || inp.AccommodationSpecialCriteria is not null;

        bool hasStandard = inp.SiteArea.HasValue
                         || inp.BuildingArea.HasValue
                         || inp.FloorArea.HasValue
                         || inp.FloorCount.HasValue
                         || inp.BuildingHeight.HasValue
                        || inp.RoadFrontageWidth.HasValue;

        if (hasExpert)   return ReviewLevel.Expert;
        if (hasDetailed) return ReviewLevel.Detailed;
        if (hasStandard) return ReviewLevel.Standard;
        return ReviewLevel.Quick;
    }

    /// <summary>
    /// 규칙 목록을 reviewLevel 이상인 항목만 필터링하여 반환합니다.
    /// _ruleLevelMap에 미등록된 ruleId는 항상 포함됩니다.
    /// </summary>
    public static List<ReviewItemRuleRecord> FilterByLevel(
        IEnumerable<ReviewItemRuleRecord> rules, ReviewLevel level)
    {
        return rules
            .Where(r => GetMinLevel(r.Id) <= level)
            .ToList();
    }

    /// <summary>
    /// 특정 ruleId의 최소 표시 단계를 반환합니다.
    /// 미등록이면 Quick(0)을 반환합니다.
    /// </summary>
    public static ReviewLevel GetMinLevel(string ruleId) =>
        _ruleLevelMap.TryGetValue(ruleId, out var level) ? level : ReviewLevel.Quick;

    /// <summary>ReviewLevel → 소문자 문자열 변환.</summary>
    public static string LevelToString(ReviewLevel level) => level switch
    {
        ReviewLevel.Quick    => "quick",
        ReviewLevel.Standard => "standard",
        ReviewLevel.Detailed => "detailed",
        ReviewLevel.Expert   => "expert",
        _                    => "quick",
    };
}
