namespace AutomationRawCheck.Domain.Models;

#region AddressResolveResult record
/// <summary>
/// Represents one address-to-coordinate resolution candidate.
/// </summary>
public record AddressResolveResult
{
    /// <summary>Resolved WGS84 coordinate.</summary>
    public CoordinateQuery Coordinate { get; init; }

    /// <summary>Normalized address text when available.</summary>
    public string? NormalizedAddress { get; init; }

    /// <summary>Provider name used for geocoding.</summary>
    public string? Provider { get; init; }

    /// <summary>Address type such as <c>road</c> or <c>parcel</c>.</summary>
    public string? AddressType { get; init; }

    public AddressResolveResult(
        CoordinateQuery coordinate,
        string? normalizedAddress = null,
        string? provider = null,
        string? addressType = null)
    {
        Coordinate = coordinate;
        NormalizedAddress = normalizedAddress;
        Provider = provider;
        AddressType = addressType;
    }
}
#endregion
