// =============================================================================
// RegulationCheckService.cs
// 규제 검토 핵심 서비스 - 모든 프로바이더를 조합해 1차 판정 결과를 생성합니다.
// =============================================================================

using System.Diagnostics;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Application.Law;
using AutomationRawCheck.Domain.Models;
using AutomationRawCheck.Infrastructure.Configuration;
using AutomationRawCheck.Infrastructure.ExtraLayers;
using AutomationRawCheck.Infrastructure.Spatial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Application.Services;

#region RegulationCheckService 클래스

public sealed class RegulationCheckService : IRegulationCheckService
{
    #region 상수

    private const string NotFoundMessage =
        "입력 좌표에 해당하는 용도지역 정보를 찾을 수 없습니다. " +
        "좌표(WGS84 경도/위도)가 올바른지 확인하거나 관할 기관에 문의하세요.";

    private const string PreliminaryMessage =
        "용도지역 기반 1차 판정 결과입니다. " +
        "지구단위계획, 개발제한구역, 자치법규, 개별 법령 검토가 추가로 필요할 수 있습니다. " +
        "본 결과는 참고용이며 확정 법규 판단의 근거로 사용할 수 없습니다.";

    #endregion

    #region 필드 및 생성자

    private readonly IZoningLayerProvider            _zoningProvider;
    private readonly ILawReferenceProvider           _lawProvider;
    private readonly IDistrictUnitPlanProvider       _dupProvider;
    private readonly IDevelopmentRestrictionProvider _drpProvider;
    private readonly IDevActRestrictionProvider      _darProvider;
    private readonly VWorldDevRestrictionProvider    _vworldDevProvider;
    private readonly VWorldApiOptions                _vworldApiOptions;
    private readonly DevelopmentActionPermitApiProvider _devActPermitApiProvider;
    private readonly DevelopmentActionPermitApiOptions _devActPermitApiOptions;
    private readonly ILogger<RegulationCheckService> _logger;

