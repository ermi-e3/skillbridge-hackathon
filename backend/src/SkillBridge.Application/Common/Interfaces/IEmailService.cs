namespace SkillBridge.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string toEmail, string toName, string confirmationLink, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string toEmail, string toName, string resetLink, CancellationToken cancellationToken = default);
    Task SendEmailChangeConfirmationAsync(string toEmail, string toName, string confirmationLink, CancellationToken cancellationToken = default);
    Task SendSecurityNotificationAsync(string toEmail, string toName, string subject, string message, CancellationToken cancellationToken = default);
}

