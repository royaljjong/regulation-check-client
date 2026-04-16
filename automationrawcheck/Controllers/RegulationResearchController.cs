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

    [HttpPost("source-sync-package")]
    [ProducesResponseType(typeof(RegulationSourceSyncPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public IActionResult PostSourceSyncPackage([FromBody] RegulationSourceSyncRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(new { error = "subject is required." });

        return Ok(_researchService.BuildSourceSyncPackage(request));
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
