namespace AutomationRawCheck.Application.UseProfiles;

/// <summary>
/// Canonical input catalog for generalized use-profile based reviews.
/// </summary>
public static class UseProfileInputCatalog
{
    public static readonly IReadOnlyList<string> CommonInputs =
    [
        "siteArea",
        "buildingArea",
        "floorArea",
        "floorCount",
        "buildingHeight",
        "roadFrontageWidth",
        "mainUse",
        "subUses",
    ];

    public static readonly IReadOnlyList<string> SemiCommonInputs =
    [
        "unitCount",
        "roomCount",
        "guestRoomCount",
        "bedCount",
        "studentCount",
        "occupantCount",
        "vehicleIngressType",
    ];

    public static readonly IReadOnlyList<string> SpecializedInputs =
    [
        "medicalSpecialCriteria",
        "educationSpecialCriteria",
        "hazardousMaterialProfile",
        "logisticsOperationProfile",
        "accommodationSpecialCriteria",
    ];
}
