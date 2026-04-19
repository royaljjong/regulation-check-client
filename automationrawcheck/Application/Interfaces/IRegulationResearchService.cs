using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Interfaces;

public interface IRegulationResearchService
{
    RegulationSearchHubResponseDto BuildSearchHub(RegulationSearchHubRequestDto request);
    LawChangeCompareResponseDto CompareLawChanges(LawChangeCompareRequestDto request);
    RegulationSourceSyncPackageDto BuildSourceSyncPackage(RegulationSourceSyncRequestDto request);
    Task<RegulationSourceSyncRunResponseDto> RunSourceSyncAsync(RegulationSourceSyncRequestDto request, CancellationToken ct = default);
    RegulationResearchServiceStatusDto GetStatus();
}
