// =============================================================================
// RegulationCheckResult.cs
// 규제 검토 최종 결과 도메인 모델 - API 응답의 핵심 데이터 구조
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region RegulationCheckResult 레코드

/// <summary>
/// 좌표 기반 건축/토지 법규 1차 검토 결과 도메인 모델입니다.
/// <para>
/// 주의: 오프라인 공간데이터 기반 참고용 1차 판정이며,
/// 실제 건축 허가 판단의 근거로 사용해서는 안 됩니다.
/// </para>
/// </summary>
public record RegulationCheckResult
{
    /// <summary>입력 좌표 쿼리</summary>
    public CoordinateQuery Input { get; init; }

    /// <summary>판정된 용도지역 피처 (해당 없으면 null)</summary>
    public ZoningFeature? Zoning { get; init; }

    /// <summary>규제 검토 요약 (상태 + 안내 메시지)</summary>
    public RegulationSummary RegulationSummary { get; init; }

    /// <summary>관련 법령 참조 목록 (현재 stub)</summary>
    public IReadOnlyList<LawReference> LawReferences { get; init; }

    /// <summary>지구단위계획, 개발제한구역 등 추가 레이어 중첩 판정 결과</summary>
    public ExtraLayerInfo ExtraLayers { get; init; }

    /// <summary>
    /// 용도지역 코드 기반 건축 법규 참고 정보 (국토계획법 기준 참고값).
    /// <para>확정 수치가 아니며 조례·지구단위계획 추가 확인이 필요합니다.</para>
    /// null이면 해당 코드 매핑 없음 또는 용도지역 미발견.
    /// </summary>
    public ZoningRegulationInfo? RegulationInfo { get; init; }

    /// <summary>
    /// 공간 레이어 메타데이터.
    /// 원천 파일명, 로드 시각, 피처 수 등 데이터 추적 정보를 포함합니다.
    /// </summary>
    public SpatialLayerMeta LayerMeta { get; init; }

    /// <summary>진단용 사유 (내부용)</summary>
    public string? DebugReason { get; init; }

    /// <summary>가장 가까운 피처까지의 거리 (m). NotFound 시 진단용.</summary>
    public double? NearestDistance { get; init; }

    /// <summary>매칭된 피처의 원본 속성 문자열 (JSON 또는 텍스트)</summary>
    public string? ZoningRaw { get; init; }

    /// <summary>RegulationCheckResult를 초기화합니다.</summary>
    public RegulationCheckResult(
        CoordinateQuery             input,
        ZoningFeature?              zoning,
        RegulationSummary           regulationSummary,
        IReadOnlyList<LawReference> lawReferences,
        ExtraLayerInfo              extraLayers,
        SpatialLayerMeta            layerMeta,
        ZoningRegulationInfo?       regulationInfo = null,
        string?                     debugReason    = null,
        double?                     nearestDistance = null,
        string?                     zoningRaw      = null)
    {
        Input             = input;
        Zoning            = zoning;
        RegulationSummary = regulationSummary;
        LawReferences     = lawReferences;
        ExtraLayers       = extraLayers;
        LayerMeta         = layerMeta;
        RegulationInfo    = regulationInfo;
        DebugReason       = debugReason;
        NearestDistance   = nearestDistance;
        ZoningRaw         = zoningRaw;
    }
}

#endregion
