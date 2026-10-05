using FluentValidation;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;

namespace SkillBridge.Application.Auth;

public sealed class AuthService(
    IIdentityService identityService,
    IJwtTokenService jwtTokenService,
    ICurrentUser currentUser,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator)
{
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with
        {
            Email = (request.Email ?? string.Empty).Trim(),
            FullName = (request.FullName ?? string.Empty).Trim(),
            // A Candidate's company value does not form part of their account.
            CompanyName = request.Role == Roles.Employer ? request.CompanyName?.Trim() : null
        };

        await ValidateAsync(registerValidator, normalized, cancellationToken);

        if (normalized.Role is not (Roles.Candidate or Roles.Employer))
        {
            throw new BusinessRuleException(
                "auth.invalid_role", "Invalid account type", "Role must be Candidate or Employer.");
        }

        if (normalized.Role == Roles.Employer && string.IsNullOrWhiteSpace(normalized.CompanyName))
        {
            throw new BusinessRuleException(
                "auth.company_required", "Company name required", "Employers must provide a company name.");
        }

        var user = await identityService.RegisterAsync(normalized, cancellationToken);
        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request with { Email = (request.Email ?? string.Empty).Trim() };
        await ValidateAsync(loginValidator, normalized, cancellationToken);

        var user = await identityService.AuthenticateAsync(normalized, cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedException(
                "auth.invalid_credentials", "Invalid credentials", "Invalid email or password.");
        }

        return CreateResponse(user);
    }

    public async Task<AuthUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(currentUser.UserId))
        {
            throw Unauthenticated();
        }

        return await identityService.FindByIdAsync(currentUser.UserId, cancellationToken)
            ?? throw Unauthenticated();
    }

    private AuthResponse CreateResponse(AuthUser user)
    {
        var token = jwtTokenService.Generate(user);
        return new AuthResponse(
            token.Value, token.ExpiresAt, user.UserId, user.Email, user.FullName, user.Role, user.CompanyName);
    }

    private static async Task ValidateAsync<T>(
        IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(error => char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..])
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            throw new RequestValidationException(errors);
        }
    }

    private static UnauthorizedException Unauthenticated() =>
        new("auth.unauthenticated", "Authentication required", "Please sign in to continue.");
}
