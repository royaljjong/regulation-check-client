using AutomationRawCheck.Application.Rules;
using AutomationRawCheck.Application.UseProfiles;

namespace AutomationRawCheck.Application.Services;

public sealed record UseProfileRuleCoverageSummary
{
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
    public string RuleDataStatus { get; init; } = "profile_only";
    public string CoverageNote { get; init; } = string.Empty;
}

/// <summary>
/// Reports explicit JSON coverage first, then seed JSON, then generic fallback coverage.
/// </summary>
public static class UseProfileRuleCoverageAnalyzer
{
    public static UseProfileRuleCoverageSummary Analyze(string selectedUse)
    {
        var explicitReviewRuleCount = RuleStore.ReviewItemRules.Count(rule => MatchesUse(rule.SelectedUse, rule.ApplicableUses, selectedUse));
        var explicitLawLayerRuleCount = RuleStore.LawLayerRules.Count(rule => MatchesUse(rule.SelectedUse, rule.ApplicableUses, selectedUse));

        var rawSeedReviewRuleCount = RuleStore.ReviewItemSeedRules.Count(rule => MatchesUse(rule.SelectedUse, rule.ApplicableUses, selectedUse));
        var rawSeedLawLayerRuleCount = RuleStore.LawLayerSeedRules.Count(rule => MatchesUse(rule.SelectedUse, rule.ApplicableUses, selectedUse));

        // Explicit rules take precedence. If explicit rules exist for a layer, seed rules are dormant.
        var seedReviewRuleCount = explicitReviewRuleCount > 0 ? 0 : rawSeedReviewRuleCount;
        var seedLawLayerRuleCount = explicitLawLayerRuleCount > 0 ? 0 : rawSeedLawLayerRuleCount;

        var fallbackReviewRuleCount = 0;
        var fallbackLawLayerRuleCount = 0;

        if (UseProfileRegistry.TryGet(selectedUse, out var profile) &&
            explicitReviewRuleCount == 0 &&
            seedReviewRuleCount == 0 &&
            explicitLawLayerRuleCount == 0 &&
            seedLawLayerRuleCount == 0)
        {
            fallbackReviewRuleCount = UseProfileFallbackRuleFactory.BuildReviewItemRules(profile).Count;

            var fallbackLawLayers = UseProfileFallbackRuleFactory.BuildLawLayerRules(profile);
            fallbackLawLayerRuleCount =
                fallbackLawLayers.Core.Count +
                fallbackLawLayers.ExtendedCore.Count +
                fallbackLawLayers.Mep.Count;
        }

        var effectiveReviewRuleCount = explicitReviewRuleCount + seedReviewRuleCount + fallbackReviewRuleCount;
        var effectiveLawLayerRuleCount = explicitLawLayerRuleCount + seedLawLayerRuleCount + fallbackLawLayerRuleCount;
        var usesSeed = seedReviewRuleCount > 0 || seedLawLayerRuleCount > 0;
        var usesFallback = fallbackReviewRuleCount > 0 || fallbackLawLayerRuleCount > 0;

        var status = (effectiveReviewRuleCount, effectiveLawLayerRuleCount, usesSeed, usesFallback) switch
        {
            (> 0, > 0, false, false) => "connected",
            (> 0, > 0, true, false) => "seed_connected",
            (> 0, > 0, false, true) => "fallback_connected",
            (0, 0, _, _) => "profile_only",
            _ => "partial",
        };

        var note = status switch
        {
            "connected" => "UseProfile is connected through explicit review_item_rules and law_layer_rules.",
            "seed_connected" => $"UseProfile is connected through seed rules. explicit(review={explicitReviewRuleCount}, law={explicitLawLayerRuleCount}), seed(review={seedReviewRuleCount}, law={seedLawLayerRuleCount})",
            "fallback_connected" => $"UseProfile has no explicit or seed rules, so generic fallback rules are active. fallback(review={fallbackReviewRuleCount}, law={fallbackLawLayerRuleCount})",
            "partial" => $"UseProfile coverage is partial. explicit(review={explicitReviewRuleCount}, law={explicitLawLayerRuleCount}), seed(review={seedReviewRuleCount}, law={seedLawLayerRuleCount}), fallback(review={fallbackReviewRuleCount}, law={fallbackLawLayerRuleCount})",
            _ => "UseProfile exists, but no review_item_rules or law_layer_rules are connected yet. Manual review is still the dominant path.",
        };

        return new UseProfileRuleCoverageSummary
        {
            ReviewRuleCount = effectiveReviewRuleCount,
            LawLayerRuleCount = effectiveLawLayerRuleCount,
            ExplicitReviewRuleCount = explicitReviewRuleCount,
            ExplicitLawLayerRuleCount = explicitLawLayerRuleCount,
            SeedReviewRuleCount = seedReviewRuleCount,
            SeedLawLayerRuleCount = seedLawLayerRuleCount,
            FallbackReviewRuleCount = fallbackReviewRuleCount,
            FallbackLawLayerRuleCount = fallbackLawLayerRuleCount,
            UsesSeed = usesSeed,
            UsesFallback = usesFallback,
            RuleDataStatus = status,
            CoverageNote = note,
        };
    }

    private static bool MatchesUse(string selectedUseValue, IReadOnlyCollection<string> applicableUses, string requestedUse)
    {
        if (string.Equals(selectedUseValue, requestedUse, StringComparison.Ordinal))
            return true;

        return applicableUses.Count > 0 &&
               applicableUses.Contains(requestedUse, StringComparer.Ordinal);
    }
}
