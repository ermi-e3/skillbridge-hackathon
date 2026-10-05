using SkillBridge.Application.Auth;

namespace SkillBridge.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<AuthUser> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthUser?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, AuthUser>> FindByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
}
