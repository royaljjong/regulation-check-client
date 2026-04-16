using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Domain.Models;
using ReviewTaskStatus = AutomationRawCheck.Domain.Models.TaskStatus;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Converts legacy review items into standardized project tasks.
/// reviewItems remain the rule-layer intermediate result.
/// </summary>
public static class TaskLayerMapper
{
    public static IReadOnlyList<ReviewTask> Map(IEnumerable<ReviewItemDto> reviewItems)
    {
        return reviewItems
            .GroupBy(BuildTaskGroupKey)
            .Select(group =>
            {
                var first = group.First();
                var status = group.Select(MapStatus).OrderByDescending(StatusWeight).First();
                var reason = string.Join(" / ", group.Select(x => x.JudgeNote ?? x.Description).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                var action = BuildAction(first, status);

                return new ReviewTask
                {
                    TaskId = BuildTaskId(first),
                    Category = NormalizeCategory(first.Category),
                    Title = first.Title,
                    Action = action,
                    Status = status,
                    Reason = reason,
                    RelatedRuleIds = group.Select(x => x.RuleId).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToList(),
                    Priority = group.Any(x => x.Priority == "high")
                        ? "high"
                        : group.Any(x => x.Priority == "medium") ? "medium" : "low",
                };
            })
            .ToList();
    }

    public static ProjectChecklistSummary Summarize(IEnumerable<ReviewTask> tasks)
    {
        var arr = tasks.ToArray();
        return new ProjectChecklistSummary
        {
            Fail = arr.Count(t => t.Status == ReviewTaskStatus.Fail),
            Warning = arr.Count(t => t.Status == ReviewTaskStatus.Warning),
            Ok = arr.Count(t => t.Status == ReviewTaskStatus.Ok),
            Info = arr.Count(t => t.Status == ReviewTaskStatus.Info),
            ManualReview = arr.Count(t => t.Status == ReviewTaskStatus.ManualReview),
        };
    }

    private static string BuildTaskGroupKey(ReviewItemDto item) =>
        $"{NormalizeCategory(item.Category)}::{item.Title}";

    private static string BuildTaskId(ReviewItemDto item)
    {
        var seed = item.RuleId ?? item.Title;
        var normalized = new string(seed.Where(char.IsLetterOrDigit).ToArray());
        return $"task-{normalized.ToLowerInvariant()}";
    }

    private static string NormalizeCategory(string? category) => category switch
    {
        "밀도" => "밀도",
        "피난/계단" => "피난",
        "주차" => "주차",
        "방화" => "방화",
        "중첩규제" => "인허가",
        "지구단위계획" => "조례",
        _ => category ?? "기타",
    };

    private static ReviewTaskStatus MapStatus(ReviewItemDto item)
    {
        return item.JudgeStatus switch
        {
            "pending" => ReviewTaskStatus.ManualReview,
            "reference" => ReviewTaskStatus.Info,
            "active" => ClassifyActive(item),
            _ => item.IsAutoCheckable ? ReviewTaskStatus.Info : ReviewTaskStatus.ManualReview,
        };
    }

    private static ReviewTaskStatus ClassifyActive(ReviewItemDto item)
    {
        var note = item.JudgeNote ?? string.Empty;
        if (RegexLike(note, "불가|초과|위반|미달|부적합")) return ReviewTaskStatus.Fail;
        if (RegexLike(note, "주의|검토 필요|추가 확인|가능성")) return ReviewTaskStatus.Warning;
        return ReviewTaskStatus.Ok;
    }

    private static int StatusWeight(ReviewTaskStatus status) => status switch
    {
        ReviewTaskStatus.Fail => 5,
        ReviewTaskStatus.ManualReview => 4,
        ReviewTaskStatus.Warning => 3,
        ReviewTaskStatus.Info => 2,
        _ => 1,
    };

    private static string BuildAction(ReviewItemDto item, ReviewTaskStatus status) => status switch
    {
        ReviewTaskStatus.Fail => "계획안 수정 또는 법규 기준 재검토",
        ReviewTaskStatus.Warning => "추가 근거와 도면 기준으로 검토 보강",
        ReviewTaskStatus.ManualReview => "필수 입력 또는 수동 검토 자료 보강",
        ReviewTaskStatus.Info => "상세 법규와 조례 기준 확인",
        _ => "현재 기준 유지",
    };

    private static bool RegexLike(string text, string tokenGroup)
    {
        return tokenGroup.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Any(text.Contains);
    }
}
