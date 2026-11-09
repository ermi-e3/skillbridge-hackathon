using SkillBridge.Application.Auth;

namespace SkillBridge.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<AuthUser> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthUser?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, AuthUser>> FindByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);

    Task<string> GenerateEmailConfirmationTokenAsync(string userId, CancellationToken cancellationToken = default);
    Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default);

    Task<(string Token, AuthUser User)?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);
    Task<string> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task SetPasswordAsync(string userId, SetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<string> GenerateChangeEmailTokenAsync(string userId, string newEmail, CancellationToken cancellationToken = default);
    Task ConfirmChangeEmailAsync(string userId, ConfirmChangeEmailRequest request, CancellationToken cancellationToken = default);

    Task<AuthUser> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<AccountSecurityStatusDto> GetSecurityStatusAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(string userId, DeleteAccountRequest request, CancellationToken cancellationToken = default);

    Task<AuthUser> ProcessSocialLoginAsync(SocialUserInfo socialUser, string? requestedRole = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LinkedAccountDto>> GetLinkedAccountsAsync(string userId, CancellationToken cancellationToken = default);
    Task LinkSocialAccountAsync(string userId, SocialUserInfo socialUser, CancellationToken cancellationToken = default);
    Task UnlinkSocialAccountAsync(string userId, string provider, CancellationToken cancellationToken = default);
}
