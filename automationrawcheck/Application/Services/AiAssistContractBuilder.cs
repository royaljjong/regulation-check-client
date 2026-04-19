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
                "허용/불허 또는 적합/부적합을 확정하지 말 것.",
                "법적 수치나 기준값을 계산해서 단정하지 말 것.",
                "관련 법령명, 조문 탐색 힌트, 검색 키워드, 조례 탐색 안내, 추가 확인 필요사항만 안내할 것.",
                "반드시 한국어 JSON 응답 형식을 유지할 것.",
            ],
        };
    }
}
