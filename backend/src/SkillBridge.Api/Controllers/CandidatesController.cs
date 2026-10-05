using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Applications;
using SkillBridge.Domain.Common;

namespace SkillBridge.Api.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize(Roles = Roles.Candidate)]
public sealed class CandidateApplicationsController(ApplicationsService applicationsService) : ControllerBase
{
    [HttpGet("me/applications")]
    [ProducesResponseType<IReadOnlyList<ApplicationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> GetMyApplications(
        [FromQuery] string? status, CancellationToken ct) =>
        Ok(await applicationsService.GetMyApplicationsAsync(status, ct));
}
