using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;

namespace SkillBridge.Auth.Tests;

// This controller is registered only by AuthApiFactory. It is never shipped in the API.
[ApiController]
[Route("__tests/authorization")]
public sealed class RoleVerificationController(ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Roles = Roles.Candidate)]
    [HttpGet("candidate")]
    public IActionResult Candidate() => CurrentIdentity();

    [Authorize(Roles = Roles.Employer)]
    [HttpGet("employer")]
    public IActionResult Employer() => CurrentIdentity();

    private IActionResult CurrentIdentity() => Ok(new
    {
        userId = currentUser.UserId,
        claimsUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
        isAuthenticated = currentUser.IsAuthenticated,
        isCandidate = currentUser.IsInRole(Roles.Candidate),
        isEmployer = currentUser.IsInRole(Roles.Employer)
    });
}
