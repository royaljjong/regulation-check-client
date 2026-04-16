// =============================================================================
// OverlayZoneResult.cs
// 공간 레이어 중첩 판정 결과 도메인 모델
// - 지구단위계획구역, 개발제한구역, 도시자연공원구역 등 중첩 레이어 판정에 사용합니다.
// =============================================================================

namespace AutomationRawCheck.Domain.Models;

#region OverlayConfidenceLevel 열거형

/// <summary>
/// 오버레이 판정의 데이터 신뢰도를 나타냅니다.
/// <para>
/// 판정값(IsInside)과 별개로, 해당 판정이 얼마나 신뢰할 수 있는 데이터 기반인지 표현합니다.
/// </para>
/// </summary>
public enum OverlayConfidenceLevel
{
    /// <summary>
    /// 정상 판정. 데이터가 유효하고 결과를 신뢰할 수 있습니다.
    /// </summary>
    Normal,

    /// <summary>
    /// 경계 근접. 좌표가 폴리곤 경계 200m 이내에 있어 정밀도 한계로 결과가 불확실합니다.
    /// </summary>
    NearBoundary,

    /// <summary>
    /// 데이터 없음. SHP 파일 미로드 또는 필터 후 피처 없음 상태입니다.
    /// 이 경우 IsInside=false는 "해당 없음"이 아니라 "확인 불가"를 의미합니다.
    /// </summary>
    DataUnavailable,
}

#endregion

#region OverlayZoneResult 레코드

/// <summary>
/// 특정 공간 레이어(지구단위계획구역, 개발제한구역 등)에 입력 좌표가 포함되는지
/// 판정한 결과를 나타냅니다.
/// </summary>
/// <param name="IsInside">해당 구역 폴리곤 내부에 좌표가 포함되면 <c>true</c>.</param>
/// <param name="Name">발견된 구역 명칭 (예: "개발제한구역"). 미포함 시 <c>null</c>.</param>
/// <param name="Code">발견된 구역 코드 (예: "UDV100"). 미포함 시 <c>null</c>.</param>
/// <param name="Source">
/// 원천 데이터 식별자.
/// 데이터 미보유 시 "데이터 미보유 (향후 연동 예정)" 등 안내 문자열.
/// </param>
/// <param name="Note">추가 안내 메시지 (null 허용).</param>
/// <param name="Confidence">
/// 판정 데이터 신뢰도.
/// Normal = 정상, NearBoundary = 경계 근접, DataUnavailable = 데이터 없음.
/// 기본값: Normal.
/// </param>
public record OverlayZoneResult(
    bool    IsInside,
    string? Name,
    string? Code,
    string  Source,
    string? Note       = null,
    OverlayConfidenceLevel Confidence = OverlayConfidenceLevel.Normal);

#endregion
