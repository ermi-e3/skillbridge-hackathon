using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.CandidateProfiles;
using SkillBridge.Domain.Common;

namespace SkillBridge.Api.Controllers;

[ApiController]
[Route("api/candidates/me/profile")]
[Authorize(Roles = Roles.Candidate)]
public sealed class CandidateProfileController(ICandidateProfileService profileService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CandidateProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateProfileResponse>> Get(CancellationToken ct) =>
        Ok(await profileService.GetMyProfileAsync(ct));

    [HttpPut]
    [ProducesResponseType<CandidateProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateProfileResponse>> Update(UpdateCandidateProfileRequest request, CancellationToken ct) =>
        Ok(await profileService.UpdateMyProfileAsync(request, ct));
}
