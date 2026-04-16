// =============================================================================
// BuildingReviewJudgeService.cs
// 검토 항목별 판정 엔진 (POST /review 전용)
//
// [판정 상태 정의]
//   active    : 입력값 기반 계산 판정 완료 (수치 비교 결과 포함)
//   reference : 용도지역 법정 기준치 제시 (건물 입력 부족)
//   pending   : 판정에 필요한 입력값 없음 (기준치도 표시 불가)
//
// [구현 범위]
//   공동주택    : RI-APT-001~006
//   제1종근린  : RI-NH1-001~006
//   제2종근린  : RI-NH2-001~006
//   업무시설   : RI-OFF-001~007
//   오버레이   : RI-OVL-DUP-001, RI-OVL-DAR-001
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Rules;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// 검토 항목 규칙 레코드에 대해 입력값 기반 판정 상태와 판정 메모를 계산합니다.
/// </summary>
public static class BuildingReviewJudgeService
{
    // ── 공개 진입점 ───────────────────────────────────────────────────────────

    /// <summary>
    /// 규칙 레코드에 대한 판정 상태(status)와 판정 메모(note)를 반환합니다.
    /// </summary>
    /// <param name="rule">검토 항목 규칙 레코드</param>
    /// <param name="zoneName">판정된 용도지역명 (null = 미판정)</param>
    /// <param name="limits">법정 건폐율·용적률 상한 (null = 해당 용도지역 기준 없음)</param>
    /// <param name="inp">건축 규모 입력 (null = 미입력)</param>
    /// <returns>(status, note) 튜플</returns>
    public static (string Status, string? Note) Judge(
        ReviewItemRuleRecord          rule,
        string?                       zoneName,
        (double Bcr, double Far)?     limits,
        BuildingInputsDto?            inp)
    {
        return rule.Id switch
        {
            // ── 공동주택 ──────────────────────────────────────────────────────
            "RI-APT-001" => JudgeApt001(zoneName, inp),
            "RI-APT-002" => JudgeDensity(zoneName, limits, inp),
            "RI-APT-003" => JudgeRoadFrontage(inp, extraNote: "건축선 지정 여부 및 세대수 접도 기준 추가 확인 필요."),
            "RI-APT-004" => JudgeAptParking(inp),
            "RI-APT-005" => JudgeEvacStairs(inp),
            "RI-APT-006" => JudgeElevator(inp, includeOccupant: false),

            // ── 제1종근린생활시설 ──────────────────────────────────────────────
            "RI-NH1-001" => JudgeNh1AllowedUse(zoneName, inp),
            "RI-NH1-002" => JudgeDensity(zoneName, limits, inp),
            "RI-NH1-003" => JudgeRoadFrontage(inp, extraNote: "차량 진출입 방향·건축선 지정 여부 추가 확인 필요."),
            "RI-NH1-004" => JudgeNhParking(zoneName, inp),
            "RI-NH1-005" => JudgeEvacStairs(inp),
            "RI-NH1-006" => JudgeFireCompartment(inp, checkMixedUse: false),

            // ── 제2종근린생활시설 ──────────────────────────────────────────────
            "RI-NH2-001" => JudgeNh2AllowedUse(zoneName, inp),
            "RI-NH2-002" => JudgeDensity(zoneName, limits, inp),
            "RI-NH2-003" => JudgeRoadFrontage(inp, extraNote: "다중이용시설 진출입 동선·건축선 지정 여부 추가 확인 필요."),
            "RI-NH2-004" => JudgeNhParking(zoneName, inp),
            "RI-NH2-005" => JudgeNh2EvacStairs(inp),
            "RI-NH2-006" => JudgeNh2FireCompartment(inp),

            // ── 업무시설 ──────────────────────────────────────────────────────
            "RI-OFF-001" => JudgeOff001(zoneName, inp),
            "RI-OFF-002" => JudgeDensity(zoneName, limits, inp),
            "RI-OFF-003" => JudgeOffRoad(inp),
            "RI-OFF-004" => JudgeEvacStairs(inp),
            "RI-OFF-005" => JudgeElevator(inp, includeOccupant: true),
            "RI-OFF-006" => JudgeOffParking(zoneName, inp),
            "RI-OFF-007" => JudgeFireCompartment(inp, checkMixedUse: true),

            // ── 오버레이 공통 ─────────────────────────────────────────────────
            "RI-OVL-DUP-001" => JudgeOverlayDup(inp),
            "RI-OVL-DAR-001" => ("reference", "개발제한구역 내 개발행위 허가 선결 필요 ⚠️ — 관할 지자체 건축 담당 확인."),

            // 미등록 규칙 — 기준치 제시로 fallback
            _ => ("reference", null),
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 공동주택 판정 함수
    // ─────────────────────────────────────────────────────────────────────────

    private static (string, string?) JudgeApt001(string? zoneName, BuildingInputsDto? inp)
    {
        var allowance = GetApartmentZoneAllowance(zoneName);

        if (inp?.HousingSubtype is not null)
        {
            var subtypeNote = GetApartmentSubtypeNote(zoneName, inp.HousingSubtype);
            return ("active", $"{subtypeNote} ({zoneName ?? "용도지역 미판정"}).");
        }

        return ("reference",
            $"{allowance}. " +
            "세부 유형(아파트/연립/다세대) 선택 시 더 정확한 판정 가능.");
    }

    private static (string, string?) JudgeAptParking(BuildingInputsDto? inp)
    {
        if (inp?.UnitCount is null)
            return ("pending", "세대수 입력 시 주차대수 산정 가능 (세대별 전용면적 85m² 기준 1.0대/세대).");

        int    units    = inp.UnitCount.Value;
        double unitArea = inp.UnitArea ?? 84.0;
        double ratio    = unitArea <= 85.0 ? 1.0 : 1.5;
        int    minPark  = (int)Math.Ceiling(units * ratio);

        var areaNote = inp.UnitArea.HasValue
            ? $"전용면적 {unitArea:F0}m²"
            : "전용면적 미입력 (84m² 기준 적용)";

        var parkTypeNote = inp.ParkingType switch
        {
            "underground" => " 지하주차장 — 기계식/자주식 계획 구분 확인 필요.",
            "mechanical"  => " 기계식 주차장 — 추가 구조 검토 필요.",
            _             => " 주차 방식(지하/지상/기계식) 별도 확인 필요.",
        };

        return ("active",
            $"{units}세대 × {areaNote} — {ratio:F1}대/세대 기준 최소 {minPark}대 산정.{parkTypeNote}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 공통 판정 함수 (APT/NH/OFF 공유)
    // ─────────────────────────────────────────────────────────────────────────

    private static (string, string?) JudgeDensity(
        string? zoneName, (double Bcr, double Far)? limits, BuildingInputsDto? inp)
    {
        if (limits is null)
            return ("reference",
                $"{zoneName ?? "용도지역 미판정"} — 법정 건폐율·용적률 기준 없음. " +
                "지자체 조례 확인 필요.");

        var (bcr, far) = limits.Value;

        if (inp?.FloorArea is not null && inp.SiteArea is not null)
        {
            double calcFar = inp.FloorArea.Value / inp.SiteArea.Value * 100.0;
            string farNote = calcFar <= far
                ? $"계획 용적률 {calcFar:F0}% ≤ 법정 상한 {far:F0}% ✅"
                : $"계획 용적률 {calcFar:F0}% > 법정 상한 {far:F0}% — 초과 ⚠️";

            string bcrNote;
            if (inp.FloorCount is not null && inp.FloorCount.Value > 0)
            {
                double estBcr = inp.FloorArea.Value / inp.FloorCount.Value / inp.SiteArea.Value * 100.0;
                bcrNote = estBcr <= bcr
                    ? $"추정 건폐율 {estBcr:F0}% ≤ 법정 상한 {bcr:F0}% ✅ (층별 균등 가정)"
                    : $"추정 건폐율 {estBcr:F0}% > 법정 상한 {bcr:F0}% — 초과 가능성 ⚠️ (층별 균등 가정)";
            }
            else
            {
                bcrNote = $"법정 건폐율 상한: {bcr:F0}% (건축면적 입력 시 정밀 계산 가능)";
            }

            return ("active",
                $"{farNote} / {bcrNote}. 조례 실적용 기준은 법정 상한보다 낮을 수 있습니다.");
        }

        return ("reference",
            $"법정 건폐율 상한: {bcr:F0}%, 법정 용적률 상한: {far:F0}%. " +
            "연면적·대지면적 입력 시 초과 여부 계산 가능. 조례 기준 별도 확인 필요.");
    }

    private static (string, string?) JudgeRoadFrontage(
        BuildingInputsDto? inp, string extraNote)
    {
        if (inp?.RoadFrontageWidth is null)
            return ("reference",
                $"접도 요건: 2m 이상 (4m 이상 권장). " +
                $"도로 접면 폭 입력 시 충족 여부 판정 가능. {extraNote}");

        double w = inp.RoadFrontageWidth.Value;
        string frontageNote = w >= 4.0
            ? $"도로 접면 {w:F1}m — 일반적 접도 요건 충족 ✅"
            : w >= 2.0
                ? $"도로 접면 {w:F1}m — 최소 요건(2m) 충족, 4m 미만 — 추가 확인 ⚠️"
                : $"도로 접면 {w:F1}m — 최소 접도 요건(2m) 미충족 위험 ❌";

        return ("active", $"{frontageNote}. {extraNote}");
    }

    private static (string, string?) JudgeEvacStairs(BuildingInputsDto? inp)
    {
        if (inp?.FloorCount is null)
            return ("reference",
                "피난계단 기준: 직통계단 2개소 이상(3층·각층 200m² 초과 시) / 특별피난계단(11층 이상). " +
                "층수 입력 시 의무 해당 여부 판정 가능.");

        int floors = inp.FloorCount.Value;
        string stairNote;

        if (floors >= 11)
            stairNote = $"{floors}층 — 특별피난계단 2개소 이상 설치 의무 ⚠️ (건축법 시행령 제35조)";
        else if (floors >= 6)
            stairNote = $"{floors}층 — 직통계단 2개소 이상 의무 ⚠️ (각 층 바닥면적 확인 필요)";
        else if (floors >= 3)
            stairNote = $"{floors}층 — 직통계단 설치 필요 (각 층 바닥면적 200m² 기준 추가 확인 필요)";
        else
            stairNote = $"{floors}층 — 층수 기준 피난계단 강화 적용 없음 ✅";

        return ("active", $"{stairNote}.");
    }

    private static (string, string?) JudgeElevator(BuildingInputsDto? inp, bool includeOccupant)
    {
        if (inp?.FloorCount is null)
            return ("reference",
                "승강기 기준: 6층 이상 의무 / 31m 초과 비상용 승강기 의무. " +
                "층수·높이 입력 시 해당 여부 판정 가능.");

        int    floors = inp.FloorCount.Value;
        double height = inp.BuildingHeight ?? (floors * 3.0); // 3m/층 추정
        bool   heightEstimated = inp.BuildingHeight is null;

        var notes = new List<string>();

        if (floors >= 6)
            notes.Add($"{floors}층 — 승강기 설치 의무 ⚠️, 장애인용 승강기 포함 필요");
        else
            notes.Add($"{floors}층 — 층수 기준 승강기 설치 의무 없음 ✅");

        if (height > 31.0)
        {
            string heightLabel = heightEstimated
                ? $"추정 높이 {height:F1}m (층당 3m 가정)"
                : $"높이 {height:F1}m";
            notes.Add($"{heightLabel} > 31m — 비상용 승강기 설치 의무 ⚠️");
        }

        if (includeOccupant && inp.OccupantCount.HasValue)
        {
            int elevCount = (int)Math.Ceiling(inp.OccupantCount.Value / 100.0);
            notes.Add($"상주 인원 {inp.OccupantCount.Value}명 — 승강기 약 {elevCount}대 이상 검토 권장");
        }

        return ("active", string.Join(" / ", notes) + ".");
    }

    private static (string, string?) JudgeFireCompartment(
        BuildingInputsDto? inp, bool checkMixedUse)
    {
        if (inp?.FloorArea is null)
            return ("reference",
                "방화구획 기준: 1,000m²/구획 (스프링클러 설치 시 3,000m²). " +
                "연면적 입력 시 적용 여부 판정 가능.");

        double fa = inp.FloorArea.Value;
        string fireNote;

        if (fa > 3000)
            fireNote = $"연면적 {fa:F0}m² — 방화구획(스프링클러 설치 시 3,000m²/구획) 필수 ⚠️";
        else if (fa > 1000)
            fireNote = $"연면적 {fa:F0}m² — 방화구획(1,000m²/구획 기준) 적용 대상 ⚠️";
        else if (fa > 500)
            fireNote = $"연면적 {fa:F0}m² — 방화구획 검토 대상 (1,000m² 미만으로 의무 구획은 아니나 실내 구조·용도에 따라 적용될 수 있음) ⚠️";
        else
            fireNote = $"연면적 {fa:F0}m² — 방화구획 면적 기준 이하 ✅";

        string? mixedNote = null;
        if (checkMixedUse && inp.MixedUseRatio.HasValue)
        {
            double pct = inp.MixedUseRatio.Value * 100.0;
            mixedNote = $" / 복합 용도 비율 {pct:F0}% — 용도 간 방화구획 분리 계획 필요 ⚠️";
        }

        return ("active", fireNote + mixedNote + ".");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 근린생활시설 판정 함수
    // ─────────────────────────────────────────────────────────────────────────

    private static (string, string?) JudgeNh1AllowedUse(string? zoneName, BuildingInputsDto? inp)
    {
        string zoneNote = GetNhZoneAllowance(zoneName, "제1종근린생활시설");

        if (inp?.DetailUseSubtype is not null)
        {
            string areaNote = inp.DetailUseFloorArea.HasValue
                ? GetNh1SubtypeAreaNote(inp.DetailUseSubtype, inp.DetailUseFloorArea.Value)
                : $"{inp.DetailUseSubtype} — 바닥면적 입력 시 면적 상한 초과 여부 판정 가능";
            return ("active", $"{zoneNote}. {areaNote}.");
        }

        return ("reference",
            $"{zoneNote}. 업종 세부 선택 시 바닥면적 상한 정밀 판정 가능.");
    }

    private static (string, string?) JudgeNh2AllowedUse(string? zoneName, BuildingInputsDto? inp)
    {
        string zoneNote = GetNhZoneAllowance(zoneName, "제2종근린생활시설");

        if (inp?.DetailUseSubtype is not null)
        {
            var multiNote = inp.IsMultipleOccupancy == true
                ? " / 다중이용업 해당 — 소방·안전 강화 기준 추가 적용 ⚠️"
                : string.Empty;
            string areaNote = inp.DetailUseFloorArea.HasValue
                ? GetNh2SubtypeAreaNote(inp.DetailUseSubtype, inp.DetailUseFloorArea.Value)
                : $"{inp.DetailUseSubtype} — 바닥면적 입력 시 면적 상한 초과 여부 판정 가능";
            return ("active", $"{zoneNote}. {areaNote}{multiNote}.");
        }

        if (inp?.IsMultipleOccupancy == true)
            return ("reference",
                $"{zoneNote}. 다중이용업 해당 — 소방·안전 강화 기준 별도 적용 ⚠️. " +
                "업종 세부 선택 시 정밀 판정 가능.");

        return ("reference", $"{zoneNote}. 업종 세부 선택 시 정밀 판정 가능.");
    }

    private static (string, string?) JudgeNhParking(string? zoneName, BuildingInputsDto? inp)
    {
        if (inp?.FloorArea is null)
            return ("reference",
                "주차기준: 시설 연면적 기준 134~150m²/대. " +
                "연면적 입력 시 최소 주차대수 산정 가능.");

        double fa = inp.FloorArea.Value;
        bool   isCommercial = zoneName?.Contains("상업") == true;
        double ratio        = isCommercial ? 134.0 : 150.0;
        int    minPark      = (int)Math.Ceiling(fa / ratio);

        string doSimNote = isCommercial
            ? "도심 여부에 따라 주차대수 감면 조례 적용 가능"
            : "지역·조례 기준 별도 확인 필요";

        return ("active",
            $"연면적 {fa:F0}m² ÷ {ratio:F0}m²/대 = 최소 약 {minPark}대 산정. {doSimNote}.");
    }

    private static (string, string?) JudgeNh2EvacStairs(BuildingInputsDto? inp)
    {
        var (baseStatus, baseNote) = JudgeEvacStairs(inp);

        // 2종근린 고시원 강화 기준 추가
        if (inp?.DetailUseSubtype is "고시원" or "다중이용업소")
        {
            string extra = "고시원·다중이용업소 — 간이스프링클러·방화구획 강화 기준 추가 적용 ⚠️.";
            return (baseStatus, baseNote is not null ? $"{baseNote} {extra}" : extra);
        }

        return (baseStatus, baseNote);
    }

    private static (string, string?) JudgeNh2FireCompartment(BuildingInputsDto? inp)
    {
        var (status, note) = JudgeFireCompartment(inp, checkMixedUse: false);

        string? multiNote = null;
        if (inp?.IsHighRiskOccupancy == true)
            multiNote = " / 화재위험 업종 해당 — 방화구획 강화 기준 적용 ⚠️";
        if (inp?.IsMultipleOccupancy == true)
            multiNote = (multiNote ?? string.Empty) +
                        " / 다중이용업 해당 — 소방시설 강화 기준 적용 ⚠️";

        if (multiNote is null) return (status, note);

        // JudgeFireCompartment는 "." 로 끝나므로 삽입 전에 제거 후 재조합
        string baseNote = note?.TrimEnd('.').TrimEnd() ?? string.Empty;
        return (status, baseNote + multiNote + ".");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 업무시설 판정 함수
    // ─────────────────────────────────────────────────────────────────────────

    private static (string, string?) JudgeOff001(string? zoneName, BuildingInputsDto? inp)
    {
        string zoneNote = GetOfficeZoneAllowance(zoneName);

        if (inp?.OfficeSubtype is not null)
        {
            string subtypeNote = inp.OfficeSubtype switch
            {
                "오피스텔" => GetOpstZoneNote(zoneName),
                "일반업무" => $"일반업무시설 — {zoneName ?? "용도지역 미판정"} 내 허용 여부 지자체 확인 필요",
                _         => $"{inp.OfficeSubtype} — 세부 유형 확인 필요",
            };
            return ("active", $"{subtypeNote}.");
        }

        return ("reference",
            $"{zoneNote}. 세부 유형(오피스텔/일반업무) 선택 시 더 정확한 판정 가능.");
    }

    private static (string, string?) JudgeOffRoad(BuildingInputsDto? inp)
    {
        var (baseStatus, baseNote) = JudgeRoadFrontage(inp,
            extraNote: "건축선 지정 여부·하역 공간 계획 추가 확인 필요.");

        string? loadingNote = null;
        if (inp?.HasLoadingBay == true)
            loadingNote = " / 하역 공간 계획 있음 ✅";
        else if (inp?.HasLoadingBay == false)
            loadingNote = " / 하역 공간 계획 없음 — 대형 업무시설 하역 동선 계획 필요 ⚠️";

        return (baseStatus, baseNote + loadingNote);
    }

    private static (string, string?) JudgeOffParking(string? zoneName, BuildingInputsDto? inp)
    {
        if (inp?.FloorArea is null)
            return ("reference",
                "업무시설 주차기준: 150m²/대 (25,000m² 이상 시 교통영향평가 가능성 높음). " +
                "연면적 입력 시 산정 가능.");

        double fa        = inp.FloorArea.Value;
        int    minPark   = (int)Math.Ceiling(fa / 150.0);
        string parkNote  = $"연면적 {fa:F0}m² ÷ 150m²/대 = 최소 약 {minPark}대 산정";

        string tiaNote = fa >= 25_000
            ? " / 연면적 25,000m² 이상 — 교통영향평가 대상 가능성 높음 ⚠️ (지자체 기준 확인)"
            : $" / 연면적 {fa:F0}m² — 교통영향평가 대상 여부 지자체 확인 필요";

        bool? isCommercial = zoneName?.Contains("상업");
        string pubNote = inp.HasPublicSpace == true
            ? " / 공개공지 계획 있음 ✅"
            : (inp.HasPublicSpace == false && fa >= 5_000 && isCommercial == true)
                ? " / 연면적 5,000m² 이상 상업지역 — 공개공지 설치 의무 ⚠️"
                : string.Empty;

        return ("active", parkNote + tiaNote + pubNote + ".");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 오버레이 판정 함수
    // ─────────────────────────────────────────────────────────────────────────

    private static (string, string?) JudgeOverlayDup(BuildingInputsDto? inp)
    {
        if (inp?.HasDistrictUnitPlanDocument == true)
            return ("active",
                "지구단위계획 결정도·지침서 보유 ✅ — 개별 지침 우선 적용 확인 후 설계 반영 필요.");

        return ("reference",
            "지구단위계획구역 해당 — 개별 지침이 법정 기준보다 우선 적용됩니다. " +
            "관할 구청 도시계획과에서 결정도·지침서 확인 필요 ⚠️.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 용도지역별 허용 여부 헬퍼
    // ─────────────────────────────────────────────────────────────────────────

    private static string GetApartmentZoneAllowance(string? zoneName)
    {
        if (zoneName is null) return "용도지역 미판정 — 허용 여부 확인 불가";
        if (zoneName.Contains("제1종전용주거")) return "단독주택만 허용 — 공동주택 건축 불가 ❌";
        if (zoneName.Contains("제2종전용주거")) return "연립·다세대 가능, 아파트 조건부 확인 필요 ⚠️";
        if (zoneName.Contains("제1종일반주거")) return "연립·다세대 허용, 아파트 불가 ❌";
        if (zoneName.Contains("제2종일반주거") ||
            zoneName.Contains("제3종일반주거") ||
            zoneName.Contains("준주거"))
            return "공동주택(아파트 포함) 건축 가능 ✅";
        if (zoneName.Contains("상업") || zoneName.Contains("준공업"))
            return "공동주택 허용 (복합 용도 비율 등 세부 확인 필요)";
        return "용도지역별 허용 여부 지자체 확인 필요";
    }

    private static string GetApartmentSubtypeNote(string? zoneName, string subtype)
    {
        if (zoneName is null) return $"{subtype} — 용도지역 미판정";

        if (zoneName.Contains("제1종전용주거"))
            return $"{subtype} 건축 불가 ❌ — 제1종전용주거지역은 단독주택만 허용";
        if (zoneName.Contains("제2종전용주거") && subtype == "아파트")
            return "아파트 — 제2종전용주거지역 조건부 허용 ⚠️ (관할 지자체 확인 필요)";
        if (zoneName.Contains("제1종일반주거") && subtype == "아파트")
            return "아파트 — 제1종일반주거지역 내 불가 ❌";

        return $"{subtype} 건축 가능 ✅";
    }

    private static string GetNhZoneAllowance(string? zoneName, string useType)
    {
        if (zoneName is null) return $"용도지역 미판정 — {useType} 허용 여부 확인 불가";
        if (zoneName.Contains("보전녹지") || zoneName.Contains("제1종전용주거"))
            return $"{zoneName} — {useType} 건축 허용 여부 별도 확인 ⚠️";
        return $"{zoneName} — {useType} 건축 가능 ✅";
    }

    private static string GetOfficeZoneAllowance(string? zoneName)
    {
        if (zoneName is null) return "용도지역 미판정 — 업무시설 허용 여부 확인 불가";
        if (zoneName.Contains("전용주거") || zoneName.Contains("제1종일반주거"))
            return $"{zoneName} — 업무시설 원칙적 불허 ❌";
        if (zoneName.Contains("제2종일반주거"))
            return $"{zoneName} — 업무시설 제한적 허용, 지자체 확인 필요 ⚠️";
        if (zoneName.Contains("제3종일반주거") || zoneName.Contains("준주거"))
            return $"{zoneName} — 업무시설(오피스텔 포함) 조건부 허용 ⚠️";
        return $"{zoneName} — 업무시설 건축 가능 ✅";
    }

    private static string GetOpstZoneNote(string? zoneName)
    {
        if (zoneName is null) return "오피스텔 — 용도지역 미판정";
        if (zoneName.Contains("준주거") || zoneName.Contains("상업"))
            return $"오피스텔 — {zoneName} 허용 ✅. 주거형 비율 규제 지자체 조례 확인 필요 ⚠️";
        if (zoneName.Contains("제3종일반주거"))
            return $"오피스텔 — {zoneName} 허용 여부 지자체 확인 ⚠️";
        return $"오피스텔 — {zoneName} 내 허용 여부 세부 확인 필요 ⚠️";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 업종별 면적 상한 헬퍼
    // ─────────────────────────────────────────────────────────────────────────

    private static string GetNh1SubtypeAreaNote(string subtype, double area)
    {
        return subtype switch
        {
            "슈퍼마켓" or "일용품소매점" => area < 1_000
                ? $"{subtype} {area:F0}m² — 1,000m² 미만 기준 충족 ✅"
                : $"{subtype} {area:F0}m² — 1,000m² 이상 ⚠️ 제2종근린생활시설 해당 확인",
            "의원" or "치과의원" or "한의원" or "약국" =>
                $"{subtype} {area:F0}m² — 제1종근린생활시설 허용 ✅ (면적 제한 없음)",
            "탁구장" or "체육도장" => area < 500
                ? $"{subtype} {area:F0}m² — 500m² 미만 기준 충족 ✅"
                : $"{subtype} {area:F0}m² — 500m² 이상 ⚠️ 제2종근린생활시설 해당 확인",
            _ => $"{subtype} {area:F0}m² — 업종별 바닥면적 상한 지자체·법령 확인 필요",
        };
    }

    private static string GetNh2SubtypeAreaNote(string subtype, double area)
    {
        return subtype switch
        {
            "고시원" => $"고시원 {area:F0}m² — 1층 이상 다락 포함 시 건축물 구조 제한 확인 필요 ⚠️",
            "PC방" or "게임제공업" => area < 500
                ? $"{subtype} {area:F0}m² — 500m² 미만 제2종 해당 ✅"
                : $"{subtype} {area:F0}m² — 500m² 이상 ⚠️ 다중이용업 여부 확인",
            "노래연습장" => $"노래연습장 {area:F0}m² — 다중이용업 해당 여부 확인 필요 ⚠️",
            _ => $"{subtype} {area:F0}m² — 업종별 바닥면적 상한 지자체·법령 확인 필요",
        };
    }
}
