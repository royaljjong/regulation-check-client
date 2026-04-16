// =============================================================================
// VWorldOptions.cs
// V-World 공간정보 오픈플랫폼 WFS API 설정 옵션
// appsettings.json "VWorld" 섹션에 바인딩됩니다.
//
// [발급처]
//   https://www.vworld.kr → 오픈API → API 키 신청 (회원가입 후 즉시 발급)
//
// [운영 환경 키 관리]
//   환경변수: VWorld__ApiKey=... 또는 dotnet user-secrets set "VWorld:ApiKey" "..."
// =============================================================================

namespace AutomationRawCheck.Infrastructure.Configuration;

#region VWorldOptions 클래스

/// <summary>
/// appsettings.json의 <c>"VWorld"</c> 섹션에 바인딩되는 V-World API 설정 클래스입니다.
/// </summary>
public sealed class VWorldOptions
{
    /// <summary>appsettings.json 섹션 키 이름</summary>
    public const string SectionName = "VWorld";

    /// <summary>
    /// V-World API 키.
    /// <para>비어 있으면 WfsDistrictUnitPlanProvider가 DataUnavailable을 반환합니다.</para>
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// V-World Data API 기본 URL.
    /// 기본값: https://api.vworld.kr/req/data
    /// </summary>
    public string DataBaseUrl { get; set; } = "https://api.vworld.kr/req/data";

    /// <summary>
    /// V-World 주소 좌표 검색 API 기본 URL.
    /// 기본값: https://api.vworld.kr/req/address
    /// </summary>
    public string GeocodingBaseUrl { get; set; } = "https://api.vworld.kr/req/address";

    /// <summary>
    /// 지구단위계획구역 레이어명 (V-World Data API 기준).
    /// <para>
    /// GetCapabilities 확인 결과: lt_c_upisuq161 = 지구단위계획
    /// </para>
    /// </summary>
    public string DistrictUnitPlanLayer { get; set; } = "LT_C_UPISUQ161";

    /// <summary>
    /// HTTP 요청 타임아웃 (초).
    /// 기본값: 5초. 초과 시 DataUnavailable 반환.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}

#endregion
