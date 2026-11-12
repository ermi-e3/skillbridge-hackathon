using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Exceptions;

namespace SkillBridge.Api.Controllers;

public sealed record RefreshRequest(string? RefreshToken = null);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    public const string RefreshTokenCookieName = "refreshToken";
    public const string CsrfCookieName = "XSRF-TOKEN";
    public const string CsrfHeaderName = "X-XSRF-TOKEN";

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        var frontendUrl = GetFrontendBaseUrl();
        var result = await authService.RegisterWithRefreshTokenAsync(request, ipAddress, frontendUrl, ct);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        SetCsrfCookie();
        return Ok(result.Response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("LoginRateLimit")]
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        var result = await authService.LoginWithRefreshTokenAsync(request, ipAddress, ct);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        SetCsrfCookie();
        return Ok(result.Response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("LoginRateLimit")]
    [HttpPost("social-login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> SocialLogin(SocialLoginRequest request, CancellationToken ct)
    {
        var ipAddress = GetClientIpAddress();
        var result = await authService.SocialLoginWithRefreshTokenAsync(request, ipAddress, ct);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        SetCsrfCookie();
        return Ok(result.Response);
    }

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        await authService.ConfirmEmailAsync(request, ct);
        return Ok(new { message = "Email confirmed successfully. You can now use all platform features." });
    }

    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken ct)
    {
        var frontendUrl = GetFrontendBaseUrl();
        await authService.ResendConfirmationEmailAsync(request, frontendUrl, ct);
        return Ok(new { message = "If an account with that email exists and is unconfirmed, a verification link has been sent." });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var frontendUrl = GetFrontendBaseUrl();
        await authService.ForgotPasswordAsync(request, frontendUrl, ct);
        return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(request, ct);
        return Ok(new { message = "Your password has been reset successfully. Please sign in with your new password." });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest? request, CancellationToken ct)
    {
        if (!ValidateCsrfToken())
        {
            throw new ForbiddenException("auth.csrf_invalid", "Invalid CSRF token", "Anti-forgery token mismatch or missing.");
        }

        var token = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token) && Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken))
        {
            token = cookieToken;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedException("auth.missing_refresh_token", "Missing refresh token", "No refresh token provided.");
        }

        var ipAddress = GetClientIpAddress();
        var result = await authService.RefreshTokenAsync(token, ipAddress, ct);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        SetCsrfCookie();
        return Ok(result.Response);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? request, CancellationToken ct)
    {
        if (!ValidateCsrfToken())
        {
            throw new ForbiddenException("auth.csrf_invalid", "Invalid CSRF token", "Anti-forgery token mismatch or missing.");
        }

        var token = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token) && Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken))
        {
            token = cookieToken;
        }

        var ipAddress = GetClientIpAddress();
        await authService.LogoutAsync(token, ipAddress, ct);
        ClearAuthCookies();
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var token = SetCsrfCookie();
        return Ok(new { csrfToken = token });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<AuthUser>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthUser>> Me(CancellationToken ct) =>
        Ok(await authService.GetCurrentUserAsync(ct));

    private void SetRefreshTokenCookie(string token, DateTime expiresAt)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expiresAt
        };
        Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
    }

    private string SetCsrfCookie()
    {
        var csrfToken = Guid.NewGuid().ToString("N");
        var cookieOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };
        Response.Cookies.Append(CsrfCookieName, csrfToken, cookieOptions);
        return csrfToken;
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth"
        });
        Response.Cookies.Delete(CsrfCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }

    private bool ValidateCsrfToken()
    {
        if (Request.Cookies.TryGetValue(CsrfCookieName, out var cookieCsrf) && !string.IsNullOrWhiteSpace(cookieCsrf))
        {
            if (!Request.Headers.TryGetValue(CsrfHeaderName, out var headerCsrf) || headerCsrf != cookieCsrf)
            {
                return false;
            }
        }
        return true;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string GetFrontendBaseUrl()
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return origin.TrimEnd('/');
        }

        var referer = Request.Headers.Referer.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Authority}";
        }

        return "http://localhost:4200";
    }
}
