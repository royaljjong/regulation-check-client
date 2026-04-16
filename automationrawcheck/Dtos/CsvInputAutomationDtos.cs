using Microsoft.AspNetCore.Http;

namespace AutomationRawCheck.Api.Dtos;

public sealed class CsvUploadFormDto
{
    public IFormFile? File { get; init; }
}

public sealed class CsvUploadCellDto
{
    public string Column { get; init; } = string.Empty;
    public string? Value { get; init; }
}

public sealed class CsvUploadRowPreviewDto
{
    public int RowNumber { get; init; }
    public List<CsvUploadCellDto> Cells { get; init; } = new();
}

public sealed class CsvFieldMappingDto
{
    public string SourceLabel { get; init; } = string.Empty;
    public string NormalizedKey { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string TargetField { get; init; } = string.Empty;
    public string Confidence { get; init; } = "low";
}

public sealed class CsvInputAutomationResultDto
{
    public string SchemaVersion { get; init; } = "csv_input_automation_v1";
    public string Token { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Delimiter { get; init; } = ",";
    public int RowCount { get; init; }
    public string? SuggestedSelectedUse { get; init; }
    public string? SuggestedReviewLevel { get; init; }
    public BuildingInputsDto SuggestedBuildingInputs { get; init; } = new();
    public List<CsvFieldMappingDto> Mappings { get; init; } = new();
    public List<CsvUploadRowPreviewDto> PreviewRows { get; init; } = new();
    public List<string> SummaryLines { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
