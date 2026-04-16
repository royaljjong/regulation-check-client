namespace AutomationRawCheck.Domain.Models;

#region Search type
/// <summary>
/// Search input type.
/// </summary>
public enum ParcelSearchType
{
    /// <summary>Search by WGS84 coordinate.</summary>
    Coordinate,

    /// <summary>Search by jibun address text.</summary>
    JibunAddress,

    /// <summary>Search by road address text.</summary>
    RoadAddress
}
#endregion

#region ParcelSearchRequest record
/// <summary>
/// Represents a parcel search request by coordinate or address text.
/// </summary>
/// <param name="SearchType">Search input type.</param>
/// <param name="AddressText">Address text for address-based search types.</param>
/// <param name="Coordinate">Coordinate for coordinate-based search.</param>
public record ParcelSearchRequest(
    ParcelSearchType SearchType,
    string? AddressText,
    CoordinateQuery? Coordinate);
#endregion
