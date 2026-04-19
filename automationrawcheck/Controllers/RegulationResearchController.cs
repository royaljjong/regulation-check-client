using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutomationRawCheck.Api.Controllers;

[ApiController]
[Route("api/regulation-check/research")]
public sealed class RegulationResearchController : ControllerBase
{
    private readonly IAiAssistService _aiAssistService;
    private readonly IRegulationResearchService _researchService;

    public RegulationResearchController(
        IAiAssistService aiAssistService,
        IRegulationResearchService researchService)
    {
        _aiAssistService = aiAssistService;
        _researchService = researchService;
    }

    [HttpPost("search-hub")]
    [ProducesResponseType(typeof(RegulationSearchHubResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public IActionResult PostSearchHub([FromBody] RegulationSearchHubRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedUse))
            return BadRequest(new { error = "selectedUse is required." });

        return Ok(_researchService.BuildSearchHub(request));
    }

    [HttpPost("law-change-compare")]
    [ProducesResponseType(typeof(LawChangeCompareResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public IActionResult PostLawChangeCompare([FromBody] LawChangeCompareRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { error = "subject is required." });

        return Ok(_researchService.CompareLawChanges(request));
    }

    [HttpPost("official-law/search")]
    [ProducesResponseType(typeof(OfficialLawSearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostOfficialLawSearch([FromBody] OfficialLawSearchRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "query is required." });

        return Ok(await _researchService.SearchOfficialLawAsync(request, ct));
    }

    [HttpPost("official-law/body")]
    [ProducesResponseType(typeof(OfficialLawBodyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostOfficialLawBody([FromBody] OfficialLawBodyRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Id) && string.IsNullOrWhiteSpace(request.Mst))
            return BadRequest(new { error = "id or mst is required." });

        return Ok(await _researchService.GetOfficialLawBodyAsync(request, ct));
    }

    [HttpPost("source-sync-package")]
    [ProducesResponseType(typeof(RegulationSourceSyncPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public IActionResult PostSourceSyncPackage([FromBody] RegulationSourceSyncRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { error = "subject is required." });

        return Ok(_researchService.BuildSourceSyncPackage(request));
    }

    [HttpPost("source-sync-run")]
    [ProducesResponseType(typeof(RegulationSourceSyncRunResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostSourceSyncRun([FromBody] RegulationSourceSyncRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { error = "subject is required." });

        return Ok(await _researchService.RunSourceSyncAsync(request, ct));
    }

    [HttpPost("ai-assist-preview")]
    [ProducesResponseType(typeof(AiAssistResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public IActionResult PostAiAssistPreview([FromBody] AiAssistRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedUse))
            return BadRequest(new { error = "selectedUse is required." });

        return Ok(_aiAssistService.BuildPreview(request));
    }

    [HttpPost("ai-assist/request-package")]
    [ProducesResponseType(typeof(AiAssistRequestPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public IActionResult PostAiAssistRequestPackage([FromBody] AiAssistRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedUse))
            return BadRequest(new { error = "selectedUse is required." });

        return Ok(_aiAssistService.BuildRequestPackage(request));
    }

    [HttpPost("ai-assist/run")]
    [ProducesResponseType(typeof(AiAssistRunResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostAiAssistRun([FromBody] AiAssistRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedUse))
            return BadRequest(new { error = "selectedUse is required." });

        return Ok(await _aiAssistService.RunAsync(request, ct));
    }

    [HttpGet("ai-assist/status")]
    [ProducesResponseType(typeof(AiAssistServiceStatusDto), StatusCodes.Status200OK)]
    public IActionResult GetAiAssistStatus()
    {
        return Ok(_aiAssistService.GetStatus());
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(RegulationResearchServiceStatusDto), StatusCodes.Status200OK)]
    public IActionResult GetResearchStatus()
    {
        return Ok(_researchService.GetStatus());
    }
}
