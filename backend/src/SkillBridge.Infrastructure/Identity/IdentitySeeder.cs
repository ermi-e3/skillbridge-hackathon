using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Entities;
using SkillBridge.Infrastructure.Persistence;

namespace SkillBridge.Infrastructure.Identity;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<AppUser> userManager,
    IIdentityService identityService,
    AppDbContext dbContext)
{
    public async Task SeedAsync(bool seedDemoUsers, CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(Roles.Candidate, cancellationToken);
        await EnsureRoleAsync(Roles.Employer, cancellationToken);

        if (!seedDemoUsers)
            return;

        await EnsureDemoUserAsync(
            new RegisterRequest("admin@hackathon.local", "Admin123!", "Demo Employer", Roles.Employer, "SkillBridge Demo Company"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("user@hackathon.local", "User123!", "Demo Candidate", Roles.Candidate),
            cancellationToken);
    }

    private async Task EnsureRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await roleManager.RoleExistsAsync(roleName))
            return;

        var role = new IdentityRole(roleName);
        try
        {
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded && !await roleManager.RoleExistsAsync(roleName))
                throw new InvalidOperationException("The required identity roles could not be initialized.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "RoleNameIndex" })
        {
            // Another API instance may have seeded this role after our existence check.
            dbContext.Entry(role).State = EntityState.Detached;
            if (!await roleManager.RoleExistsAsync(roleName))
                throw new InvalidOperationException("The required identity roles could not be initialized.");
        }
    }

    private async Task EnsureDemoUserAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            try
            {
                await identityService.RegisterAsync(request, cancellationToken);
                return;
            }
            catch (BusinessRuleException exception) when (exception.Code == "auth.email_taken")
            {
                // Registration is atomic; a concurrent seeder may already have completed it.
                user = await userManager.FindByEmailAsync(request.Email);
            }
        }

        if (user is null)
            throw new InvalidOperationException("A demo account could not be initialized.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Any(role => role != request.Role) || roles.Count > 1)
            throw new InvalidOperationException("A demo account has an unexpected role.");

        if (roles.Count == 0)
        {
            var assignment = await userManager.AddToRoleAsync(user, request.Role);
            if (!assignment.Succeeded)
                throw new InvalidOperationException("A demo account role could not be initialized.");
        }

        var hasCandidateProfile = await dbContext.CandidateProfiles
            .AnyAsync(profile => profile.UserId == user.Id, cancellationToken);
        if (request.Role == Roles.Candidate && !hasCandidateProfile)
            dbContext.CandidateProfiles.Add(new CandidateProfile(user.Id));
        else if (request.Role == Roles.Employer && hasCandidateProfile)
            throw new InvalidOperationException("The employer demo account has an unexpected candidate profile.");

        var updateUser = false;
        if (string.IsNullOrWhiteSpace(user.FullName))
        {
            user.FullName = request.FullName;
            updateUser = true;
        }
        if (request.Role == Roles.Employer && string.IsNullOrWhiteSpace(user.CompanyName))
        {
            user.CompanyName = request.CompanyName;
            updateUser = true;
        }
        if (updateUser && !(await userManager.UpdateAsync(user)).Succeeded)
            throw new InvalidOperationException("A demo account could not be initialized.");

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
