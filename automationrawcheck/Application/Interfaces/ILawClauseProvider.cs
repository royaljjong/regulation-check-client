// =============================================================================
// ILawClauseProvider.cs
// normalizedKey 기반 법제처 조문 텍스트 조회 인터페이스
//
// [구현체]
//   OpenLawClauseProvider — 법제처 DRF API + IMemoryCache 3단계 캐싱
//
// [사용 위치]
//   RegulationCheckController — includeLegalBasis=true 쿼리 파라미터 시 호출
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

/// <summary>
/// normalizedKey를 입력받아 법제처 DRF API에서 조문 원문 텍스트를 조회합니다.
/// <para>
/// 단건 조회: <see cref="GetClauseAsync"/><br/>
/// 일괄 조회: <see cref="GetClausesAsync"/> — 내부적으로 최대 3개 병렬 + 캐시 공유
/// </para>
/// <para>
/// API 실패·고시·조례 플레이스홀더는 null을 반환하며 시스템을 중단하지 않습니다.
/// </para>
/// </summary>
public interface ILawClauseProvider
{
    /// <summary>
    /// normalizedKey 하나에 대한 조문 텍스트를 반환합니다.
    /// </summary>
    /// <param name="normalizedKey">예: "건축법/11", "주차장법시행령/별표1"</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>조회 결과 또는 null (실패/미지원)</returns>
    Task<LawClauseResult?> GetClauseAsync(string normalizedKey, CancellationToken ct = default);

    /// <summary>
    /// normalizedKey 목록에 대해 일괄 조회합니다.
    /// </summary>
    /// <param name="normalizedKeys">조회할 키 목록 (중복 자동 제거)</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>성공한 키만 포함된 딕셔너리 (실패 키는 제외)</returns>
    Task<IReadOnlyDictionary<string, LawClauseResult>> GetClausesAsync(
        IEnumerable<string> normalizedKeys,
        CancellationToken   ct = default);
}
