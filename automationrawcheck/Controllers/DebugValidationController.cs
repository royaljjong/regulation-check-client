// =============================================================================
// DebugValidationController.cs
// 내부 검증 전용 엔드포인트 — 외부 공개 금지
//
// [목적]
//   서버 기동 후 Swagger(GET /api/debug/validate)로 호출해
//   5개 고정 좌표에 대한 규제 검토 응답이 의도대로 생성되는지 확인합니다.
//
// [검증 항목]
//   - summaryText가 비어 있지 않고 완성된 문장인가
//   - 용도지역 발견 시 summaryText가 zoneName을 포함하는가
//   - needsAdditionalReview가 Preliminary 판정에서 항상 true인가
//   - cautionNotes가 1건 이상 생성되는가
//   - 개발제한구역 내부(drp.IsInside=true)일 때 경고 문구가 반영되는가
//   - 용도지역 미발견 시 summaryText가 "찾을 수 없습니다" 메시지를 포함하는가
//
// [UQ171 관련 엔드포인트]
//   - GET /api/debug/validate/dev-act-restriction : 개발행위허가제한지역 4개 케이스 검증
//   - GET /api/debug/dev-act-restriction/samples  : 피처 centroid/경계 샘플 좌표 추출 (검증용)
//
// [주의]
//   - 이 엔드포인트는 내부 검증 전용입니다.
//   - 프로덕션 환경에서는 라우팅 또는 미들웨어로 외부 접근을 차단하세요.
//   - 좌표별 "expected" 값은 추정치입니다. SHP 데이터 범위에 따라 달라질 수 있습니다.
// =============================================================================

using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using AutomationRawCheck.Infrastructure.ExtraLayers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace AutomationRawCheck.Api.Controllers;

#region 검증 결과 모델

/// <summary>단일 검증 단언 결과</summary>
public sealed record AssertionResult(
    string Name,
    bool   Pass,
    string Detail);

/// <summary>테스트 케이스별 검증 결과</summary>
public sealed record CaseValidationResult(
    string                  Label,
    double                  Longitude,
    double                  Latitude,
    string?                 ExpectedZoneHint,
    bool                    ExpectGreenBelt,
    // ── 실제 응답 ──────────────────────────────────────
    string                  ActualStatus,
    string?                 ActualZoneName,
    string?                 ActualZoneCode,
    bool                    ActualDrpIsInside,
    string                  ActualDrpConfidence,     // ← 신뢰도 레벨 (진단용)
    string?                 ActualDrpNote,           // ← 거리/커버리지 진단 메시지
    bool                    ActualNeedsAdditionalReview,
    string                  ActualSummaryText,
    IReadOnlyList<string>   ActualCautionNotes,
    // ── 단언 결과 ──────────────────────────────────────
    IReadOnlyList<AssertionResult> Assertions,
    IReadOnlyList<string>          Observations);

#endregion

#region DebugValidationController

/// <summary>
/// 내부 검증 전용 컨트롤러입니다.
/// <para>
/// <b>프로덕션 환경 외부 공개 금지.</b>
/// </para>
/// </summary>
[ApiController]
[Route("api/debug")]
[Produces("application/json")]
public sealed class DebugValidationController : ControllerBase
{
    public sealed record DevActPermitSampleParseRequest(string Json);

    private static readonly string[] DefaultDevActPermitCandidatePaths =
    [
        string.Empty,
        "api",
        "api/check",
        "api/coordinate",
        "api/dev-act-permit",
        "api/dev-act-permit/check",
        "coordinate",
        "check",
        "dev-act-permit",
        "dev-act-permit/check",
        "permit",
        "permit/check"
    ];

    #region 테스트 케이스 정의

    /// <summary>
    /// 내부 검증용 고정 테스트 케이스.
    /// ExpectedZoneHint: 예상 용도지역 키워드(null=미지정).
    /// ExpectGreenBelt: 개발제한구역 포함 예상 여부.
    /// </summary>
    private sealed record TestCase(
        string  Label,
        double  Longitude,
        double  Latitude,
        string? ExpectedZoneHint,
        bool    ExpectGreenBelt);

