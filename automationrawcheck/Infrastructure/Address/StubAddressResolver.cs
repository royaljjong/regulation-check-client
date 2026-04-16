// =============================================================================
// StubAddressResolver.cs
// IAddressResolver Stub 구현체 — 주소 변환 미구현 placeholder
// 항상 빈 목록을 반환합니다.
// =============================================================================

using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.Address;

#region StubAddressResolver 클래스

/// <summary>
/// 주소 → 좌표 변환 Stub 구현체입니다.
/// <para>현재 MVP 단계에서는 항상 빈 목록을 반환합니다.</para>
/// </summary>
public sealed class StubAddressResolver : IAddressResolver
{
    private static readonly IReadOnlyList<AddressResolveResult> Empty =
        Array.Empty<AddressResolveResult>();

    private readonly ILogger<StubAddressResolver> _logger;

    /// <summary>StubAddressResolver를 초기화합니다.</summary>
    public StubAddressResolver(ILogger<StubAddressResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AddressResolveResult>> ResolveAsync(
        string addressText,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "주소 → 좌표 변환 미구현 (Stub). 입력 주소: {Address}.", addressText);
        return Task.FromResult(Empty);
    }
}

#endregion
