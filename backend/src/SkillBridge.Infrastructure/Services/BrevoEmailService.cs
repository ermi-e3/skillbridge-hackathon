using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Infrastructure.Services;

public sealed class BrevoEmailService(
    HttpClient httpClient,
    IOptions<BrevoOptions> options,
    ILogger<BrevoEmailService> logger) : IEmailService
{
    private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

    public async Task SendEmailConfirmationAsync(
        string toEmail,
        string toName,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        var subject = "Verify your SkillBridge email address";
        var htmlContent = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;"">
    <h2 style=""color: #0f172a;"">Welcome to SkillBridge, {System.Net.WebUtility.HtmlEncode(toName)}!</h2>
    <p style=""color: #475569; font-size: 16px; line-height: 1.5;"">
        Please verify your email address to complete your registration and access all features.
    </p>
    <div style=""margin: 32px 0;"">
        <a href=""{System.Net.WebUtility.HtmlEncode(confirmationLink)}"" 
           style=""background-color: #0d9488; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">
            Verify Email Address
        </a>
    </div>
    <p style=""color: #64748b; font-size: 14px;"">
        If you did not create a SkillBridge account, you can safely ignore this email.
    </p>
    <hr style=""border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
    <p style=""color: #94a3b8; font-size: 12px;"">SkillBridge Platform</p>
</div>";

        await SendEmailInternalAsync(toEmail, toName, subject, htmlContent, cancellationToken);
    }

    public async Task SendPasswordResetAsync(
        string toEmail,
        string toName,
        string resetLink,
        CancellationToken cancellationToken = default)
    {
        var subject = "Reset your SkillBridge password";
        var htmlContent = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;"">
    <h2 style=""color: #0f172a;"">Password Reset Request</h2>
    <p style=""color: #475569; font-size: 16px; line-height: 1.5;"">
        Hello {System.Net.WebUtility.HtmlEncode(toName)}, we received a request to reset the password for your SkillBridge account.
    </p>
    <div style=""margin: 32px 0;"">
        <a href=""{System.Net.WebUtility.HtmlEncode(resetLink)}"" 
           style=""background-color: #0d9488; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">
            Reset Password
        </a>
    </div>
    <p style=""color: #64748b; font-size: 14px;"">
        This link will expire soon. If you did not request a password reset, please ignore this email or review your account security.
    </p>
    <hr style=""border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
    <p style=""color: #94a3b8; font-size: 12px;"">SkillBridge Platform</p>
</div>";

        await SendEmailInternalAsync(toEmail, toName, subject, htmlContent, cancellationToken);
    }

    public async Task SendEmailChangeConfirmationAsync(
        string toEmail,
        string toName,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        var subject = "Confirm your new email address";
        var htmlContent = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;"">
    <h2 style=""color: #0f172a;"">Confirm Email Change</h2>
    <p style=""color: #475569; font-size: 16px; line-height: 1.5;"">
        Hello {System.Net.WebUtility.HtmlEncode(toName)}, click the button below to confirm that you want to use this address for your SkillBridge account.
    </p>
    <div style=""margin: 32px 0;"">
        <a href=""{System.Net.WebUtility.HtmlEncode(confirmationLink)}"" 
           style=""background-color: #0d9488; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">
            Confirm New Email
        </a>
    </div>
    <p style=""color: #64748b; font-size: 14px;"">
        If you did not request this change, please contact support immediately.
    </p>
    <hr style=""border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
    <p style=""color: #94a3b8; font-size: 12px;"">SkillBridge Platform</p>
</div>";

        await SendEmailInternalAsync(toEmail, toName, subject, htmlContent, cancellationToken);
    }

    public async Task SendSecurityNotificationAsync(
        string toEmail,
        string toName,
        string subject,
        string message,
        CancellationToken cancellationToken = default)
    {
        var htmlContent = $@"
<div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px;"">
    <h2 style=""color: #0f172a;"">Security Alert</h2>
    <p style=""color: #475569; font-size: 16px; line-height: 1.5;"">
        Hello {System.Net.WebUtility.HtmlEncode(toName)},
    </p>
    <div style=""padding: 16px; background-color: #f8fafc; border-left: 4px solid #0d9488; margin: 16px 0;"">
        <p style=""color: #1e293b; margin: 0;"">{System.Net.WebUtility.HtmlEncode(message)}</p>
    </div>
    <p style=""color: #64748b; font-size: 14px;"">
        If this was not done by you, please sign in to your account and reset your password immediately.
    </p>
    <hr style=""border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
    <p style=""color: #94a3b8; font-size: 12px;"">SkillBridge Platform</p>
</div>";

        await SendEmailInternalAsync(toEmail, toName, subject, htmlContent, cancellationToken);
    }

    private async Task SendEmailInternalAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken)
    {
        var config = options.Value;

        // Offline / mock mode or missing API key: log and proceed
        if (config.UseMock || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            logger.LogInformation(
                "[Brevo Mock Email] To: {Email} ({Name}) | Subject: {Subject}",
                toEmail,
                toName,
                subject);
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint);
            request.Headers.Add("api-key", config.ApiKey);
            request.Headers.Add("accept", "application/json");

            var payload = new
            {
                sender = new { name = config.SenderName, email = config.SenderEmail },
                to = new[] { new { email = toEmail, name = string.IsNullOrWhiteSpace(toName) ? toEmail : toName } },
                subject = subject,
                htmlContent = htmlContent
            };

            request.Content = JsonContent.Create(payload);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Brevo email delivery responded with status {StatusCode}: {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Do not expose Brevo API key or internal secrets
            logger.LogError(ex, "Failed to send email to {Email} via Brevo", toEmail);
        }
    }
}

