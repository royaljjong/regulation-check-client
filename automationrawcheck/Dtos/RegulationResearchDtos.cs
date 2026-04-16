using Swashbuckle.AspNetCore.Annotations;

namespace AutomationRawCheck.Api.Dtos;

public sealed class RegulationSearchHubRequestDto
{
    public string SelectedUse { get; init; } = string.Empty;
    public string? OrdinanceRegion { get; init; }
    public UseProfileSummaryDto? UseProfile { get; init; }
    public List<ReviewTaskDto> Tasks { get; init; } = new();
    public List<ManualReviewCardDto> ManualReviewSet { get; init; } = new();
    public List<OrdinanceReviewCardDto> OrdinanceCards { get; init; } = new();
}

public sealed class RegulationSearchHubTargetDto
{
    public string TargetId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<string> Keywords { get; init; } = new();
    public List<string> Checkpoints { get; init; } = new();
    public string? LinkHint { get; init; }
    public string? DepartmentHint { get; init; }
}

public sealed class RegulationSearchHubResponseDto
{
    public string SchemaVersion { get; init; } = "search_hub_v1";
    public string SelectedUse { get; init; } = string.Empty;
    public string? OrdinanceRegion { get; init; }
    public List<RegulationSearchHubTargetDto> Targets { get; init; } = new();
    public List<string> SummaryLines { get; init; } = new();
}

public sealed class RegulationResearchServiceStatusDto
{
    public string SearchHubMode { get; init; } = "metadata_composition";
    public string LawChangeMode { get; init; } = "manual_compare";
    public bool SupportsLiveLawSource { get; init; }
    public bool SupportsMunicipalOrdinanceSync { get; init; }
    public bool SupportsPdfLinkHub { get; init; }
    public bool LiveLawSourceConfigured { get; init; }
    public bool MunicipalOrdinanceConfigured { get; init; }
    public bool PdfHubConfigured { get; init; }
    public string? LiveLawSourceEndpoint { get; init; }
    public string? MunicipalOrdinanceEndpoint { get; init; }
    public string? PdfHubEndpoint { get; init; }
    public List<string> SummaryLines { get; init; } = new();
}

public sealed class RegulationSourceSyncRequestDto
{
    public string Subject { get; init; } = string.Empty;
    public string? OrdinanceRegion { get; init; }
    public List<string> SearchKeywords { get; init; } = new();
}

public sealed class RegulationSourceSyncPackageDto
{
    public string SchemaVersion { get; init; } = "regulation_source_sync_v1";
    public string SearchHubMode { get; init; } = "metadata_composition";
    public string LawChangeMode { get; init; } = "manual_compare";
    public string Subject { get; init; } = string.Empty;
    public string? OrdinanceRegion { get; init; }
    public string? LiveLawSourceEndpoint { get; init; }
    public string? MunicipalOrdinanceEndpoint { get; init; }
    public string? PdfHubEndpoint { get; init; }
    public List<string> SearchKeywords { get; init; } = new();
    public List<string> SummaryLines { get; init; } = new();
}

public sealed class LawChangeCompareClauseDto
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Text { get; init; }
    public string? EffectiveDate { get; init; }
}

public sealed class LawChangeCompareRequestDto
{
    public string Subject { get; init; } = string.Empty;
    public List<LawChangeCompareClauseDto> CurrentClauses { get; init; } = new();
    public List<LawChangeCompareClauseDto> AmendedClauses { get; init; } = new();
}

public sealed class LawChangeDiffDto
{
    public string ChangeType { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? CurrentText { get; init; }
    public string? AmendedText { get; init; }
    public string? CurrentEffectiveDate { get; init; }
    public string? AmendedEffectiveDate { get; init; }
}

public sealed class LawChangeCompareResponseDto
{
    public string SchemaVersion { get; init; } = "law_change_compare_v1";
    public string Subject { get; init; } = string.Empty;
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ChangedCount { get; init; }
    public List<LawChangeDiffDto> Diffs { get; init; } = new();
    public List<string> SummaryLines { get; init; } = new();
}

public sealed class AiAssistRequestDto
{
    [SwaggerSchema(Description = "Selected use from the review flow.")]
    public string SelectedUse { get; init; } = string.Empty;
    public UseProfileSummaryDto? UseProfile { get; init; }
    public Dictionary<string, object?> PlanningContext { get; init; } = new(StringComparer.Ordinal);
    public List<ReviewItemDto> ReviewItems { get; init; } = new();
    public List<ReviewTaskDto> Tasks { get; init; } = new();
    public List<ManualReviewCardDto> ManualReviewSet { get; init; } = new();
    public string? OrdinanceRegion { get; init; }
}

public sealed class AiAssistHintDto
{
    public string HintType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public List<string> Keywords { get; init; } = new();
    public List<string> RelatedRuleIds { get; init; } = new();
}

public sealed class AiAssistResponseDto
{
    public string SchemaVersion { get; init; } = "gemma4_assist_contract_v1";
    public string ModelRole { get; init; } = "legal_navigation_only";
    public string Provider { get; set; } = "gemma4";
    public string Model { get; set; } = "gemma4";
    public string ExecutionMode { get; set; } = "preview";
    public bool IsConfigured { get; set; }
    public string SelectedUse { get; init; } = string.Empty;
    public string? UseProfileKey { get; init; }
    public string? OrdinanceRegion { get; init; }
    public List<AiAssistHintDto> Hints { get; init; } = new();
    public List<string> Guardrails { get; init; } = new();
}

public sealed class AiAssistServiceStatusDto
{
    public string Provider { get; init; } = "gemma4";
    public string Model { get; init; } = "gemma4";
    public string ExecutionMode { get; init; } = "preview";
    public bool Enabled { get; init; }
    public bool IsConfigured { get; init; }
    public bool EndpointConfigured { get; init; }
    public bool ApiKeyConfigured { get; init; }
    public string? Endpoint { get; init; }
    public string? ApiKeyHeaderName { get; init; }
    public List<string> SummaryLines { get; init; } = new();
}

public sealed class AiAssistRequestPackageDto
{
    public string SchemaVersion { get; init; } = "gemma4_request_package_v1";
    public string Provider { get; init; } = "gemma4";
    public string Model { get; init; } = "gemma4";
    public string ExecutionMode { get; init; } = "preview";
    public string OutputContract { get; init; } = "structured_json_only";
    public Dictionary<string, object?> InputPayload { get; init; } = new(StringComparer.Ordinal);
    public List<string> Guardrails { get; init; } = new();
    public List<string> SummaryLines { get; init; } = new();
}
