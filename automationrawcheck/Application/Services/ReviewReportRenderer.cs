using System.Text;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Prepares format-specific export plans and render-ready payloads for downstream PDF/Word renderers.
/// </summary>
public sealed class ReviewReportRenderer : IReviewReportRenderer
{
    public ReviewReportExportPlanDto BuildExportPlan(BuildingReviewReportPackageDto package, string format)
    {
        var normalizedFormat = NormalizeFormat(format);
        var (mimeType, templateKey, extension) = normalizedFormat switch
        {
            "pdf" => ("application/pdf", "legal_review_pdf_v1", ".pdf"),
            "docx" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "legal_review_docx_v1", ".docx"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "지원하지 않는 보고서 포맷입니다."),
        };

        return new ReviewReportExportPlanDto
        {
            Status = "ready",
            Format = normalizedFormat,
            MimeType = mimeType,
            RendererKey = "report_renderer_v1",
            TemplateKey = templateKey,
            SuggestedFileName = $"{package.Metadata.SuggestedFileNameBase}{extension}",
            Package = package,
        };
    }

    public ReviewReportRenderResultDto BuildRenderResult(BuildingReviewReportPackageDto package, string format)
    {
        var exportPlan = BuildExportPlan(package, format);
        var markdown = BuildMarkdownDocument(package, exportPlan.Format);

        return new ReviewReportRenderResultDto
        {
            Status = "render_ready",
            Format = exportPlan.Format,
            TargetMimeType = exportPlan.MimeType,
            PayloadMimeType = "text/markdown",
            RendererKey = exportPlan.RendererKey,
            TemplateKey = exportPlan.TemplateKey,
            SuggestedFileName = exportPlan.SuggestedFileName,
            PayloadType = "markdown_document",
            PayloadText = markdown,
            ExportPlan = exportPlan,
        };
    }

    public ReviewReportArtifactDto BuildMarkdownArtifact(BuildingReviewReportPackageDto package)
    {
        var markdown = BuildMarkdownDocument(package, "md");

        return new ReviewReportArtifactDto
        {
            Status = "artifact_ready",
            Format = "md",
            MimeType = "text/markdown",
            Encoding = "utf-8",
            SuggestedFileName = $"{package.Metadata.SuggestedFileNameBase}.md",
            Text = markdown,
        };
    }

    public ReviewSnapshotCompareReportExportPlanDto BuildCompareExportPlan(ReviewSnapshotCompareReportPackageDto package, string format)
    {
        var normalizedFormat = NormalizeFormat(format);
        var (mimeType, templateKey, extension) = normalizedFormat switch
        {
            "pdf" => ("application/pdf", "legal_review_compare_pdf_v1", ".pdf"),
            "docx" => ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "legal_review_compare_docx_v1", ".docx"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "지원하지 않는 비교 보고서 형식입니다."),
        };

        return new ReviewSnapshotCompareReportExportPlanDto
        {
            Status = "ready",
            Format = normalizedFormat,
            MimeType = mimeType,
            RendererKey = "report_renderer_v1",
            TemplateKey = templateKey,
            SuggestedFileName = $"{package.Metadata.SuggestedFileNameBase}{extension}",
            Package = package,
        };
    }

    public ReviewSnapshotCompareReportRenderResultDto BuildCompareRenderResult(ReviewSnapshotCompareReportPackageDto package, string format)
    {
        var exportPlan = BuildCompareExportPlan(package, format);
        var artifact = BuildCompareMarkdownArtifact(package);

        return new ReviewSnapshotCompareReportRenderResultDto
        {
            Status = "render_ready",
            Format = exportPlan.Format,
            TargetMimeType = exportPlan.MimeType,
            PayloadMimeType = artifact.MimeType,
            RendererKey = exportPlan.RendererKey,
            TemplateKey = exportPlan.TemplateKey,
            SuggestedFileName = exportPlan.SuggestedFileName,
            PayloadType = "markdown_document",
            PayloadText = artifact.Text,
            ExportPlan = exportPlan,
        };
    }

    public ReviewReportArtifactDto BuildCompareMarkdownArtifact(ReviewSnapshotCompareReportPackageDto package)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {package.Preview.Title}");
        sb.AppendLine();
        sb.AppendLine($"- GeneratedAt: {package.Metadata.GeneratedAt}");
        sb.AppendLine($"- PackageVersion: {package.Metadata.PackageVersion}");
        sb.AppendLine($"- PreviewSchemaVersion: {package.Metadata.PreviewSchemaVersion}");
        sb.AppendLine();

        foreach (var section in package.Preview.Sections.OrderBy(section => section.Order))
        {
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine();

            foreach (var field in section.Fields)
                sb.AppendLine($"- {field.Label}: {field.Value}");

            if (section.Highlights.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("요약:");
                foreach (var highlight in section.Highlights)
                    sb.AppendLine($"- {highlight}");
            }

            sb.AppendLine();
        }

        return new ReviewReportArtifactDto
        {
            Status = "artifact_ready",
            Format = "md",
            MimeType = "text/markdown",
            Encoding = "utf-8",
            SuggestedFileName = $"{package.Metadata.SuggestedFileNameBase}.md",
            Text = sb.ToString().TrimEnd(),
        };
    }

    private static string BuildMarkdownDocument(BuildingReviewReportPackageDto package, string targetFormat)
    {
        var sb = new StringBuilder();
        var preview = package.Preview;

        sb.AppendLine($"# {preview.Title}");
        sb.AppendLine();
        sb.AppendLine($"- Format: {targetFormat}");
        sb.AppendLine($"- GeneratedAt: {package.Metadata.GeneratedAt}");
        sb.AppendLine($"- PackageVersion: {package.Metadata.PackageVersion}");
        sb.AppendLine($"- PreviewSchemaVersion: {package.Metadata.PreviewSchemaVersion}");
        sb.AppendLine($"- DevelopmentActionApiSource: {package.Metadata.DevelopmentActionApiStatus.Source}");
        sb.AppendLine($"- DevelopmentActionApiStatus: {package.Metadata.DevelopmentActionApiStatus.Status}");
        sb.AppendLine($"- DevelopmentActionApiConfidence: {package.Metadata.DevelopmentActionApiStatus.Confidence}");
        if (!string.IsNullOrWhiteSpace(package.Metadata.DevelopmentActionApiStatus.Note))
            sb.AppendLine($"- DevelopmentActionApiNote: {package.Metadata.DevelopmentActionApiStatus.Note}");
        sb.AppendLine();

        foreach (var section in preview.Sections.OrderBy(section => section.Order))
        {
            sb.AppendLine($"## {section.Title}");
            sb.AppendLine();

            if (section.Fields.Count > 0)
            {
                foreach (var field in section.Fields)
                    sb.AppendLine($"- {field.Label}: {field.Value}");

                sb.AppendLine();
            }

            if (section.Highlights.Count > 0)
            {
                sb.AppendLine("요약:");
                foreach (var highlight in section.Highlights)
                    sb.AppendLine($"- {highlight}");

                sb.AppendLine();
            }
        }

        if (package.Preview.LegalBasisEntries.Count > 0)
        {
            sb.AppendLine("## 근거조문 상세");
            sb.AppendLine();

            foreach (var entry in package.Preview.LegalBasisEntries)
            {
                sb.AppendLine($"### {entry.Title}");
                sb.AppendLine();
                sb.AppendLine($"- RuleId: {entry.RuleId}");
                foreach (var clause in entry.Clauses)
                {
                    var clauseLabel = string.IsNullOrWhiteSpace(clause.ArticleRef)
                        ? clause.LawName
                        : $"{clause.LawName} {clause.ArticleRef}";
                    sb.AppendLine($"- {clauseLabel}");
                    if (!string.IsNullOrWhiteSpace(clause.ClauseText))
                        sb.AppendLine($"  - {clause.ClauseText}");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string NormalizeFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return "pdf";

        return format.Trim().ToLowerInvariant();
    }
}
