using Microsoft.AspNetCore.Identity;

namespace SkillBridge.Infrastructure.Identity;

/// <summary>A user of either role. The role itself is stored by Identity (AspNetUserRoles).</summary>
public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Employers only.</summary>
    public string? CompanyName { get; set; }
}
