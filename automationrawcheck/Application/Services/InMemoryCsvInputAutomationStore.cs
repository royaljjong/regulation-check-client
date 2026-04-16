using System.Collections.Concurrent;
using AutomationRawCheck.Api.Dtos;
using AutomationRawCheck.Application.Interfaces;

namespace AutomationRawCheck.Application.Services;

public sealed class InMemoryCsvInputAutomationStore : ICsvInputAutomationStore
{
    private readonly ConcurrentDictionary<string, CsvInputAutomationResultDto> _sessions = new(StringComparer.Ordinal);

    public CsvInputAutomationResultDto Save(CsvInputAutomationResultDto session)
    {
        _sessions[session.Token] = session;
        return session;
    }

    public CsvInputAutomationResultDto? Get(string token)
    {
        return _sessions.TryGetValue(token, out var session) ? session : null;
    }
}
