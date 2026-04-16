using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Rules;
using AutomationRawCheck.Application.UseProfiles;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Builds the next-step input guidance from the common UseProfile definition.
/// </summary>
public static class NextLevelHintBuilder
{
    private static readonly Dictionary<string, (string Label, string Reason)> _fieldMeta =
        new(StringComparer.Ordinal)
        {
            ["selectedUse"] = ("계획 용도", "공통 엔진에서 사용할 내부 UseProfile을 확정합니다."),
            ["siteArea"] = ("대지면적 (m2)", "건폐율과 용적률 산정의 기준값입니다."),
            ["buildingArea"] = ("건축면적 (m2)", "건폐율과 배치 리스크 검토에 필요합니다."),
            ["floorArea"] = ("연면적 (m2)", "용적률, 주차, 피난, 방화 트리거 산정에 필요합니다."),
            ["floorCount"] = ("층수", "피난계단, 승강기, 방화 강화 기준에 필요합니다."),
            ["buildingHeight"] = ("높이 (m)", "피난, 승강기, 소방 관련 강화 기준을 확인합니다."),
            ["roadFrontageWidth"] = ("도로폭 (m)", "접도 조건과 차량 접근 가능성을 확인합니다."),
            ["unitCount"] = ("세대수", "공동주택 주차 및 피난 기준을 세부 판정합니다."),
            ["unitArea"] = ("세대당 전용면적 (m2)", "공동주택 세부 기준과 주차 기준에 필요합니다."),
            ["housingSubtype"] = ("주택 유형", "공동주택 세부 분류와 허용 여부를 확인합니다."),
            ["parkingType"] = ("주차 방식", "주차 계획의 실현 가능성과 규정 적용 방식을 확인합니다."),
            ["detailUseSubtype"] = ("세부 업종", "근린생활시설 세부 허용 여부와 면적 기준을 확인합니다."),
            ["detailUseFloorArea"] = ("세부 업종 면적 (m2)", "업종별 면적 상한과 분류 기준을 확인합니다."),
            ["isMultipleOccupancy"] = ("다중이용 여부", "방화와 피난 강화 기준 적용 여부를 판단합니다."),
            ["officeSubtype"] = ("업무시설 세부 유형", "오피스텔 등 세부 유형별 기준을 구분합니다."),
            ["occupantCount"] = ("수용인원", "피난, 승강기, 집회성 사용 리스크를 판단합니다."),
            ["mixedUseRatio"] = ("혼합용도 비율", "복합용도에 따른 방화와 공용부 기준을 검토합니다."),
            ["roomCount"] = ("실수", "운영 규모와 피난, 주차 기준의 보정값으로 사용합니다."),
            ["guestRoomCount"] = ("객실수", "숙박시설의 객실 기준, 피난, 방화 검토에 필요합니다."),
            ["bedCount"] = ("병상수", "의료시설의 특별 피난 및 운영 기준 검토에 필요합니다."),
            ["studentCount"] = ("학생수", "교육시설의 피난 및 면적 기준 검토에 필요합니다."),
            ["vehicleIngressType"] = ("차량 출입 방식", "공장, 창고, 물류시설의 차량 동선 검토에 필요합니다."),
            ["medicalSpecialCriteria"] = ("의료 특수 기준", "의료법 등 특별법 검토 포인트를 구조화합니다."),
            ["educationSpecialCriteria"] = ("교육 특수 기준", "학생 동선, 학급 운영, 부속시설 기준을 보강합니다."),
            ["hazardousMaterialProfile"] = ("위험물 프로필", "위험물 저장·취급 여부에 따른 수동 검토를 유도합니다."),
            ["logisticsOperationProfile"] = ("물류 운영 프로필", "하역, 상하차, 대형차 운영 기준을 확인합니다."),
            ["accommodationSpecialCriteria"] = ("숙박 특수 기준", "객실 운영과 숙박 부속시설 기준을 보강합니다."),
        };

    public static NextLevelHintDto? Build(
        string selectedUse,
        ReviewLevel currentLevel,
        BuildingInputsDto? inputs,
        IEnumerable<ReviewItemRuleRecord> allRules)
    {
        if (currentLevel >= ReviewLevel.Detailed)
        {
            return new NextLevelHintDto
            {
                Note = $"현재 {ReviewLevelDetector.LevelToString(currentLevel)} 단계에는 자동 검토에 필요한 핵심 입력이 이미 반영되어 있습니다.",
            };
        }

        if (!UseProfileRegistry.TryGet(selectedUse, out var useProfile))
        {
            return new NextLevelHintDto
            {
                Note = $"'{selectedUse}'에 대한 UseProfile이 등록되어 있지 않아 다음 단계 입력 가이드를 만들 수 없습니다.",
            };
        }

        var nextLevel = (ReviewLevel)((int)currentLevel + 1);
        var nextLevelKey = ReviewLevelDetector.LevelToString(nextLevel);

        if (!useProfile.RequiredInputsByLevel.TryGetValue(nextLevelKey, out var candidateFields) ||
            candidateFields.Count == 0)
        {
            return new NextLevelHintDto
            {
                Note = $"현재 {ReviewLevelDetector.LevelToString(currentLevel)} 단계 다음에 추가로 요구되는 입력셋이 아직 정의되어 있지 않습니다.",
            };
        }

        var missingFields = candidateFields
            .Distinct(StringComparer.Ordinal)
            .Where(field => !IsFieldProvided(field, inputs))
            .ToList();

        if (missingFields.Count == 0)
        {
            return new NextLevelHintDto
            {
                Note = $"다음 단계({nextLevelKey})에서 필요한 입력은 이미 모두 제공되었습니다. reviewLevel을 '{nextLevelKey}'로 올리면 추가 Task와 Checklist가 열립니다.",
            };
        }

        var additionalInputs = missingFields
            .Select(ToInputFieldDto)
            .ToList();

        var willUnlock = allRules
            .Where(rule => ReviewLevelDetector.GetMinLevel(rule.Id) == nextLevel)
            .OrderBy(rule => rule.SortOrder)
            .Select(rule => $"{rule.Title} ({rule.Id})")
            .ToList();

        var taskCategories = allRules
            .Where(rule => ReviewLevelDetector.GetMinLevel(rule.Id) == nextLevel)
            .Select(rule => NormalizeCategory(rule.Category))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new NextLevelHintDto
        {
            NextLevel = nextLevelKey,
            AdditionalInputsNeeded = additionalInputs,
            WillUnlock = willUnlock,
            WillAddTaskCategories = taskCategories,
            WillChangeChecklist = BuildChecklistChanges(taskCategories),
        };
    }

