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

        await SeedDemoDataAsync(cancellationToken);
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

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var adminUser = await userManager.FindByEmailAsync("admin@hackathon.local");
        var candidateUser = await userManager.FindByEmailAsync("user@hackathon.local");

        if (adminUser is not null && !await dbContext.Jobs.AnyAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            var jobs = new[]
            {
                new Job(adminUser.Id, "Junior Backend Developer",
                    "Build and maintain REST APIs for our logistics platform using ASP.NET Core, C#, and PostgreSQL.",
                    "Addis Ababa", [1, 3, 5], now.AddDays(-2)),
                new Job(adminUser.Id, "Full-Stack Software Engineer",
                    "Join our agile product team building modern web applications with Angular, TypeScript, and .NET Core microservices.",
                    "Remote", [1, 2, 3, 4, 12], now.AddDays(-1)),
                new Job(adminUser.Id, "Frontend Angular Specialist",
                    "Design and implement intuitive, responsive user experiences and enterprise component libraries using Angular and TypeScript.",
                    "Addis Ababa (Hybrid)", [2, 4, 8, 9, 10], now)
            };

            dbContext.Jobs.AddRange(jobs);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (candidateUser is not null)
        {
            var profile = await dbContext.CandidateProfiles
                .Include(p => p.Skills)
                .FirstOrDefaultAsync(p => p.UserId == candidateUser.Id, cancellationToken);

            if (profile is not null && !profile.HasSkills)
            {
                profile.Update(
                    "Junior .NET Developer",
                    "Passionate full-stack developer with experience building ASP.NET Core Web APIs and modern Angular frontends.",
                    "https://github.com/demo-candidate",
                    [1, 2, 3, 4, 5],
                    DateTime.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
