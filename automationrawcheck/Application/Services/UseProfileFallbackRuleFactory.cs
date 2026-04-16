using AutomationRawCheck.Application.Rules;
using AutomationRawCheck.Application.UseProfiles;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Generates baseline review-item and law-layer rules from UseProfile metadata
/// when explicit JSON rules are not yet connected for a use.
/// </summary>
public static class UseProfileFallbackRuleFactory
{
    public static IReadOnlyList<ReviewItemRuleRecord> BuildReviewItemRules(UseProfileDefinition profile)
    {
        var records = new List<ReviewItemRuleRecord>();
        var sortOrder = 10;

        foreach (var bundle in profile.RuleBundles)
        {
            var item = CreateReviewItem(profile, bundle, sortOrder);
            if (item is not null)
            {
                records.Add(item);
                sortOrder += 10;
            }
        }

        return records;
    }

    public static (
        IReadOnlyList<LawLayerRuleRecord> Core,
        IReadOnlyList<LawLayerRuleRecord> ExtendedCore,
        IReadOnlyList<LawLayerRuleRecord> Mep) BuildLawLayerRules(UseProfileDefinition profile)
    {
        var core = new List<LawLayerRuleRecord>();
        var extendedCore = new List<LawLayerRuleRecord>();
        var mep = new List<LawLayerRuleRecord>();
        var sortOrder = 10;

        foreach (var bundle in profile.RuleBundles)
        {
            var item = CreateLawLayer(profile, bundle, sortOrder);
            if (item is null)
                continue;

            switch (item.LayerType)
            {
                case "core":
                    core.Add(item);
                    break;
                case "extendedCore":
                    extendedCore.Add(item);
                    break;
                case "mep":
                    mep.Add(item);
                    break;
            }

            sortOrder += 10;
        }

        return (core, extendedCore, mep);
    }

