using SkillBridge.Application.Auth;

namespace SkillBridge.Application.Common.Interfaces;

public sealed record SocialUserInfo(
    string Provider,
    string ProviderKey,
    string Email,
    string FullName,
    bool EmailVerified);

public interface ISocialAuthValidator
{
    Task<SocialUserInfo> ValidateAsync(SocialLoginRequest request, CancellationToken cancellationToken = default);
}

