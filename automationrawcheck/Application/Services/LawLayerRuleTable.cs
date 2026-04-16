using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Rules;
using AutomationRawCheck.Application.UseProfiles;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Maps selected-use rule records into Core / Extended Core / MEP law layers.
/// When explicit JSON rules are not connected for a use, profile-derived fallback layers are used.
/// </summary>
public static class LawLayerRuleTable
{
    public static IReadOnlyList<string> SupportedUses { get; } = UseProfileRegistry.SupportedDisplayNames;

    public static (
        List<CoreLawItemDto> Core,
        List<CoreLawItemDto> ExtendedCore,
        List<MepLawItemDto> Mep
    ) GetLayers(
        string selectedUse,
        bool? districtUnitPlanIsInside,
        bool? developmentRestrictionIsInside)
    {
        var rules = GetRuleSet(selectedUse);

        var coreOverlay = districtUnitPlanIsInside == true
            ? rules
                .Where(r => r.LayerType == "core" &&
                            r.SelectedUse == "*" &&
                            r.Trigger.WhenDistrictUnitPlan == true)
                .Select(ToCoreDto)
                .ToList()
            : [];

        var coreRegular = rules
            .Where(r => r.LayerType == "core" &&
                        MatchesUse(r, selectedUse) &&
                        r.Trigger.AlwaysInclude)
            .Select(ToCoreDto)
            .ToList();

        var extOverlay = developmentRestrictionIsInside == true
            ? rules
                .Where(r => r.LayerType == "extendedCore" &&
                            r.SelectedUse == "*" &&
                            r.Trigger.WhenDevelopmentRestriction == true)
                .Select(ToCoreDto)
                .ToList()
            : [];

        var extRegular = rules
            .Where(r => r.LayerType == "extendedCore" &&
                        MatchesUse(r, selectedUse) &&
                        r.Trigger.AlwaysInclude)
            .Select(ToCoreDto)
            .ToList();

        var mep = rules
            .Where(r => r.LayerType == "mep" &&
                        MatchesUse(r, selectedUse) &&
                        r.Trigger.AlwaysInclude)
            .Select(ToMepDto)
            .ToList();

        return (
            coreOverlay.Concat(coreRegular).ToList(),
            extOverlay.Concat(extRegular).ToList(),
            mep
        );
    }

    public static (
        List<LawLayerRuleRecord> Core,
        List<LawLayerRuleRecord> ExtendedCore,
        List<LawLayerRuleRecord> Mep
    ) GetLayersRaw(
        string selectedUse,
        bool? districtUnitPlanIsInside,
        bool? developmentRestrictionIsInside)
    {
        var rules = GetRuleSet(selectedUse);

        var coreOverlay = districtUnitPlanIsInside == true
            ? rules
                .Where(r => r.LayerType == "core" &&
                            r.SelectedUse == "*" &&
                            r.Trigger.WhenDistrictUnitPlan == true)
                .ToList()
            : [];

        var coreRegular = rules
            .Where(r => r.LayerType == "core" &&
                        MatchesUse(r, selectedUse) &&
                        r.Trigger.AlwaysInclude)
            .ToList();

        var extOverlay = developmentRestrictionIsInside == true
            ? rules
                .Where(r => r.LayerType == "extendedCore" &&
                            r.SelectedUse == "*" &&
                            r.Trigger.WhenDevelopmentRestriction == true)
                .ToList()
            : [];

        var extRegular = rules
            .Where(r => r.LayerType == "extendedCore" &&
                        MatchesUse(r, selectedUse) &&
                        r.Trigger.AlwaysInclude)
            .ToList();

        var mep = rules
            .Where(r => r.LayerType == "mep" &&
                        MatchesUse(r, selectedUse) &&
                        r.Trigger.AlwaysInclude)
            .ToList();

        return (
            coreOverlay.Concat(coreRegular).ToList(),
            extOverlay.Concat(extRegular).ToList(),
            mep
        );
    }

    private static IReadOnlyList<LawLayerRuleRecord> GetRuleSet(string selectedUse)
    {
        var rules = RuleStore.LawLayerRules;
        var hasExplicitUseRules = rules.Any(r => MatchesUse(r, selectedUse));
        var seedRules = (
            Core: RuleStore.LawLayerSeedRules.Where(r => r.LayerType == "core" && MatchesUse(r, selectedUse)).ToList(),
            ExtendedCore: RuleStore.LawLayerSeedRules.Where(r => r.LayerType == "extendedCore" && MatchesUse(r, selectedUse)).ToList(),
            Mep: RuleStore.LawLayerSeedRules.Where(r => r.LayerType == "mep" && MatchesUse(r, selectedUse)).ToList()
        );

        if (hasExplicitUseRules)
            return rules;

        if (seedRules.Core.Count > 0 || seedRules.ExtendedCore.Count > 0 || seedRules.Mep.Count > 0)
        {
            return rules
                .Concat(seedRules.Core)
                .Concat(seedRules.ExtendedCore)
                .Concat(seedRules.Mep)
                .ToList();
        }

        if (!UseProfileRegistry.TryGet(selectedUse, out var profile))
            return rules;

        var fallback = UseProfileFallbackRuleFactory.BuildLawLayerRules(profile);
        return rules
            .Concat(fallback.Core)
            .Concat(fallback.ExtendedCore)
            .Concat(fallback.Mep)
            .ToList();
    }

    private static bool MatchesUse(LawLayerRuleRecord rule, string selectedUse) =>
        rule.SelectedUse == selectedUse ||
        (rule.ApplicableUses.Count > 0 &&
         rule.ApplicableUses.Contains(selectedUse, StringComparer.Ordinal));

    private static CoreLawItemDto ToCoreDto(LawLayerRuleRecord rule) => new()
    {
        Law = rule.Law ?? string.Empty,
        Scope = rule.Scope ?? string.Empty,
    };

    private static MepLawItemDto ToMepDto(LawLayerRuleRecord rule) => new()
    {
        Title = rule.Title ?? string.Empty,
        TeamTag = rule.TeamTag ?? string.Empty,
    };
}
