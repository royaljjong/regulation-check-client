namespace AutomationRawCheck.Infrastructure.Configuration;

public sealed class AiAssistOptions
{
    public const string SectionName = "AiAssist";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "gemma4";
    public string Model { get; set; } = "gemma4";
    public string ExecutionMode { get; set; } = "preview";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeaderName { get; set; } = "Authorization";
}
