using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.UseProfiles;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Structures manual-review candidates into explicit cards for the review flow.
/// </summary>
public static class ManualReviewLayerBuilder
{
    public static IReadOnlyList<ManualReviewCardDto> Build(
        UseProfileDefinition? profile,
        IEnumerable<ReviewItemDto> reviewItems,
        BuildingInputsDto? inputs,
        bool? districtUnitPlan,
        bool? developmentActionRestriction,
        OverlayDecisionDto? developmentActionDetail)
    {
        var cards = reviewItems
            .Where(item => string.Equals(item.JudgeStatus, "pending", StringComparison.Ordinal))
            .Select(item => new ManualReviewCardDto
            {
                ManualReviewId = $"manual-{Sanitize(item.RuleId ?? item.Title)}",
                Category = NormalizeCategory(item.Category),
                Title = item.Title,
                Prompt = item.JudgeNote ?? item.Description,
                Status = "manual_review",
                SourceType = "rule_pending",
                RelatedRuleIds = item.RuleId is null ? [] : [item.RuleId],
                RequiredInputs = item.RequiredInputs.ToList(),
                SuggestedChecks = BuildSuggestedChecks(item),
                SearchHints = BuildSearchHints(profile, item),
            });

        var templateCards = BuildTemplateCards(profile, inputs, districtUnitPlan, developmentActionRestriction, developmentActionDetail);

        return cards
            .Concat(templateCards)
            .GroupBy(card => card.ManualReviewId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static IEnumerable<ManualReviewCardDto> BuildTemplateCards(
        UseProfileDefinition? profile,
        BuildingInputsDto? inputs,
        bool? districtUnitPlan,
        bool? developmentActionRestriction,
        OverlayDecisionDto? developmentActionDetail)
    {
        if (profile is null)
            yield break;

        if (profile.ManualCheckTemplates.Contains("district-unit-plan", StringComparer.Ordinal) &&
            (districtUnitPlan != false || inputs?.HasDistrictUnitPlanDocument != true))
        {
            yield return new ManualReviewCardDto
            {
                ManualReviewId = "manual-district-unit-plan",
                Category = "수동검토",
                Title = "지구단위계획 문서 확인",
                Prompt = "지구단위계획 결정도서와 시행지침을 확인해 배치, 공개공지, 보행동선, 추가 건축기준 반영 여부를 검토하세요.",
                Status = "manual_review",
                SourceType = "template_district_unit_plan",
                SuggestedChecks =
                [
                    "결정도서 보유 여부 확인",
                    "배치 및 공개공지 관련 추가 기준 확인",
                    "보행동선 및 인접 대지 제한 여부 확인",
                ],
                SearchHints = profile.LegalSearchHints
                    .Concat(["지구단위계획 결정도서", "지구단위계획 시행지침"])
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            };
        }

        if (developmentActionRestriction == true || inputs?.HasDevActRestrictionConsult != true)
        {
            yield return new ManualReviewCardDto
            {
                ManualReviewId = "manual-development-action",
                Category = "협의",
                Title = "개발행위허가 및 사전협의 확인",
                Prompt = "개발행위허가 대상 여부와 지자체 사전협의 필요 사항을 검토하세요.",
                Status = "manual_review",
                SourceType = "template_development_action",
                SuggestedChecks =
                [
                    "허가 대상 개발행위인지 확인",
                    "교통, 배수, 조경 등 협의 항목 정리",
                    "지자체 사전협의 회신 여부 확인",
                ],
                SearchHints = profile.LegalSearchHints
                    .Concat(["개발행위허가 운영지침", "개발행위허가 사전협의"])
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            };
        }

        if (developmentActionDetail is null || string.Equals(developmentActionDetail.Source, "none", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ManualReviewCardDto
            {
                ManualReviewId = "manual-development-action-api-readiness",
                Category = "인허가",
                Title = "개발행위허가 API 연동 설정 확인",
                Prompt = "개발행위허가 판단이 API로 확인되지 않았습니다. debug config/probe/parse-sample 경로로 설정과 응답 path를 먼저 검증하세요.",
                Status = "manual_review",
                SourceType = "api_unavailable",
                SuggestedChecks =
                [
                    "DevelopmentActionPermitApi Enabled/BaseUrl/RequestPath 확인",
                    "debug/dev-act-permit/config 에서 readiness.canCall 확인",
                    "debug/dev-act-permit/parse-sample 로 ResponseRootPath/InsideFieldPath 검증",
                ],
                SearchHints = profile.LegalSearchHints
                    .Concat(["개발행위허가 API", "개발행위허가 응답 path", "development action permit api"])
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            };
        }
        else if (string.Equals(developmentActionDetail.Source, "shp", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ManualReviewCardDto
            {
                ManualReviewId = "manual-development-action-api-fallback",
                Category = "인허가",
                Title = "개발행위허가 API fallback 결과 확인",
                Prompt = "현재 개발행위허가 판단은 SHP fallback 기준입니다. 실제 API 응답과 차이가 없는지 좌표 probe로 대조 확인이 필요합니다.",
                Status = "manual_review",
                SourceType = "api_fallback_shp",
                SuggestedChecks =
                [
                    "debug/dev-act-permit/probe 로 동일 좌표 API 결과 확인",
                    "SHP fallback 결과와 API 결과 비교",
                    "상충 시 보고서에 fallback 사용 사실 기록",
                ],
                SearchHints = profile.LegalSearchHints
                    .Concat(["개발행위허가 fallback", "SHP fallback", "development action restriction"])
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            };
        }
    }

    private static List<string> BuildSuggestedChecks(ReviewItemDto item)
    {
        var checks = new List<string>();
        if (item.RequiredInputs.Count > 0)
            checks.Add($"추가 입력 정보: {string.Join(", ", item.RequiredInputs)}");
        if (item.RelatedLaws.Count > 0)
            checks.Add($"근거 법규 확인: {string.Join(", ", item.RelatedLaws.Take(2))}");
        if (checks.Count == 0)
            checks.Add("관련 법규와 계획도서를 대조해 수동 판단");
        return checks;
    }

    private static List<string> BuildSearchHints(UseProfileDefinition? profile, ReviewItemDto item)
    {
        var hints = new List<string>();
        hints.AddRange(profile?.LegalSearchHints ?? []);
        hints.AddRange(item.RelatedLaws.Take(3));
        if (!string.IsNullOrWhiteSpace(item.Category))
            hints.Add($"{item.Category} 기준");

        return hints
            .Where(static hint => !string.IsNullOrWhiteSpace(hint))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string NormalizeCategory(string category) =>
        string.IsNullOrWhiteSpace(category) ? "수동검토" : category;

    private static string Sanitize(string seed) =>
        new string(seed.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
