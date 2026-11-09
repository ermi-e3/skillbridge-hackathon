using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/account")]
public sealed class AccountController(
    AuthService authService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("security-status")]
    [ProducesResponseType<AccountSecurityStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountSecurityStatusDto>> GetSecurityStatus(CancellationToken ct)
    {
        var status = await authService.GetSecurityStatusAsync(currentUser.UserId!, ct);
        return Ok(status);
    }

    [HttpPut("profile")]
    [ProducesResponseType<AuthUser>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthUser>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
    {
        var updated = await authService.UpdateProfileAsync(currentUser.UserId!, request, ct);
        return Ok(updated);
    }

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(currentUser.UserId!, request, ct);
        return Ok(new { message = "Password changed successfully." });
    }

    [HttpPost("set-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request, CancellationToken ct)
    {
        await authService.SetPasswordAsync(currentUser.UserId!, request, ct);
        return Ok(new { message = "Password set successfully." });
    }

    [HttpPost("change-email-request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestEmailChange(ChangeEmailRequest request, CancellationToken ct)
    {
        var frontendUrl = GetFrontendBaseUrl();
        await authService.RequestEmailChangeAsync(currentUser.UserId!, request, frontendUrl, ct);
        return Ok(new { message = "Confirmation link sent to your new email address." });
    }

    [HttpPost("confirm-change-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmChangeEmailRequest request, CancellationToken ct)
    {
        await authService.ConfirmEmailChangeAsync(currentUser.UserId!, request, ct);
        return Ok(new { message = "Email address updated successfully." });
    }

    [HttpGet("social-accounts")]
    [ProducesResponseType<IReadOnlyList<LinkedAccountDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LinkedAccountDto>>> GetLinkedAccounts(CancellationToken ct)
    {
        var accounts = await authService.GetLinkedAccountsAsync(currentUser.UserId!, ct);
        return Ok(accounts);
    }

    [HttpPost("social-accounts/link")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkSocialAccount(SocialLoginRequest request, CancellationToken ct)
    {
        await authService.LinkSocialAccountAsync(currentUser.UserId!, request, ct);
        return Ok(new { message = $"{request.Provider} account linked successfully." });
    }

    [HttpPost("social-accounts/unlink")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlinkSocialAccount([FromBody] UnlinkAccountRequest request, CancellationToken ct)
    {
        await authService.UnlinkSocialAccountAsync(currentUser.UserId!, request.Provider, ct);
        return Ok(new { message = $"{request.Provider} account unlinked successfully." });
    }

    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LogoutAllDevices(CancellationToken ct)
    {
        await authService.LogoutAllDevicesAsync(currentUser.UserId!, ct);
        ClearAuthCookies();
        return Ok(new { message = "Logged out from all devices successfully." });
    }

    [HttpPost("delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, CancellationToken ct)
    {
        await authService.DeleteAccountAsync(currentUser.UserId!, request, ct);
        ClearAuthCookies();
        return Ok(new { message = "Account deleted successfully." });
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(AuthController.RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth"
        });
        Response.Cookies.Delete(AuthController.CsrfCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
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

public sealed record UnlinkAccountRequest(string Provider);

