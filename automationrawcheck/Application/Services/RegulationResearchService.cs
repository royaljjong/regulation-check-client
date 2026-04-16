using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;
using AutomationRawCheck.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AutomationRawCheck.Application.Services;

public sealed class RegulationResearchService : IRegulationResearchService
{
    private readonly RegulationResearchOptions _options;

    public RegulationResearchService(IOptions<RegulationResearchOptions> options)
    {
        _options = options.Value;
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
                "Law change compare currently diffs provided current/amended clauses and does not fetch live law sources.",
                "Live law source sync, municipal ordinance crawling, and PDF hub linking remain external integration work."
            ]
        };
    }
}
