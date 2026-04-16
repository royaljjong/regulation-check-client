// =============================================================================
// IRegulationCheckService.cs
// 규제 검토 서비스 인터페이스 - 핵심 비즈니스 로직 추상화
// =============================================================================

using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Interfaces;

#region IRegulationCheckService 인터페이스 정의

/// <summary>
/// 좌표 입력을 받아 건축/토지 법규 1차 검토 결과를 반환하는 서비스 인터페이스입니다.
/// </summary>
public interface IRegulationCheckService
{
    /// <summary>
    /// 좌표 기반 법규 1차 검토를 수행합니다.
    /// </summary>
    /// <param name="query">경도/위도 좌표 쿼리</param>
    /// <param name="ct">취소 토큰</param>
    /// <returns>규제 검토 결과 (참고용 1차 판정)</returns>
    Task<RegulationCheckResult> CheckAsync(CoordinateQuery query, CancellationToken ct = default);
}

#endregion
