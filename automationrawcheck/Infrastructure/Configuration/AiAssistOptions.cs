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
    public string ApiKeyPrefix { get; set; } = "Bearer";
    public string SystemInstruction { get; set; } =
        "You are Gemma4 acting as a legal navigation assistant. " +
        "You must never decide pass/fail, permitted/prohibited use, or compute legal numbers. " +
        "Only return article navigation hints, ordinance search keywords, missing review reminders, and manual-review guidance in structured JSON.";
}
