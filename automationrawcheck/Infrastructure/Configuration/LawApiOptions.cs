// =============================================================================
// LawApiOptions.cs
// 법제처 국가법령정보 공동활용 API 설정 옵션
// appsettings.json "LawApi" 섹션에 바인딩됩니다.
// =============================================================================

namespace AutomationRawCheck.Infrastructure.Configuration;

#region LawApiOptions 클래스

/// <summary>
/// appsettings.json의 <c>"LawApi"</c> 섹션에 바인딩되는 법제처 API 설정 클래스입니다.
/// <para>
/// 현재 MVP 단계에서는 실제 API를 호출하지 않으며,
/// <c>StubLawReferenceProvider</c>가 이 설정을 무시합니다.
/// </para>
/// TODO (확장 포인트):
/// 실제 법제처 API 연동 시 <c>LawInfoApiProvider</c>에서 이 옵션을 주입받아 사용합니다.
/// 운영 환경에서는 ServiceKey를 환경변수(LawApi__ServiceKey) 또는 UserSecrets로 관리하세요.
/// </summary>
public sealed class LawApiOptions
{
    #region 섹션 키

    /// <summary>appsettings.json 섹션 키 이름</summary>
    public const string SectionName = "LawApi";

    #endregion

    #region 설정 프로퍼티

    /// <summary>
    /// 법제처 API 활성화 여부 (기존 ILawReferenceProvider 용).
    /// false이면 <c>LawReferenceHttpProvider</c>가 stub 모드로 동작합니다.
    /// 기본값: false.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// normalizedKey 기반 조문 조회 활성화 여부 (ILawClauseProvider 용).
    /// true이면 <c>OpenLawClauseProvider</c>가 법제처 DRF API를 호출합니다.
    /// 기본값: true (OcValue 설정 시 즉시 동작).
    /// </summary>
    public bool ClauseEnabled { get; set; } = true;

    /// <summary>
    /// 법제처 DRF API OC 파라미터 값 (인증 식별자).
    /// 미설정 시 ServiceKey를 fallback으로 사용합니다.
    /// <para>설정: appsettings.json "LawApi:OcValue" 또는 환경변수 LawApi__OcValue</para>
    /// </summary>
    public string OcValue { get; set; } = string.Empty;

    /// <summary>
    /// 법제처 국가법령정보 공동활용 API 서비스 키 (보조).
    /// OcValue가 비어 있을 때 fallback으로 사용됩니다.
    /// </summary>
    public string ServiceKey { get; set; } = string.Empty;

    /// <summary>
    /// API 기본 URL.
    /// 기본값: https://open.law.go.kr/LSW/openapi.do
    /// </summary>
    public string BaseUrl { get; set; } = "http://www.law.go.kr/DRF";

    /// <summary>
    /// HTTP 요청 타임아웃 (초).
    /// 기본값: 10초.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// HTTP 재시도 횟수 (0 = 재시도 안 함).
    /// 재시도 간격: 1초 × 지수 백오프 (1s → 2s).
    /// 기본값: 1 (1회 재시도).
    /// </summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>
    /// includeLegalBasis=true 응답에서 항목당 최대 조문 수.
    /// 초과 시 상위 항목만 반환 (0 = 무제한).
    /// 기본값: 10.
    /// </summary>
    public int MaxClausesPerItem { get; set; } = 10;

    /// <summary>
    /// 조문 텍스트 최대 글자 수. 초과 시 "…" 붙임.
    /// 기본값: 500.
    /// </summary>
    public int ClauseMaxLength { get; set; } = 500;

    /// <summary>
    /// 법령용어 API URL.
    /// 기본값: https://open.law.go.kr/LSW/lawTermInfoServiceJO.do
    /// TODO: 법령용어 검색 기능 구현 시 사용.
    /// </summary>
    public string TermSearchUrl { get; set; } = "https://open.law.go.kr/LSW/lawTermInfoServiceJO.do";

    #endregion
}

#endregion
