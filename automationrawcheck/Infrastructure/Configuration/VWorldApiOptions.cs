// =============================================================================
// VWorldApiOptions.cs
// VWorld 개발제한구역 API 전용 설정 옵션
// appsettings.json "VWorldApi" 섹션에 바인딩됩니다.
// =============================================================================

namespace AutomationRawCheck.Infrastructure.Configuration;

#region VWorldApiOptions 클래스

/// <summary>
/// appsettings.json의 <c>"VWorldApi"</c> 섹션에 바인딩되는 VWorld 개발제한구역 API 설정입니다.
/// </summary>
public sealed class VWorldApiOptions
{
    /// <summary>appsettings.json 섹션 키 이름</summary>
    public const string SectionName = "VWorldApi";

    /// <summary>
    /// VWorld 개발제한구역 API 활성화 여부.
    /// false이면 API 호출 없이 SHP 결과만 사용합니다.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// VWorld Data API 기본 URL.
    /// 기본값: https://api.vworld.kr/req/data
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.vworld.kr/req/data";

    /// <summary>
    /// VWorld API 인증키.
    /// 코드에 하드코딩 금지 — 반드시 설정에서 읽을 것.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// VWorld API 도메인 파라미터 (빈 문자열 허용).
    /// </summary>
    public string Domain { get; set; } = string.Empty;
}

#endregion
