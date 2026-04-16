using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Interfaces;

public interface IReviewReportRenderer
{
    ReviewReportExportPlanDto BuildExportPlan(BuildingReviewReportPackageDto package, string format);
    ReviewReportRenderResultDto BuildRenderResult(BuildingReviewReportPackageDto package, string format);
    ReviewReportArtifactDto BuildMarkdownArtifact(BuildingReviewReportPackageDto package);
    ReviewSnapshotCompareReportExportPlanDto BuildCompareExportPlan(ReviewSnapshotCompareReportPackageDto package, string format);
    ReviewSnapshotCompareReportRenderResultDto BuildCompareRenderResult(ReviewSnapshotCompareReportPackageDto package, string format);
    ReviewReportArtifactDto BuildCompareMarkdownArtifact(ReviewSnapshotCompareReportPackageDto package);
}
