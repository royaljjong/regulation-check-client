using System.Text.Json;
using System.Xml.Linq;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Application.Services;

public sealed class RegulationResearchService : IRegulationResearchService
{
    private const string HttpClientName = "regulation-research-live";
    private const string LawHttpClientName = "LawApi";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RegulationResearchOptions _options;

    public RegulationResearchService(
        IHttpClientFactory httpClientFactory,
        IOptions<RegulationResearchOptions> options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public RegulationSearchHubResponseDto BuildSearchHub(RegulationSearchHubRequestDto request)
    {
        return RegulationSearchHubBuilder.Build(request);
    }

    public LawChangeCompareResponseDto CompareLawChanges(LawChangeCompareRequestDto request)
    {
        return LawChangeComparisonBuilder.Build(request);
    }

    public async Task<OfficialLawSearchResponseDto> SearchOfficialLawAsync(OfficialLawSearchRequestDto request, CancellationToken ct = default)
    {
        var target = NormalizeOfficialLawTarget(request.Target);
        var query = request.Query?.Trim() ?? string.Empty;
        var display = Math.Clamp(request.Display <= 0 ? 20 : request.Display, 1, 100);
        var candidateQueries = BuildOfficialLawCandidateQueries(query);

        if (!IsAnyOfficialLawSearchConfigured())
        {
            return new OfficialLawSearchResponseDto
            {
                Target = target,
                Query = query,
                IsConfigured = false,
                SummaryLines =
                [
                    "Official law API is not configured.",
                    "Set RegulationResearch:OfficialLawApiOc or OfficialLawPublicDataServiceKey."
                ]
            };
        }

        try
        {
            if (IsOfficialLawApiConfigured())
            {
                var client = _httpClientFactory.CreateClient(LawHttpClientName);
                var items = new List<OfficialLawSearchItemDto>();
                string? lastBody = null;
                int? lastStatusCode = null;

                foreach (var candidateQuery in candidateQueries)
                {
                    var url = $"{_options.OfficialLawApiBaseUrl.TrimEnd('/')}/lawSearch.do?OC={Uri.EscapeDataString(_options.OfficialLawApiOc)}&target={Uri.EscapeDataString(target)}&type=JSON&query={Uri.EscapeDataString(candidateQuery)}&display={display}";
                    using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    lastBody = body;
                    lastStatusCode = (int)response.StatusCode;

                    if (!response.IsSuccessStatusCode)
                        continue;

                    MergeOfficialLawItems(items, ParseOfficialLawSearch(body, target));
                    if (items.Count >= display)
                        break;
                }

                if (items.Count == 0 && lastStatusCode is not null && lastStatusCode >= 400)
                {
                    return new OfficialLawSearchResponseDto
                    {
                        Target = target,
                        Query = query,
                        IsConfigured = true,
                        SummaryLines =
                        [
                            $"official_law_api_http={lastStatusCode}",
                            Clip(lastBody) ?? "empty_response"
                        ]
                    };
                }

                return new OfficialLawSearchResponseDto
                {
                    Target = target,
                    Query = query,
                    IsConfigured = true,
                    Items = items.Take(display).ToList(),
                    SummaryLines =
                    [
                        "searchSource=law.go.kr_drf",
                        $"target={target}",
                        $"query={query}",
                        $"candidateQueries={candidateQueries.Count}",
                        $"items={Math.Min(items.Count, display)}"
                    ]
                };
            }

            var publicItems = await SearchOfficialLawViaPublicDataAsync(candidateQueries, target, display, ct).ConfigureAwait(false);
            return new OfficialLawSearchResponseDto
            {
                Target = target,
                Query = query,
                IsConfigured = true,
                Items = publicItems,
                SummaryLines =
                [
                    "searchSource=data.go.kr_serviceKey",
                    $"target={target}",
                    $"query={query}",
                    $"candidateQueries={candidateQueries.Count}",
                    $"items={publicItems.Count}"
                ]
            };
        }
        catch (Exception ex)
        {
            return new OfficialLawSearchResponseDto
            {
                Target = target,
                Query = query,
                IsConfigured = true,
                SummaryLines =
                [
                    $"official_law_api_error={ex.GetType().Name}"
                ]
            };
        }
    }

    public async Task<OfficialLawBodyResponseDto> GetOfficialLawBodyAsync(OfficialLawBodyRequestDto request, CancellationToken ct = default)
    {
        var target = NormalizeOfficialLawTarget(request.Target);

        if (!IsOfficialLawApiConfigured())
        {
            return new OfficialLawBodyResponseDto
            {
                Target = target,
                Id = request.Id,
                Mst = request.Mst,
                IsConfigured = false,
                SummaryLines =
                [
                    "Official law body lookup requires law.go.kr DRF OC.",
                    "Search can still work with OfficialLawPublicDataServiceKey, but body lookup needs OfficialLawApiOc."
                ]
            };
        }

        if (string.IsNullOrWhiteSpace(request.Id) && string.IsNullOrWhiteSpace(request.Mst))
        {
            return new OfficialLawBodyResponseDto
            {
                Target = target,
                Id = request.Id,
                Mst = request.Mst,
                IsConfigured = true,
                SummaryLines = ["Either id or mst is required."]
            };
        }

        try
        {
            var client = _httpClientFactory.CreateClient(LawHttpClientName);
            var queryParts = new List<string>
            {
                $"OC={Uri.EscapeDataString(_options.OfficialLawApiOc)}",
                $"target={Uri.EscapeDataString(target)}",
                "type=JSON"
            };

            if (!string.IsNullOrWhiteSpace(request.Id))
                queryParts.Add($"ID={Uri.EscapeDataString(request.Id)}");

            if (!string.IsNullOrWhiteSpace(request.Mst))
                queryParts.Add($"MST={Uri.EscapeDataString(request.Mst)}");

            var url = $"{_options.OfficialLawApiBaseUrl.TrimEnd('/')}/lawService.do?{string.Join("&", queryParts)}";
            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            return new OfficialLawBodyResponseDto
            {
                Target = target,
                Id = request.Id,
                Mst = request.Mst,
                IsConfigured = true,
                Title = TryExtractOfficialLawBodyTitle(body, target),
                Link = BuildOfficialLawLink(target, request.Id, request.Mst),
                RawBody = Clip(body),
                SummaryLines =
                [
                    $"target={target}",
                    $"http={(int)response.StatusCode}",
                    response.IsSuccessStatusCode ? "body_loaded" : "body_load_failed"
                ]
            };
        }
        catch (Exception ex)
        {
            return new OfficialLawBodyResponseDto
            {
                Target = target,
                Id = request.Id,
                Mst = request.Mst,
                IsConfigured = true,
                SummaryLines =
                [
                    $"official_law_body_error={ex.GetType().Name}"
                ]
            };
        }
    }

    public RegulationSourceSyncPackageDto BuildSourceSyncPackage(RegulationSourceSyncRequestDto request)
    {
        var keywords = request.SearchKeywords
            .Where(static keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RegulationSourceSyncPackageDto
        {
            SearchHubMode = _options.SearchHubMode,
            LawChangeMode = _options.ChangeTrackingMode,
            Subject = request.Subject,
            OrdinanceRegion = request.OrdinanceRegion,
            LiveLawSourceEndpoint = string.IsNullOrWhiteSpace(_options.LiveLawSourceEndpoint) ? null : _options.LiveLawSourceEndpoint,
            MunicipalOrdinanceEndpoint = string.IsNullOrWhiteSpace(_options.MunicipalOrdinanceEndpoint) ? null : _options.MunicipalOrdinanceEndpoint,
            PdfHubEndpoint = string.IsNullOrWhiteSpace(_options.PdfHubEndpoint) ? null : _options.PdfHubEndpoint,
            SearchKeywords = keywords,
            SummaryLines =
            [
                $"subject={request.Subject}",
                $"keywords={keywords.Count}",
                "Package is ready for an external regulation source synchronizer."
            ]
        };
    }

    public async Task<RegulationSourceSyncRunResponseDto> RunSourceSyncAsync(RegulationSourceSyncRequestDto request, CancellationToken ct = default)
    {
        var package = BuildSourceSyncPackage(request);
        var targets = new List<RegulationSourceSyncProbeTargetDto>
        {
            await ProbeAsync("live_law_source", package.LiveLawSourceEndpoint, package, ct).ConfigureAwait(false),
            await ProbeAsync("municipal_ordinance", package.MunicipalOrdinanceEndpoint, package, ct).ConfigureAwait(false),
            await ProbeAsync("pdf_hub", package.PdfHubEndpoint, package, ct).ConfigureAwait(false)
        };

        return new RegulationSourceSyncRunResponseDto
        {
            Package = package,
            Targets = targets,
            SummaryLines =
            [
                $"subject={request.Subject}",
                $"configuredTargets={targets.Count(static target => target.Configured)}",
                $"successfulTargets={targets.Count(static target => target.HttpStatusCode is >= 200 and < 300)}"
            ]
        };
    }

    public RegulationResearchServiceStatusDto GetStatus()
    {
        return new RegulationResearchServiceStatusDto
        {
            SearchHubMode = _options.SearchHubMode,
            LawChangeMode = _options.ChangeTrackingMode,
            SupportsOfficialLawApi = true,
            OfficialLawApiConfigured = IsOfficialLawApiConfigured() || IsOfficialLawPublicDataConfigured(),
            OfficialLawApiBaseUrl = string.IsNullOrWhiteSpace(_options.OfficialLawApiBaseUrl) ? null : _options.OfficialLawApiBaseUrl,
            OfficialLawPublicDataConfigured = IsOfficialLawPublicDataConfigured(),
            OfficialLawPublicDataBaseUrl = string.IsNullOrWhiteSpace(_options.OfficialLawPublicDataBaseUrl) ? null : _options.OfficialLawPublicDataBaseUrl,
            SupportsLiveLawSource = !string.IsNullOrWhiteSpace(_options.LiveLawSourceEndpoint),
            SupportsMunicipalOrdinanceSync = !string.IsNullOrWhiteSpace(_options.MunicipalOrdinanceEndpoint),
            SupportsPdfLinkHub = !string.IsNullOrWhiteSpace(_options.PdfHubEndpoint),
            LiveLawSourceConfigured = !string.IsNullOrWhiteSpace(_options.LiveLawSourceEndpoint),
            MunicipalOrdinanceConfigured = !string.IsNullOrWhiteSpace(_options.MunicipalOrdinanceEndpoint),
            PdfHubConfigured = !string.IsNullOrWhiteSpace(_options.PdfHubEndpoint),
            LiveLawSourceEndpoint = string.IsNullOrWhiteSpace(_options.LiveLawSourceEndpoint) ? null : _options.LiveLawSourceEndpoint,
            MunicipalOrdinanceEndpoint = string.IsNullOrWhiteSpace(_options.MunicipalOrdinanceEndpoint) ? null : _options.MunicipalOrdinanceEndpoint,
            PdfHubEndpoint = string.IsNullOrWhiteSpace(_options.PdfHubEndpoint) ? null : _options.PdfHubEndpoint,
            SummaryLines =
            [
                IsOfficialLawApiConfigured()
                    ? "Official law API is configured for law.go.kr DRF search/body lookup."
                    : IsOfficialLawPublicDataConfigured()
                        ? "Official law search is configured through data.go.kr serviceKey. Body lookup still needs DRF OC."
                        : "Official law API is available but OC/serviceKey is not configured yet.",
                "Search hub is built from use profile hints, task hints, manual review cards, and ordinance cards.",
                "Law change compare can diff supplied current/amended clauses and can now probe configured live source endpoints.",
                "Live source execution still depends on valid external endpoints and credentials."
            ]
        };
    }

    private bool IsOfficialLawApiConfigured()
        => _options.OfficialLawApiEnabled
           && !string.IsNullOrWhiteSpace(_options.OfficialLawApiOc)
           && !string.IsNullOrWhiteSpace(_options.OfficialLawApiBaseUrl);

    private bool IsOfficialLawPublicDataConfigured()
        => _options.OfficialLawApiEnabled
           && !string.IsNullOrWhiteSpace(_options.OfficialLawPublicDataServiceKey)
           && !string.IsNullOrWhiteSpace(_options.OfficialLawPublicDataBaseUrl);

    private bool IsAnyOfficialLawSearchConfigured()
        => IsOfficialLawApiConfigured() || IsOfficialLawPublicDataConfigured();

    private static string NormalizeOfficialLawTarget(string? target)
        => (target ?? "law").Trim().ToLowerInvariant() switch
        {
            "ordin" => "ordin",
            "expc" => "expc",
            _ => "law",
        };

    private static List<OfficialLawSearchItemDto> ParseOfficialLawSearch(string body, string target)
    {
        var items = new List<OfficialLawSearchItemDto>();

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("LawSearch", out var lawSearch))
                return items;

            if (!lawSearch.TryGetProperty(target, out var targetElement))
                return items;

            if (targetElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in targetElement.EnumerateArray())
                    TryAddOfficialLawSearchItem(item, target, items);
            }
            else if (targetElement.ValueKind == JsonValueKind.Object)
            {
                TryAddOfficialLawSearchItem(targetElement, target, items);
            }
        }
        catch (JsonException)
        {
        }

