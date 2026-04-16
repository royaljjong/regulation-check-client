using System.Collections.ObjectModel;

namespace AutomationRawCheck.Application.UseProfiles;

public static class UseProfileRegistry
{
    private static readonly IReadOnlyDictionary<string, UseProfileDefinition> _profiles;

    static UseProfileRegistry()
    {
        var profiles = new Dictionary<string, UseProfileDefinition>(StringComparer.Ordinal)
        {
                ["공동주택"] = CreateProfile(
                    key: "residential.multi_family",
                    displayName: "공동주택",
                    functionalGroup: "residential",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["unitCount", "unitArea", "housingSubtype", "parkingType"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "Elevator", "FireCompartment", "Energy"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.Accessibility, RuleBundleCatalog.Energy, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["density-review", "parking-review", "egress-review", "ordinance-review"],
                    manualTemplates: ["district-unit-plan", "ordinance-confirmation"],
                    legalSearchHints: ["공동주택 용도", "주택법 공동주택", "건축법 피난계단 공동주택"],
                    semiCommon: ["unitCount", "parkingType"],
                    specialized: ["housingSubtype", "unitArea"],
                    coverage: AutomationCoverage.Medium),

                ["제1종근린생활시설"] = CreateProfile(
                    key: "neighborhood.facility.type1",
                    displayName: "제1종근린생활시설",
                    functionalGroup: "neighborhood",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["detailUseSubtype", "detailUseFloorArea", "roomCount"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "FireCompartment"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.Accessibility, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["allowed-use-review", "parking-review", "fire-review", "ordinance-review"],
                    manualTemplates: ["district-unit-plan", "urban-planning-facility"],
                    legalSearchHints: ["제1종근린생활시설 업종 기준", "건축법 근린생활시설", "근생 바닥면적 기준"],
                    semiCommon: ["detailUseSubtype", "roomCount"],
                    specialized: ["detailUseFloorArea"],
                    coverage: AutomationCoverage.Medium),

                ["제2종근린생활시설"] = CreateProfile(
                    key: "neighborhood.facility.type2",
                    displayName: "제2종근린생활시설",
                    functionalGroup: "neighborhood",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["detailUseSubtype", "detailUseFloorArea", "isMultipleOccupancy"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "FireCompartment"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.Accessibility, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["allowed-use-review", "parking-review", "fire-review", "manual-review"],
                    manualTemplates: ["district-unit-plan", "ordinance-confirmation"],
                    legalSearchHints: ["제2종근린생활시설 업종 기준", "고시원 기준", "근생 방화 기준"],
                    semiCommon: ["detailUseSubtype", "isMultipleOccupancy"],
                    specialized: ["detailUseFloorArea"],
                    coverage: AutomationCoverage.Medium),

                ["업무시설"] = CreateProfile(
                    key: "office.general",
                    displayName: "업무시설",
                    functionalGroup: "office",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["officeSubtype", "occupantCount", "mixedUseRatio"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "Elevator", "FireCompartment", "Energy"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.Accessibility, RuleBundleCatalog.Energy, RuleBundleCatalog.StructureReview, RuleBundleCatalog.MEPReview],
                    taskTemplates: ["density-review", "parking-review", "fire-review", "circulation-review"],
                    manualTemplates: ["traffic-impact-review", "ordinance-confirmation"],
                    legalSearchHints: ["업무시설 허용 용도", "오피스텔 주차 기준", "업무시설 교통영향평가"],
                    semiCommon: ["occupantCount"],
                    specialized: ["officeSubtype", "mixedUseRatio"],
                    coverage: AutomationCoverage.Medium),

                ["교육시설"] = CreateProfile(
                    key: "education.general",
                    displayName: "교육시설",
                    functionalGroup: "education",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["studentCount", "occupantCount"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "Elevator", "FireCompartment", "Accessibility"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.Accessibility, RuleBundleCatalog.StructureReview, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["density-review", "egress-review", "accessibility-review", "manual-review"],
                    manualTemplates: ["district-unit-plan", "school-route-review"],
                    legalSearchHints: ["교육시설 학생수 기준", "교육연구시설 피난", "학교시설 접근성"],
                    semiCommon: ["studentCount", "occupantCount"],
                    specialized: ["educationSpecialCriteria"],
                    coverage: AutomationCoverage.Medium),

                ["의료시설"] = CreateProfile(
                    key: "medical.general",
                    displayName: "의료시설",
                    functionalGroup: "medical",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["bedCount", "occupantCount"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "Elevator", "FireCompartment", "Accessibility", "MEP"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.Accessibility, RuleBundleCatalog.MEPReview, RuleBundleCatalog.StructureReview, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["egress-review", "fire-review", "mep-review", "manual-review"],
                    manualTemplates: ["district-unit-plan", "medical-special-law-review"],
                    legalSearchHints: ["의료시설 병상수 기준", "의료시설 피난 강화", "의료법 건축 검토"],
                    semiCommon: ["bedCount", "occupantCount"],
                    specialized: ["medicalSpecialCriteria"],
                    coverage: AutomationCoverage.Low),

                ["숙박시설"] = CreateProfile(
                    key: "lodging.general",
                    displayName: "숙박시설",
                    functionalGroup: "lodging",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["guestRoomCount", "roomCount", "occupantCount"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "Elevator", "FireCompartment", "Energy"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.Energy, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["density-review", "fire-review", "egress-review", "manual-review"],
                    manualTemplates: ["district-unit-plan", "accommodation-operation-review"],
                    legalSearchHints: ["숙박시설 객실수 기준", "숙박시설 방화", "숙박시설 피난"],
                    semiCommon: ["guestRoomCount", "roomCount", "occupantCount"],
                    specialized: ["accommodationSpecialCriteria"],
                    coverage: AutomationCoverage.Medium),

                ["공장"] = CreateProfile(
                    key: "industrial.factory",
                    displayName: "공장",
                    functionalGroup: "industrial",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["occupantCount", "vehicleIngressType"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "FireCompartment", "Structure", "MEP"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.StructureReview, RuleBundleCatalog.MEPReview, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["density-review", "circulation-review", "fire-review", "manual-review"],
                    manualTemplates: ["hazardous-material-review", "development-action"],
                    legalSearchHints: ["공장 위험물 검토", "산업집적법 공장", "공장 주차 및 소방"],
                    semiCommon: ["occupantCount", "vehicleIngressType"],
                    specialized: ["hazardousMaterialProfile"],
                    coverage: AutomationCoverage.Low),

                ["창고시설"] = CreateProfile(
                    key: "warehouse.general",
                    displayName: "창고시설",
                    functionalGroup: "warehouse",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["roomCount", "vehicleIngressType"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "FireCompartment", "Structure"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.StructureReview, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["density-review", "parking-review", "circulation-review", "manual-review"],
                    manualTemplates: ["loading-operation-review", "development-action"],
                    legalSearchHints: ["창고시설 면적 기준", "창고시설 하역 기준", "창고시설 방화"],
                    semiCommon: ["roomCount", "vehicleIngressType"],
                    specialized: ["logisticsOperationProfile"],
                    coverage: AutomationCoverage.Medium),

                ["물류시설"] = CreateProfile(
                    key: "logistics.general",
                    displayName: "물류시설",
                    functionalGroup: "logistics",
                    standardInputs: ["siteArea", "buildingArea", "floorArea", "floorCount", "buildingHeight", "roadFrontageWidth"],
                    detailedInputs: ["vehicleIngressType", "occupantCount"],
                    derivedMetrics: ["BCR", "FAR", "Parking", "Egress", "FireCompartment", "Structure", "MEP"],
                    ruleBundles: [RuleBundleCatalog.Density, RuleBundleCatalog.Access, RuleBundleCatalog.Parking, RuleBundleCatalog.Egress, RuleBundleCatalog.FireCompartment, RuleBundleCatalog.StructureReview, RuleBundleCatalog.MEPReview, RuleBundleCatalog.OrdinanceReview],
                    taskTemplates: ["parking-review", "circulation-review", "fire-review", "manual-review"],
                    manualTemplates: ["loading-operation-review", "traffic-impact-review"],
                    legalSearchHints: ["물류시설 차량동선", "물류시설 하역 기준", "대형면적 물류시설"],
                    semiCommon: ["vehicleIngressType", "occupantCount"],
                    specialized: ["logisticsOperationProfile"],
                    coverage: AutomationCoverage.Low),
        };

        ValidateProfiles(profiles);
        _profiles = new ReadOnlyDictionary<string, UseProfileDefinition>(profiles);
    }

    public static IReadOnlyList<UseProfileDefinition> All => _profiles.Values.ToList();

    public static IReadOnlyList<string> SupportedDisplayNames =>
        _profiles.Keys.OrderBy(static x => x, StringComparer.Ordinal).ToList();

    public static bool TryGet(string selectedUse, out UseProfileDefinition profile) =>
        _profiles.TryGetValue(selectedUse, out profile!);

    public static bool IsSupported(string? selectedUse) =>
        selectedUse is not null && _profiles.ContainsKey(selectedUse);

    public static IReadOnlyList<string> StandardOnboardingProcedure =>
        UseProfileTemplateCatalog.StandardOnboardingProcedure;

    private static UseProfileDefinition CreateProfile(
        string key,
        string displayName,
        string functionalGroup,
        IReadOnlyList<string> standardInputs,
        IReadOnlyList<string> detailedInputs,
        IReadOnlyList<string> derivedMetrics,
        IReadOnlyList<string> ruleBundles,
        IReadOnlyList<string> taskTemplates,
        IReadOnlyList<string> manualTemplates,
        IReadOnlyList<string> legalSearchHints,
        IReadOnlyList<string>? semiCommon,
        IReadOnlyList<string>? specialized,
        AutomationCoverage coverage)
    {
        return new UseProfileDefinition
        {
            Identity = new UseProfileIdentity
            {
                Key = key,
                DisplayName = displayName,
                FunctionalGroup = functionalGroup,
                SampleSet = "generalized-samples",
            },
            RequiredInputsByLevel = new(StringComparer.Ordinal)
            {
                ["quick"] = ["selectedUse"],
                ["standard"] = standardInputs.ToList(),
                ["detailed"] = detailedInputs.ToList(),
            },
            DerivedMetrics = derivedMetrics.ToList(),
            RuleBundles = ruleBundles.ToList(),
            TaskTemplates = taskTemplates.ToList(),
            ManualCheckTemplates = manualTemplates.ToList(),
            LegalSearchHints = legalSearchHints.ToList(),
            InputGroups = DefaultInputGroups(semiCommon, specialized),
            Coverage = coverage,
        };
    }

    private static Dictionary<UseInputGroup, List<string>> DefaultInputGroups(
        IEnumerable<string>? semiCommon = null,
        IEnumerable<string>? specialized = null)
    {
        return new Dictionary<UseInputGroup, List<string>>
        {
            [UseInputGroup.Common] = UseProfileInputCatalog.CommonInputs.ToList(),
            [UseInputGroup.SemiCommon] = semiCommon?.Distinct(StringComparer.Ordinal).ToList() ?? [],
            [UseInputGroup.Specialized] = specialized?.Distinct(StringComparer.Ordinal).ToList() ?? [],
        };
    }

    private static void ValidateProfiles(IReadOnlyDictionary<string, UseProfileDefinition> profiles)
    {
        foreach (var (displayName, profile) in profiles)
        {
            if (!string.Equals(displayName, profile.Identity.DisplayName, StringComparison.Ordinal))
                throw new InvalidOperationException($"UseProfile display name mismatch: {displayName}");

            if (profile.RuleBundles.Any(bundle => !RuleBundleCatalog.All.Contains(bundle)))
                throw new InvalidOperationException($"Unknown rule bundle in {displayName}");

            if (profile.TaskTemplates.Any(template => !UseProfileTemplateCatalog.TaskTemplates.Contains(template)))
                throw new InvalidOperationException($"Unknown task template in {displayName}");

            if (profile.ManualCheckTemplates.Any(template => !UseProfileTemplateCatalog.ManualReviewTemplates.Contains(template)))
                throw new InvalidOperationException($"Unknown manual template in {displayName}");
        }
    }
}
