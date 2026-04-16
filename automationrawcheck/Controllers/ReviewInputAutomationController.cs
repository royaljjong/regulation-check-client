using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutomationRawCheck.Api.Controllers;

[ApiController]
[Route("api/regulation-check/input-automation")]
public sealed class ReviewInputAutomationController : ControllerBase
{
    private readonly CsvInputAutomationService _csvInputAutomationService;

    public ReviewInputAutomationController(CsvInputAutomationService csvInputAutomationService)
    {
        _csvInputAutomationService = csvInputAutomationService;
    }

    [HttpPost("csv-upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CsvInputAutomationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadCsv([FromForm] CsvUploadFormDto request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { error = "file is required." });
        }

        await using var stream = request.File.OpenReadStream();
        var result = await _csvInputAutomationService.ImportAsync(request.File.FileName, stream, cancellationToken);
        return Ok(result);
    }

    [HttpGet("csv-uploads/{token}")]
    [ProducesResponseType(typeof(CsvInputAutomationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetCsvUpload(string token)
    {
        var result = _csvInputAutomationService.Get(token);
        return result is null
            ? NotFound(new { error = "csv upload token not found." })
            : Ok(result);
    }
}