    private static readonly TestCase[] Cases =
    {
        // ── Case 1: 서울 광화문 (중심상업지역 예상) ──────────────────────────
        new("서울 광화문 (중심/일반상업 예상)",
            Longitude:        126.9769,
            Latitude:         37.5752,
            ExpectedZoneHint: "상업",
            ExpectGreenBelt:  false),

        // ── Case 2: 서울 강남 신논현 (일반주거/상업 예상) ─────────────────────
        new("서울 강남 신논현 (주거·상업 예상)",
            Longitude:        127.0258,
            Latitude:         37.5046,
            ExpectedZoneHint: null,
            ExpectGreenBelt:  false),

        // ── Case 3: 서울 노원구 아파트 단지 (일반주거 예상) ───────────────────
        new("서울 노원구 아파트 (일반주거 예상)",
            Longitude:        127.0654,
            Latitude:         37.6543,
            ExpectedZoneHint: "주거",
            ExpectGreenBelt:  false),

        // ── Case 4: 서울 외곽 청계산 인근 (개발제한구역 추정) ──────────────────
        new("서울 청계산 인근 (그린벨트 추정)",
            Longitude:        127.0195,
            Latitude:         37.3920,
            ExpectedZoneHint: null,
            ExpectGreenBelt:  true),

        // ── Case 5: 국내 SHP 범위 외 좌표 (NotFound 예상) ─────────────────────
        new("SHP 범위 외 좌표 (NotFound 예상)",
            Longitude:        128.9000,
            Latitude:         35.1000,
            ExpectedZoneHint: null,
            ExpectGreenBelt:  false),
    };

    #endregion

    #region 필드 및 생성자

    private readonly IRegulationCheckService              _service;
    private readonly IDevelopmentRestrictionProvider     _drpProvider;
    private readonly IDevActRestrictionProvider          _darProvider;
    private readonly DevelopmentActionPermitApiProvider  _developmentActionPermitApiProvider;
    private readonly DevelopmentActionPermitApiOptions   _devActPermitApiOptions;
    private readonly ILogger<DebugValidationController>  _logger;

