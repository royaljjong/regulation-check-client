using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

public static class RegulationSearchHubBuilder
{
    public static RegulationSearchHubResponseDto Build(RegulationSearchHubRequestDto request)
    {
        var targets = new List<RegulationSearchHubTargetDto>();
        var keywordPool = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hint in request.UseProfile?.LegalSearchHints ?? [])
            keywordPool.Add(hint);

        foreach (var manual in request.ManualReviewSet)
        {
            targets.Add(new RegulationSearchHubTargetDto
            {
                TargetId = manual.ManualReviewId,
                Category = "manual_review",
                Title = manual.Title,
                Keywords = manual.SearchHints.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Checkpoints = manual.SuggestedChecks.ToList(),
            });

            foreach (var hint in manual.SearchHints)
                keywordPool.Add(hint);
        }

        foreach (var ordinance in request.OrdinanceCards)
        {
            targets.Add(new RegulationSearchHubTargetDto
            {
                TargetId = ordinance.OrdinanceId,
                Category = "ordinance",
                Title = ordinance.Title,
                Keywords = ordinance.Keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Checkpoints = ordinance.CheckItems.ToList(),
                LinkHint = ordinance.Link,
                DepartmentHint = ordinance.Department,
            });

            foreach (var keyword in ordinance.Keywords)
                keywordPool.Add(keyword);
        }

        foreach (var task in request.Tasks.Where(static task => task.Status is "manual_review" or "warning" or "fail"))
        {
            targets.Add(new RegulationSearchHubTargetDto
            {
                TargetId = task.TaskId,
                Category = "task_followup",
                Title = task.Title,
                Keywords = [task.Category, task.Title],
                Checkpoints = [task.Action, task.Reason],
            });

            keywordPool.Add(task.Category);
            keywordPool.Add(task.Title);
        }

        var summaryLines = new List<string>
        {
            $"Search targets: {targets.Count}",
            $"Keyword hints: {keywordPool.Count}",
        };

        if (!string.IsNullOrWhiteSpace(request.OrdinanceRegion))
            summaryLines.Add($"Ordinance region: {request.OrdinanceRegion}");

        return new RegulationSearchHubResponseDto
        {
            SelectedUse = request.SelectedUse,
            OrdinanceRegion = request.OrdinanceRegion,
            Targets = targets,
            SummaryLines = summaryLines,
        };
    }
}
