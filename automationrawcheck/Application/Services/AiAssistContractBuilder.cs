using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

public static class AiAssistContractBuilder
{
    public static AiAssistResponseDto Build(AiAssistRequestDto request)
    {
        var hints = new List<AiAssistHintDto>();

        foreach (var task in request.Tasks.Where(static task => task.Status is "warning" or "manual_review" or "fail").Take(6))
        {
            hints.Add(new AiAssistHintDto
            {
                HintType = "missing_review_reminder",
                Title = task.Title,
                Message = task.Action,
                Keywords = [task.Category, task.Title],
                RelatedRuleIds = task.RelatedRuleIds.ToList(),
            });
        }

        foreach (var reviewItem in request.ReviewItems.Where(static item => !string.IsNullOrWhiteSpace(item.RuleId)).Take(6))
        {
            var keywords = reviewItem.RelatedLaws
                .Where(static law => !string.IsNullOrWhiteSpace(law))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            hints.Add(new AiAssistHintDto
            {
                HintType = "rule_location_hint",
                Title = reviewItem.Title,
                Message = reviewItem.JudgeNote ?? reviewItem.Description ?? reviewItem.Title,
                Keywords = keywords,
                RelatedRuleIds = string.IsNullOrWhiteSpace(reviewItem.RuleId) ? [] : [reviewItem.RuleId],
            });
        }

        foreach (var manual in request.ManualReviewSet.Take(6))
        {
            hints.Add(new AiAssistHintDto
            {
                HintType = "search_keyword_suggestion",
                Title = manual.Title,
                Message = manual.Prompt,
                Keywords = manual.SearchHints.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList(),
                RelatedRuleIds = manual.RelatedRuleIds.ToList(),
            });
        }

        return new AiAssistResponseDto
        {
            SelectedUse = request.SelectedUse,
            UseProfileKey = request.UseProfile?.Key,
            OrdinanceRegion = request.OrdinanceRegion,
            Hints = hints.Take(12).ToList(),
            Guardrails =
            [
                "Do not decide permitted or prohibited use.",
                "Do not compute legal numbers or thresholds.",
                "Only provide article navigation, search keywords, ordinance search guidance, and reminder hints.",
                "Output must remain structured JSON.",
            ],
        };
    }
}