    /// <summary>DebugValidationController를 초기화합니다.</summary>
    public DebugValidationController(
        IRegulationCheckService              service,
        IDevelopmentRestrictionProvider      drpProvider,
        IDevActRestrictionProvider           darProvider,
        DevelopmentActionPermitApiProvider   developmentActionPermitApiProvider,
        IOptions<DevelopmentActionPermitApiOptions> devActPermitApiOptions,
        ILogger<DebugValidationController>   logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _drpProvider = drpProvider ?? throw new ArgumentNullException(nameof(drpProvider));
        _darProvider = darProvider ?? throw new ArgumentNullException(nameof(darProvider));
        _developmentActionPermitApiProvider = developmentActionPermitApiProvider ?? throw new ArgumentNullException(nameof(developmentActionPermitApiProvider));
        _devActPermitApiOptions = devActPermitApiOptions?.Value ?? throw new ArgumentNullException(nameof(devActPermitApiOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region GET /api/debug/validate

    /// <summary>
    /// [내부 검증 전용] 고정 테스트 좌표 5건에 대한 규제 검토 응답을 검증합니다.
    /// </summary>
    /// <remarks>
    /// 각 케이스별로 summaryText, cautionNotes, needsAdditionalReview,
    /// 개발제한구역 반영 여부 등 주요 단언 항목을 점검합니다.
    /// <br/>
    /// 외부 공개 금지 — 내부 검증 목적으로만 사용하세요.
    /// </remarks>
    [HttpGet("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateAsync(CancellationToken ct)
    {
        _logger.LogInformation("내부 검증 시작: {Count}건 테스트 케이스", Cases.Length);

        var caseResults = new List<CaseValidationResult>(Cases.Length);

        foreach (var tc in Cases)
        {
            // ── 1. 서비스 호출 + DTO 변환 ─────────────────────────────────────
            var query      = new CoordinateQuery(tc.Longitude, tc.Latitude);
            var domain     = await _service.CheckAsync(query, ct);
            var dto        = RegulationCheckResponseDto.MapFrom(domain);

            // ── 2. 응답 필드 추출 ──────────────────────────────────────────────
            var status              = dto.RegulationSummary.Status;
            var zoneName            = dto.Zoning?.ZoneName;
            var zoneCode            = dto.Zoning?.ZoneCode;
            var drpIsInside         = dto.ExtraLayers.DevelopmentRestriction?.IsInside ?? false;
            var needsReview         = dto.RegulationInfo?.NeedsAdditionalReview ?? false;
            var summaryText         = dto.SummaryText;
            var cautionNotes        = (IReadOnlyList<string>)(dto.RegulationSummary.Caution);

            // ── 3. 단언 실행 ──────────────────────────────────────────────────
            var assertions   = RunAssertions(tc, status, zoneName, drpIsInside,
                                             needsReview, summaryText, cautionNotes);

            // ── 4. 이상 징후 관찰 기록 ────────────────────────────────────────
            var observations = BuildObservations(tc, status, zoneName, drpIsInside, summaryText);

            var drpDto = dto.ExtraLayers.DevelopmentRestriction;

            caseResults.Add(new CaseValidationResult(
                Label:                       tc.Label,
                Longitude:                   tc.Longitude,
                Latitude:                    tc.Latitude,
                ExpectedZoneHint:            tc.ExpectedZoneHint,
                ExpectGreenBelt:             tc.ExpectGreenBelt,
                ActualStatus:                status,
                ActualZoneName:              zoneName,
                ActualZoneCode:              zoneCode,
                ActualDrpIsInside:           drpIsInside,
                ActualDrpConfidence:         drpDto?.Status ?? string.Empty,
                ActualDrpNote:               drpDto?.Note,
                ActualNeedsAdditionalReview: needsReview,
                ActualSummaryText:           summaryText,
                ActualCautionNotes:          cautionNotes,
                Assertions:                  assertions,
                Observations:                observations));
        }

        // ── 전체 집계 ─────────────────────────────────────────────────────────
        var totalAssertions = caseResults.Sum(r => r.Assertions.Count);
        var passCount       = caseResults.Sum(r => r.Assertions.Count(a => a.Pass));
        var failCount       = totalAssertions - passCount;

        _logger.LogInformation(
            "내부 검증 완료: 전체 단언 {Total}건 / 통과 {Pass}건 / 실패 {Fail}건",
            totalAssertions, passCount, failCount);

        return Ok(new
        {
            generatedAt  = DateTimeOffset.UtcNow,
            note         = "내부 검증 전용. 프로덕션 환경 외부 공개 금지.",
            aggregate    = new
            {
                totalCases       = caseResults.Count,
                totalAssertions,
                passCount,
                failCount,
                allPass          = failCount == 0
            },
            cases = caseResults
        });
    }

    #endregion

    #region GET /api/debug/dev-act-permit/config

    [HttpGet("dev-act-permit/config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetDevelopmentActionPermitApiConfig([FromQuery] double? longitude = null, [FromQuery] double? latitude = null)
    {
        var options = _devActPermitApiOptions;
        var sampleQuery = longitude.HasValue && latitude.HasValue
            ? new CoordinateQuery(longitude.Value, latitude.Value)
            : null;
        var diagnostics = _developmentActionPermitApiProvider.GetDiagnostics(sampleQuery);

        return Ok(new
        {
            enabled = options.Enabled,
            baseUrl = options.BaseUrl,
            requestPath = options.RequestPath,
            hasUserId = !string.IsNullOrWhiteSpace(options.UserId),
            hasServiceKey = !string.IsNullOrWhiteSpace(options.ServiceKey),
            timeoutSeconds = options.TimeoutSeconds,
            query = new
            {
                longitudeParameterName = options.LongitudeParameterName,
                latitudeParameterName = options.LatitudeParameterName,
                userIdParameterName = options.UserIdParameterName,
                serviceKeyParameterName = options.ServiceKeyParameterName,
                staticQueryParameterKeys = diagnostics.StaticQueryParameterKeys,
                requestHeaderKeys = diagnostics.RequestHeaderKeys,
            },
            response = new
            {
                responseRootPath = diagnostics.EffectiveResponseRootPath,
                insideFieldPath = options.InsideFieldPath,
                nameFieldPath = options.NameFieldPath,
                codeFieldPath = options.CodeFieldPath,
            },
            readiness = new
            {
                canCall = diagnostics.CanCall,
                missing = diagnostics.MissingFields,
                sampleCoordinate = new
                {
                    longitude = sampleQuery?.Longitude ?? 127.3845,
                    latitude = sampleQuery?.Latitude ?? 36.3504,
                },
                maskedRequestUri = diagnostics.MaskedRequestUri,
            },
            effective = new
            {
                longitudeParameterName = diagnostics.EffectiveLongitudeParameterName,
                latitudeParameterName = diagnostics.EffectiveLatitudeParameterName,
                userIdParameterName = diagnostics.EffectiveUserIdParameterName,
                serviceKeyParameterName = diagnostics.EffectiveServiceKeyParameterName,
                responseRootPath = diagnostics.EffectiveResponseRootPath,
                insideFieldPath = diagnostics.EffectiveInsideFieldPath,
                nameFieldPath = diagnostics.EffectiveNameFieldPath,
                codeFieldPath = diagnostics.EffectiveCodeFieldPath,
            },
            guide = new
            {
                nextAction = diagnostics.CanCall
                    ? "maskedRequestUri를 기준으로 실제 API 요청 형식을 점검하고 좌표 테스트를 진행하세요."
                    : "누락 필드를 채운 뒤 동일 엔드포인트를 다시 호출해 readiness.canCall=true를 확인하세요.",
                note = "userId와 serviceKey는 마스킹되어 반환됩니다."
            },
        });
    }

    #endregion

    #region GET /api/debug/dev-act-permit/probe

    [HttpGet("dev-act-permit/probe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProbeDevelopmentActionPermitApiAsync(
        [FromQuery] double longitude,
        [FromQuery] double latitude,
        CancellationToken ct)
    {
        var query = new CoordinateQuery(longitude, latitude);
        var diagnostics = _developmentActionPermitApiProvider.GetDiagnostics(query);
        var overlay = diagnostics.CanCall
            ? await _developmentActionPermitApiProvider.TryGetOverlayAsync(query, ct)
            : null;

        return Ok(new
        {
            generatedAt = DateTimeOffset.UtcNow,
            request = new
            {
                longitude,
                latitude,
            },
            readiness = new
            {
                canCall = diagnostics.CanCall,
                missing = diagnostics.MissingFields,
                maskedRequestUri = diagnostics.MaskedRequestUri,
            },
            overlay = overlay is null ? null : new
            {
                isInside = overlay.IsInside,
                name = overlay.Name,
                code = overlay.Code,
                source = overlay.Source,
                confidence = overlay.Confidence.ToString(),
                note = overlay.Note,
            },
            guide = overlay is null
                ? "overlay가 null이면 설정 누락, API 응답 구조 불일치, 또는 실제 API 미연결 가능성을 우선 확인하세요."
                : "overlay.source=api 이고 isInside/name/code가 기대대로 나오면 API 경로가 정상입니다.",
        });
    }

    #endregion

    #region POST /api/debug/dev-act-permit/parse-sample

    [HttpPost("dev-act-permit/parse-sample")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ParseDevelopmentActionPermitSample([FromBody] DevActPermitSampleParseRequest request)
    {
        var diagnostics = _developmentActionPermitApiProvider.GetDiagnostics();
        var parse = _developmentActionPermitApiProvider.ParseSampleResponse(request.Json);

        return Ok(new
        {
            generatedAt = DateTimeOffset.UtcNow,
            readiness = new
            {
                canCall = diagnostics.CanCall,
                missing = diagnostics.MissingFields,
                responseRootPath = diagnostics.EffectiveResponseRootPath,
                insideFieldPath = diagnostics.EffectiveInsideFieldPath,
                nameFieldPath = diagnostics.EffectiveNameFieldPath,
                codeFieldPath = diagnostics.EffectiveCodeFieldPath,
            },
            parse = new
            {
                success = parse.Success,
                error = parse.Error,
                overlay = parse.Overlay is null ? null : new
                {
                    isInside = parse.Overlay.IsInside,
                    name = parse.Overlay.Name,
                    code = parse.Overlay.Code,
                    source = parse.Overlay.Source,
                    confidence = parse.Overlay.Confidence.ToString(),
                    note = parse.Overlay.Note,
                },
            },
            guide = parse.Success
                ? "샘플 JSON 파싱이 성공했습니다. 이제 동일 path 설정으로 probe와 실제 API 호출을 검증하면 됩니다."
                : "error 값을 기준으로 ResponseRootPath 또는 InsideFieldPath 설정을 먼저 조정하세요.",
        });
    }

    #endregion

    #region GET /api/debug/validate/dev-act-restriction

    private static readonly (string Label, double Lon, double Lat)[] DevActRestrictionCases =
    {
        ("UQ171 내부 후보 (개발행위허가제한지역 추정)",  127.3845, 36.3504),
        ("외부 일반 좌표",                              126.9780, 37.5665),
        ("경계 인접 추정",                              129.3110, 35.5387),
        ("커버리지 애매 후보",                          127.1480, 35.8242),
    };

    /// <summary>
    /// [내부 검증 전용] UQ171 개발행위허가제한지역 overlay 응답을 4개 좌표 케이스로 검증합니다.
    /// </summary>
    /// <remarks>
    /// UQ171 레이어(ATRB_SE=UQQ900)는 개발행위허가제한지역 데이터입니다.
    /// 지구단위계획구역 데이터가 아닙니다.
    /// </remarks>
    [HttpGet("validate/dev-act-restriction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateDevActRestrictionAsync(CancellationToken ct)
    {
        var results = new List<object>(DevActRestrictionCases.Length);

        foreach (var tc in DevActRestrictionCases)
        {
            var query  = new CoordinateQuery(tc.Lon, tc.Lat);
            var domain = await _service.CheckAsync(query, ct);
            var dto    = RegulationCheckResponseDto.MapFrom(domain);
            var dar    = dto.ExtraLayers.DevelopmentActionRestriction;

            results.Add(new
            {
                label     = tc.Label,
                longitude = tc.Lon,
                latitude  = tc.Lat,
                zoneName  = dto.Zoning?.ZoneName,
                developmentActionRestriction = dar is null ? null : new
                {
                    isInside   = dar.IsInside,
                    name       = dar.Name,
                    confidence = dar.Confidence,
                    note       = dar.Note,
                    source     = dar.Source,
                },
                cautionNotes = dto.RegulationSummary.Caution,
                summaryText  = dto.SummaryText,
                observations = BuildDevActRestrictionObservations(dar)
            });
        }

        return Ok(new
        {
            generatedAt = DateTimeOffset.UtcNow,
            note        = "UQ171 개발행위허가제한지역 overlay 내부 검증 전용. UQ171 = UQQ900(개발행위허가제한지역), 지구단위계획 아님.",
            totalCases  = results.Count,
            cases       = results
        });
    }

    #endregion

    #region GET /api/debug/dev-act-restriction/samples

    /// <summary>
    /// [내부 검증 전용] 로드된 UQ171 피처에서 WGS84 centroid/경계 인접 샘플 좌표를 추출합니다.
    /// isInside=true / NearBoundary 검증에 쓸 실좌표 확보용.
    /// </summary>
    [HttpGet("dev-act-restriction/samples")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevActRestrictionSamplesAsync(
        [FromQuery] int max = 15,
        CancellationToken ct = default)
    {
        if (_darProvider is not ShapefileDevActRestrictionProvider shpDar)
        {
            return Ok(new { available = false, provider = _darProvider.GetType().Name });
        }

        var samples = await shpDar.GetSamplePointsAsync(Math.Clamp(max, 1, 50), ct);

        var rows = samples.Select(s => new
        {
            featureIndex = s.FeatureIndex,
            name         = s.Name,
            code         = s.Code,
            dgmNm        = s.DgmNm,
            alias        = s.Alias,
            atrbSe       = s.AtrbSe,
            uqCode       = s.UqCode,
            areaSqM      = s.AreaSqM,
            centroid     = new { lon = s.CentroidLon, lat = s.CentroidLat },
            nearOutside  = s.NearOutsideLon.HasValue
                ? new { lon = s.NearOutsideLon.Value, lat = s.NearOutsideLat!.Value }
                : null,
            hint = $"centroid: {s.CentroidLat},{s.CentroidLon} | outside: {s.NearOutsideLat:F6},{s.NearOutsideLon:F6}"
        }).ToList();

        return Ok(new
        {
            generatedAt  = DateTimeOffset.UtcNow,
            note         = "UQ171(개발행위허가제한지역) 내부 검증용 샘플 좌표. " +
                           "centroid는 isInside=true 검증용, nearOutside는 NearBoundary 검증용.",
            totalSamples = rows.Count,
            samples      = rows
        });
    }

    private static IReadOnlyList<string> BuildDevActRestrictionObservations(
        OverlayZoneResultDto? dar)
    {
        var obs = new List<string>();

        if (dar is null)
        {
            obs.Add("DevelopmentActionRestriction이 null — MapOverlay 또는 DI 연결 확인 필요");
            return obs;
        }

        if (dar.IsInside && string.IsNullOrWhiteSpace(dar.Name))
            obs.Add("IsInside=true이지만 Name이 없음 — DGM_NM/ALIAS 속성 품질 확인 필요");

        if (!dar.IsInside && dar.Confidence == "DataUnavailable")
            obs.Add("DataUnavailable — UQ171 파일이 없거나 피처 수 0건. 파일 경로 및 패턴 확인 필요");

        if (!dar.IsInside && dar.Confidence == "NearBoundary")
            obs.Add("NearBoundary — 경계 200m 이내. 개발행위 허가 여부 현장 직접 확인 권장");

        if (obs.Count == 0)
            obs.Add("이상 징후 없음");

        return obs;
    }

    #endregion

    #region 단언 메서드

    /// <summary>
    /// 테스트 케이스에 대해 주요 단언을 실행합니다.
    /// </summary>
    private static IReadOnlyList<AssertionResult> RunAssertions(
        TestCase              tc,
        string                status,
        string?               zoneName,
        bool                  drpIsInside,
        bool                  needsReview,
        string                summaryText,
        IReadOnlyList<string> cautionNotes)
    {
        var list = new List<AssertionResult>();

        // ── A1: summaryText가 비어 있지 않고 최소 길이 충족 ──────────────────
        list.Add(Assert(
            "A1_SummaryText_NotEmpty",
            summaryText.Length > 20,
            $"summaryText.Length={summaryText.Length} (기대 > 20)"));

        // ── A2: 용도지역 발견 시 summaryText가 zoneName을 포함 ────────────────
        if (zoneName is not null)
        {
            list.Add(Assert(
                "A2_SummaryText_ContainsZoneName",
                summaryText.Contains(zoneName),
                $"summaryText에 '{zoneName}' 포함 여부"));
        }

        // ── A3: Preliminary 판정 시 needsAdditionalReview == true ─────────────
        if (status == "Preliminary")
        {
            list.Add(Assert(
                "A3_NeedsAdditionalReview_True",
                needsReview,
                $"needsAdditionalReview={needsReview} (Preliminary이면 항상 true여야 함)"));
        }

        // ── A4: 용도지역 발견 시 cautionNotes가 1건 이상 ─────────────────────
        if (status == "Preliminary")
        {
            list.Add(Assert(
                "A4_CautionNotes_NotEmpty",
                cautionNotes.Count > 0,
                $"cautionNotes.Count={cautionNotes.Count}"));
        }

        // ── A5: 그린벨트 내부 시 summaryText에 제한 문구 포함 ────────────────
        if (drpIsInside)
        {
            list.Add(Assert(
                "A5_GreenBelt_SummaryTextContainsRestriction",
                summaryText.Contains("제한"),
                $"drp.IsInside=true이므로 summaryText에 '제한' 포함 기대"));

            // ── A6: 그린벨트 내부 시 caution 첫 항목이 경고 문구 ────────────
            var firstCaution = cautionNotes.Count > 0 ? cautionNotes[0] : "";
            list.Add(Assert(
                "A6_GreenBelt_FirstCautionIsWarning",
                firstCaution.Contains("⚠") && firstCaution.Contains("개발제한구역"),
                $"첫 caution 항목: '{firstCaution}'"));
        }

        // ── A7: 용도지역 미발견 시 summaryText가 안내 문구 포함 ──────────────
        if (status == "NotFound")
        {
            list.Add(Assert(
                "A7_NotFound_SummaryContainsGuidance",
                summaryText.Contains("찾을 수 없습니다"),
                $"NotFound이므로 summaryText에 안내 문구 기대"));
        }

        // ── A8: 예상 용도지역 힌트 비교 (soft — 관찰용) ──────────────────────
        if (tc.ExpectedZoneHint is not null && zoneName is not null)
        {
            var hintMatch = zoneName.Contains(tc.ExpectedZoneHint);
            list.Add(Assert(
                "A8_ZoneHint_SoftMatch",
                hintMatch,
                $"ZoneName='{zoneName}', 기대 힌트='{tc.ExpectedZoneHint}' " +
                (hintMatch ? "(일치)" : "(불일치 — SHP 범위/데이터 확인 필요)")));
        }

        return list;
    }

    private static AssertionResult Assert(string name, bool pass, string detail) =>
        new(name, pass, detail);

    #endregion

    #region 관찰(Observation) 메서드

    /// <summary>
    /// 이상 징후가 있는 경우 원인 후보를 문자열로 기록합니다.
    /// </summary>
    private static IReadOnlyList<string> BuildObservations(
        TestCase tc,
        string   status,
        string?  zoneName,
        bool     drpIsInside,
        string   summaryText)
    {
        var obs = new List<string>();

        // 예상과 다른 그린벨트 판정
        if (tc.ExpectGreenBelt && !drpIsInside)
        {
            obs.Add("그린벨트 추정 좌표이지만 drp.IsInside=false — " +
                    "UQ141 SHP 레이어 커버리지 또는 좌표 확인 필요");
        }
        if (!tc.ExpectGreenBelt && drpIsInside)
        {
            obs.Add("그린벨트 비예상 좌표이지만 drp.IsInside=true — " +
                    "오버레이 판정 로직 또는 SHP 폴리곤 경계 확인 필요");
        }

        // 용도지역 미발견이지만 그린벨트 예상이 아닌 경우
        if (status == "NotFound" && tc.ExpectedZoneHint is not null)
        {
            obs.Add($"'{tc.ExpectedZoneHint}' 기대했으나 NotFound — " +
                    "SHP 데이터 범위 외 좌표이거나 Point-in-Polygon 불일치 가능성");
        }

        // summaryText 이상
        if (summaryText.Length <= 20)
        {
            obs.Add("summaryText가 너무 짧음 — SummaryTextBuilder 분기 로직 확인 필요");
        }

        // 힌트 있는데 zoneName null
        if (tc.ExpectedZoneHint is not null && zoneName is null && status != "NotFound")
        {
            obs.Add("용도지역명이 null이지만 status가 NotFound가 아님 — " +
                    "ZoningFeature.Name 매핑 또는 SHP 속성 필드 확인 필요");
        }

        if (obs.Count == 0)
            obs.Add("이상 징후 없음");

        return obs;
    }

    private static IReadOnlyList<string> BuildMissingFields(DevelopmentActionPermitApiOptions options)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            missing.Add("BaseUrl");
        if (string.IsNullOrWhiteSpace(options.UserId))
            missing.Add("UserId");
        if (string.IsNullOrWhiteSpace(options.ServiceKey))
            missing.Add("ServiceKey");
        if (string.IsNullOrWhiteSpace(options.InsideFieldPath))
            missing.Add("InsideFieldPath");
        if (string.IsNullOrWhiteSpace(options.LongitudeParameterName))
            missing.Add("LongitudeParameterName");
        if (string.IsNullOrWhiteSpace(options.LatitudeParameterName))
            missing.Add("LatitudeParameterName");

        return missing;
    }

    #endregion
}

#endregion


