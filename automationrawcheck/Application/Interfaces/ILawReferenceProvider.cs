// =============================================================================
// ILawReferenceProvider.cs
// 법령 참조 프로바이더 인터페이스
// 구현체: Infrastructure/Law/StubLawReferenceProvider.cs
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region ILawReferenceProvider 인터페이스

/// <summary>
/// 용도지역 코드에 해당하는 관련 법령 참조 목록을 제공하는 인터페이스입니다.
/// <para>
/// 현재 구현체: <c>StubLawReferenceProvider</c> — 빈 목록 반환 (stub).
/// </para>
/// <para>
/// TODO (확장 포인트):
/// 법제처 국가법령정보 공동활용 API 연동 시 이 인터페이스를 구현하는
/// <c>LawInfoApiProvider</c> 클래스를 만들고 DI만 교체하면 됩니다.
/// API 키: appsettings.json "LawApi:ServiceKey" 섹션에 저장.
/// </para>
/// </summary>
public interface ILawReferenceProvider
{
    #region 메서드

    /// <summary>
    /// 용도지역 코드에 해당하는 관련 법령 참조 목록을 반환합니다.
    /// </summary>
    /// <param name="zoningCode">용도지역 코드 (예: UQ110)</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>
    /// 관련 법령 참조 목록.
    /// Stub 구현은 빈 목록을 반환합니다.
    /// </returns>
    Task<IReadOnlyList<LawReference>> GetReferencesAsync(string zoningCode, CancellationToken ct = default);

    #endregion
}

#endregion
