using FluentValidation;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;

namespace SkillBridge.Application.Auth;

public sealed class AuthService(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IEmailService emailService,
    ISocialAuthValidator socialAuthValidator,
    ICurrentUser currentUser,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<ConfirmEmailRequest> confirmEmailValidator,
    IValidator<ResendConfirmationRequest> resendConfirmationValidator,
    IValidator<ForgotPasswordRequest> forgotPasswordValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator,
    IValidator<SocialLoginRequest> socialLoginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    IValidator<SetPasswordRequest> setPasswordValidator,
    IValidator<ChangeEmailRequest> changeEmailValidator,
    IValidator<ConfirmChangeEmailRequest> confirmChangeEmailValidator,
    IValidator<UpdateProfileRequest> updateProfileValidator,
    IValidator<DeleteAccountRequest> deleteAccountValidator)
{
    public async Task<AuthResult> RegisterWithRefreshTokenAsync(
        RegisterRequest request,
        string? ipAddress = null,
        string? frontendBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with
        {
            Email = (request.Email ?? string.Empty).Trim(),
            FullName = (request.FullName ?? string.Empty).Trim(),
            CompanyName = request.Role == Roles.Employer ? request.CompanyName?.Trim() : null
        };

        await ValidateAsync(registerValidator, normalized, cancellationToken);

        if (normalized.Role is not (Roles.Candidate or Roles.Employer))
        {
            throw new BusinessRuleException(
                "auth.invalid_role", "Invalid account type", "Role must be Candidate or Employer.");
        }

        if (normalized.Role == Roles.Employer && string.IsNullOrWhiteSpace(normalized.CompanyName))
        {
            throw new BusinessRuleException(
                "auth.company_required", "Company name required", "Employers must provide a company name.");
        }

        var user = await identityService.RegisterAsync(normalized, cancellationToken);

        // Generate and send confirmation email
        try
        {
            var token = await identityService.GenerateEmailConfirmationTokenAsync(user.UserId, cancellationToken);
            var baseUrl = string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:4200" : frontendBaseUrl.TrimEnd('/');
            var link = $"{baseUrl}/confirm-email?userId={Uri.EscapeDataString(user.UserId)}&token={Uri.EscapeDataString(token)}";
            await emailService.SendEmailConfirmationAsync(user.Email, user.FullName, link, cancellationToken);
        }
        catch (Exception)
        {
            // Do not fail registration if email delivery experiences temporary issue
        }

        var refreshResult = await refreshTokenService.CreateRefreshTokenAsync(
            user.UserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return new AuthResult(CreateResponse(user), refreshResult.RawToken, refreshResult.ExpiresAt);
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await RegisterWithRefreshTokenAsync(request, cancellationToken: cancellationToken);
        return result.Response;
    }

    public async Task<AuthResult> LoginWithRefreshTokenAsync(
        LoginRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { Email = (request.Email ?? string.Empty).Trim() };
        await ValidateAsync(loginValidator, normalized, cancellationToken);

        var user = await identityService.AuthenticateAsync(normalized, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedException(
                "auth.invalid_credentials", "Invalid credentials", "Invalid email or password.");
        }

        var refreshResult = await refreshTokenService.CreateRefreshTokenAsync(
            user.UserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return new AuthResult(CreateResponse(user), refreshResult.RawToken, refreshResult.ExpiresAt);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await LoginWithRefreshTokenAsync(request, cancellationToken: cancellationToken);
        return result.Response;
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(confirmEmailValidator, request, cancellationToken);
        await identityService.ConfirmEmailAsync(request, cancellationToken);
    }

    public async Task ResendConfirmationEmailAsync(
        ResendConfirmationRequest request,
        string? frontendBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { Email = request.Email.Trim() };
        await ValidateAsync(resendConfirmationValidator, normalized, cancellationToken);

        var user = await identityService.FindByEmailAsync(normalized.Email, cancellationToken);
        if (user is not null && !user.IsEmailConfirmed)
        {
            var token = await identityService.GenerateEmailConfirmationTokenAsync(user.UserId, cancellationToken);
            var baseUrl = string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:4200" : frontendBaseUrl.TrimEnd('/');
            var link = $"{baseUrl}/confirm-email?userId={Uri.EscapeDataString(user.UserId)}&token={Uri.EscapeDataString(token)}";
            await emailService.SendEmailConfirmationAsync(user.Email, user.FullName, link, cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string? frontendBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { Email = request.Email.Trim() };
        await ValidateAsync(forgotPasswordValidator, normalized, cancellationToken);

        var resetInfo = await identityService.GeneratePasswordResetTokenAsync(normalized.Email, cancellationToken);
        if (resetInfo is not null)
        {
            var baseUrl = string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:4200" : frontendBaseUrl.TrimEnd('/');
            var link = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(resetInfo.Value.User.Email)}&token={Uri.EscapeDataString(resetInfo.Value.Token)}";
            await emailService.SendPasswordResetAsync(resetInfo.Value.User.Email, resetInfo.Value.User.FullName, link, cancellationToken);
        }
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { Email = request.Email.Trim() };
        await ValidateAsync(resetPasswordValidator, normalized, cancellationToken);

        var userId = await identityService.ResetPasswordAsync(normalized, cancellationToken);

        // Security: Revoke all active refresh token families on password reset
        await refreshTokenService.RevokeAllUserRefreshTokensAsync(userId, "PasswordReset", null, cancellationToken);

        var user = await identityService.FindByIdAsync(userId, cancellationToken);
        if (user is not null)
        {
            await emailService.SendSecurityNotificationAsync(
                user.Email,
                user.FullName,
                "Password changed",
                "Your account password was successfully reset. If you did not make this change, contact support immediately.",
                cancellationToken);
        }
    }

    public async Task<AuthResult> SocialLoginWithRefreshTokenAsync(
        SocialLoginRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(socialLoginValidator, request, cancellationToken);

        var socialUser = await socialAuthValidator.ValidateAsync(request, cancellationToken);
        var user = await identityService.ProcessSocialLoginAsync(socialUser, request.Role, cancellationToken);

        var refreshResult = await refreshTokenService.CreateRefreshTokenAsync(
            user.UserId,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return new AuthResult(CreateResponse(user), refreshResult.RawToken, refreshResult.ExpiresAt);
    }

    public async Task ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(changePasswordValidator, request, cancellationToken);
        await identityService.ChangePasswordAsync(userId, request, cancellationToken);

        var user = await identityService.FindByIdAsync(userId, cancellationToken);
        if (user is not null)
        {
            await emailService.SendSecurityNotificationAsync(
                user.Email,
                user.FullName,
                "Password changed",
                "Your password has been changed successfully.",
                cancellationToken);
        }
    }

    public async Task SetPasswordAsync(
        string userId,
        SetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(setPasswordValidator, request, cancellationToken);
        await identityService.SetPasswordAsync(userId, request, cancellationToken);
    }

    public async Task RequestEmailChangeAsync(
        string userId,
        ChangeEmailRequest request,
        string? frontendBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { NewEmail = request.NewEmail.Trim() };
        await ValidateAsync(changeEmailValidator, normalized, cancellationToken);

        var user = await identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw Unauthenticated();

        var token = await identityService.GenerateChangeEmailTokenAsync(userId, normalized.NewEmail, cancellationToken);
        var baseUrl = string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:4200" : frontendBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/confirm-email-change?email={Uri.EscapeDataString(normalized.NewEmail)}&token={Uri.EscapeDataString(token)}";

        await emailService.SendEmailChangeConfirmationAsync(normalized.NewEmail, user.FullName, link, cancellationToken);
    }

    public async Task ConfirmEmailChangeAsync(
        string userId,
        ConfirmChangeEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { NewEmail = request.NewEmail.Trim() };
        await ValidateAsync(confirmChangeEmailValidator, normalized, cancellationToken);
        await identityService.ConfirmChangeEmailAsync(userId, normalized, cancellationToken);
    }

    public async Task<AuthUser> UpdateProfileAsync(
        string userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(updateProfileValidator, request, cancellationToken);
        return await identityService.UpdateProfileAsync(userId, request, cancellationToken);
    }

    public async Task<AccountSecurityStatusDto> GetSecurityStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await identityService.GetSecurityStatusAsync(userId, cancellationToken);
    }

    public async Task DeleteAccountAsync(
        string userId,
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(deleteAccountValidator, request, cancellationToken);
        await refreshTokenService.RevokeAllUserRefreshTokensAsync(userId, "AccountDeleted", null, cancellationToken);
        await identityService.DeleteAccountAsync(userId, request, cancellationToken);
    }

    public async Task LogoutAllDevicesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await refreshTokenService.RevokeAllUserRefreshTokensAsync(userId, "LogoutAllDevices", null, cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedAccountDto>> GetLinkedAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await identityService.GetLinkedAccountsAsync(userId, cancellationToken);
    }

    public async Task LinkSocialAccountAsync(
        string userId,
        SocialLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(socialLoginValidator, request, cancellationToken);
        var socialUser = await socialAuthValidator.ValidateAsync(request, cancellationToken);
        await identityService.LinkSocialAccountAsync(userId, socialUser, cancellationToken);
    }

    public async Task UnlinkSocialAccountAsync(
        string userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        await identityService.UnlinkSocialAccountAsync(userId, provider, cancellationToken);
    }

    public async Task<AuthResult> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var (user, newRefreshToken) = await refreshTokenService.RotateRefreshTokenAsync(
            rawRefreshToken,
            ipAddress,
            cancellationToken);

        return new AuthResult(CreateResponse(user), newRefreshToken.RawToken, newRefreshToken.ExpiresAt);
    }

    public async Task LogoutAsync(
        string? rawRefreshToken,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            await refreshTokenService.RevokeRefreshTokenAsync(
                rawRefreshToken,
                "Logout",
                ipAddress,
                cancellationToken);
        }
    }

    public async Task<AuthUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId))
        {
            throw Unauthenticated();
        }

        return await identityService.FindByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw Unauthenticated();
    }

    private AuthResponse CreateResponse(AuthUser user)
    {
        var token = jwtTokenService.Generate(user);
        return new AuthResponse(
            token.Value,
            token.ExpiresAt,
            user.UserId,
            user.Email,
            user.FullName,
            user.Role,
            user.CompanyName,
            user.IsEmailConfirmed);
    }

    private static async Task ValidateAsync<T>(
        IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(error => char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..])
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            throw new RequestValidationException(errors);
        }
    }

    private static UnauthorizedException Unauthenticated() =>
        new("auth.unauthenticated", "Authentication required", "Please sign in to continue.");
}
