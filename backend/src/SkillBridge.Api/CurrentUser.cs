using System.Security.Claims;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Api;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string UserId => IsAuthenticated
        ? User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User?.FindFirstValue("sub")
            ?? throw Unauthenticated()
        : throw Unauthenticated();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => IsAuthenticated && User!.IsInRole(role);

    private static UnauthorizedException Unauthenticated() =>
        new("auth.unauthenticated", "Unauthorized", "Please sign in to continue.");
}
