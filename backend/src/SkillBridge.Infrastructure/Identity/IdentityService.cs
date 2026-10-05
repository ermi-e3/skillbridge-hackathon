using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Entities;
using SkillBridge.Infrastructure.Persistence;

namespace SkillBridge.Infrastructure.Identity;

public sealed class IdentityService(UserManager<AppUser> userManager, AppDbContext dbContext) : IIdentityService
{
    public async Task<AuthUser> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Role is not (Roles.Candidate or Roles.Employer))
            throw new BusinessRuleException("auth.invalid_role", "Invalid role", "Role must be Candidate or Employer.");

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw EmailTaken();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            CompanyName = request.Role == Roles.Employer ? request.CompanyName?.Trim() : null
        };
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var creation = await userManager.CreateAsync(user, request.Password);
            EnsureCreated(creation);
            cancellationToken.ThrowIfCancellationRequested();

            var roleAssignment = await userManager.AddToRoleAsync(user, request.Role);
            if (!roleAssignment.Succeeded)
                throw new InvalidOperationException("The account role could not be assigned.");

            if (request.Role == Roles.Candidate)
                dbContext.CandidateProfiles.Add(new CandidateProfile(user.Id));

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToAuthUser(user, request.Role);
        }
        catch (DbUpdateException exception) when (IsDuplicateEmail(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // A seeder can reuse this scoped context to load the concurrent winner.
            dbContext.Entry(user).State = EntityState.Detached;
            throw EmailTaken();
        }
    }

    public async Task<AuthUser?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return null;

        return await LoadAuthUserAsync(user, cancellationToken);
    }

    public async Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await LoadAuthUserAsync(user, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, AuthUser>> FindByIdsAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var idList = userIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<string, AuthUser>();

        var users = await dbContext.Users
            .Where(u => idList.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, AuthUser>();
        foreach (var user in users)
        {
            var authUser = await LoadAuthUserAsync(user, cancellationToken);
            if (authUser is not null)
            {
                result[user.Id] = authUser;
            }
        }
        return result;
    }

    private async Task<AuthUser?> LoadAuthUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || roles[0] is not (Roles.Candidate or Roles.Employer))
            return null;

        return ToAuthUser(user, roles[0]);
    }

    private static AuthUser ToAuthUser(AppUser user, string role) =>
        new(user.Id, user.Email!, user.FullName, role, user.CompanyName);

    private static void EnsureCreated(IdentityResult result)
    {
        if (result.Succeeded)
            return;

        if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            throw EmailTaken();

        throw new BusinessRuleException("validation.failed", "Invalid registration", "Registration details are invalid.");
    }

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "EmailIndex" or "UserNameIndex"
        };

    private static BusinessRuleException EmailTaken() =>
        new("auth.email_taken", "Email already registered", "This email is already registered.");
}
