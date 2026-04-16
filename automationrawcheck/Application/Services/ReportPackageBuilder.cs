using System.Text.RegularExpressions;
using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Packages review results into a stable contract for downstream PDF/Word renderers.
/// </summary>
public static class ReportPackageBuilder
{
    public static BuildingReviewReportPackageDto Build(BuildingReviewRequestDto request, BuildingReviewResponseDto review)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var title = review.ReportPreview.Title;
        var addressToken = review.Location.ResolvedAddress
                           ?? review.Location.InputAddress
                           ?? "site";

        return new BuildingReviewReportPackageDto
        {
            Metadata = new ReportPackageMetadataDto
            {
                PackageVersion = "report_package_v1",
                PreviewSchemaVersion = review.ReportPreview.SchemaVersion,
                Title = title,
                SuggestedFileNameBase = BuildSuggestedFileName(request.SelectedUse, addressToken, timestamp),
                GeneratedAt = timestamp.ToString("O"),
                SupportedFormats = ["pdf", "docx"],
                IntermediateFormats = ["md"],
                DevelopmentActionApiStatus = review.DevelopmentActionApiStatus,
            },
            Preview = review.ReportPreview,
            Checklist = review.Checklist,
            Tasks = review.Tasks,
            ManualReviews = review.ManualReviews,
            OrdinanceReviews = review.OrdinanceReviews,
            SourceReview = review,
        };
    }

    private static string BuildSuggestedFileName(string selectedUse, string addressToken, DateTimeOffset timestamp)
    {
        var normalizedUse = Sanitize(selectedUse);
        var normalizedAddress = Sanitize(addressToken);
        var dateToken = timestamp.ToString("yyyyMMdd");

        return $"{normalizedUse}_{normalizedAddress}_{dateToken}_review_report";
    }

    private static string Sanitize(string value)
    {
        var compact = Regex.Replace(value, "\\s+", "_");
        return Regex.Replace(compact, "[^0-9A-Za-z가-힣_\\-]", string.Empty);
    }
}
