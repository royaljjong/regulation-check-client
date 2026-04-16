using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.UseProfiles;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Produces ordinance and administrative follow-up cards separated from rule evaluation.
/// </summary>
public static class OrdinanceLayerBuilder
{
    private sealed record OrdinanceRegionMetadata(
        string Region,
        string? Link,
        string? Department,
        IReadOnlyList<string> Keywords,
        IReadOnlyList<string> CheckItems);

    private static readonly IReadOnlyDictionary<string, OrdinanceRegionMetadata> RegionMetadata =
        new Dictionary<string, OrdinanceRegionMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["서울"] = new(
                "서울",
                "https://legal.seoul.go.kr",
                "자치구 건축과 / 도시계획과",
                ["서울 건축조례", "서울 도시계획조례", "지구단위계획 시행지침"],
                ["자치구별 부설주차장 조례 확인", "지구단위계획 시행지침 대조"]),
            ["경기"] = new(
                "경기",
                "https://www.gg.go.kr/contents/contents.do?ciIdx=515&menuId=2457",
                "시군 건축과 / 도시정책과",
                ["경기도 건축조례", "시군 도시계획조례", "개발행위허가 운영지침"],
                ["시군별 도시계획조례 적용 여부 확인", "개발행위허가 운영기준 확인"]),
        };

    public static IReadOnlyList<OrdinanceReviewCardDto> Build(
        UseProfileDefinition? profile,
        string? resolvedAddress,
        string? zoneName,
        bool? districtUnitPlan,
        bool? developmentActionRestriction,
        bool? urbanPlanningFacility,
        OverlayDecisionDto? developmentActionDetail)
    {
        var regionKey = GuessRegionKey(resolvedAddress);
        var cards = new List<OrdinanceReviewCardDto>();

        if (regionKey is not null && RegionMetadata.TryGetValue(regionKey, out var metadata))
        {
            cards.Add(new OrdinanceReviewCardDto
            {
                OrdinanceId = $"ordinance-{regionKey.ToLowerInvariant()}",
                Region = metadata.Region,
                Title = $"{metadata.Region} 조례 및 행정기준 확인",
                SourceType = "region_metadata",
                Link = metadata.Link,
                Department = metadata.Department,
                Keywords = metadata.Keywords.Concat(profile?.LegalSearchHints ?? []).Distinct().ToList(),
                CheckItems = metadata.CheckItems.ToList(),
            });
        }

        if (districtUnitPlan != false)
        {
            cards.Add(new OrdinanceReviewCardDto
            {
                OrdinanceId = "ordinance-district-unit-plan",
                Region = regionKey ?? "unknown",
                Title = "지구단위계획 결정도서 확인",
                SourceType = "district_unit_plan",
                Keywords = (profile?.LegalSearchHints ?? [])
                    .Concat(["지구단위계획", zoneName ?? "용도지역"])
                    .Distinct()
                    .ToList(),
                CheckItems = ["결정도서 확인", "추가 건축기준 반영", "배치 및 동선 제한 확인"],
            });
        }

        if (urbanPlanningFacility != false)
        {
            cards.Add(new OrdinanceReviewCardDto
            {
                OrdinanceId = "ordinance-urban-planning-facility",
                Region = regionKey ?? "unknown",
                Title = "도시계획시설 저촉 여부 확인",
                SourceType = "urban_planning_facility",
                Keywords = (profile?.LegalSearchHints ?? [])
                    .Concat(["도시계획시설", zoneName ?? "용도지역"])
                    .Distinct()
                    .ToList(),
                CheckItems = ["시설결정 여부 확인", "저촉 범위 확인", "사업시행 협의 필요 여부 확인"],
            });
        }

        if (developmentActionRestriction != false)
        {
            var developmentActionSourceType = developmentActionDetail?.Source switch
            {
                "api" => "development_action_api",
                "shp" => "development_action_fallback",
                _ => "development_action_unverified",
            };

            var developmentActionCheckItems = new List<string>
            {
                "허가 대상 여부 검토",
                "사전협의 필요 부서 확인",
                "보완자료 목록 정리",
            };

            if (string.Equals(developmentActionSourceType, "development_action_fallback", StringComparison.Ordinal))
                developmentActionCheckItems.Insert(0, "SHP fallback 결과와 API 결과 비교");
            else if (string.Equals(developmentActionSourceType, "development_action_unverified", StringComparison.Ordinal))
                developmentActionCheckItems.Insert(0, "개발행위허가 API 또는 담당 부서 회신으로 1차 확인");

            cards.Add(new OrdinanceReviewCardDto
            {
                OrdinanceId = "ordinance-development-action",
                Region = regionKey ?? "unknown",
                Title = "개발행위허가 필요 여부 확인",
                SourceType = developmentActionSourceType,
                Keywords = (profile?.LegalSearchHints ?? [])
                    .Concat(["개발행위허가", "행정협의", zoneName ?? "용도지역"])
                    .Distinct()
                    .ToList(),
                CheckItems = developmentActionCheckItems,
            });
        }

        return cards
            .GroupBy(card => card.OrdinanceId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static string? GuessRegionKey(string? resolvedAddress)
    {
        if (string.IsNullOrWhiteSpace(resolvedAddress))
            return null;

        if (resolvedAddress.Contains("서울", StringComparison.OrdinalIgnoreCase))
            return "서울";
        if (resolvedAddress.Contains("경기", StringComparison.OrdinalIgnoreCase))
            return "경기";
        return null;
    }
}
