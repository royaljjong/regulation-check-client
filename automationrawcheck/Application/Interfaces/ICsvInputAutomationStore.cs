using AutomationRawCheck.Api.Dtos;

namespace AutomationRawCheck.Application.Interfaces;

public interface ICsvInputAutomationStore
{
    CsvInputAutomationResultDto Save(CsvInputAutomationResultDto session);
    CsvInputAutomationResultDto? Get(string token);
}
