using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

public static class LawChangeComparisonBuilder
{
    public static LawChangeCompareResponseDto Build(LawChangeCompareRequestDto request)
    {
        var current = request.CurrentClauses.ToDictionary(static clause => clause.Key, StringComparer.Ordinal);
        var amended = request.AmendedClauses.ToDictionary(static clause => clause.Key, StringComparer.Ordinal);
        var diffs = new List<LawChangeDiffDto>();

        foreach (var key in amended.Keys.Except(current.Keys, StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal))
        {
            var clause = amended[key];
            diffs.Add(new LawChangeDiffDto
            {
                ChangeType = "added",
                Key = key,
                Title = clause.Title,
                AmendedText = clause.Text,
                AmendedEffectiveDate = clause.EffectiveDate,
            });
        }

        foreach (var key in current.Keys.Except(amended.Keys, StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal))
        {
            var clause = current[key];
            diffs.Add(new LawChangeDiffDto
            {
                ChangeType = "removed",
                Key = key,
                Title = clause.Title,
                CurrentText = clause.Text,
                CurrentEffectiveDate = clause.EffectiveDate,
            });
        }

        foreach (var key in current.Keys.Intersect(amended.Keys, StringComparer.Ordinal).OrderBy(static key => key, StringComparer.Ordinal))
        {
            var currentClause = current[key];
            var amendedClause = amended[key];
            if (string.Equals(currentClause.Text, amendedClause.Text, StringComparison.Ordinal) &&
                string.Equals(currentClause.EffectiveDate, amendedClause.EffectiveDate, StringComparison.Ordinal))
            {
                continue;
            }

            diffs.Add(new LawChangeDiffDto
            {
                ChangeType = "changed",
                Key = key,
                Title = amendedClause.Title,
                CurrentText = currentClause.Text,
                AmendedText = amendedClause.Text,
                CurrentEffectiveDate = currentClause.EffectiveDate,
                AmendedEffectiveDate = amendedClause.EffectiveDate,
            });
        }

        var addedCount = diffs.Count(static diff => diff.ChangeType == "added");
        var removedCount = diffs.Count(static diff => diff.ChangeType == "removed");
        var changedCount = diffs.Count(static diff => diff.ChangeType == "changed");

        return new LawChangeCompareResponseDto
        {
            Subject = request.Subject,
            AddedCount = addedCount,
            RemovedCount = removedCount,
            ChangedCount = changedCount,
            Diffs = diffs,
            SummaryLines =
            [
                $"Added clauses: {addedCount}",
                $"Removed clauses: {removedCount}",
                $"Changed clauses: {changedCount}",
            ],
        };
    }
}
