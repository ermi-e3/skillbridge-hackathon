using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkillBridge.Application.Auth;
using SkillBridge.Infrastructure.Identity;
using SkillBridge.Infrastructure.Services;
using Xunit;

namespace SkillBridge.Auth.Tests;

public class UserManagementAndAuthUnitTests
{
    [Fact]
    public async Task SocialAuthValidator_ParsesMockTokensCorrectly()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new HttpClient();
        var validator = new SocialAuthValidator(client, config, NullLogger<SocialAuthValidator>.Instance);

        var googleReq = new SocialLoginRequest("Google", IdToken: "mock_google_john@example.com_John-Doe");
        var result = await validator.ValidateAsync(googleReq);

        Assert.Equal("Google", result.Provider);
        Assert.Equal("john@example.com", result.Email);
        Assert.Equal("John Doe", result.FullName);
        Assert.True(result.EmailVerified);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Test client")]
    public async Task BrevoEmailService_ExecutesGracefullyInMockMode()
    {
        var options = Options.Create(new BrevoOptions
        {
            UseMock = true,
            ApiKey = string.Empty,
            SenderEmail = "noreply@skillbridge.com",
            SenderName = "SkillBridge"
        });

        var client = new HttpClient();
        var service = new BrevoEmailService(client, options, NullLogger<BrevoEmailService>.Instance);

        await service.SendEmailConfirmationAsync("user@example.com", "Test User", "https://skillbridge.test/confirm");
        await service.SendPasswordResetAsync("user@example.com", "Test User", "https://skillbridge.test/reset");
        await service.SendEmailChangeConfirmationAsync("new@example.com", "Test User", "https://skillbridge.test/change");
        await service.SendSecurityNotificationAsync("user@example.com", "Test User", "Security Notice", "Your account was accessed.");
    }

    [Theory]
    [InlineData("Valid1Pass", true)]
    [InlineData("short1A", false)] // < 8 chars
    [InlineData("lowercaseonly1", false)] // no uppercase
    [InlineData("UPPERCASEONLY1", false)] // no lowercase
    [InlineData("NoDigitAtAll!", false)] // no digit
    public void ResetPasswordRequestValidator_EnforcesComplexity(string password, bool expectedValid)
    {
        var validator = new ResetPasswordRequestValidator();
        var request = new ResetPasswordRequest("user@example.com", "token-123", password);
        var result = validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("valid@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    public void ForgotPasswordRequestValidator_ValidatesEmail(string email, bool expectedValid)
    {
        var validator = new ForgotPasswordRequestValidator();
        var request = new ForgotPasswordRequest(email);
        var result = validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("user-123", "token-abc", true)]
    [InlineData("", "token-abc", false)]
    [InlineData("user-123", "", false)]
    public void ConfirmEmailRequestValidator_RequiresUserIdAndToken(string userId, string token, bool expectedValid)
    {
        var validator = new ConfirmEmailRequestValidator();
        var request = new ConfirmEmailRequest(userId, token);
        var result = validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void DeleteAccountRequestValidator_InstantiatesAndValidates()
    {
        var validator = new DeleteAccountRequestValidator();
        var request = new DeleteAccountRequest("ValidPassword123!");
        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }
}

