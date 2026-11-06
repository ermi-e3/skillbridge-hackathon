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
        var result = await authService.RegisterWithRefreshTokenAsync(request, ipAddress, ct);
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
}
