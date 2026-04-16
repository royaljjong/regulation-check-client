// =============================================================================
// RegulationCheckController.cs
// ê±´ì¶•/? ì? ë²•ê·œ ê²€??API ì»¨íŠ¸ë¡¤ëŸ¬
// ?”ë“œ?¬ì¸??ëª©ë¡:
//   POST /api/regulation-check/coordinate      - ì¢Œí‘œ ê¸°ë°˜ ë²•ê·œ 1ì°?ê²€??(MVP ?µì‹¬)
//   POST /api/regulation-check/address         - ì£¼ì†Œ/ì§€ë²??ìŠ¤?????„ë³´ ëª©ë¡ + ìµœìš°??ë²•ê·œ ê²€??//   POST /api/regulation-check/address/select  - ?¹ì • ?„ë³´ ?¸ë±??? íƒ ???´ë‹¹ ì¢Œí‘œ ë²•ê·œ ê²€??//   POST /api/regulation-check/parcel          - ì§€ë²?ì£¼ì†Œ ê¸°ë°˜ ë²•ê·œ ê²€??(?„ì¬ placeholder)
//   POST /api/regulation-check/law-layers      - ?©ë„ë³?Core/Extended/MEP ë²•ê·œ ?ˆì´??ì¡°íšŒ
//   GET  /api/regulation-check/health          - ?¬ìŠ¤ì²´í¬
// =============================================================================

using System.Diagnostics;
using System.Text;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Application.Rules;
using AutomationRawCheck.Application.Services;
using AutomationRawCheck.Application.UseProfiles;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LawClauseDict = System.Collections.Generic.IReadOnlyDictionary<string, AutomationRawCheck.Domain.Models.LawClauseResult>;

namespace AutomationRawCheck.Api.Controllers;

#region RegulationCheckController ?´ë˜??
/// <summary>
/// ê±´ì¶•/? ì? ë²•ê·œ ê²€??API ì»¨íŠ¸ë¡¤ëŸ¬?…ë‹ˆ??
/// <para>
/// ì£¼ì˜: ëª¨ë“  ê²°ê³¼??ì°¸ê³ ??1ì°??ì •?´ë©°, ?¤ì œ ê±´ì¶• ?ˆê? ?ë‹¨??ê·¼ê±°ë¡??¬ìš©?????†ìŠµ?ˆë‹¤.
/// </para>
/// </summary>
[ApiController]
[Route("api/regulation-check")]
[Produces("application/json")]
public sealed class RegulationCheckController : ControllerBase
{
    #region ?„ë“œ ë°??ì„±??
    private readonly IRegulationCheckService _service;
    private readonly IParcelSearchProvider   _parcelSearchProvider;
    private readonly IAddressResolver        _addressResolver;
    private readonly ILawClauseProvider      _clauseProvider;
    private readonly IReviewReportRenderer   _reviewReportRenderer;
    private readonly IReviewSnapshotStore    _reviewSnapshotStore;
    private readonly CsvInputAutomationService _csvInputAutomationService;
    private readonly int                     _maxClausesPerItem;
    private readonly ILogger<RegulationCheckController> _logger;

