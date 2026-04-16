using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.Law;

#region StubLawReferenceProvider class
/// <summary>
/// Stub implementation of <see cref="ILawReferenceProvider"/>.
/// </summary>
/// <remarks>
/// This provider returns an empty list in the current MVP stage.
/// Replace it with a real implementation when live law APIs are connected.
/// Example endpoints:
/// <list type="bullet">
/// <item><description><c>https://open.law.go.kr/LSW/openapi.do</c></description></item>
/// <item><description><c>https://open.law.go.kr/LSW/lawTermInfoServiceJO.do</c></description></item>
/// </list>
/// </remarks>
public sealed class StubLawReferenceProvider : ILawReferenceProvider
{
    private readonly ILogger<StubLawReferenceProvider> _logger;

    public StubLawReferenceProvider(ILogger<StubLawReferenceProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LawReference>> GetReferencesAsync(
        string zoningCode,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Law reference lookup skipped in stub provider. ZoningCode={Code}",
            zoningCode);

        IReadOnlyList<LawReference> empty = Array.Empty<LawReference>();
        return Task.FromResult(empty);
    }
}
#endregion
