// =============================================================================
// StubParcelSearchProvider.cs
// 지번/주소 → 좌표 변환 stub 구현체
// - 현재는 항상 null 반환 (미구현 상태).
// - 향후 외부 주소 API 연동 시 이 클래스를 실제 구현으로 교체하세요.
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.Parcel;

#region StubParcelSearchProvider 클래스

/// <summary>
/// 주소 → 좌표 변환 stub 구현체입니다.
/// <para>현재 MVP 단계에서는 주소 변환 기능을 제공하지 않습니다.</para>
/// TODO: 외부 주소 검색 API 연동 시 이 클래스를 교체 또는 상속하여 구현하세요.
/// 연동 후보: VWorld 주소 검색 API, 카카오맵 주소 API, 행정안전부 도로명주소 API
/// </summary>
public sealed class StubParcelSearchProvider : IParcelSearchProvider
{
    #region 필드 및 생성자

    private readonly ILogger<StubParcelSearchProvider> _logger;

    /// <summary>StubParcelSearchProvider를 초기화합니다.</summary>
    public StubParcelSearchProvider(ILogger<StubParcelSearchProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region IParcelSearchProvider 구현

    /// <inheritdoc/>
    /// <remarks>
    /// TODO: 실제 주소 → 좌표 변환 서비스 연동 위치.
    /// 현재는 항상 null을 반환합니다 (주소 검색 미구현 상태).
    /// </remarks>
    public Task<CoordinateQuery?> ResolveAddressAsync(string addressText, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "주소 → 좌표 변환 미구현 (Stub). 요청 주소: {Address}. " +
            "향후 외부 주소 검색 API 연동 후 활성화됩니다.",
            addressText);

        // TODO: 외부 주소 검색 API 호출 구현
        return Task.FromResult<CoordinateQuery?>(null);
    }

    #endregion
}

#endregion