    /// <summary>RegulationCheckControllerë¥?ì´ˆê¸°?”í•©?ˆë‹¤.</summary>
    public RegulationCheckController(
        IRegulationCheckService            service,
        IParcelSearchProvider              parcelSearchProvider,
        IAddressResolver                   addressResolver,
        ILawClauseProvider                 clauseProvider,
        IReviewReportRenderer              reviewReportRenderer,
        IReviewSnapshotStore               reviewSnapshotStore,
        CsvInputAutomationService          csvInputAutomationService,
        IOptions<LawApiOptions>            lawOptions,
        ILogger<RegulationCheckController> logger)
    {
        _service              = service              ?? throw new ArgumentNullException(nameof(service));
        _parcelSearchProvider = parcelSearchProvider ?? throw new ArgumentNullException(nameof(parcelSearchProvider));
        _addressResolver      = addressResolver      ?? throw new ArgumentNullException(nameof(addressResolver));
        _clauseProvider       = clauseProvider       ?? throw new ArgumentNullException(nameof(clauseProvider));
        _reviewReportRenderer = reviewReportRenderer ?? throw new ArgumentNullException(nameof(reviewReportRenderer));
        _reviewSnapshotStore  = reviewSnapshotStore  ?? throw new ArgumentNullException(nameof(reviewSnapshotStore));
        _csvInputAutomationService = csvInputAutomationService ?? throw new ArgumentNullException(nameof(csvInputAutomationService));
        _maxClausesPerItem    = lawOptions?.Value.MaxClausesPerItem ?? 10;
        _logger               = logger               ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region POST /coordinate - ì¢Œí‘œ ê¸°ë°˜ ë²•ê·œ ê²€??(MVP ?µì‹¬ ê¸°ëŠ¥)

    /// <summary>
    /// [?µì‹¬] ì¢Œí‘œ(ê²½ë„/?„ë„) ê¸°ë°˜ ë²•ê·œ 1ì°?ê²€? ë? ?˜í–‰?©ë‹ˆ??
    /// </summary>
    /// <param name="request">WGS84 ê²½ë„/?„ë„ ì¢Œí‘œ ?”ì²­ DTO</param>
    /// <param name="ct">ì·¨ì†Œ ? í°</param>
    /// <returns>?©ë„ì§€??ë°?1ì°??ì • ê²°ê³¼ DTO</returns>
    /// <remarks>
    /// ë¡œì»¬???€?¥ëœ ?©ë„ì§€??SHP/CSV ?°ì´?°ë? ê¸°ë°˜?¼ë¡œ ì¢Œí‘œê°€ ?í•˜???©ë„ì§€??„ ?ì •?©ë‹ˆ??
    /// <br/>
    /// ë°˜í™˜ ê²°ê³¼???¤í”„?¼ì¸ ê³µê°„?°ì´??ê¸°ë°˜ ì°¸ê³ ??1ì°??ì •?…ë‹ˆ??
    /// ?¤ì œ ê±´ì¶• ?ˆê? ?ë‹¨??ê·¼ê±°ë¡??¬ìš©?????†ìŠµ?ˆë‹¤.
    /// ì§€êµ¬ë‹¨?„ê³„?? ê°œë°œ?œí•œêµ¬ì—­, ?ì¹˜ë²•ê·œ ??ì¶”ê? ê²€? ê? ?„ìš”?©ë‹ˆ??
    /// <br/>
    /// ?˜í”Œ ?”ì²­:
    /// <code>
    /// POST /api/regulation-check/coordinate
    /// { "longitude": 127.1234, "latitude": 37.1234 }
    /// </code>
    /// </remarks>
    [HttpPost("coordinate")]
    [ProducesResponseType(typeof(RegulationCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostCoordinateAsync(
        [FromBody] CoordinateRequestDto request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogInformation(
            "ì¢Œí‘œ ê¸°ë°˜ ë²•ê·œ ê²€???”ì²­: Lon={Lon}, Lat={Lat}",
            request.Longitude, request.Latitude);

        var query = new CoordinateQuery(request.Longitude, request.Latitude);
        var sw    = Stopwatch.StartNew();
        var result = await _service.CheckAsync(query, ct);
        sw.Stop();
        var response = RegulationCheckResponseDto.MapFrom(result, sw.ElapsedMilliseconds);

        return Ok(response);
    }

    #endregion

    #region POST /address - ì£¼ì†Œ/ì§€ë²??ìŠ¤??ê¸°ë°˜ ë²•ê·œ ê²€??(V-World Geocoding ?°ë™)

    /// <summary>
    /// ì£¼ì†Œ ?ëŠ” ì§€ë²??ìŠ¤?¸ë? ì¢Œí‘œë¡?ë³€?˜í•œ ??ë²•ê·œ 1ì°?ê²€? ë? ?˜í–‰?©ë‹ˆ??
    /// </summary>
    /// <param name="request">ì£¼ì†Œ/ì§€ë²??ìŠ¤???”ì²­ DTO</param>
    /// <param name="ct">ì·¨ì†Œ ? í°</param>
    /// <returns>Geocoding ê²°ê³¼(?•ê·œ??ì£¼ì†Œ, ì¢Œí‘œ)?€ ë²•ê·œ ê²€??ê²°ê³¼ë¥??¬í•¨???‘ë‹µ DTO</returns>
    /// <remarks>
    /// ì²˜ë¦¬ ?ë¦„:
    /// <list type="number">
    ///   <item>V-World ì£¼ì†Œ ì¢Œí‘œ ê²€??APIë¡?ì£¼ì†Œ ??WGS84 ì¢Œí‘œ ë³€??(?„ë¡œëª??°ì„ , ë¯¸ë°œê²???ì§€ë²??¬ì‹œ??</item>
    ///   <item>ë³€?˜ëœ ì¢Œí‘œë¡?ê¸°ì¡´ coordinate ?”ì§„ ?¸ì¶œ</item>
    ///   <item>Geocoding ?•ë³´(?•ê·œ??ì£¼ì†Œ, ì¢Œí‘œ, ?„ë³´ ???€ ë²•ê·œ ê²€??ê²°ê³¼ë¥??©ì³ ë°˜í™˜</item>
    /// </list>
    /// ë³µìˆ˜ ?„ë³´ ì²˜ë¦¬: V-Worldê°€ ë°˜í™˜??ì²?ë²ˆì§¸(ìµœìš°?? ê²°ê³¼ë¥??¬ìš©?©ë‹ˆ?? candidateCount?€ candidateNoteë¡??„ë³´ ?˜ë? ?•ì¸?????ˆìŠµ?ˆë‹¤.
    /// <br/>
    /// ?˜í”Œ ?”ì²­:
    /// <code>
    /// POST /api/regulation-check/address
    /// { "query": "?œìš¸?¹ë³„??ê°•ë‚¨êµ??ë™?€ë¡?513" }
    /// </code>
    /// </remarks>
    [HttpPost("address")]
    [ProducesResponseType(typeof(AddressCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostAddressAsync(
        [FromBody] AddressCheckRequestDto request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogInformation("ì£¼ì†Œ ê¸°ë°˜ ë²•ê·œ ê²€???”ì²­: {Query}", request.Query);

        // ?€?€ 1?¨ê³„: ì£¼ì†Œ ??ì¢Œí‘œ ?„ë³´ ëª©ë¡ ë³€???€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var candidates = await _addressResolver.ResolveAsync(request.Query, ct);

        if (candidates.Count == 0)
        {
            _logger.LogWarning("ì£¼ì†Œ ì¢Œí‘œ ë³€???¤íŒ¨: {Query}", request.Query);
            return NotFound(new
            {
                status     = 404,
                title      = "ì£¼ì†Œë¥?ì°¾ì„ ???†ìŠµ?ˆë‹¤",
                detail     = $"?…ë ¥?˜ì‹  ì£¼ì†Œ \"{request.Query}\"???´ë‹¹?˜ëŠ” ì¢Œí‘œë¥?ì°¾ì? ëª»í–ˆ?µë‹ˆ?? " +
                             "?„ë¡œëª?ì£¼ì†Œ ?ëŠ” ì§€ë²?ì£¼ì†Œë¡??¤ì‹œ ?œë„?˜ê±°?? " +
                             "????êµ??™ê¹Œì§€ ?¬í•¨???„ì²´ ì£¼ì†Œë¡??…ë ¥??ì£¼ì„¸??",
                inputQuery = request.Query
            });
        }

        // ?€?€ 2?¨ê³„: ìµœìš°???„ë³´(0ë²?ë¡?ë²•ê·œ ê²€??(ê¸°ì¡´ ?”ì§„ ?¬ì‚¬?? ?€?€?€?€?€?€?€?€?€?€?€?€
        var selected   = candidates[0];
        var sw1        = Stopwatch.StartNew();
        var regulationResult = await _service.CheckAsync(selected.Coordinate, ct);
        sw1.Stop();
        var regulationDto    = RegulationCheckResponseDto.MapFrom(regulationResult, sw1.ElapsedMilliseconds);

        // ?€?€ 3?¨ê³„: ?‘ë‹µ ì¡°ë¦½ ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var candidateNote = candidates.Count > 1
            ? $"{candidates.Count}ê°??„ë³´ê°€ ?•ì¸?˜ì—ˆ?µë‹ˆ?? " +
              "ìµœìš°???„ë³´(candidates[0])ë¥?ê¸°ì??¼ë¡œ ë²•ê·œ ê²€? ë? ?˜í–‰?ˆìŠµ?ˆë‹¤. " +
              "?¤ë¥¸ ?„ì¹˜ë¥??•ì¸?˜ë ¤ë©?candidates ëª©ë¡?ì„œ ?í•˜??ì¢Œí‘œë¥?" +
              "POST /api/regulation-check/coordinate??ì§ì ‘ ?”ì²­?˜ì„¸??"
            : null;

        var candidateDtos = candidates
            .Select(c => new AddressCandidateDto
            {
                Address     = c.NormalizedAddress ?? request.Query,
                Latitude    = c.Coordinate.Latitude,
                Longitude   = c.Coordinate.Longitude,
                AddressType = c.AddressType ?? string.Empty
            })
            .ToList();

        var response = new AddressCheckResponseDto
        {
            InputQuery        = request.Query,
            Candidates        = candidateDtos,
            Selected          = candidateDtos[0],
            GeocodingProvider = selected.Provider,
            CandidateCount    = candidates.Count,
            CandidateNote     = candidateNote,
            RegulationResult  = regulationDto
        };

        return Ok(response);
    }

    #endregion

    #region POST /address/select - ?¹ì • ?„ë³´ ?¸ë±??? íƒ ???•ì • ë²•ê·œ ê²€??
    /// <summary>
    /// ì£¼ì†Œ ?„ë³´ ëª©ë¡?ì„œ ?¹ì • ?¸ë±?¤ë? ? íƒ???´ë‹¹ ì¢Œí‘œë¡?ë²•ê·œ ê²€? ë? ?˜í–‰?©ë‹ˆ??
    /// </summary>
    /// <param name="request">ì£¼ì†Œ ?ìŠ¤??+ ? íƒ???„ë³´ ?¸ë±??(0-ê¸°ë°˜)</param>
    /// <param name="ct">ì·¨ì†Œ ? í°</param>
    /// <returns>? íƒ???„ë³´ ?•ë³´?€ ?´ë‹¹ ì¢Œí‘œ ê¸°ë°˜ ë²•ê·œ ê²€??ê²°ê³¼</returns>
    /// <remarks>
    /// ?¼ë°˜?ì¸ ?¬ìš© ?ë¦„:
    /// <list type="number">
    ///   <item>POST /address ??candidates ëª©ë¡ ?•ì¸ (candidateCount, candidateNote ?¬í•¨)</item>
    ///   <item>?í•˜???„ë³´ ?¸ë±???•ì¸</item>
    ///   <item>POST /address/select ???´ë‹¹ ?¸ë±??ê¸°ì? ?•ì • ë²•ê·œ ê²€??/item>
    /// </list>
    /// candidateIndexê°€ candidates.Count ë²”ìœ„ë¥?ì´ˆê³¼?˜ë©´ 400??ë°˜í™˜?©ë‹ˆ??
    /// <br/>
    /// ?˜í”Œ ?”ì²­:
    /// <code>
    /// POST /api/regulation-check/address/select
    /// { "query": "?œìš¸ ê°•ë‚¨êµ??ë™?€ë¡?513", "candidateIndex": 1 }
    /// </code>
    /// </remarks>
    [HttpPost("address/select")]
    [ProducesResponseType(typeof(AddressSelectResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostAddressSelectAsync(
        [FromBody] AddressSelectRequestDto request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogInformation(
            "ì£¼ì†Œ ?„ë³´ ? íƒ ë²•ê·œ ê²€???”ì²­: Query={Query}, CandidateIndex={Index}",
            request.Query, request.CandidateIndex);

        // ?€?€ 1?¨ê³„: ì£¼ì†Œ ???„ë³´ ëª©ë¡ ì¡°íšŒ ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var candidates = await _addressResolver.ResolveAsync(request.Query, ct);

        if (candidates.Count == 0)
        {
            _logger.LogWarning("ì£¼ì†Œ ì¢Œí‘œ ë³€???¤íŒ¨ (select): {Query}", request.Query);
            return NotFound(new
            {
                status     = 404,
                title      = "ì£¼ì†Œë¥?ì°¾ì„ ???†ìŠµ?ˆë‹¤",
                detail     = $"?…ë ¥?˜ì‹  ì£¼ì†Œ \"{request.Query}\"???´ë‹¹?˜ëŠ” ì¢Œí‘œë¥?ì°¾ì? ëª»í–ˆ?µë‹ˆ?? " +
                             "?„ë¡œëª?ì£¼ì†Œ ?ëŠ” ì§€ë²?ì£¼ì†Œë¡??¤ì‹œ ?œë„??ì£¼ì„¸??",
                inputQuery = request.Query
            });
        }

        // ?€?€ 2?¨ê³„: ?¸ë±??ë²”ìœ„ ê²€ì¦??€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        if (request.CandidateIndex >= candidates.Count)
        {
            _logger.LogWarning(
                "?„ë³´ ?¸ë±??ë²”ìœ„ ì´ˆê³¼: Index={Index}, CandidateCount={Count}, Query={Query}",
                request.CandidateIndex, candidates.Count, request.Query);
            return BadRequest(new
            {
                status         = 400,
                title          = "?„ë³´ ?¸ë±??ë²”ìœ„ ì´ˆê³¼",
                detail         = $"candidateIndex={request.CandidateIndex}?€ ? íš¨?˜ì? ?ŠìŠµ?ˆë‹¤. " +
                                 $"? íš¨ ë²”ìœ„: 0 ~ {candidates.Count - 1} ({candidates.Count}ê±??„ë³´)",
                candidateCount = candidates.Count,
                inputQuery     = request.Query
            });
        }

        // ?€?€ 3?¨ê³„: ? íƒ???„ë³´ë¡?ë²•ê·œ ê²€???€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var selected         = candidates[request.CandidateIndex];
        var sw2              = Stopwatch.StartNew();
        var regulationResult = await _service.CheckAsync(selected.Coordinate, ct);
        sw2.Stop();
        var regulationDto    = RegulationCheckResponseDto.MapFrom(regulationResult, sw2.ElapsedMilliseconds);

        var selectedDto = new AddressCandidateDto
        {
            Address     = selected.NormalizedAddress ?? request.Query,
            Latitude    = selected.Coordinate.Latitude,
            Longitude   = selected.Coordinate.Longitude,
            AddressType = selected.AddressType ?? string.Empty
        };

        _logger.LogInformation(
            "?„ë³´ ? íƒ ë²•ê·œ ê²€???„ë£Œ: Index={Index}/{Total}, Address={Address}, " +
            "Lon={Lon}, Lat={Lat}",
            request.CandidateIndex, candidates.Count, selectedDto.Address,
            selectedDto.Longitude, selectedDto.Latitude);

        return Ok(new AddressSelectResponseDto
        {
            InputQuery        = request.Query,
            CandidateIndex    = request.CandidateIndex,
            CandidateCount    = candidates.Count,
            SelectedCandidate = selectedDto,
            GeocodingProvider = selected.Provider,
            RegulationResult  = regulationDto
        });
    }

    #endregion

    #region POST /parcel - ì§€ë²?ì£¼ì†Œ ê¸°ë°˜ ë²•ê·œ ê²€??(?„ì¬ placeholder)

    /// <summary>
    /// [Placeholder] ì§€ë²??ëŠ” ?„ë¡œëª?ì£¼ì†Œ ê¸°ë°˜ ë²•ê·œ ê²€? ë? ?˜í–‰?©ë‹ˆ??
    /// </summary>
    /// <param name="request">ì§€ë²??„ë¡œëª?ì£¼ì†Œ ?ëŠ” ì¢Œí‘œ ?”ì²­ DTO</param>
    /// <param name="ct">ì·¨ì†Œ ? í°</param>
    /// <returns>?©ë„ì§€??ë°?1ì°??ì • ê²°ê³¼ DTO ?ëŠ” ë¯¸êµ¬???ˆë‚´ ë©”ì‹œì§€</returns>
    /// <remarks>
    /// MVP ?¨ê³„?ì„œ ì§€?í•˜??searchType:
    /// <list type="bullet">
    ///   <item><term>Coordinate</term><description>ì¢Œí‘œ ì§ì ‘ ?…ë ¥ ???¤ì œ ?ì • ?˜í–‰</description></item>
    ///   <item><term>JibunAddress</term><description>ì§€ë²?ì£¼ì†Œ ?ìŠ¤?????„ì¬ placeholder (ì£¼ì†Œ ë³€???œë¹„??ë¯¸ì—°??</description></item>
    ///   <item><term>RoadAddress</term><description>?„ë¡œëª?ì£¼ì†Œ ?ìŠ¤?????„ì¬ placeholder (ì£¼ì†Œ ë³€???œë¹„??ë¯¸ì—°??</description></item>
    /// </list>
    /// TODO: ?¸ë? ì£¼ì†Œ ê²€??API(VWorld ?? ?°ë™ ??JibunAddress/RoadAddress ?œì„±???ˆì •.
    /// <br/>
    /// ?˜í”Œ ?”ì²­ (ì¢Œí‘œ ?€??:
    /// <code>
    /// POST /api/regulation-check/parcel
    /// { "searchType": "Coordinate", "longitude": 127.1234, "latitude": 37.1234 }
    /// </code>
    /// ?˜í”Œ ?”ì²­ (ì§€ë²?ì£¼ì†Œ ?€??- ?„ì¬ placeholder):
    /// <code>
    /// POST /api/regulation-check/parcel
    /// { "searchType": "JibunAddress", "addressText": "ê²½ê¸°???±ë‚¨??ë¶„ë‹¹êµ??•ì??1-1" }
    /// </code>
    /// </remarks>
    [HttpPost("parcel")]
    [ProducesResponseType(typeof(RegulationCheckResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostParcelAsync(
        [FromBody] ParcelSearchRequestDto request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var domain = request.ToDomain();

        _logger.LogInformation(
            "ì§€ë²?ì£¼ì†Œ ë²•ê·œ ê²€???”ì²­: SearchType={Type}, Address={Addr}, Coord={Coord}",
            domain.SearchType, domain.AddressText, domain.Coordinate);

        // ?€?€ ì¼€?´ìŠ¤ 1: ì¢Œí‘œ ì§ì ‘ ?…ë ¥ ???¤ì œ ?ì • ?˜í–‰ ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        if (domain.SearchType == ParcelSearchType.Coordinate && domain.Coordinate is not null)
        {
            var swP1 = Stopwatch.StartNew();
            var result = await _service.CheckAsync(domain.Coordinate, ct);
            swP1.Stop();
            return Ok(RegulationCheckResponseDto.MapFrom(result, swP1.ElapsedMilliseconds));
        }

        // ?€?€ ì¼€?´ìŠ¤ 2: ì£¼ì†Œ ?ìŠ¤????ì¢Œí‘œ ë³€???œë„ ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        if (!string.IsNullOrWhiteSpace(domain.AddressText))
        {
            var coord = await _parcelSearchProvider.ResolveAddressAsync(domain.AddressText, ct);

            if (coord is not null)
            {
                // ì£¼ì†Œ ë³€???±ê³µ ??ì¢Œí‘œë¡??ì •
                var swP2 = Stopwatch.StartNew();
                var result = await _service.CheckAsync(coord, ct);
                swP2.Stop();
                return Ok(RegulationCheckResponseDto.MapFrom(result, swP2.ElapsedMilliseconds));
            }

            // ì£¼ì†Œ ë³€???¤íŒ¨ (stub ?ëŠ” API ë¯¸ì—°?? ??501 ?‘ë‹µ
            _logger.LogWarning("ì£¼ì†Œ ë³€???¤íŒ¨ ?ëŠ” ë¯¸êµ¬?? {Addr}", domain.AddressText);
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                status = 501,
                title = "ÁÖ¼Ò °Ë»ö ¹Ì±¸Çö",
                detail =
                    "ì§€ë²??„ë¡œëª?ì£¼ì†Œ ê¸°ë°˜ ê²€?‰ì? ?„ì¬ MVP ?¨ê³„?ì„œ ì§€?ë˜ì§€ ?ŠìŠµ?ˆë‹¤. " +
                    "ì¢Œí‘œ(Coordinate) ?€?…ìœ¼ë¡??”ì²­?˜ê±°?? " +
                    "?¥í›„ ì£¼ì†Œ ê²€??API ?°ë™ ???´ìš©?´ì£¼?¸ìš”.",
                searchType = domain.SearchType.ToString(),
                addressText = domain.AddressText
            });
        }

        // ?€?€ ì¼€?´ìŠ¤ 3: ?…ë ¥ê°?ë¶€ì¡??€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        return BadRequest(new ValidationProblemDetails
        {
            Title = "?”ì²­ ê°??¤ë¥˜",
            Detail = "Coordinate ?€?…ì´ë©?longitude/latitudeë¥??…ë ¥?˜ì„¸?? " +
                     "JibunAddress/RoadAddress ?€?…ì´ë©?addressTextë¥??…ë ¥?˜ì„¸??"
        });
    }

    #endregion

    #region POST /review-items - ê³„íš ?©ë„ ê¸°ë°˜ ê²€????ª© ì¡°íšŒ

    /// <summary>
    /// ê³µê°„ ?ì • ê²°ê³¼?€ ê³„íš ?©ë„ë¥??…ë ¥ë°›ì•„ ê²€? í•´??????ª© ëª©ë¡??ë°˜í™˜?©ë‹ˆ??
    /// </summary>
    /// <param name="request">?©ë„ì§€??·ì˜¤ë²„ë ˆ???ì • ê²°ê³¼ + ê³„íš ?©ë„</param>
    /// <returns>ì¹´í…Œê³ ë¦¬ë³?ê²€????ª© ëª©ë¡</returns>
    /// <remarks>
    /// ì§€???©ë„: ê³µë™ì£¼íƒ | ??ì¢…ê·¼ë¦°ìƒ?œì‹œ??| ??ì¢…ê·¼ë¦°ìƒ?œì‹œ??| ?…ë¬´?œì„¤
    /// <br/>
    /// isAutoCheckable=false ??ª©?€ ë©´ì Â·ì¸µìˆ˜Â·ì¡°ë? ??ì¶”ê? ?•ë³´ê°€ ?ˆì–´???ë‹¨ ê°€?¥í•œ ?˜ë™ ê²€????ª©?…ë‹ˆ??
    /// <br/>
    /// ?˜í”Œ ?”ì²­:
    /// <code>
    /// POST /api/regulation-check/review-items
    /// {
    ///   "zoneName": "??ì¢…ì¼ë°˜ì£¼ê±°ì???,
    ///   "districtUnitPlanIsInside": false,
    ///   "developmentRestrictionIsInside": false,
    ///   "selectedUse": "ê³µë™ì£¼íƒ"
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("review-items")]
    [ProducesResponseType(typeof(ReviewItemsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostReviewItems(
        [FromBody]  ReviewItemsRequestDto request,
        [FromQuery] bool                  includeLegalBasis = false,
        CancellationToken                 ct                = default)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedUse))
            return BadRequest(new { error = "selectedUse???„ìˆ˜?…ë‹ˆ??" });

        var supportedUses = UseProfileRegistry.SupportedDisplayNames;
        if (!UseProfileRegistry.IsSupported(request.SelectedUse))
            return BadRequest(new
            {
                error        = $"ì§€?í•˜ì§€ ?ŠëŠ” ?©ë„?…ë‹ˆ?? {request.SelectedUse}",
                supportedUses
            });

        _logger.LogDebug(
            "ê²€????ª© ì¡°íšŒ: Use={Use}, Zone={Zone}, DUP={Dup}, DRP={Drp}, Clauses={Cls}",
            request.SelectedUse, request.ZoneName,
            request.DistrictUnitPlanIsInside, request.DevelopmentRestrictionIsInside,
            includeLegalBasis);

        var sw = Stopwatch.StartNew();
        List<ReviewItemDto> reviewItems;

        if (!includeLegalBasis)
        {
            // ê¸°ë³¸ ê²½ë¡œ (ê¸°ì¡´ê³??™ì¼)
            reviewItems = ReviewItemRuleTable.GetReviewItems(
                request.SelectedUse,
                request.ZoneName,
                request.DistrictUnitPlanIsInside,
                request.DevelopmentRestrictionIsInside);
            sw.Stop();
            _logger.LogInformation(
                "review-items ê¸°ë³¸ ?‘ë‹µ ?„ë£Œ: Use={Use}, items={Count}, elapsed={Elapsed}ms",
                request.SelectedUse, reviewItems.Count, sw.ElapsedMilliseconds);
        }
        else
        {
            // ?•ì¥ ê²½ë¡œ ??ì¡°ë¬¸ ?ìŠ¤???¬í•¨
            var rawRules = ReviewItemRuleTable.GetReviewItemsRaw(
                request.SelectedUse,
                request.ZoneName,
                request.DistrictUnitPlanIsInside,
                request.DevelopmentRestrictionIsInside);

            var allKeys = rawRules
                .SelectMany(r => r.LegalBasis.Select(lb => lb.NormalizedKey))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _logger.LogDebug(
                "review-items ì¡°ë¬¸ ?¼ê´„ ì¡°íšŒ ?œì‘: Use={Use}, uniqueKeys={Keys}",
                request.SelectedUse, allKeys.Count);

            var clauseDict = await _clauseProvider
                .GetClausesAsync(allKeys, ct)
                .ConfigureAwait(false);

            reviewItems = rawRules
                .Select(r => ToReviewItemDto(r, clauseDict, _maxClausesPerItem))
                .ToList();

            sw.Stop();
            _logger.LogInformation(
                "review-items ?•ì¥ ?‘ë‹µ ?„ë£Œ: Use={Use}, items={Count}, clauseKeys={Keys}, clauseHit={Hit}, elapsed={Elapsed}ms",
                request.SelectedUse, reviewItems.Count, allKeys.Count, clauseDict.Count, sw.ElapsedMilliseconds);
        }

        return Ok(new ReviewItemsResponseDto
        {
            SelectedUse = request.SelectedUse,
            ZoneName    = request.ZoneName,
            ReviewItems = reviewItems,
        });
    }

    #endregion

    #region POST /law-layers - ?©ë„ë³?ë²•ê·œ ?ˆì´??ì¡°íšŒ

    /// <summary>
    /// ê³„íš ?©ë„?€ ?¤ë²„?ˆì´ ?ì • ê²°ê³¼ë¥??…ë ¥ë°›ì•„ 3ê°?ë²•ê·œ ?ˆì´?´ë? ë°˜í™˜?©ë‹ˆ??
    /// </summary>
    /// <param name="request">ê³„íš ?©ë„ + ?¤ë²„?ˆì´ ?¬ë? DTO</param>
    /// <returns>Core / Extended Core / MEP 3ê°??ˆì´??ë²•ê·œ ëª©ë¡</returns>
    /// <remarks>
    /// - Core Layer        : ê±´ì¶• ê¸°ë³¸ ë²•ê·œ (?©ë„Â·êµ¬ì¡°Â·?¼ë‚œÂ·ë°€??
    /// - Extended Core     : ê±´ì¶• ?„ìˆ˜ ?°ê³„ ë²•ê·œ (?Œë°©Â·?´ì§„Â·?ë„ˆì§€Â·?„ìƒ)
    /// - MEP Layer         : ?‘ë ¥??ë²•ê·œ (?„ê¸°Â·ê¸°ê³„Â·?Œë°©Â·?„ìƒ) ???ë™ ?ì • ë¶ˆê?, ?°ê³„ ê²€???„ìš”
    ///
    /// ì¡°ê±´ë¶€ ??ª©:
    /// - districtUnitPlanIsInside=true ??Core ì²???ª©?¼ë¡œ "ì§€êµ¬ë‹¨?„ê³„??ê°œë³„ ì§€ì¹? ?½ì…
    /// - developmentRestrictionIsInside=true ??ExtendedCore ì²???ª©?¼ë¡œ "ê°œë°œ?œí•œêµ¬ì—­" ê²½ê³  ?½ì…
    ///
    /// ?˜í”Œ ?”ì²­:
    /// <code>
    /// POST /api/regulation-check/law-layers
    /// {
    ///   "selectedUse": "ê³µë™ì£¼íƒ",
    ///   "districtUnitPlanIsInside": false,
    ///   "developmentRestrictionIsInside": false
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("law-layers")]
    [ProducesResponseType(typeof(LawLayersResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostLawLayers(
        [FromBody]  LawLayersRequestDto request,
        [FromQuery] bool                includeLegalBasis = false,
        CancellationToken               ct                = default)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedUse))
            return BadRequest(new { error = "selectedUse???„ìˆ˜?…ë‹ˆ??" });

        if (!UseProfileRegistry.IsSupported(request.SelectedUse))
            return BadRequest(new
            {
                error         = $"ì§€?í•˜ì§€ ?ŠëŠ” ?©ë„?…ë‹ˆ?? {request.SelectedUse}",
                supportedUses = UseProfileRegistry.SupportedDisplayNames,
            });

        _logger.LogDebug(
            "ë²•ê·œ ?ˆì´??ì¡°íšŒ: Use={Use}, DUP={Dup}, DRP={Drp}, Clauses={Cls}",
            request.SelectedUse,
            request.DistrictUnitPlanIsInside,
            request.DevelopmentRestrictionIsInside,
            includeLegalBasis);

        var swL = Stopwatch.StartNew();
        List<CoreLawItemDto> core, extendedCore;
        List<MepLawItemDto>  mep;

        if (!includeLegalBasis)
        {
            // ê¸°ë³¸ ê²½ë¡œ (ê¸°ì¡´ê³??™ì¼)
            (core, extendedCore, mep) = LawLayerRuleTable.GetLayers(
                request.SelectedUse,
                request.DistrictUnitPlanIsInside,
                request.DevelopmentRestrictionIsInside);
            swL.Stop();
            _logger.LogInformation(
                "law-layers ê¸°ë³¸ ?‘ë‹µ ?„ë£Œ: Use={Use}, core={C}, ext={E}, mep={M}, elapsed={Elapsed}ms",
                request.SelectedUse, core.Count, extendedCore.Count, mep.Count, swL.ElapsedMilliseconds);
        }
        else
        {
            // ?•ì¥ ê²½ë¡œ ??ì¡°ë¬¸ ?ìŠ¤???¬í•¨
            var rawLayers = LawLayerRuleTable.GetLayersRaw(
                request.SelectedUse,
                request.DistrictUnitPlanIsInside,
                request.DevelopmentRestrictionIsInside);

            var allKeys = rawLayers.Core
                .Concat(rawLayers.ExtendedCore)
                .Concat(rawLayers.Mep)
                .SelectMany(r => r.LegalBasis.Select(lb => lb.NormalizedKey))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _logger.LogDebug(
                "law-layers ì¡°ë¬¸ ?¼ê´„ ì¡°íšŒ ?œì‘: Use={Use}, uniqueKeys={Keys}",
                request.SelectedUse, allKeys.Count);

            var clauseDict = await _clauseProvider
                .GetClausesAsync(allKeys, ct)
                .ConfigureAwait(false);

            core         = rawLayers.Core.Select(r => ToCoreDtoWithClauses(r, clauseDict, _maxClausesPerItem)).ToList();
            extendedCore = rawLayers.ExtendedCore.Select(r => ToCoreDtoWithClauses(r, clauseDict, _maxClausesPerItem)).ToList();
            mep          = rawLayers.Mep.Select(r => ToMepDtoWithClauses(r, clauseDict, _maxClausesPerItem)).ToList();

            swL.Stop();
            _logger.LogInformation(
                "law-layers ?•ì¥ ?‘ë‹µ ?„ë£Œ: Use={Use}, core={C}, ext={E}, mep={M}, clauseKeys={Keys}, clauseHit={Hit}, elapsed={Elapsed}ms",
                request.SelectedUse, core.Count, extendedCore.Count, mep.Count,
                allKeys.Count, clauseDict.Count, swL.ElapsedMilliseconds);
        }

        return Ok(new LawLayersResponseDto
        {
            SelectedUse      = request.SelectedUse,
            CoreLaws         = core,
            ExtendedCoreLaws = extendedCore,
            MepLaws          = mep,
        });
    }

    #endregion

    #region POST /review - ?µí•© ê²€???”ë“œ?¬ì¸??(reviewLevel + buildingInputs ê¸°ë°˜ ?ì •)

    /// <summary>
    /// ì£¼ì†Œ ?ëŠ” ì¢Œí‘œ + ê³„íš ?©ë„ + ê±´ë¬¼ ê·œëª¨ ?…ë ¥??ë°›ì•„
    /// ?©ë„ì§€???ë™ ?ì • + ?¨ê³„ë³?ë²•ê·œ ê²€????ª© ?ì • + ?¤ìŒ ?¨ê³„ ?ŒíŠ¸ë¥??µí•© ë°˜í™˜?©ë‹ˆ??
    /// </summary>
    /// <param name="request">?µí•© ê²€???”ì²­ DTO</param>
    /// <param name="ct">ì·¨ì†Œ ? í°</param>
    /// <returns>?µí•© ê²€??ê²°ê³¼ DTO</returns>
    /// <remarks>
    /// reviewLevel???ëµ?˜ë©´ buildingInputs ?œê³µ ?„ë“œ ê¸°ë°˜?¼ë¡œ ?ë™ ì¶”ë¡ ?©ë‹ˆ??
    /// <list type="bullet">
    ///   <item>?…ë ¥ ?†ìŒ ??quick</item>
    ///   <item>floorArea/floorCount/siteArea ì¤?1ê°??´ìƒ ??standard</item>
    ///   <item>unitCount/detailUseSubtype/officeSubtype ì¤?1ê°??´ìƒ ??detailed</item>
    /// </list>
    /// ?˜í”Œ ?”ì²­ (quick):
    /// <code>
    /// POST /api/regulation-check/review
    /// { "address": "?œìš¸??ë§ˆí¬êµ??”ë“œì»µë¶ë¡?396", "selectedUse": "ê³µë™ì£¼íƒ" }
    /// </code>
    /// ?˜í”Œ ?”ì²­ (standard):
    /// <code>
    /// POST /api/regulation-check/review
    /// {
    ///   "address": "?œìš¸??ë§ˆí¬êµ??”ë“œì»µë¶ë¡?396",
    ///   "selectedUse": "ê³µë™ì£¼íƒ",
    ///   "buildingInputs": { "floorArea": 12000, "floorCount": 18, "siteArea": 2000 }
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("review")]
    [ProducesResponseType(typeof(BuildingReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReviewAsync(
        [FromBody] BuildingReviewRequestDto request,
        CancellationToken                   ct)
    {
        var (errorResult, response) = await ExecuteReviewAsync(request, ct);
        if (errorResult is not null)
            return errorResult;

        return Ok(response);
    }

    [HttpPost("review/report-package")]
    [ProducesResponseType(typeof(BuildingReviewReportPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReviewReportPackageAsync(
        [FromBody] BuildingReviewRequestDto request,
        CancellationToken                   ct)
    {
        var (errorResult, response, package) = await ExecuteReviewPackageAsync(request, ct);
        if (errorResult is not null)
            return errorResult;

        ArgumentNullException.ThrowIfNull(package);
        return Ok(package);
    }

    [HttpPost("review/report-export")]
    [ProducesResponseType(typeof(ReviewReportExportPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReviewReportExportAsync(
        [FromBody] ReviewReportExportRequestDto request,
        CancellationToken                       ct)
    {
        if (request.ReviewRequest is null)
            return BadRequest(new { error = "reviewRequest???„ìˆ˜?…ë‹ˆ??" });

        var (errorResult, response, package) = await ExecuteReviewPackageAsync(request.ReviewRequest, ct);
        if (errorResult is not null)
            return errorResult;

        ArgumentNullException.ThrowIfNull(package);
        var exportPlan = _reviewReportRenderer.BuildExportPlan(package, request.Format);
        return Ok(exportPlan);
    }

    [HttpPost("review/report-render")]
    [ProducesResponseType(typeof(ReviewReportRenderResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReviewReportRenderAsync(
        [FromBody] ReviewReportExportRequestDto request,
        CancellationToken                       ct)
    {
        if (request.ReviewRequest is null)
            return BadRequest(new { error = "reviewRequest???„ìˆ˜?…ë‹ˆ??" });

        var (errorResult, response, package) = await ExecuteReviewPackageAsync(request.ReviewRequest, ct);
        if (errorResult is not null)
            return errorResult;

        ArgumentNullException.ThrowIfNull(package);
        var renderResult = _reviewReportRenderer.BuildRenderResult(package, request.Format);
        return Ok(renderResult);
    }

    [HttpPost("review/report-markdown")]
    [Produces("text/markdown")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReviewReportMarkdownAsync(
        [FromBody] BuildingReviewRequestDto request,
        CancellationToken                   ct)
    {
        var (errorResult, response, package) = await ExecuteReviewPackageAsync(request, ct);
        if (errorResult is not null)
            return errorResult;

        ArgumentNullException.ThrowIfNull(package);
        var artifact = _reviewReportRenderer.BuildMarkdownArtifact(package);
        var bytes = Encoding.UTF8.GetBytes(artifact.Text);
        return File(bytes, artifact.MimeType, artifact.SuggestedFileName);
    }

    [HttpPost("review/snapshots")]
    [ProducesResponseType(typeof(ReviewSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReviewSnapshotAsync(
        [FromBody] BuildingReviewRequestDto request,
        CancellationToken                   ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectContext?.ProjectId))
            return BadRequest(new { error = "projectContext.projectId???¤ëƒ…???€?¥ì— ?„ìˆ˜?…ë‹ˆ??" });

        var (errorResult, response, package) = await ExecuteReviewPackageAsync(request, ct);
        if (errorResult is not null)
            return errorResult;

        ArgumentNullException.ThrowIfNull(package);
        var snapshot = _reviewSnapshotStore.Save(request, response!, package);
        return Ok(snapshot);
    }

    [HttpGet("review/snapshots/{snapshotId}")]
    [ProducesResponseType(typeof(ReviewSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewSnapshot(string snapshotId)
    {
        var snapshot = _reviewSnapshotStore.Get(snapshotId);
        if (snapshot is null)
            return NotFound(new { error = $"snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤: {snapshotId}" });

        return Ok(snapshot);
    }

    [HttpPost("review/snapshots/{snapshotId}/replay")]
    [ProducesResponseType(typeof(BuildingReviewResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReviewSnapshotReplayAsync(
        string snapshotId,
        CancellationToken ct)
    {
        var snapshot = _reviewSnapshotStore.Get(snapshotId);
        if (snapshot is null)
            return NotFound(new { error = $"snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤: {snapshotId}" });

        var (errorResult, response) = await ExecuteReviewAsync(snapshot.Request, ct);
        if (errorResult is not null)
            return errorResult;

        return Ok(response);
    }

    [HttpGet("review/projects/{projectId}/history")]
    [ProducesResponseType(typeof(List<ReviewSnapshotSummaryDto>), StatusCodes.Status200OK)]
    public IActionResult GetReviewProjectHistory(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId???„ìˆ˜?…ë‹ˆ??" });

        return Ok(_reviewSnapshotStore.ListByProject(projectId));
    }

    [HttpGet("review/projects/{projectId}/workspace-summary")]
    [ProducesResponseType(typeof(ReviewProjectWorkspaceSummaryDto), StatusCodes.Status200OK)]
    public IActionResult GetReviewProjectWorkspaceSummary(
        string projectId,
        [FromQuery] string? scenarioId = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId is required." });

        var snapshots = _reviewSnapshotStore.ListByProject(projectId)
            .Where(snapshot => string.IsNullOrWhiteSpace(scenarioId) || string.Equals(snapshot.ScenarioId, scenarioId, StringComparison.Ordinal))
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToList();

        var compareArchives = _reviewSnapshotStore.ListCompareReportsByProject(projectId, scenarioId)
            .OrderByDescending(archive => archive.CreatedAt)
            .ToList();

        var baseline = _reviewSnapshotStore.GetBaselineByProject(projectId, scenarioId);
        var latest = _reviewSnapshotStore.GetLatestByProject(projectId, scenarioId);

        var summaryLines = new List<string>
        {
            $"Snapshots: {snapshots.Count}",
            $"Compare archives: {compareArchives.Count}",
            baseline is null ? "Baseline: not set" : $"Baseline: {baseline.SnapshotId}",
            latest is null ? "Latest: not available" : $"Latest: {latest.SnapshotId}",
        };

        if (baseline is not null && latest is not null)
        {
            summaryLines.Add(
                string.Equals(baseline.SnapshotId, latest.SnapshotId, StringComparison.Ordinal)
                    ? "Baseline matches the latest snapshot."
                    : "Baseline differs from the latest snapshot.");
        }

        return Ok(new ReviewProjectWorkspaceSummaryDto
        {
            ProjectId = projectId,
            ScenarioId = scenarioId,
            Baseline = baseline,
            Latest = latest,
            SnapshotCount = snapshots.Count,
            CompareArchiveCount = compareArchives.Count,
            RecentSnapshots = snapshots.Take(5).ToList(),
            RecentCompareArchives = compareArchives.Take(5).ToList(),
            SummaryLines = summaryLines,
        });
    }

    [HttpGet("review/projects/{projectId}/latest")]
    [ProducesResponseType(typeof(ReviewSnapshotSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewProjectLatest(
        string projectId,
        [FromQuery] string? scenarioId = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId???„ìˆ˜?…ë‹ˆ??" });

        var latest = _reviewSnapshotStore.GetLatestByProject(projectId, scenarioId);
        if (latest is null)
            return NotFound(new { error = "ìµœì‹  snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤.", projectId, scenarioId });

        return Ok(latest);
    }

    [HttpPost("review/compare")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompare(
        [FromBody] ReviewSnapshotCompareRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.LeftSnapshotId) ||
            string.IsNullOrWhiteSpace(request.RightSnapshotId))
            return BadRequest(new { error = "leftSnapshotId?€ rightSnapshotId???„ìˆ˜?…ë‹ˆ??" });

        var compareResult = _reviewSnapshotStore.Compare(request.LeftSnapshotId, request.RightSnapshotId);
        if (compareResult is null)
            return NotFound(new { error = "ë¹„êµ??snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤." });

        return Ok(compareResult);
    }

    [HttpPost("review/compare/report-package")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareReportPackage(
        [FromBody] ReviewSnapshotCompareRequestDto request)
    {
        var comparePackageResult = TryBuildCompareReportPackage(request);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        return Ok(comparePackageResult.Package);
    }

    [HttpPost("review/compare/report-markdown")]
    [Produces("text/markdown")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareReportMarkdown(
        [FromBody] ReviewSnapshotCompareRequestDto request)
    {
        var comparePackageResult = TryBuildCompareReportPackage(request);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var package = comparePackageResult.Package!;
        var artifact = _reviewReportRenderer.BuildCompareMarkdownArtifact(package);
        var bytes = Encoding.UTF8.GetBytes(artifact.Text);
        return File(bytes, artifact.MimeType, artifact.SuggestedFileName);
    }

    [HttpPost("review/compare/report-export")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportExportPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareReportExport(
        [FromBody] ReviewSnapshotCompareReportExportRequestDto request)
    {
        if (request.CompareRequest is null)
            return BadRequest(new { error = "compareRequest???„ìˆ˜?…ë‹ˆ??" });

        var comparePackageResult = TryBuildCompareReportPackage(request.CompareRequest);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var exportPlan = _reviewReportRenderer.BuildCompareExportPlan(comparePackageResult.Package!, request.Format);
        return Ok(exportPlan);
    }

    [HttpPost("review/compare/report-render")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportRenderResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareReportRender(
        [FromBody] ReviewSnapshotCompareReportExportRequestDto request)
    {
        if (request.CompareRequest is null)
            return BadRequest(new { error = "compareRequest???„ìˆ˜?…ë‹ˆ??" });

        var comparePackageResult = TryBuildCompareReportPackage(request.CompareRequest);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var renderResult = _reviewReportRenderer.BuildCompareRenderResult(comparePackageResult.Package!, request.Format);
        return Ok(renderResult);
    }

    [HttpPost("review/compare/report-archives")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareArchiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareReportArchive(
        [FromBody] ReviewSnapshotCompareRequestDto request)
    {
        var comparePackageResult = TryBuildCompareReportPackage(request);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var package = comparePackageResult.Package!;
        var archive = _reviewSnapshotStore.SaveCompareReport(
            package.Comparison.Left.ProjectId,
            package.Comparison.Left.ScenarioId,
            package);

        return Ok(archive);
    }

    [HttpGet("review/compare/report-archives/{compareReportId}")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareArchiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewCompareReportArchive(string compareReportId)
    {
        var archive = _reviewSnapshotStore.GetCompareReport(compareReportId);
        if (archive is null)
            return NotFound(new { error = "ë¹„êµ ë³´ê³ ???„ì¹´?´ë¸Œë¥?ì°¾ì„ ???†ìŠµ?ˆë‹¤.", compareReportId });

        return Ok(archive);
    }

    [HttpGet("review/compare/report-archives/{compareReportId}/report-package")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewCompareReportArchivePackage(string compareReportId)
    {
        var archive = _reviewSnapshotStore.GetCompareReport(compareReportId);
        if (archive is null)
            return NotFound(new { error = "compare report archive was not found.", compareReportId });

        return Ok(archive.ReportPackage);
    }

    [HttpGet("review/compare/report-archives/{compareReportId}/report-markdown")]
    [Produces("text/markdown")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewCompareReportArchiveMarkdown(string compareReportId)
    {
        var archive = _reviewSnapshotStore.GetCompareReport(compareReportId);
        if (archive is null)
            return NotFound(new { error = "compare report archive was not found.", compareReportId });

        var artifact = _reviewReportRenderer.BuildCompareMarkdownArtifact(archive.ReportPackage);
        var bytes = Encoding.UTF8.GetBytes(artifact.Text);
        return File(bytes, artifact.MimeType, artifact.SuggestedFileName);
    }

    [HttpGet("review/compare/report-archives/{compareReportId}/report-export")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportExportPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewCompareReportArchiveExport(
        string compareReportId,
        [FromQuery] string format = "pdf")
    {
        var archive = _reviewSnapshotStore.GetCompareReport(compareReportId);
        if (archive is null)
            return NotFound(new { error = "compare report archive was not found.", compareReportId });

        var exportPlan = _reviewReportRenderer.BuildCompareExportPlan(archive.ReportPackage, format);
        return Ok(exportPlan);
    }

    [HttpGet("review/compare/report-archives/{compareReportId}/report-render")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportRenderResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewCompareReportArchiveRender(
        string compareReportId,
        [FromQuery] string format = "pdf")
    {
        var archive = _reviewSnapshotStore.GetCompareReport(compareReportId);
        if (archive is null)
            return NotFound(new { error = "compare report archive was not found.", compareReportId });

        var renderResult = _reviewReportRenderer.BuildCompareRenderResult(archive.ReportPackage, format);
        return Ok(renderResult);
    }

    [HttpGet("review/projects/{projectId}/compare/report-archives")]
    [ProducesResponseType(typeof(List<ReviewSnapshotCompareArchiveSummaryDto>), StatusCodes.Status200OK)]
    public IActionResult GetReviewCompareReportArchives(
        string projectId,
        [FromQuery] string? scenarioId = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId???„ìˆ˜?…ë‹ˆ??" });

        return Ok(_reviewSnapshotStore.ListCompareReportsByProject(projectId, scenarioId));
    }
    [HttpPost("review/projects/{projectId}/compare/latest")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareLatest(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId is required." });

        var latest = _reviewSnapshotStore.GetLatestByProject(projectId, request.ScenarioId);
        if (latest is null)
            return NotFound(new { error = "latest snapshot was not found.", projectId, request.ScenarioId });

        var baselineSnapshotId = ResolveBaselineSnapshotId(projectId, request);
        if (string.IsNullOrWhiteSpace(baselineSnapshotId))
            return BadRequest(new { error = "baselineSnapshotId or stored project baseline is required." });

        if (string.Equals(latest.SnapshotId, baselineSnapshotId, StringComparison.Ordinal))
            return BadRequest(new { error = "baseline snapshot already matches the latest snapshot.", latestSnapshotId = latest.SnapshotId });

        var compareResult = _reviewSnapshotStore.Compare(baselineSnapshotId, latest.SnapshotId);
        if (compareResult is null)
            return NotFound(new { error = "compare snapshots were not found." });

        return Ok(compareResult);
    }

    [HttpGet("review/projects/{projectId}/baseline")]
    [ProducesResponseType(typeof(ProjectReviewBaselineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult GetReviewProjectBaseline(
        string projectId,
        [FromQuery] string? scenarioId = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId???„ìˆ˜?…ë‹ˆ??" });

        var baseline = _reviewSnapshotStore.GetBaselineByProject(projectId, scenarioId);
        if (baseline is null)
            return NotFound(new { error = "ê¸°ì? snapshot??ì§€?•ë˜ì§€ ?Šì•˜?µë‹ˆ??", projectId, scenarioId });

        return Ok(baseline);
    }

    [HttpPost("review/projects/{projectId}/baseline")]
    [ProducesResponseType(typeof(ProjectReviewBaselineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewProjectBaseline(
        string projectId,
        [FromBody] SetProjectReviewBaselineRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId???„ìˆ˜?…ë‹ˆ??" });

        if (string.IsNullOrWhiteSpace(request.SnapshotId))
            return BadRequest(new { error = "snapshotId???„ìˆ˜?…ë‹ˆ??" });

        var baseline = _reviewSnapshotStore.SetBaseline(projectId, request.SnapshotId, request.ScenarioId);
        if (baseline is null)
            return NotFound(new { error = "ê¸°ì??¼ë¡œ ì§€?•í•  snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤.", projectId, request.SnapshotId, request.ScenarioId });

        return Ok(baseline);
    }

    [HttpPost("review/projects/{projectId}/compare/active-baseline")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareActiveBaseline(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return BadRequest(new { error = "projectId???„ìˆ˜?…ë‹ˆ??" });

        var baseline = _reviewSnapshotStore.GetBaselineByProject(projectId, request.ScenarioId);
        if (baseline is null)
            return NotFound(new { error = "?„ë¡œ?íŠ¸ ê¸°ì? snapshot??ì§€?•ë˜ì§€ ?Šì•˜?µë‹ˆ??", projectId, request.ScenarioId });

        var latest = _reviewSnapshotStore.GetLatestByProject(projectId, request.ScenarioId);
        if (latest is null)
            return NotFound(new { error = "ìµœì‹  snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤.", projectId, request.ScenarioId });

        if (string.Equals(latest.SnapshotId, baseline.SnapshotId, StringComparison.Ordinal))
            return BadRequest(new { error = "ê¸°ì? snapshot???´ë? ìµœì‹  snapshot?…ë‹ˆ??", latestSnapshotId = latest.SnapshotId });

        var compareResult = _reviewSnapshotStore.Compare(baseline.SnapshotId, latest.SnapshotId);
        if (compareResult is null)
            return NotFound(new { error = "ë¹„êµ??snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤." });

        return Ok(compareResult);
    }

    [HttpPost("review/projects/{projectId}/compare/active-baseline/report-package")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportPackageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareActiveBaselineReportPackage(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestRequestDto request)
    {
        var comparePackageResult = TryBuildActiveBaselineCompareReportPackage(projectId, request);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        return Ok(comparePackageResult.Package);
    }

    [HttpPost("review/projects/{projectId}/compare/active-baseline/report-markdown")]
    [Produces("text/markdown")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareActiveBaselineReportMarkdown(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestRequestDto request)
    {
        var comparePackageResult = TryBuildActiveBaselineCompareReportPackage(projectId, request);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var artifact = _reviewReportRenderer.BuildCompareMarkdownArtifact(comparePackageResult.Package!);
        var bytes = Encoding.UTF8.GetBytes(artifact.Text);
        return File(bytes, artifact.MimeType, artifact.SuggestedFileName);
    }

    [HttpPost("review/projects/{projectId}/compare/active-baseline/report-export")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportExportPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareActiveBaselineReportExport(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestExportRequestDto request)
    {
        var comparePackageResult = TryBuildActiveBaselineCompareReportPackage(projectId, request.CompareLatestRequest);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var exportPlan = _reviewReportRenderer.BuildCompareExportPlan(comparePackageResult.Package!, request.Format);
        return Ok(exportPlan);
    }

    [HttpPost("review/projects/{projectId}/compare/active-baseline/report-render")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareReportRenderResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareActiveBaselineReportRender(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestExportRequestDto request)
    {
        var comparePackageResult = TryBuildActiveBaselineCompareReportPackage(projectId, request.CompareLatestRequest);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var renderResult = _reviewReportRenderer.BuildCompareRenderResult(comparePackageResult.Package!, request.Format);
        return Ok(renderResult);
    }

    [HttpPost("review/projects/{projectId}/compare/active-baseline/report-archives")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareArchiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareActiveBaselineReportArchive(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestRequestDto request)
    {
        var comparePackageResult = TryBuildActiveBaselineCompareReportPackage(projectId, request);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var package = comparePackageResult.Package!;
        var archive = _reviewSnapshotStore.SaveCompareReport(projectId, request.ScenarioId, package);
        return Ok(archive);
    }

    [HttpPost("review/projects/{projectId}/compare/active-baseline/report-archives/upsert")]
    [ProducesResponseType(typeof(ReviewSnapshotCompareArchiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public IActionResult PostReviewCompareActiveBaselineReportArchiveUpsert(
        string projectId,
        [FromBody] ReviewSnapshotCompareLatestRequestDto request)
    {
        var comparePackageResult = TryBuildActiveBaselineCompareReportPackage(projectId, request);
        if (comparePackageResult.ErrorResult is not null)
            return comparePackageResult.ErrorResult;

        var package = comparePackageResult.Package!;
        var existingArchive = _reviewSnapshotStore.FindCompareReport(
            projectId,
            package.Comparison.Left.SnapshotId,
            package.Comparison.Right.SnapshotId,
            request.ScenarioId);

        if (existingArchive is not null)
            return Ok(existingArchive);

        var archive = _reviewSnapshotStore.SaveCompareReport(projectId, request.ScenarioId, package);
        return Ok(archive);
    }


    private string? ResolveBaselineSnapshotId(string projectId, ReviewSnapshotCompareLatestRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.BaselineSnapshotId))
            return request.BaselineSnapshotId;

        return _reviewSnapshotStore.GetBaselineByProject(projectId, request.ScenarioId)?.SnapshotId;
    }
    private (IActionResult? ErrorResult, ReviewSnapshotCompareReportPackageDto? Package) TryBuildActiveBaselineCompareReportPackage(
        string projectId,
        ReviewSnapshotCompareLatestRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return (BadRequest(new { error = "projectId is required." }), null);

        var baseline = _reviewSnapshotStore.GetBaselineByProject(projectId, request.ScenarioId);
        if (baseline is null)
            return (NotFound(new { error = "project baseline was not found.", projectId, request.ScenarioId }), null);

        var latest = _reviewSnapshotStore.GetLatestByProject(projectId, request.ScenarioId);
        if (latest is null)
            return (NotFound(new { error = "latest snapshot was not found.", projectId, request.ScenarioId }), null);

        if (string.Equals(latest.SnapshotId, baseline.SnapshotId, StringComparison.Ordinal))
            return (BadRequest(new { error = "baseline snapshot already matches the latest snapshot.", latestSnapshotId = latest.SnapshotId }), null);

        return TryBuildCompareReportPackage(new ReviewSnapshotCompareRequestDto
        {
            LeftSnapshotId = baseline.SnapshotId,
            RightSnapshotId = latest.SnapshotId,
        });
    }

    private async Task<(IActionResult? ErrorResult, BuildingReviewResponseDto? Response)> ExecuteReviewAsync(
        BuildingReviewRequestDto request,
        CancellationToken ct)
    {
        // ?€?€ 1. ê¸°ë³¸ ê²€ì¦??€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        if (string.IsNullOrWhiteSpace(request.SelectedUse))
            return (BadRequest(new { error = "selectedUse???„ìˆ˜?…ë‹ˆ??" }), null);

        var supportedUses = UseProfileRegistry.SupportedDisplayNames;
        if (!UseProfileRegistry.IsSupported(request.SelectedUse))
            return (BadRequest(new { error = $"ì§€?í•˜ì§€ ?ŠëŠ” ?©ë„?…ë‹ˆ?? {request.SelectedUse}", supportedUses }), null);

        bool hasAddress = !string.IsNullOrWhiteSpace(request.Address);
        bool hasCoord   = request.Longitude.HasValue && request.Latitude.HasValue;
        if (!hasAddress && !hasCoord)
            return (BadRequest(new { error = "address ?ëŠ” longitude/latitude ì¤??˜ë‚˜???„ìˆ˜?…ë‹ˆ??" }), null);

        var sw = Stopwatch.StartNew();

        // ?€?€ 2. ?„ì¹˜ ?´ì„ ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        CoordinateQuery   coordinate;
        LocationSummaryDto locationDto;

        if (hasAddress)
        {
            var candidates = await _addressResolver.ResolveAsync(request.Address!, ct);
            if (candidates.Count == 0)
            {
                _logger.LogWarning("review: ì£¼ì†Œ ì¢Œí‘œ ë³€???¤íŒ¨ ??{Address}", request.Address);
                return (NotFound(new
                {
                    title      = "ì£¼ì†Œë¥?ì°¾ì„ ???†ìŠµ?ˆë‹¤",
                    detail     = $"\"{request.Address}\" ???´ë‹¹?˜ëŠ” ì¢Œí‘œë¥?ì°¾ì? ëª»í–ˆ?µë‹ˆ??",
                    inputQuery = request.Address,
                }), null);
            }

            var best   = candidates[0];
            coordinate = best.Coordinate;
            locationDto = new LocationSummaryDto
            {
                InputAddress      = request.Address,
                ResolvedAddress   = best.NormalizedAddress,
                Longitude         = coordinate.Longitude,
                Latitude          = coordinate.Latitude,
                GeocodingProvider = best.Provider,
            };
        }
        else
        {
            coordinate = new CoordinateQuery(request.Longitude!.Value, request.Latitude!.Value);
            locationDto = new LocationSummaryDto
            {
                Longitude = coordinate.Longitude,
                Latitude  = coordinate.Latitude,
            };
        }

        // ?€?€ 3. ?©ë„ì§€???ë™ ?ì • ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var zoningResult = await _service.CheckAsync(coordinate, ct);
        var zoneName     = zoningResult.Zoning?.Name;
        var zoneCode     = zoningResult.Zoning?.Code;
        var limits       = ZoneLimitTable.GetLimit(zoneName);
        var dupInside    = zoningResult.ExtraLayers.DistrictUnitPlan?.IsInside;
        var darInside    = zoningResult.ExtraLayers.DevelopmentRestriction?.IsInside;
        var darActInside = zoningResult.ExtraLayers.DevelopmentActionRestriction?.IsInside;
        var darActDetail = MapOverlayDecision(zoningResult.ExtraLayers.DevelopmentActionRestriction);

        var zoningDto = zoneName is not null
            ? new ZoningSummaryDto
            {
                ZoneName        = zoneName,
                BcRatioLimitPct = limits?.Bcr,
                FarLimitPct     = limits?.Far,
                Note = limits.HasValue
                    ? "êµ?† ê³„íšë²?ë²•ì • ?í•œ (ì¡°ë??ì„œ ?˜í–¥?????ˆìŒ)"
                    : $"'{zoneName}'?€(?? ê±´í?¨Â·ìš©?ë¥  ê¸°ì????±ë¡?˜ì? ?Šì? ?©ë„ì§€??…?ˆë‹¤. ì§€?ì²´ ê±´ì¶• ?´ë‹¹ ë¶€?œì— ì§ì ‘ ?•ì¸?˜ì„¸??",
            }
            : null;

        var overlaysDto = new OverlaySummaryDto
        {
            DistrictUnitPlan             = dupInside,
            DevelopmentRestriction       = darInside,
            DevelopmentActionRestriction = darActInside,
            DevelopmentActionRestrictionDetail = darActDetail,
        };

        // ?€?€ 4. ReviewLevel ê²°ì • ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var reviewLevel = ReviewLevelDetector.Parse(request.ReviewLevel)
                       ?? ReviewLevelDetector.Detect(request.BuildingInputs);

        _logger.LogDebug(
            "review: Use={Use}, Level={Level}, Zone={Zone}, DUP={Dup}, DAR={Dar}",
            request.SelectedUse, ReviewLevelDetector.LevelToString(reviewLevel),
            zoneName, dupInside, darInside);

        // ?€?€ 5. ê·œì¹™ ì¡°íšŒ + ?ˆë²¨ ?„í„°ë§??€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var allRules   = ReviewItemRuleTable.GetReviewItemsRaw(
            request.SelectedUse, zoneName, dupInside, darInside);
        var levelRules = ReviewLevelDetector.FilterByLevel(allRules, reviewLevel);

        // ?€?€ 6. ?ì • + DTO ë³€??(includeLegalBasis ë¶„ê¸°) ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        List<ReviewItemDto> reviewItems;

        if (!request.IncludeLegalBasis)
        {
            reviewItems = levelRules
                .Select(r =>
                {
                    var (status, note) = BuildingReviewJudgeService.Judge(
                        r, zoneName, limits, request.BuildingInputs);
                    return BuildReviewItemDto(r, status, note, null, 0);
                })
                .ToList();
        }
        else
        {
            var allKeys = levelRules
                .SelectMany(r => r.LegalBasis.Select(lb => lb.NormalizedKey))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _logger.LogDebug(
                "review ì¡°ë¬¸ ?¼ê´„ ì¡°íšŒ: Use={Use}, uniqueKeys={Keys}",
                request.SelectedUse, allKeys.Count);

            var clauseDict = await _clauseProvider
                .GetClausesAsync(allKeys, ct)
                .ConfigureAwait(false);

            reviewItems = levelRules
                .Select(r =>
                {
                    var (status, note) = BuildingReviewJudgeService.Judge(
                        r, zoneName, limits, request.BuildingInputs);
                    return BuildReviewItemDto(r, status, note, clauseDict, _maxClausesPerItem);
                })
                .ToList();
        }

        // ?€?€ 7. inputSummary + nextLevelHint ?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
        var inputSummary  = BuildInputSummary(request.BuildingInputs);

        sw.Stop();
        _logger.LogInformation(
            "review ?„ë£Œ: Use={Use}, Level={Level}, Zone={Zone}, items={Count}, elapsed={Elapsed}ms",
            request.SelectedUse, ReviewLevelDetector.LevelToString(reviewLevel),
            zoneName, reviewItems.Count, sw.ElapsedMilliseconds);

        var response = ReviewResponseComposer.Compose(
            request,
            reviewLevel,
            zoneName,
            zoneCode,
            dupInside,
            darInside,
            darActInside,
            locationDto,
            zoningDto,
            overlaysDto,
            reviewItems,
            allRules,
            inputSummary,
            sw.ElapsedMilliseconds);

        return (null, response);
    }

    private static BuildingReviewRequestDto MergeRequestWithCsvAutomation(
        BuildingReviewRequestDto request,
        CsvInputAutomationResultDto csv)
    {
        var mergedInputs = MergeBuildingInputs(request.BuildingInputs, csv.SuggestedBuildingInputs);

        return new BuildingReviewRequestDto
        {
            Address = request.Address,
            Longitude = request.Longitude,
            Latitude = request.Latitude,
            ReviewLevel = request.ReviewLevel ?? csv.SuggestedReviewLevel,
            SelectedUse = string.IsNullOrWhiteSpace(request.SelectedUse) ? (csv.SuggestedSelectedUse ?? string.Empty) : request.SelectedUse,
            BuildingInputs = mergedInputs,
            IncludeLegalBasis = request.IncludeLegalBasis,
            ProjectContext = request.ProjectContext,
            GeometryInput = request.GeometryInput,
            CsvUploadToken = request.CsvUploadToken
        };
    }

    private static BuildingInputsDto MergeBuildingInputs(BuildingInputsDto? current, BuildingInputsDto suggested)
    {
        current ??= new BuildingInputsDto();

        return new BuildingInputsDto
        {
            SiteArea = current.SiteArea ?? suggested.SiteArea,
            BuildingArea = current.BuildingArea ?? suggested.BuildingArea,
            FloorArea = current.FloorArea ?? suggested.FloorArea,
            FloorCount = current.FloorCount ?? suggested.FloorCount,
            BuildingHeight = current.BuildingHeight ?? suggested.BuildingHeight,
            RoadFrontageWidth = current.RoadFrontageWidth ?? suggested.RoadFrontageWidth,
            UnitCount = current.UnitCount ?? suggested.UnitCount,
            RoomCount = current.RoomCount ?? suggested.RoomCount,
            GuestRoomCount = current.GuestRoomCount ?? suggested.GuestRoomCount,
            BedCount = current.BedCount ?? suggested.BedCount,
            StudentCount = current.StudentCount ?? suggested.StudentCount,
            UnitArea = current.UnitArea ?? suggested.UnitArea,
            HousingSubtype = current.HousingSubtype ?? suggested.HousingSubtype,
            ParkingType = current.ParkingType ?? suggested.ParkingType,
            VehicleIngressType = current.VehicleIngressType ?? suggested.VehicleIngressType,
            DetailUseSubtype = current.DetailUseSubtype ?? suggested.DetailUseSubtype,
            DetailUseFloorArea = current.DetailUseFloorArea ?? suggested.DetailUseFloorArea,
            IsMultipleOccupancy = current.IsMultipleOccupancy ?? suggested.IsMultipleOccupancy,
            IsHighRiskOccupancy = current.IsHighRiskOccupancy ?? suggested.IsHighRiskOccupancy,
            HasDisabilityUsers = current.HasDisabilityUsers ?? suggested.HasDisabilityUsers,
            OfficeSubtype = current.OfficeSubtype ?? suggested.OfficeSubtype,
            MixedUseRatio = current.MixedUseRatio ?? suggested.MixedUseRatio,
            OccupantCount = current.OccupantCount ?? suggested.OccupantCount,
            HasPublicSpace = current.HasPublicSpace ?? suggested.HasPublicSpace,
            HasLoadingBay = current.HasLoadingBay ?? suggested.HasLoadingBay,
            MedicalSpecialCriteria = current.MedicalSpecialCriteria ?? suggested.MedicalSpecialCriteria,
            EducationSpecialCriteria = current.EducationSpecialCriteria ?? suggested.EducationSpecialCriteria,
            HazardousMaterialProfile = current.HazardousMaterialProfile ?? suggested.HazardousMaterialProfile,
            LogisticsOperationProfile = current.LogisticsOperationProfile ?? suggested.LogisticsOperationProfile,
            AccommodationSpecialCriteria = current.AccommodationSpecialCriteria ?? suggested.AccommodationSpecialCriteria,
            HasDistrictUnitPlanDocument = current.HasDistrictUnitPlanDocument ?? suggested.HasDistrictUnitPlanDocument,
            HasDevActRestrictionConsult = current.HasDevActRestrictionConsult ?? suggested.HasDevActRestrictionConsult
        };
    }
    private async Task<(IActionResult? ErrorResult, BuildingReviewResponseDto? Response, BuildingReviewReportPackageDto? Package)> ExecuteReviewPackageAsync(
        BuildingReviewRequestDto request,
        CancellationToken ct)
    {
        var (errorResult, response) = await ExecuteReviewAsync(request, ct);
        if (errorResult is not null || response is null)
            return (errorResult, response, null);

        var package = ReportPackageBuilder.Build(request, response);
        return (null, response, package);
    }

    private (IActionResult? ErrorResult, ReviewSnapshotCompareReportPackageDto? Package) TryBuildCompareReportPackage(
        ReviewSnapshotCompareRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.LeftSnapshotId) ||
            string.IsNullOrWhiteSpace(request.RightSnapshotId))
            return (BadRequest(new { error = "leftSnapshotId?€ rightSnapshotId???„ìˆ˜?…ë‹ˆ??" }), null);

        var compareResult = _reviewSnapshotStore.Compare(request.LeftSnapshotId, request.RightSnapshotId);
        if (compareResult is null)
            return (NotFound(new { error = "ë¹„êµ??snapshot??ì°¾ì„ ???†ìŠµ?ˆë‹¤." }), null);

        return (null, ReviewSnapshotCompareReportBuilder.Build(compareResult));
    }

    #endregion

    #region /review ?„ìš© ?¬í¼ ë©”ì„œ??
    /// <summary>
    /// ReviewItemRuleRecord ??ReviewItemDto ë³€??(/review ?„ìš©).
    /// judgeStatus, judgeNote, ruleId ?¬í•¨.
    /// </summary>
    private static ReviewItemDto BuildReviewItemDto(
        ReviewItemRuleRecord rule,
        string               judgeStatus,
        string?              judgeNote,
        LawClauseDict?       clauses,
        int                  maxClauses) => new()
    {
        RuleId          = rule.Id,
        Category        = rule.Category,
        Title           = rule.Title,
        Description     = rule.Description,
        RequiredInputs  = rule.RequiredInputs,
        RelatedLaws     = rule.RelatedLaws,
        IsAutoCheckable = rule.IsAutoCheckable,
        Priority        = rule.Priority,
        JudgeStatus     = judgeStatus,
        JudgeNote       = judgeNote,
        LegalBasisClauses = (clauses is not null && rule.LegalBasis.Count > 0)
            ? ApplyMaxClauses(rule.LegalBasis, clauses, maxClauses)
            : null,
    };

    /// <summary>
    /// buildingInputs?ì„œ ?œê³µ???„ë“œ?€ ?„ë½???„ë“œë¥?ë¶„ë¥˜?˜ì—¬ InputSummaryDtoë¥??ì„±?©ë‹ˆ??
    /// </summary>
    private static InputSummaryDto BuildInputSummary(BuildingInputsDto? inp)
    {
        if (inp is null)
        {
            return new InputSummaryDto
            {
                Provided    = [],
                Missing     = ["siteArea", "floorArea", "floorCount"],
                MissingNote = "´ëÁö¸éÀû, ¿¬¸éÀû, Ãş¼ö ÀÔ·Â Àü¿¡´Â ±âº» Ç×¸ñ¸¸ »êÁ¤ °¡´ÉÇÕ´Ï´Ù.",
            };
        }

        var provided = new List<string>();
        var missing  = new List<string>();

        void Check(bool hasValue, string field)
        {
            if (hasValue) provided.Add(field);
            else          missing.Add(field);
        }

        // Standard ?µì‹¬ ?„ë“œ (??ƒ ì¶”ì )
        Check(inp.SiteArea.HasValue,          "siteArea");
        Check(inp.FloorArea.HasValue,         "floorArea");
        Check(inp.FloorCount.HasValue,        "floorCount");

        // ? íƒ Standard ?„ë“œ (?œê³µ??ê²½ìš°ë§?provided???¬í•¨)
        if (inp.BuildingHeight.HasValue)    provided.Add("buildingHeight");
        if (inp.RoadFrontageWidth.HasValue) provided.Add("roadFrontageWidth");

        // Detailed ?„ë“œ (?œê³µ??ê²½ìš°ë§?provided???¬í•¨)
        if (inp.UnitCount.HasValue)           provided.Add("unitCount");
        if (inp.UnitArea.HasValue)            provided.Add("unitArea");
        if (inp.HousingSubtype is not null)   provided.Add("housingSubtype");
        if (inp.ParkingType is not null)      provided.Add("parkingType");
        if (inp.DetailUseSubtype is not null) provided.Add("detailUseSubtype");
        if (inp.DetailUseFloorArea.HasValue)  provided.Add("detailUseFloorArea");
        if (inp.IsMultipleOccupancy.HasValue) provided.Add("isMultipleOccupancy");
        if (inp.OfficeSubtype is not null)    provided.Add("officeSubtype");
        if (inp.OccupantCount.HasValue)       provided.Add("occupantCount");
        if (inp.MixedUseRatio.HasValue)       provided.Add("mixedUseRatio");

        static string FieldLabel(string f) => f switch
        {
            "siteArea"    => "?€ì§€ë©´ì ",
            "floorArea"   => "°èÈ¹ ¿¬¸éÀû",
            "floorCount"  => "ê³„íš ì¸µìˆ˜",
            _             => f,
        };
        string? missingNote = missing.Count > 0
            ? $"{string.Join(", ", missing.Select(FieldLabel))} ì¶”ê? ?…ë ¥ ??ë°€?„Â·í”¼?œÂ·ë°©????ª©??ê³„ì‚° ?ì •?¼ë¡œ ?„í™˜?????ˆìŠµ?ˆë‹¤."
            : null;

        return new InputSummaryDto
        {
            Provided    = provided,
            Missing     = missing,
            MissingNote = missingNote,
        };
    }

    #endregion

    private static OverlayDecisionDto? MapOverlayDecision(OverlayZoneResult? overlay)
    {
        if (overlay is null)
            return null;

        var normalizedSource = overlay.Source switch
        {
            "api" => "api",
            "shp" => "shp",
            "none" => "none",
            _ => "shp",
        };

        var isUnavailable = normalizedSource == "none" ||
                            overlay.Confidence == OverlayConfidenceLevel.DataUnavailable;

        var status = normalizedSource switch
        {
            "api" => "confirmed",
            "shp" => isUnavailable ? "unavailable" : "fallback",
            _ => "unavailable",
        };

        var confidence = status switch
        {
            "confirmed" => "high",
            "fallback" => "medium",
            _ => "low",
        };

        return new OverlayDecisionDto
        {
            IsInside = isUnavailable ? null : overlay.IsInside,
            Source = normalizedSource,
            Status = status,
            Confidence = confidence,
            Name = overlay.Name,
            Code = overlay.Code,
            Note = overlay.Note,
        };
    }

    #region legalBasis ì¡°ë¬¸ ?¬í•¨ DTO ë³€???¬í¼ (includeLegalBasis=true ?„ìš©)

    private static CoreLawItemDto ToCoreDtoWithClauses(
        LawLayerRuleRecord                           rule,
        IReadOnlyDictionary<string, LawClauseResult> clauses,
        int                                          maxClauses) => new()
    {
        Law   = rule.Law   ?? string.Empty,
        Scope = rule.Scope ?? string.Empty,
        LegalBasisClauses = rule.LegalBasis.Count == 0
            ? null
            : ApplyMaxClauses(rule.LegalBasis, clauses, maxClauses),
    };

    private static MepLawItemDto ToMepDtoWithClauses(
        LawLayerRuleRecord                           rule,
        IReadOnlyDictionary<string, LawClauseResult> clauses,
        int                                          maxClauses) => new()
    {
        Title   = rule.Title   ?? string.Empty,
        TeamTag = rule.TeamTag ?? string.Empty,
        LegalBasisClauses = rule.LegalBasis.Count == 0
            ? null
            : ApplyMaxClauses(rule.LegalBasis, clauses, maxClauses),
    };

    private static ReviewItemDto ToReviewItemDto(
        ReviewItemRuleRecord                         rule,
        IReadOnlyDictionary<string, LawClauseResult> clauses,
        int                                          maxClauses) => new()
    {
        Category        = rule.Category,
        Title           = rule.Title,
        Description     = rule.Description,
        RequiredInputs  = rule.RequiredInputs,
        RelatedLaws     = rule.RelatedLaws,
        IsAutoCheckable = rule.IsAutoCheckable,
        Priority        = rule.Priority,
        LegalBasisClauses = rule.LegalBasis.Count == 0
            ? null
            : ApplyMaxClauses(rule.LegalBasis, clauses, maxClauses),
    };

    /// <summary>
    /// legalBasis ëª©ë¡??LawClauseDtoë¡?ë³€?˜í•˜ê³?maxClauses ?œí•œ???ìš©?©ë‹ˆ??
    /// maxClauses = 0?´ë©´ ?„ì²´ ë°˜í™˜.
    /// </summary>
    private static List<LawClauseDto> ApplyMaxClauses(
        IReadOnlyList<LegalReferenceRecord>          legalBasis,
        IReadOnlyDictionary<string, LawClauseResult> clauses,
        int                                          maxClauses)
    {
        var source = maxClauses > 0
            ? legalBasis.Take(maxClauses)
            : (IEnumerable<LegalReferenceRecord>)legalBasis;

        return source
            .Select(lb => ToClauseDto(lb, clauses.GetValueOrDefault(lb.NormalizedKey)))
            .ToList();
    }

    /// <summary>
    /// LegalReferenceRecord + ì¡°íšŒ ê²°ê³¼(null ?ˆìš©)ë¥?LawClauseDtoë¡?ë³€?˜í•©?ˆë‹¤.
    /// API ì¡°íšŒ ?¤íŒ¨ ?œì—??JSON ë©”í??°ì´??ê¸°ë°˜?¼ë¡œ ì°¸ì¡° ?•ë³´ë¥??œê³µ?©ë‹ˆ??
    /// </summary>
    private static LawClauseDto ToClauseDto(
        LegalReferenceRecord lb,
        LawClauseResult?     result)
    {
        if (result is not null)
        {
            return new LawClauseDto
            {
                NormalizedKey = lb.NormalizedKey,
                LawName       = result.LawName,
                ArticleRef    = result.ArticleRef,
                ClauseText    = result.ClauseText,
                Url           = result.Url,
                Source        = "openlaw_api",
            };
        }

        // API ì¡°íšŒ ?¤íŒ¨ ??ê·œì¹™ ë©”í??°ì´??ê¸°ë°˜ fallback
        return new LawClauseDto
        {
            NormalizedKey = lb.NormalizedKey,
            LawName       = lb.LawName,
            ArticleRef    = BuildFallbackArticleRef(lb),
            ClauseText    = lb.ClauseTextSummary,  // JSON practicalNote ?€??clauseTextSummary
            Url           = null,
            Source        = "rule_meta",
        };
    }

    /// <summary>LegalReferenceRecord?ì„œ ì¡°ë¬¸ ì°¸ì¡° ë¬¸ì?´ì„ ?ì„±?©ë‹ˆ??</summary>
    private static string BuildFallbackArticleRef(LegalReferenceRecord lb)
    {
        if (!string.IsNullOrWhiteSpace(lb.AppendixRef))
        {
            return lb.SubParagraph is not null
                ? $"{lb.AppendixRef} {lb.SubParagraph}"
                : lb.AppendixRef;
        }

        if (lb.Article.HasValue)
        {
            var s = $"{lb.Article}Á¶";
            if (lb.Paragraph.HasValue)    s += $" {lb.Paragraph}Ç×";
            if (lb.SubParagraph is not null) s += $" {lb.SubParagraph}";
            return s;
        }

        return lb.NormalizedKey;
    }

    #endregion

    #region GET /health - ?¬ìŠ¤ì²´í¬

    /// <summary>
    /// ?œë²„ ë°?ì»¨íŠ¸ë¡¤ëŸ¬ ?œì„± ?íƒœë¥??•ì¸?©ë‹ˆ??
    /// </summary>
    /// <returns>?íƒœ OK?€ ?€?„ìŠ¤?¬í”„</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new
        {
            status = "ok",
            timestamp = DateTimeOffset.UtcNow,
            note = "ë²•ê·œ ê²€??ë°±ì—”??API ?•ìƒ ?™ì‘ ì¤? ê²°ê³¼??ì°¸ê³ ??1ì°??ì •?…ë‹ˆ??"
        });

    #endregion
}

#endregion


