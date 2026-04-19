using System.Text.Json;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Application.Services;

public sealed class RegulationResearchService : IRegulationResearchService
{
    private const string HttpClientName = "regulation-research-live";
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
                "Search hub is built from use profile hints, task hints, manual review cards, and ordinance cards.",
                "Law change compare can diff supplied current/amended clauses and can now probe configured live source endpoints.",
                "Live source execution still depends on valid external endpoints and credentials."
            ]
        };
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
