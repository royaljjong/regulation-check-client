using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Domain.Models;

namespace AutomationRawCheck.Application.Services;

/// <summary>
/// Server-authoritative calculation adapter for the future Calculation Layer.
/// It keeps the current basic calculator logic but exposes normalized derived metrics.
/// </summary>
public static class CalculationLayerEngine
{
    public static IReadOnlyList<DerivedMetricResult> Build(BuildingReviewRequestDto request, string? zoneName)
    {
        var input = request.BuildingInputs;
        var results = new List<DerivedMetricResult>();

        if (input?.SiteArea is > 0 && input.FloorArea is > 0)
        {
            var far = Math.Round(input.FloorArea.Value / input.SiteArea.Value * 100d, 2);
            results.Add(new DerivedMetricResult
            {
                MetricId = "FAR",
                Label = "용적률",
                Value = far,
                Status = DerivedMetricStatus.Calculated,
                Note = zoneName is null ? "용도지역 미판정 상태의 순수 계산값" : $"용도지역 {zoneName} 기준 비교용 계산값",
            });
        }
        else
        {
            results.Add(Pending("FAR", "용적률", "대지면적·연면적 입력 시 계산"));
        }

        if (input?.SiteArea is > 0 && input.FloorArea is > 0 && input.FloorCount is > 0)
        {
            var buildingArea = input.FloorArea.Value / input.FloorCount.Value;
            var bcr = Math.Round(buildingArea / input.SiteArea.Value * 100d, 2);
            results.Add(new DerivedMetricResult
            {
                MetricId = "BCR",
                Label = "건폐율",
                Value = bcr,
                Status = DerivedMetricStatus.Calculated,
                Note = "층수 기반 추정 건축면적 계산값",
            });
        }
        else
        {
            results.Add(Pending("BCR", "건폐율", "대지면적·연면적·층수 입력 시 계산"));
        }

        results.Add(new DerivedMetricResult
        {
            MetricId = "Parking",
            Label = "주차 트리거",
            Value = input?.UnitCount ?? input?.FloorArea,
            Status = input?.UnitCount.HasValue == true || input?.FloorArea.HasValue == true
                ? DerivedMetricStatus.Triggered
                : DerivedMetricStatus.Pending,
            Note = input?.UnitCount.HasValue == true
                ? "세대수 기반 주차 검토 가능"
                : input?.FloorArea.HasValue == true
                    ? "연면적 기반 주차 검토 가능"
                    : "세대수 또는 연면적 입력 시 주차 검토 가능",
        });

        results.Add(new DerivedMetricResult
        {
            MetricId = "Egress",
            Label = "피난 트리거",
            Value = input?.FloorCount,
            Status = input?.FloorCount.HasValue == true ? DerivedMetricStatus.Triggered : DerivedMetricStatus.Pending,
            Note = "층수 입력 시 피난계단/특별피난계단 검토 가능",
        });

        results.Add(new DerivedMetricResult
        {
            MetricId = "Elevator",
            Label = "승강기 트리거",
            Value = input?.BuildingHeight ?? input?.FloorCount,
            Status = input?.FloorCount.HasValue == true || input?.BuildingHeight.HasValue == true
                ? DerivedMetricStatus.Triggered
                : DerivedMetricStatus.Pending,
            Note = "층수 또는 높이 입력 시 승강기 검토 가능",
        });

        results.Add(new DerivedMetricResult
        {
            MetricId = "FireCompartment",
            Label = "방화 트리거",
            Value = input?.FloorArea,
            Status = input?.FloorArea.HasValue == true ? DerivedMetricStatus.Triggered : DerivedMetricStatus.Pending,
            Note = "연면적 입력 시 방화구획 검토 가능",
        });

        results.Add(new DerivedMetricResult
        {
            MetricId = "Energy",
            Label = "에너지 트리거",
            Value = input?.FloorArea,
            Status = input?.FloorArea.HasValue == true ? DerivedMetricStatus.Triggered : DerivedMetricStatus.Pending,
            Note = "연면적 입력 시 에너지 기준 검토 가능",
        });

        return results;
    }

    private static DerivedMetricResult Pending(string metricId, string label, string note) =>
        new()
        {
            MetricId = metricId,
            Label = label,
            Status = DerivedMetricStatus.Pending,
            Note = note,
        };
}