    public RegulationCheckService(
        IZoningLayerProvider            zoningProvider,
        ILawReferenceProvider           lawProvider,
        IDistrictUnitPlanProvider       dupProvider,
        IDevelopmentRestrictionProvider drpProvider,
        IDevActRestrictionProvider      darProvider,
        VWorldDevRestrictionProvider    vworldDevProvider,
        IOptions<VWorldApiOptions>      vworldApiOptions,
        DevelopmentActionPermitApiProvider devActPermitApiProvider,
        IOptions<DevelopmentActionPermitApiOptions> devActPermitApiOptions,
        ILogger<RegulationCheckService> logger)
    {
        _zoningProvider    = zoningProvider    ?? throw new ArgumentNullException(nameof(zoningProvider));
        _lawProvider       = lawProvider       ?? throw new ArgumentNullException(nameof(lawProvider));
        _dupProvider       = dupProvider       ?? throw new ArgumentNullException(nameof(dupProvider));
        _drpProvider       = drpProvider       ?? throw new ArgumentNullException(nameof(drpProvider));
        _darProvider       = darProvider       ?? throw new ArgumentNullException(nameof(darProvider));
        _vworldDevProvider = vworldDevProvider ?? throw new ArgumentNullException(nameof(vworldDevProvider));
        _vworldApiOptions  = vworldApiOptions?.Value ?? throw new ArgumentNullException(nameof(vworldApiOptions));
        _devActPermitApiProvider = devActPermitApiProvider ?? throw new ArgumentNullException(nameof(devActPermitApiProvider));
        _devActPermitApiOptions = devActPermitApiOptions?.Value ?? throw new ArgumentNullException(nameof(devActPermitApiOptions));
        _logger            = logger            ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IRegulationCheckService 구현

    public async Task<RegulationCheckResult> CheckAsync(
        CoordinateQuery query,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("규제 검토 시작: Lon={Lon}, Lat={Lat}", query.Longitude, query.Latitude);

        var (zoning, debugReason, nearest, zoningRaw) = await _zoningProvider.GetDebugZoningAsync(query, ct);

        var meta = (_zoningProvider is ShapefileZoningLayerProvider shpProvider)
            ? shpProvider.GetCurrentMeta() ?? SpatialLayerMeta.NoData()
            : SpatialLayerMeta.NoData();

        if (zoning is null)
        {
            _logger.LogWarning(
                "용도지역 미발견: {Reason}, Lon={Lon}, Lat={Lat}",
                debugReason, query.Longitude, query.Latitude);

            var drpNotFound       = await _drpProvider.GetOverlayAsync(query, ct);
            var drpMergedNotFound = await MergeWithVWorldAsync(drpNotFound, query, ct);
            var dupNotFound       = await _dupProvider.GetOverlayAsync(query, ct);
            var darNotFoundRaw    = await _darProvider.GetOverlayAsync(query, ct);
            var darNotFound       = await MergeWithDevActPermitApiAsync(darNotFoundRaw, query, ct);

            sw.Stop();
            _logger.LogInformation(
                "규제 검토 완료(NotFound): ElapsedMs={Ms}, DrpSource={DrpSrc}, DupInside={DupIn}",
                sw.ElapsedMilliseconds, drpMergedNotFound.Source, dupNotFound.IsInside);

            return new RegulationCheckResult(
                input:             query,
                zoning:            null,
                regulationSummary: new RegulationSummary(RegulationStatus.NotFound, NotFoundMessage),
                lawReferences:     Array.Empty<LawReference>(),
                extraLayers:       new ExtraLayerInfo(dupNotFound, drpMergedNotFound, darNotFound),
                layerMeta:         meta,
                regulationInfo:    null,
                debugReason:       debugReason,
                nearestDistance:   nearest,
                zoningRaw:         zoningRaw);
        }

        _logger.LogInformation("용도지역 발견: Name={Name}, Code={Code}", zoning.Name, zoning.Code);

        IReadOnlyList<LawReference> lawRefs =
            await _lawProvider.GetReferencesAsync(zoning.Name, ct);

        var dupResult = await _dupProvider.GetOverlayAsync(query, ct);
        var drpResult = await _drpProvider.GetOverlayAsync(query, ct);
        var drpMerged = await MergeWithVWorldAsync(drpResult, query, ct);
        var darResultRaw = await _darProvider.GetOverlayAsync(query, ct);
        var darResult = await MergeWithDevActPermitApiAsync(darResultRaw, query, ct);

        var regulationInfo = ZoningRuleTable.GetInfo(zoning.Code);

        sw.Stop();
        _logger.LogInformation(
            "규제 검토 완료: ElapsedMs={Ms}, Zone={Zone}, LawCount={Laws}, " +
            "DrpSource={DrpSrc}, DrpInside={DrpIn}, DupInside={DupIn}, DarInside={DarIn}",
            sw.ElapsedMilliseconds, zoning.Name, lawRefs.Count,
            drpMerged.Source, drpMerged.IsInside, dupResult.IsInside, darResult.IsInside);

        return new RegulationCheckResult(
            input:             query,
            zoning:            zoning,
            regulationSummary: new RegulationSummary(RegulationStatus.Preliminary, PreliminaryMessage),
            lawReferences:     lawRefs,
            extraLayers:       new ExtraLayerInfo(dupResult, drpMerged, darResult),
            layerMeta:         meta,
            regulationInfo:    regulationInfo,
            debugReason:       debugReason,
            nearestDistance:   nearest,
            zoningRaw:         zoningRaw);
    }

    #endregion

    #region VWorld 개발제한구역 병합

    /// <summary>
    /// SHP 결과와 VWorld API 결과를 병합합니다.
    /// Source 필드를 "api" | "shp" | "none" 중 하나로 반환하며, 이후 DTO 매핑에서 status를 결정합니다.
    /// </summary>
    private async Task<OverlayZoneResult> MergeWithVWorldAsync(
        OverlayZoneResult shpResult,
        CoordinateQuery   query,
        CancellationToken ct)
    {
        var shpAvailable = shpResult.Confidence != OverlayConfidenceLevel.DataUnavailable;

        // API 비활성화 → SHP 결과 그대로 source 태그만 표준화
        if (!_vworldApiOptions.Enabled)
        {
            return shpAvailable
                ? shpResult with { Source = "shp" }
                : shpResult with { Source = "none" };
        }

        var apiResult = await _vworldDevProvider.IsInDevelopmentRestrictionAsync(
            query.Longitude, query.Latitude, ct);

        // [Case 1] API 성공 → source="api", status="confirmed"
        if (apiResult is not null)
        {
            _logger.LogInformation(
                "[VWorldDevRestriction] API 결과 적용: IsInside={Api}, SHP={Shp} (Point={Lon},{Lat})",
                apiResult.Value, shpResult.IsInside, query.Longitude, query.Latitude);

            var note = apiResult.Value
                ? "개발제한구역 포함 (VWorld LT_C_UD801 API 확인)"
                : "개발제한구역 미포함 (VWorld LT_C_UD801 API 확인)";

            return new OverlayZoneResult(
                IsInside:   apiResult.Value,
                Name:       apiResult.Value ? (shpResult.Name ?? "개발제한구역") : null,
                Code:       apiResult.Value ? (shpResult.Code ?? "LT_C_UD801") : null,
                Source:     "api",
                Note:       note,
                Confidence: OverlayConfidenceLevel.Normal);
        }

        // [Case 2] API 실패 + SHP 있음 → source="shp", status="fallback"
        if (shpAvailable)
        {
            _logger.LogDebug(
                "[VWorldDevRestriction] API 실패 → SHP fallback (IsInside={IsInside})",
                shpResult.IsInside);

            var fallbackNote = shpResult.IsInside
                ? "개발제한구역 포함 (SHP 로컬 데이터 — API 조회 실패)"
                : "개발제한구역 미포함 (SHP 로컬 데이터 — API 조회 실패)";

            return shpResult with { Source = "shp", Note = fallbackNote };
        }

        // [Case 3] API 실패 + SHP 없음 → source="none", status="unavailable"
        _logger.LogDebug("[VWorldDevRestriction] API 실패 + SHP 없음 → unavailable (Point={Lon},{Lat})",
            query.Longitude, query.Latitude);

        return new OverlayZoneResult(
            IsInside:   false,
            Name:       null,
            Code:       null,
            Source:     "none",
            Note:       "API 및 SHP 데이터 모두 조회 불가",
            Confidence: OverlayConfidenceLevel.DataUnavailable);
    }

    /// <summary>
    /// SHP-based development-action restriction results can be elevated by an external API when configured.
    /// </summary>
    private async Task<OverlayZoneResult> MergeWithDevActPermitApiAsync(
        OverlayZoneResult shpResult,
        CoordinateQuery query,
        CancellationToken ct)
    {
        var shpAvailable = shpResult.Confidence != OverlayConfidenceLevel.DataUnavailable;

        if (!_devActPermitApiOptions.Enabled)
        {
            return shpAvailable
                ? shpResult with { Source = "shp" }
                : shpResult with { Source = "none" };
        }

        var apiResult = await _devActPermitApiProvider.TryGetOverlayAsync(query, ct);
        if (apiResult is not null)
        {
            _logger.LogInformation(
                "[DevelopmentActionPermitApi] API result applied: IsInside={Api}, SHP={Shp} (Point={Lon},{Lat})",
                apiResult.IsInside, shpResult.IsInside, query.Longitude, query.Latitude);
            return apiResult;
        }

        if (shpAvailable)
        {
            var fallbackNote = shpResult.IsInside
                ? "개발행위허가 제한 구역 포함 (SHP 로컬 데이터 기준, API 조회 실패 또는 미지원)"
                : "개발행위허가 제한 구역 미포함 (SHP 로컬 데이터 기준, API 조회 실패 또는 미지원)";
            return shpResult with { Source = "shp", Note = fallbackNote };
        }

        return new OverlayZoneResult(
            IsInside: false,
            Name: null,
            Code: null,
            Source: "none",
            Note: "개발행위허가 정보 API와 SHP 결과를 모두 확인할 수 없습니다.",
            Confidence: OverlayConfidenceLevel.DataUnavailable);
    }

    #endregion
}

#endregion
