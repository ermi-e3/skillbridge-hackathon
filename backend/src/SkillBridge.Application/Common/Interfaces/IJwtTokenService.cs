using SkillBridge.Application.Auth;

namespace SkillBridge.Application.Common.Interfaces;

public interface IJwtTokenService
{
    AccessToken Generate(AuthUser user);
}