        return items;
    }

    private static void TryAddOfficialLawSearchItem(JsonElement element, string target, List<OfficialLawSearchItemDto> items)
    {
        string? id = ReadJsonString(element, "법령ID", "자치법규ID", "해석례일련번호", "ID");
        string? mst = ReadJsonString(element, "법령일련번호", "자치법규일련번호", "MST");
        string? title = ReadJsonString(element, "법령명한글", "자치법규명", "법령해석례명", "법령명");
        string? summary = ReadJsonString(element, "제개정구분명", "공포일자", "질의요지", "소관부처명");
        string? department = ReadJsonString(element, "소관부처명", "자치단체명", "회신기관명");
        string? promulgationDate = ReadJsonString(element, "공포일자", "시행일자", "해석일자");

        if (string.IsNullOrWhiteSpace(title))
            return;

        items.Add(new OfficialLawSearchItemDto
        {
            Id = id ?? mst ?? title,
            Mst = mst,
            Title = title,
            Summary = summary,
            Target = target,
            Department = department,
            PromulgationDate = promulgationDate,
            Link = BuildOfficialLawLink(target, id, mst)
        });
    }

    private static string? TryExtractOfficialLawBodyTitle(string body, string target)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return target switch
            {
                "ordin" => ReadNestedJsonString(root, new[] { "자치법규", "기본정보", "자치법규명" }),
                "expc" => ReadNestedJsonString(root, new[] { "법령해석례", "기본정보", "법령해석례명" }),
                _ => ReadNestedJsonString(root, new[] { "법령", "기본정보", "법령명_한글" }) ??
                     ReadNestedJsonString(root, new[] { "법령", "기본정보", "법령명한글" })
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<List<OfficialLawSearchItemDto>> SearchOfficialLawViaPublicDataAsync(IReadOnlyList<string> candidateQueries, string target, int display, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var items = new List<OfficialLawSearchItemDto>();

        foreach (var query in candidateQueries)
        {
            var url =
                $"{_options.OfficialLawPublicDataBaseUrl.TrimEnd('/')}/lawSearchList.do" +
                $"?serviceKey={Uri.EscapeDataString(_options.OfficialLawPublicDataServiceKey)}" +
                $"&target={Uri.EscapeDataString(target)}" +
                $"&query={Uri.EscapeDataString(query)}" +
                $"&numOfRows={display}&pageNo=1";

            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                continue;

            MergeOfficialLawItems(items, ParseOfficialLawSearchXml(body, target));
            if (items.Count >= display)
                break;
        }

        return items.Take(display).ToList();
    }

    private static List<string> BuildOfficialLawCandidateQueries(string query)
    {
        var normalized = (query ?? string.Empty).Trim();
        var candidates = new List<string>();
        void Add(string? value)
        {
            var trimmed = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !candidates.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                candidates.Add(trimmed);
        }

        Add(normalized);

        if (normalized.Contains("주차", StringComparison.OrdinalIgnoreCase))
        {
            Add("주차장법");
            Add("주차장법 시행령");
            Add("주차장법 시행규칙");
            Add("부설주차장 설치기준");
        }

        if (normalized.Contains("직통계단", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("피난", StringComparison.OrdinalIgnoreCase))
        {
            Add("건축법");
            Add("건축법 시행령");
            Add("건축법 시행규칙");
            Add("직통계단");
            Add("피난계단");
        }

        if (normalized.Contains("건폐율", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("용적률", StringComparison.OrdinalIgnoreCase))
        {
            Add("국토의 계획 및 이용에 관한 법률");
            Add("국토의 계획 및 이용에 관한 법률 시행령");
            Add("건축법");
        }

        if (normalized.Contains("건축법", StringComparison.OrdinalIgnoreCase))
        {
            Add("건축법");
            Add("건축법 시행령");
            Add("건축법 시행규칙");
        }

        if (normalized.Contains("지구단위계획", StringComparison.OrdinalIgnoreCase))
        {
            Add("국토의 계획 및 이용에 관한 법률");
            Add("국토의 계획 및 이용에 관한 법률 시행령");
            Add("지구단위계획");
        }

        if (normalized.Contains("소방", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("방화", StringComparison.OrdinalIgnoreCase))
        {
            Add("소방시설 설치 및 관리에 관한 법률");
            Add("화재의 예방 및 안전관리에 관한 법률");
            Add("건축법");
        }

        if (candidates.Count == 1)
        {
            Add($"{normalized} 건축법");
            Add($"{normalized} 건축법 시행령");
        }

        return candidates;
    }

    private static void MergeOfficialLawItems(List<OfficialLawSearchItemDto> destination, IEnumerable<OfficialLawSearchItemDto> additions)
    {
        foreach (var item in additions)
        {
            var exists = destination.Any(existing =>
                string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Target, item.Target, StringComparison.OrdinalIgnoreCase));

            if (!exists)
                destination.Add(item);
        }
    }

    private static List<OfficialLawSearchItemDto> ParseOfficialLawSearchXml(string xml, string target)
    {
        var items = new List<OfficialLawSearchItemDto>();

        try
        {
            var document = XDocument.Parse(xml);
            foreach (var lawElement in document.Descendants("law"))
            {
                var id = lawElement.Element("법령ID")?.Value?.Trim();
                var mst = lawElement.Element("법령일련번호")?.Value?.Trim();
                var title = lawElement.Element("법령명한글")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                items.Add(new OfficialLawSearchItemDto
                {
                    Id = string.IsNullOrWhiteSpace(id) ? (mst ?? title) : id,
                    Mst = mst,
                    Title = title,
                    Summary = lawElement.Element("제개정구분명")?.Value?.Trim(),
                    Target = target,
                    Department = lawElement.Element("소관부처명")?.Value?.Trim(),
                    PromulgationDate = lawElement.Element("공포일자")?.Value?.Trim(),
                    Link = BuildOfficialLawLink(target, id, mst)
                });
            }
        }
        catch
        {
        }

        return items;
    }

    private static string? ReadJsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static string? ReadNestedJsonString(JsonElement element, IReadOnlyList<string> path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? BuildOfficialLawLink(string target, string? id, string? mst)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return $"https://www.law.go.kr/DRF/lawService.do?target={target}&ID={Uri.EscapeDataString(id)}";

        if (!string.IsNullOrWhiteSpace(mst))
            return $"https://www.law.go.kr/DRF/lawService.do?target={target}&MST={Uri.EscapeDataString(mst)}";

        return null;
    }

    private async Task<RegulationSourceSyncProbeTargetDto> ProbeAsync(
        string targetId,
        string? endpoint,
        RegulationSourceSyncPackageDto package,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new RegulationSourceSyncProbeTargetDto
            {
                TargetId = targetId,
                Endpoint = null,
                Configured = false,
                Error = "not_configured"
            };
        }

        try
        {
            var builder = new UriBuilder(endpoint);
            var queryParts = new List<string>
            {
                $"subject={Uri.EscapeDataString(package.Subject)}"
            };

            if (!string.IsNullOrWhiteSpace(package.OrdinanceRegion))
                queryParts.Add($"ordinanceRegion={Uri.EscapeDataString(package.OrdinanceRegion)}");

            foreach (var keyword in package.SearchKeywords)
                queryParts.Add($"keyword={Uri.EscapeDataString(keyword)}");

            builder.Query = string.Join("&", queryParts);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(builder.Uri, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            return new RegulationSourceSyncProbeTargetDto
            {
                TargetId = targetId,
                Endpoint = endpoint,
                Configured = true,
                HttpStatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.MediaType,
                ResponseSnippet = Clip(body),
                Error = response.IsSuccessStatusCode ? null : $"http_{(int)response.StatusCode}"
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new RegulationSourceSyncProbeTargetDto
            {
                TargetId = targetId,
                Endpoint = endpoint,
                Configured = true,
                Error = "timeout"
            };
        }
        catch (Exception ex)
        {
            return new RegulationSourceSyncProbeTargetDto
            {
                TargetId = targetId,
                Endpoint = endpoint,
                Configured = true,
                Error = ex.GetType().Name
            };
        }
    }

    private static string? Clip(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return body;

        try
        {
            using var parsed = JsonDocument.Parse(body);
            body = parsed.RootElement.GetRawText();
        }
        catch (JsonException)
        {
        }

        return body.Length <= 1000 ? body : body[..1000] + "...";
    }
}
