using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AutomationRawCheck.Infrastructure.Parcel;

/// <summary>
/// Resolves parcel/road address text through the configured address resolver
/// and returns the best coordinate candidate for legacy /parcel flows.
/// </summary>
public sealed class VWorldParcelSearchProvider : IParcelSearchProvider
{
    private readonly IAddressResolver _addressResolver;
    private readonly ILogger<VWorldParcelSearchProvider> _logger;

    public VWorldParcelSearchProvider(
        IAddressResolver addressResolver,
        ILogger<VWorldParcelSearchProvider> logger)
    {
        _addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CoordinateQuery?> ResolveAddressAsync(string addressText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(addressText))
            return null;

        var candidates = await _addressResolver.ResolveAsync(addressText, ct).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            _logger.LogWarning("Parcel search geocoding returned no candidates. Address={Address}", addressText);
            return null;
        }

        var best = candidates[0];
        _logger.LogInformation(
            "Parcel search resolved address via {Provider}. Address={Address}, Type={AddressType}, Lon={Lon}, Lat={Lat}",
            best.Provider ?? "unknown",
            best.NormalizedAddress ?? addressText,
            best.AddressType ?? "unknown",
            best.Coordinate.Longitude,
            best.Coordinate.Latitude);

        return best.Coordinate;
    }
}
