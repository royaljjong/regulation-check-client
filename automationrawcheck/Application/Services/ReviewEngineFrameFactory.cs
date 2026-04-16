using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.UseProfiles;
using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Builds the fixed five-layer review frame from current DTOs and resolved spatial data.
/// This is an adapter layer while the old /review contract still exists.
/// </summary>
public static class ReviewEngineFrameFactory
{
    public static ReviewEngineFrame Create(
        BuildingReviewRequestDto request,
        string? zoneName,
        string? zoneCode,
        bool? districtUnitPlanIsInside,
        bool? developmentRestrictionIsInside,
        bool? developmentActionRestricted,
        OverlayDecisionDto? developmentActionDetail)
    {
        var rawValues = BuildRawInputMap(request);
        var selectedUse = request.SelectedUse;
        var profile = UseProfileRegistry.TryGet(selectedUse, out var resolvedProfile)
            ? resolvedProfile
            : null;
        var apiVerificationStatus = BuildApiVerificationStatus(developmentActionDetail);

        var derivedValues = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["selectedUseProfileKey"] = profile?.Identity.Key,
            ["automationCoverage"] = profile?.Coverage.ToString().ToLowerInvariant(),
            ["derivedMetrics"] = profile?.DerivedMetrics ?? [],
            ["calculationLayer.results"] = CalculationLayerEngine.Build(request, zoneName),
            ["developmentActionApiStatus"] = apiVerificationStatus,
        };

        return new ReviewEngineFrame
        {
            Query = new ReviewQueryContext
            {
                Address = request.Address,
                Longitude = request.Longitude,
                Latitude = request.Latitude,
                SelectedUse = selectedUse,
                ReviewLevel = request.ReviewLevel,
            },
            RawInputs = new ReviewRawInputs
            {
                Values = rawValues,
            },
            DerivedInputs = new ReviewDerivedInputs
            {
                Values = derivedValues,
            },
            PlanningContext = new ReviewPlanningContext
            {
                ZoneName = zoneName,
                ZoneCode = zoneCode,
                IsInDistrictUnitPlan = districtUnitPlanIsInside,
                IsDevelopmentRestriction = developmentRestrictionIsInside,
                NeedsDevelopmentActPermission = developmentActionRestricted,
                IsInUrbanPlanningFacility = null,
                DevelopmentActionApiStatus = apiVerificationStatus,
                Spatial = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["spatialLayer.address"] = request.Address,
                    ["spatialLayer.longitude"] = request.Longitude,
                    ["spatialLayer.latitude"] = request.Latitude,
                    ["spatialLayer.zoneName"] = zoneName,
                    ["spatialLayer.zoneCode"] = zoneCode,
                    ["spatialLayer.overlay.districtUnitPlan"] = districtUnitPlanIsInside,
                    ["spatialLayer.overlay.developmentRestriction"] = developmentRestrictionIsInside,
                    ["spatialLayer.overlay.developmentActionRestriction"] = developmentActionRestricted,
                    ["spatialLayer.overlay.developmentAction.source"] = apiVerificationStatus.Source,
                    ["spatialLayer.overlay.developmentAction.status"] = apiVerificationStatus.Status,
                    ["spatialLayer.overlay.developmentAction.confidence"] = apiVerificationStatus.Confidence,
                    ["spatialLayer.overlay.developmentAction.note"] = apiVerificationStatus.Note,
                    ["spatialLayer.urbanPlanningFacility"] = null,
                },
            },
            Output = new ReviewOutputEnvelope
            {
                Values = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["profile.identity"] = profile?.Identity,
                    ["profile.requiredInputsByLevel"] = profile?.RequiredInputsByLevel,
                    ["profile.ruleBundles"] = profile?.RuleBundles,
                    ["profile.taskTemplates"] = profile?.TaskTemplates,
                    ["profile.manualCheckTemplates"] = profile?.ManualCheckTemplates,
                    ["profile.legalSearchHints"] = profile?.LegalSearchHints,
                    ["planningContext.developmentActionApiStatus"] = apiVerificationStatus,
                    ["inputGroups.common"] = profile?.InputGroups.GetValueOrDefault(UseInputGroup.Common),
                    ["inputGroups.semiCommon"] = profile?.InputGroups.GetValueOrDefault(UseInputGroup.SemiCommon),
                    ["inputGroups.specialized"] = profile?.InputGroups.GetValueOrDefault(UseInputGroup.Specialized),
                },
            },
        };
    }

    private static ReviewApiVerificationStatus BuildApiVerificationStatus(OverlayDecisionDto? detail) => new()
    {
        Source = detail?.Source ?? "none",
        Status = detail?.Status ?? "unavailable",
        Confidence = detail?.Confidence ?? "low",
        Note = detail?.Note,
    };

    private static Dictionary<string, object?> BuildRawInputMap(BuildingReviewRequestDto request)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["selectedUse"] = request.SelectedUse,
            ["includeLegalBasis"] = request.IncludeLegalBasis,
            ["projectContext"] = request.ProjectContext,
            ["geometryInput"] = request.GeometryInput,
            ["csvUploadToken"] = request.CsvUploadToken,
        };

        if (request.BuildingInputs is null)
            return values;

        var i = request.BuildingInputs;
        values["siteArea"] = i.SiteArea;
        values["buildingArea"] = i.BuildingArea;
        values["floorArea"] = i.FloorArea;
        values["floorCount"] = i.FloorCount;
        values["buildingHeight"] = i.BuildingHeight;
        values["roadFrontageWidth"] = i.RoadFrontageWidth;
        values["mainUse"] = request.SelectedUse;
        values["subUses"] = i.DetailUseSubtype;

        values["unitCount"] = i.UnitCount;
        values["roomCount"] = i.RoomCount;
        values["guestRoomCount"] = i.GuestRoomCount;
        values["bedCount"] = i.BedCount;
        values["studentCount"] = i.StudentCount;
        values["occupantCount"] = i.OccupantCount;
        values["vehicleIngressType"] = i.VehicleIngressType ?? i.ParkingType;

        values["medicalSpecialCriteria"] = i.MedicalSpecialCriteria;
        values["educationSpecialCriteria"] = i.EducationSpecialCriteria;
        values["hazardousMaterialProfile"] = i.HazardousMaterialProfile
            ?? (i.IsHighRiskOccupancy.HasValue ? i.IsHighRiskOccupancy.Value.ToString() : null);
        values["logisticsOperationProfile"] = i.LogisticsOperationProfile
            ?? (i.HasLoadingBay.HasValue ? i.HasLoadingBay.Value.ToString() : null);
        values["accommodationSpecialCriteria"] = i.AccommodationSpecialCriteria;

        values["housingSubtype"] = i.HousingSubtype;
        values["unitArea"] = i.UnitArea;
        values["detailUseSubtype"] = i.DetailUseSubtype;
        values["detailUseFloorArea"] = i.DetailUseFloorArea;
        values["isMultipleOccupancy"] = i.IsMultipleOccupancy;
        values["officeSubtype"] = i.OfficeSubtype;
        values["mixedUseRatio"] = i.MixedUseRatio;

        return values;
    }
}
