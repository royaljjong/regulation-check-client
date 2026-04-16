using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.UseProfiles;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Resolves user-facing review categories into fixed RuleBundle summaries.
/// </summary>
public static class RuleBundleResolver
{
    private static readonly IReadOnlyDictionary<string, (string Title, string[] Categories)> BundleCategoryMap =
        new Dictionary<string, (string Title, string[] Categories)>(StringComparer.Ordinal)
        {
            [RuleBundleCatalog.Density] = ("밀도", ["밀도"]),
            [RuleBundleCatalog.Access] = ("접근/도로", ["도로/건축선"]),
            [RuleBundleCatalog.Parking] = ("주차", ["주차"]),
            [RuleBundleCatalog.Egress] = ("피난", ["피난/계단"]),
            [RuleBundleCatalog.FireCompartment] = ("방화", ["방화"]),
            [RuleBundleCatalog.Accessibility] = ("편의/수직동선", ["승강기"]),
            [RuleBundleCatalog.Energy] = ("에너지", Array.Empty<string>()),
            [RuleBundleCatalog.StructureReview] = ("구조검토", Array.Empty<string>()),
            [RuleBundleCatalog.MEPReview] = ("설비검토", Array.Empty<string>()),
            [RuleBundleCatalog.OrdinanceReview] = ("조례/행정검토", ["지구단위계획", "중첩규제", "허용용도"]),
        };

    public static IReadOnlyList<RuleBundleSummaryDto> Summarize(
        UseProfileDefinition? profile,
        IEnumerable<ReviewItemDto> reviewItems)
    {
        if (profile is null)
            return [];

        var items = reviewItems.ToList();
        var summaries = new List<RuleBundleSummaryDto>();

        foreach (var bundleId in profile.RuleBundles.Distinct(StringComparer.Ordinal))
        {
            var expectedCategories = BundleCategoryMap.TryGetValue(bundleId, out var meta)
                ? meta.Categories
                : Array.Empty<string>();

            var matchedItems = items
                .Where(item => expectedCategories.Contains(item.Category, StringComparer.Ordinal))
                .ToList();

            var status = matchedItems.Count > 0
                ? "active"
                : expectedCategories.Length == 0
                    ? "configured"
                    : "dormant";

            summaries.Add(new RuleBundleSummaryDto
            {
                BundleId = bundleId,
                Title = meta.Title,
                Status = status,
                Categories = expectedCategories.ToList(),
                RelatedRuleIds = matchedItems
                    .Select(item => item.RuleId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Cast<string>()
                    .Distinct()
                    .ToList(),
            });
        }

        return summaries;
    }
}
