using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Interfaces;

public interface IRegulationResearchService
{
    RegulationSearchHubResponseDto BuildSearchHub(RegulationSearchHubRequestDto request);
    LawChangeCompareResponseDto CompareLawChanges(LawChangeCompareRequestDto request);
    Task<OfficialLawSearchResponseDto> SearchOfficialLawAsync(OfficialLawSearchRequestDto request, CancellationToken ct = default);
    Task<OfficialLawBodyResponseDto> GetOfficialLawBodyAsync(OfficialLawBodyRequestDto request, CancellationToken ct = default);
    RegulationSourceSyncPackageDto BuildSourceSyncPackage(RegulationSourceSyncRequestDto request);
    Task<RegulationSourceSyncRunResponseDto> RunSourceSyncAsync(RegulationSourceSyncRequestDto request, CancellationToken ct = default);
    RegulationResearchServiceStatusDto GetStatus();
}
