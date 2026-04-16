// =============================================================================
// NullDistrictUnitPlanProvider.cs
// 지구단위계획구역 Null Object 구현체 — 현재 데이터셋 기준 미연동 상태
//
// [현황]
//   KLIP_004_20260201 데이터셋에 지구단위계획구역 레이어가 포함되어 있지 않습니다.
//   UQ171 파일은 개발행위허가제한지역(ATRB_SE=UQQ900) 데이터이므로 지구단위계획 대용
//   불가합니다.
//
// [연동 방법]
//   1. 지구단위계획구역 SHP 데이터를 별도 확보 (국가공간정보포털 또는 V-World API)
//   2. ShapefileDistrictUnitPlanProvider 구현 후 Program.cs DI 교체
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.ExtraLayers;

#region NullDistrictUnitPlanProvider 클래스

/// <summary>
/// 지구단위계획구역 조회 Null Object 구현체입니다.
/// <para>
/// 현재 데이터셋에 지구단위계획구역(UQ150~) SHP가 없어 데이터 미보유 상태를 반환합니다.
/// </para>
/// </summary>
public sealed class NullDistrictUnitPlanProvider : IDistrictUnitPlanProvider
{
    #region 상수

    private const string Source =
        "지구단위계획구역 (현재 데이터셋 미연동 — KLIP_004_20260201 기준)";

    private const string Note =
        "지구단위계획 데이터는 현재 연동 범위에 포함되지 않아 추가 확인이 필요합니다. " +
        "관할 지자체 도시계획부서 또는 토지이음(eum.go.kr) 직접 확인을 권장합니다.";

    #endregion

    #region 필드 및 생성자

    private readonly ILogger<NullDistrictUnitPlanProvider> _logger;

    /// <summary>NullDistrictUnitPlanProvider를 초기화합니다.</summary>
    public NullDistrictUnitPlanProvider(ILogger<NullDistrictUnitPlanProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IDistrictUnitPlanProvider 구현

    /// <inheritdoc/>
    public Task<OverlayZoneResult> GetOverlayAsync(
        CoordinateQuery query,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "지구단위계획구역 조회 (데이터 미보유). Lon={Lon}, Lat={Lat}",
            query.Longitude, query.Latitude);

        return Task.FromResult(new OverlayZoneResult(
            IsInside: false,
            Name:     null,
            Code:     null,
            Source:   Source,
            Note:     Note));
    }

    #endregion
}

#endregion