    private static ReviewItemRuleRecord? CreateReviewItem(UseProfileDefinition profile, string bundle, int sortOrder)
    {
        var selectedUse = profile.Identity.DisplayName;
        var detailedInputs = profile.RequiredInputsByLevel.TryGetValue("detailed", out var detailed)
            ? detailed
            : [];

        return bundle switch
        {
            RuleBundleCatalog.Density => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-STD-{Slug(profile)}-DENSITY",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "밀도",
                Title = $"{selectedUse} 밀도 기준 확인",
                Description = "대지면적, 건축면적, 연면적을 기준으로 건폐율·용적률 리스크를 공통 엔진에서 검토합니다.",
                RequiredInputs = ["siteArea", "buildingArea", "floorArea"],
                RelatedLaws = ["국토계획법 제77조", "국토계획법 제78조", "관할 지자체 조례"],
                IsAutoCheckable = false,
                Priority = "high",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Access => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-STD-{Slug(profile)}-ACCESS",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "도로/건축선",
                Title = $"{selectedUse} 접도 및 차량 접근 확인",
                Description = "도로폭, 접도 조건, 차량 진출입 방식 등 인허가 선행 조건을 확인합니다.",
                RequiredInputs = ["roadFrontageWidth", "vehicleIngressType"],
                RelatedLaws = ["건축법 제44조", "건축법 제46조"],
                IsAutoCheckable = false,
                Priority = "high",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Parking => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-DTL-{Slug(profile)}-PARKING",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "주차",
                Title = $"{selectedUse} 주차 기준 확인",
                Description = "규모와 운영 특성에 맞는 주차 기준 및 차량동선 리스크를 점검합니다.",
                RequiredInputs = ["floorArea", .. detailedInputs],
                RelatedLaws = ["주차장법", "주차장법 시행령", "관할 지자체 주차 조례"],
                IsAutoCheckable = false,
                Priority = "medium",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Egress => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-STD-{Slug(profile)}-EGRESS",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "피난/계단",
                Title = $"{selectedUse} 피난 및 계단 기준 확인",
                Description = "층수, 수용인원, 객실·병상·학생수 등 운영 규모에 따라 피난 기준을 확인합니다.",
                RequiredInputs = ["floorCount", "occupantCount", .. detailedInputs],
                RelatedLaws = ["건축법 제49조", "건축법 시행령 제34조", "건축법 시행령 제35조"],
                IsAutoCheckable = false,
                Priority = "high",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.FireCompartment => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-STD-{Slug(profile)}-FIRE",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "방화",
                Title = $"{selectedUse} 방화구획 및 내화 기준 확인",
                Description = "용도와 규모에 따른 방화구획, 내화구조, 마감재 강화 기준을 점검합니다.",
                RequiredInputs = ["floorArea", "floorCount", .. detailedInputs],
                RelatedLaws = ["건축법 제50조", "건축법 제52조", "건축법 시행령 제46조"],
                IsAutoCheckable = false,
                Priority = "high",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Accessibility => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-DTL-{Slug(profile)}-ACCESSIBILITY",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "승강기",
                Title = $"{selectedUse} 승강기 및 접근성 기준 확인",
                Description = "층수, 수용인원, 장애인 이용 가능성에 따라 승강기 및 접근성 기준을 확인합니다.",
                RequiredInputs = ["floorCount", "buildingHeight", "occupantCount"],
                RelatedLaws = ["건축법 시행령", "장애인·노인·임산부 등의 편의증진 보장에 관한 법률"],
                IsAutoCheckable = false,
                Priority = "medium",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Energy => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-DTL-{Slug(profile)}-ENERGY",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "협의 항목",
                Title = $"{selectedUse} 에너지 성능 트리거 확인",
                Description = "연면적과 용도에 따라 에너지 절약계획서, 설비 기준, 성능 인증 필요 여부를 검토합니다.",
                RequiredInputs = ["floorArea", "buildingHeight"],
                RelatedLaws = ["건축물의 에너지절약설계기준"],
                IsAutoCheckable = false,
                Priority = "medium",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.StructureReview => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-DTL-{Slug(profile)}-STRUCTURE",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "협의 항목",
                Title = $"{selectedUse} 구조 검토 항목 확인",
                Description = "대공간, 적재하중, 운영 하중, 층고 조건을 고려한 구조 검토 포인트를 점검합니다.",
                RequiredInputs = ["floorArea", "floorCount", .. detailedInputs],
                RelatedLaws = ["건축법", "건축구조기준"],
                IsAutoCheckable = false,
                Priority = "medium",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.MEPReview => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-DTL-{Slug(profile)}-MEP",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "협의 항목",
                Title = $"{selectedUse} 기계·전기·소방 협의 확인",
                Description = "설비, 전기, 소방 설계와 연동되는 검토 포인트를 구조화합니다.",
                RequiredInputs = ["floorArea", "occupantCount", .. detailedInputs],
                RelatedLaws = ["소방시설법", "전기설비기술기준"],
                IsAutoCheckable = false,
                Priority = "medium",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.OrdinanceReview => new ReviewItemRuleRecord
            {
                Id = $"RI-FB-QUICK-{Slug(profile)}-ORDINANCE",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                Category = "지구단위계획",
                Title = $"{selectedUse} 조례 및 지구단위계획 확인",
                Description = "관할 지자체 조례, 지구단위계획, 도시계획시설 저촉 여부를 우선 검토합니다.",
                RequiredInputs = [],
                RelatedLaws = ["도시군계획조례", "지구단위계획 결정도서"],
                IsAutoCheckable = false,
                Priority = "high",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            _ => null,
        };
    }

    private static LawLayerRuleRecord? CreateLawLayer(UseProfileDefinition profile, string bundle, int sortOrder)
    {
        var selectedUse = profile.Identity.DisplayName;
        var baseId = $"LL-FB-{Slug(profile)}";

        return bundle switch
        {
            RuleBundleCatalog.Density => new LawLayerRuleRecord
            {
                Id = $"{baseId}-CORE-DENSITY",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "core",
                Law = "국토계획법 제77조·제78조",
                Scope = $"{selectedUse}의 건폐율·용적률 상한과 조례 하향 여부를 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Access => new LawLayerRuleRecord
            {
                Id = $"{baseId}-CORE-ACCESS",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "core",
                Law = "건축법 제44조·제46조",
                Scope = $"{selectedUse}의 접도 조건, 건축선, 차량 접근 기준을 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Egress => new LawLayerRuleRecord
            {
                Id = $"{baseId}-CORE-EGRESS",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "core",
                Law = "건축법 제49조 및 시행령 제34조~제36조",
                Scope = $"{selectedUse}의 계단, 피난, 특별피난 기준을 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.FireCompartment => new LawLayerRuleRecord
            {
                Id = $"{baseId}-CORE-FIRE",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "core",
                Law = "건축법 제50조·제52조 및 시행령 제46조",
                Scope = $"{selectedUse}의 방화구획, 내화구조, 마감재 강화 기준을 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Accessibility => new LawLayerRuleRecord
            {
                Id = $"{baseId}-CORE-ACCESSIBILITY",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "core",
                Law = "건축법 시행령 및 편의증진법",
                Scope = $"{selectedUse}의 승강기, 접근성, 장애인 이용 기준을 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Parking => new LawLayerRuleRecord
            {
                Id = $"{baseId}-EXCL-PARKING",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "extendedCore",
                Law = "주차장법 및 관할 지자체 조례",
                Scope = $"{selectedUse}의 주차 대수, 부설주차장 설치, 차량동선 기준을 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.Energy => new LawLayerRuleRecord
            {
                Id = $"{baseId}-EXCL-ENERGY",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "extendedCore",
                Law = "건축물의 에너지절약설계기준",
                Scope = $"{selectedUse}의 에너지 절약 설계 및 성능 기준을 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.StructureReview => new LawLayerRuleRecord
            {
                Id = $"{baseId}-EXCL-STRUCTURE",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "extendedCore",
                Law = "건축법 및 건축구조기준",
                Scope = $"{selectedUse}의 구조 설계 및 적재하중 관련 검토가 필요합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.OrdinanceReview => new LawLayerRuleRecord
            {
                Id = $"{baseId}-EXCL-ORDINANCE",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "extendedCore",
                Law = "도시군계획조례 및 지구단위계획",
                Scope = $"{selectedUse}의 조례, 지구단위계획, 도시계획시설 저촉 여부를 검토합니다.",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            RuleBundleCatalog.MEPReview => new LawLayerRuleRecord
            {
                Id = $"{baseId}-MEP-001",
                SelectedUse = selectedUse,
                ApplicableUses = [selectedUse],
                LayerType = "mep",
                Title = $"{selectedUse} 설비·전기·소방 협의",
                TeamTag = "MEP",
                SortOrder = sortOrder,
                Trigger = new RuleTrigger { AlwaysInclude = true },
            },
            _ => null,
        };
    }

    private static string Slug(UseProfileDefinition profile)
    {
        return profile.Identity.Key
            .Replace(".", "-", StringComparison.Ordinal)
            .Replace("_", "-", StringComparison.Ordinal)
            .ToUpperInvariant();
    }
}
