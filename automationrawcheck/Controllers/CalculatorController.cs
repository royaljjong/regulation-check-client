// =============================================================================
// CalculatorController.cs
// 간이 건축 계산기 API 컨트롤러
//
// 엔드포인트:
//   POST /api/calculator/basic  - 건폐율(BCR)·용적률(FAR) 계산
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutomationRawCheck.Api.Controllers;

/// <summary>
/// 건폐율·용적률 간이 계산 API입니다.
/// <para>결과는 참고용이며 조례 기준이 다를 수 있습니다.</para>
/// </summary>
[ApiController]
[Route("api/calculator")]
[Produces("application/json")]
public sealed class CalculatorController : ControllerBase
{
    private readonly ILogger<CalculatorController> _logger;

    public CalculatorController(ILogger<CalculatorController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 대지면적·건축면적·연면적을 입력받아 건폐율(BCR)과 용적률(FAR)을 계산합니다.
    /// </summary>
    /// <param name="request">면적 입력값 + 용도지역명(선택)</param>
    /// <returns>BCR·FAR 계산 결과 및 법정 상한 초과 여부</returns>
    /// <remarks>
    /// - 건폐율(BCR) = 건축면적 / 대지면적 × 100
    /// - 용적률(FAR) = 연면적 / 대지면적 × 100
    /// - zoneName을 입력하면 법정 상한값(limit)과 초과 여부(isExceeded)를 함께 반환합니다.
    /// - 법정 상한은 국토계획법 기준이며, 실제 조례 기준과 다를 수 있습니다.
    ///
    /// 샘플 요청:
    /// <code>
    /// POST /api/calculator/basic
    /// {
    ///   "siteArea": 500,
    ///   "buildingArea": 250,
    ///   "totalFloorArea": 1200,
    ///   "zoneName": "제2종일반주거지역"
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("basic")]
    [ProducesResponseType(typeof(BasicCalculatorResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult PostBasic([FromBody] BasicCalculatorRequestDto request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogDebug(
            "계산기 요청: SiteArea={S}, BuildingArea={B}, TotalFloorArea={T}, Zone={Z}",
            request.SiteArea, request.BuildingArea, request.TotalFloorArea, request.ZoneName);

        // ── 건폐율/용적률 계산 ───────────────────────────────────────
        var bcr = Math.Round(request.BuildingArea   / request.SiteArea * 100, 2);
        var far = Math.Round(request.TotalFloorArea / request.SiteArea * 100, 2);

        // ── 법정 상한 조회 ────────────────────────────────────────────
        var limits = ZoneLimitTable.GetLimit(request.ZoneName);

        double? bcrLimit = limits?.Bcr;
        double? farLimit = limits?.Far;

        var results = new List<CalculatorResultItemDto>
        {
            new()
            {
                Type       = "BCR",
                Label      = "건폐율",
                Value      = bcr,
                Limit      = bcrLimit,
                IsExceeded = bcrLimit.HasValue && bcr > bcrLimit.Value,
                Note       = bcrLimit.HasValue
                    ? "법정 상한 기준이며, 조례 실적용값은 더 낮을 수 있습니다."
                    : "용도지역을 입력하면 법정 상한과 비교할 수 있습니다.",
            },
            new()
            {
                Type       = "FAR",
                Label      = "용적률",
                Value      = far,
                Limit      = farLimit,
                IsExceeded = farLimit.HasValue && far > farLimit.Value,
                Note       = farLimit.HasValue
                    ? "법정 상한 기준이며, 조례 실적용값은 더 낮을 수 있습니다."
                    : "용도지역을 입력하면 법정 상한과 비교할 수 있습니다.",
            },
        };

        return Ok(new BasicCalculatorResponseDto { Results = results });
    }
}
