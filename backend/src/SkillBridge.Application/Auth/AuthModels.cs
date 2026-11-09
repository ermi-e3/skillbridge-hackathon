namespace SkillBridge.Application.Auth;

public sealed record ConfirmEmailRequest(string UserId, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record SocialLoginRequest(
    string Provider,
    string? Code = null,
    string? IdToken = null,
    string? RedirectUri = null,
    string? Role = null);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record SetPasswordRequest(string NewPassword);

public sealed record ChangeEmailRequest(string NewEmail);

public sealed record ConfirmChangeEmailRequest(string NewEmail, string Token);

public sealed record UpdateProfileRequest(string FullName, string? CompanyName = null);

public sealed record DeleteAccountRequest(string? Password = null);

public sealed record LinkedAccountDto(
    string Provider,
    string ProviderDisplayName,
    bool IsLinked,
    DateTime? LinkedAt = null);

public sealed record AccountSecurityStatusDto(
    string UserId,
    string Email,
    string FullName,
    string Role,
    string? CompanyName,
    bool IsEmailConfirmed,
    bool HasPassword,
    IReadOnlyList<LinkedAccountDto> LinkedAccounts);
