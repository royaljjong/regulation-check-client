using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Interfaces;

public interface IAiAssistService
{
    AiAssistResponseDto BuildPreview(AiAssistRequestDto request);
    AiAssistRequestPackageDto BuildRequestPackage(AiAssistRequestDto request);
    AiAssistServiceStatusDto GetStatus();
}