    private static NextLevelInputFieldDto ToInputFieldDto(string field)
    {
        if (_fieldMeta.TryGetValue(field, out var meta))
        {
            return new NextLevelInputFieldDto
            {
                Field = field,
                Label = meta.Label,
                Reason = meta.Reason,
            };
        }

        return new NextLevelInputFieldDto
        {
            Field = field,
            Label = field,
            Reason = "UseProfile에 정의된 추가 입력입니다.",
        };
    }

    private static List<string> BuildChecklistChanges(IReadOnlyCollection<string> taskCategories)
    {
        var changes = new List<string>();

        if (taskCategories.Contains("밀도", StringComparer.Ordinal))
            changes.Add("밀도 체크리스트가 더 구체화됩니다.");

        if (taskCategories.Contains("주차", StringComparer.Ordinal))
            changes.Add("주차 체크리스트와 차량동선 Task가 추가됩니다.");

        if (taskCategories.Contains("피난", StringComparer.Ordinal))
            changes.Add("피난 체크리스트가 계단, 수용인원 기준까지 확장됩니다.");

        if (taskCategories.Contains("방화", StringComparer.Ordinal))
            changes.Add("방화 체크리스트가 구획과 강화 조건 중심으로 확장됩니다.");

        if (taskCategories.Contains("조례", StringComparer.Ordinal) ||
            taskCategories.Contains("인허가", StringComparer.Ordinal))
            changes.Add("조례 및 인허가 확인 항목이 더 많이 열립니다.");

        if (changes.Count == 0)
            changes.Add("다음 단계에서는 세부 검토 항목이 보강되지만 체크리스트 구조 변화는 제한적입니다.");

        return changes;
    }

    private static bool IsFieldProvided(string field, BuildingInputsDto? inputs)
    {
        if (inputs is null)
            return false;

        return field switch
        {
            "selectedUse" => true,
            "siteArea" => inputs.SiteArea.HasValue,
            "buildingArea" => inputs.BuildingArea.HasValue,
            "floorArea" => inputs.FloorArea.HasValue,
            "floorCount" => inputs.FloorCount.HasValue,
            "buildingHeight" => inputs.BuildingHeight.HasValue,
            "roadFrontageWidth" => inputs.RoadFrontageWidth.HasValue,
            "unitCount" => inputs.UnitCount.HasValue,
            "unitArea" => inputs.UnitArea.HasValue,
            "housingSubtype" => !string.IsNullOrWhiteSpace(inputs.HousingSubtype),
            "parkingType" => !string.IsNullOrWhiteSpace(inputs.ParkingType),
            "detailUseSubtype" => !string.IsNullOrWhiteSpace(inputs.DetailUseSubtype),
            "detailUseFloorArea" => inputs.DetailUseFloorArea.HasValue,
            "isMultipleOccupancy" => inputs.IsMultipleOccupancy.HasValue,
            "officeSubtype" => !string.IsNullOrWhiteSpace(inputs.OfficeSubtype),
            "occupantCount" => inputs.OccupantCount.HasValue,
            "mixedUseRatio" => inputs.MixedUseRatio.HasValue,
            "roomCount" => inputs.RoomCount.HasValue,
            "guestRoomCount" => inputs.GuestRoomCount.HasValue,
            "bedCount" => inputs.BedCount.HasValue,
            "studentCount" => inputs.StudentCount.HasValue,
            "vehicleIngressType" => !string.IsNullOrWhiteSpace(inputs.VehicleIngressType),
            "medicalSpecialCriteria" => !string.IsNullOrWhiteSpace(inputs.MedicalSpecialCriteria),
            "educationSpecialCriteria" => !string.IsNullOrWhiteSpace(inputs.EducationSpecialCriteria),
            "hazardousMaterialProfile" => !string.IsNullOrWhiteSpace(inputs.HazardousMaterialProfile),
            "logisticsOperationProfile" => !string.IsNullOrWhiteSpace(inputs.LogisticsOperationProfile),
            "accommodationSpecialCriteria" => !string.IsNullOrWhiteSpace(inputs.AccommodationSpecialCriteria),
            _ => false,
        };
    }

    private static string NormalizeCategory(string? category) => category switch
    {
        "밀도" => "밀도",
        "도로/건축선" => "인허가",
        "주차" => "주차",
        "피난/계단" => "피난",
        "승강기" => "피난",
        "방화" => "방화",
        "중첩규제" => "인허가",
        "지구단위계획" => "조례",
        "허용용도" => "조례",
        _ => category ?? "기타",
    };
}
