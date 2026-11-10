using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Exceptions;
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
            CompanyName = request.Role == Roles.Employer ? request.CompanyName?.Trim() : null,
            LockoutEnabled = true,
            EmailConfirmed = false
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
            dbContext.Entry(user).State = EntityState.Detached;
            throw EmailTaken();
        }
    }

    public async Task<AuthUser?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return null;

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new LockedException(
                "auth.account_locked",
                "Account locked",
                "Your account has been temporarily locked due to 5 consecutive failed login attempts. Please try again later.");
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            if (await userManager.GetLockoutEnabledAsync(user))
            {
                await userManager.AccessFailedAsync(user);
                if (await userManager.IsLockedOutAsync(user))
                {
                    throw new LockedException(
                        "auth.account_locked",
                        "Account locked",
                        "Your account has been temporarily locked due to 5 consecutive failed login attempts. Please try again later.");
                }
            }
            return null;
        }

        if (await userManager.GetAccessFailedCountAsync(user) > 0)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }

        return await LoadAuthUserAsync(user, cancellationToken);
    }

    public async Task<AuthUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await LoadAuthUserAsync(user, cancellationToken);
    }

    public async Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
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

    public async Task<string> GenerateEmailConfirmationTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        if (user.EmailConfirmed)
        {
            return;
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            throw new BusinessRuleException(
                "auth.invalid_confirmation_token",
                "Invalid confirmation link",
                "The confirmation link is invalid or has expired. Please request a new one.");
        }
    }

    public async Task<(string Token, AuthUser User)?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return null;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var authUser = await LoadAuthUserAsync(user, cancellationToken);
        return authUser is null ? null : (token, authUser);
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email.Trim())
            ?? throw new BusinessRuleException(
                "auth.invalid_reset_token",
                "Invalid reset token",
                "The password reset token is invalid or has expired.");

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var msg = result.Errors.FirstOrDefault()?.Description ?? "Failed to reset password. The link may have expired.";
            throw new BusinessRuleException("auth.invalid_reset_token", "Password reset failed", msg);
        }

        // Reset lockout if present
        if (await userManager.IsLockedOutAsync(user))
        {
            await userManager.SetLockoutEndDateAsync(user, null);
        }

        return user.Id;
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var msg = result.Errors.FirstOrDefault()?.Description ?? "Failed to change password. Please ensure your current password is correct.";
            throw new BusinessRuleException("auth.invalid_password", "Password change failed", msg);
        }
    }

    public async Task SetPasswordAsync(string userId, SetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        if (await userManager.HasPasswordAsync(user))
        {
            throw new BusinessRuleException(
                "auth.password_already_set",
                "Password already set",
                "A password has already been configured for this account. Use Change Password instead.");
        }

        var result = await userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
        {
            var msg = result.Errors.FirstOrDefault()?.Description ?? "Failed to set password.";
            throw new BusinessRuleException("auth.invalid_password", "Password setup failed", msg);
        }
    }

    public async Task<string> GenerateChangeEmailTokenAsync(string userId, string newEmail, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        var normalizedEmail = newEmail.Trim();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null && existingUser.Id != userId)
        {
            throw EmailTaken();
        }

        return await userManager.GenerateChangeEmailTokenAsync(user, normalizedEmail);
    }

    public async Task ConfirmChangeEmailAsync(string userId, ConfirmChangeEmailRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        var normalizedEmail = request.NewEmail.Trim();
        var result = await userManager.ChangeEmailAsync(user, normalizedEmail, request.Token);
        if (!result.Succeeded)
        {
            var msg = result.Errors.FirstOrDefault()?.Description ?? "The confirmation link is invalid or has expired.";
            throw new BusinessRuleException("auth.invalid_email_change_token", "Email change failed", msg);
        }

        user.UserName = normalizedEmail;
        await userManager.UpdateAsync(user);
    }

    public async Task<AuthUser> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        user.FullName = request.FullName.Trim();
        if (request.CompanyName is not null)
        {
            user.CompanyName = request.CompanyName.Trim();
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new BusinessRuleException("auth.profile_update_failed", "Update failed", "Unable to update profile.");
        }

        return (await LoadAuthUserAsync(user, cancellationToken))!;
    }

    public async Task<AccountSecurityStatusDto> GetSecurityStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        var hasPassword = await userManager.HasPasswordAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? Roles.Candidate;
        var logins = await userManager.GetLoginsAsync(user);
        var linkedProviders = logins.Select(l => l.LoginProvider).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var providers = new[] { "Google", "GitHub" };
        var linkedAccounts = providers.Select(p => new LinkedAccountDto(
            Provider: p,
            ProviderDisplayName: p,
            IsLinked: linkedProviders.Contains(p),
            LinkedAt: null)).ToList();

        return new AccountSecurityStatusDto(
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            Role: role,
            CompanyName: user.CompanyName,
            IsEmailConfirmed: user.EmailConfirmed,
            HasPassword: hasPassword,
            LinkedAccounts: linkedAccounts);
    }

    public async Task DeleteAccountAsync(string userId, DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        if (await userManager.HasPasswordAsync(user))
        {
            if (string.IsNullOrWhiteSpace(request.Password) || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                throw new UnauthorizedException("auth.invalid_credentials", "Invalid password", "Password confirmation is required to delete your account.");
            }
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Delete candidate profile if candidate
        var profile = await dbContext.CandidateProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            dbContext.CandidateProfiles.Remove(profile);
        }

        // Clean up refresh tokens
        var tokens = await dbContext.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(cancellationToken);
        dbContext.RefreshTokens.RemoveRange(tokens);

        await dbContext.SaveChangesAsync(cancellationToken);

        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            await tx.RollbackAsync(cancellationToken);
            throw new BusinessRuleException("auth.delete_failed", "Account deletion failed", "Could not delete user account.");
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<AuthUser> ProcessSocialLoginAsync(
        SocialUserInfo socialUser,
        string? requestedRole = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Existing login with this provider and provider key
        var existingUser = await userManager.FindByLoginAsync(socialUser.Provider, socialUser.ProviderKey);
        if (existingUser is not null)
        {
            if (!existingUser.EmailConfirmed && socialUser.EmailVerified)
            {
                existingUser.EmailConfirmed = true;
                await userManager.UpdateAsync(existingUser);
            }
            return (await LoadAuthUserAsync(existingUser, cancellationToken))!;
        }

        // 2. Existing user with same email
        var emailUser = await userManager.FindByEmailAsync(socialUser.Email.Trim());
        if (emailUser is not null)
        {
            if (!socialUser.EmailVerified)
            {
                throw new BusinessRuleException(
                    "auth.unverified_external_email",
                    "Email not verified",
                    "The external provider has not verified this email address.");
            }

            var linkResult = await userManager.AddLoginAsync(
                emailUser,
                new UserLoginInfo(socialUser.Provider, socialUser.ProviderKey, socialUser.Provider));

            if (!linkResult.Succeeded)
            {
                throw new BusinessRuleException("auth.link_failed", "Linking failed", "Could not link external login.");
            }

            if (!emailUser.EmailConfirmed)
            {
                emailUser.EmailConfirmed = true;
                await userManager.UpdateAsync(emailUser);
            }

            return (await LoadAuthUserAsync(emailUser, cancellationToken))!;
        }

        // 3. New local user creation
        var role = requestedRole is Roles.Employer ? Roles.Employer : Roles.Candidate;
        var newUser = new AppUser
        {
            UserName = socialUser.Email.Trim(),
            Email = socialUser.Email.Trim(),
            FullName = string.IsNullOrWhiteSpace(socialUser.FullName) ? socialUser.Email : socialUser.FullName.Trim(),
            EmailConfirmed = socialUser.EmailVerified,
            LockoutEnabled = true
        };

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var creation = await userManager.CreateAsync(newUser);
            EnsureCreated(creation);

            var roleRes = await userManager.AddToRoleAsync(newUser, role);
            if (!roleRes.Succeeded)
            {
                throw new InvalidOperationException("Failed to assign default role.");
            }

            if (role == Roles.Candidate)
            {
                dbContext.CandidateProfiles.Add(new CandidateProfile(newUser.Id));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var addLogin = await userManager.AddLoginAsync(
                newUser,
                new UserLoginInfo(socialUser.Provider, socialUser.ProviderKey, socialUser.Provider));

            if (!addLogin.Succeeded)
            {
                throw new InvalidOperationException("Failed to attach external login info.");
            }

            await tx.CommitAsync(cancellationToken);
            return ToAuthUser(newUser, role);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<LinkedAccountDto>> GetLinkedAccountsAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        var logins = await userManager.GetLoginsAsync(user);
        var linkedProviders = logins.Select(l => l.LoginProvider).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var providers = new[] { "Google", "GitHub" };

        return providers.Select(p => new LinkedAccountDto(
            Provider: p,
            ProviderDisplayName: p,
            IsLinked: linkedProviders.Contains(p),
            LinkedAt: null)).ToList();
    }

    public async Task LinkSocialAccountAsync(string userId, SocialUserInfo socialUser, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        var existingUser = await userManager.FindByLoginAsync(socialUser.Provider, socialUser.ProviderKey);
        if (existingUser is not null)
        {
            if (existingUser.Id == userId)
            {
                return; // already linked to this user
            }

            throw new BusinessRuleException(
                "auth.social_already_linked",
                "Account already linked",
                "This external login is already associated with another SkillBridge account.");
        }

        var result = await userManager.AddLoginAsync(
            user,
            new UserLoginInfo(socialUser.Provider, socialUser.ProviderKey, socialUser.Provider));

        if (!result.Succeeded)
        {
            var msg = result.Errors.FirstOrDefault()?.Description ?? "Failed to link account.";
            throw new BusinessRuleException("auth.link_failed", "Account linking failed", msg);
        }
    }

    public async Task UnlinkSocialAccountAsync(string userId, string provider, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("auth.user_not_found", "User not found", "User not found.");

        var logins = await userManager.GetLoginsAsync(user);
        var target = logins.FirstOrDefault(l => l.LoginProvider.Equals(provider, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        var hasPassword = await userManager.HasPasswordAsync(user);
        if (!hasPassword && logins.Count <= 1)
        {
            throw new BusinessRuleException(
                "auth.cannot_unlink_only_auth",
                "Cannot disconnect account",
                "You cannot unlink your only login method. Please set a password first.");
        }

        var result = await userManager.RemoveLoginAsync(user, target.LoginProvider, target.ProviderKey);
        if (!result.Succeeded)
        {
            throw new BusinessRuleException("auth.unlink_failed", "Unlink failed", "Could not remove external login.");
        }
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
        new(user.Id, user.Email!, user.FullName, role, user.CompanyName, user.EmailConfirmed);

    private static void EnsureCreated(IdentityResult result)
    {
        if (result.Succeeded)
            return;

        if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            throw EmailTaken();

        var detail = string.Join("; ", result.Errors.Select(e => e.Description));
        if (string.IsNullOrWhiteSpace(detail))
            detail = "Registration details are invalid.";

        throw new BusinessRuleException("validation.failed", "Invalid registration", detail);
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
