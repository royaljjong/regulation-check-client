namespace AutomationRawCheck.Application.UseProfiles;

public static class UseProfileTemplateCatalog
{
    public static readonly IReadOnlySet<string> TaskTemplates = new HashSet<string>(StringComparer.Ordinal)
    {
        "density-review",
        "parking-review",
        "egress-review",
        "ordinance-review",
        "allowed-use-review",
        "fire-review",
        "manual-review",
        "circulation-review",
        "accessibility-review",
        "mep-review",
    };

    public static readonly IReadOnlySet<string> ManualReviewTemplates = new HashSet<string>(StringComparer.Ordinal)
    {
        "district-unit-plan",
        "ordinance-confirmation",
        "urban-planning-facility",
        "traffic-impact-review",
        "school-route-review",
        "medical-special-law-review",
        "accommodation-operation-review",
        "hazardous-material-review",
        "development-action",
        "loading-operation-review",
    };

    public static readonly IReadOnlyList<string> StandardOnboardingProcedure =
    [
        "법적 용도명 정의",
        "기능형 그룹 지정",
        "입력셋 정의",
        "Bundle 선택",
        "TaskTemplate 선택",
        "ManualReviewTemplate 등록",
    ];
}
