namespace SkillBridge.Infrastructure.Services;

public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; set; } = string.Empty;
    public string SenderName { get; set; } = "SkillBridge";
    public string SenderEmail { get; set; } = "noreply@skillbridge.com";
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    public bool UseMock { get; set; } = true;
}

