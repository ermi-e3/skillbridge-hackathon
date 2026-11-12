using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Applications;
using SkillBridge.Domain.Common;

namespace SkillBridge.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public sealed class ApplicationsController(ApplicationsService applicationsService) : ControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> GetById(int id, CancellationToken ct) =>
        Ok(await applicationsService.GetApplicationByIdAsync(id, ct));

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Roles.Employer)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> ChangeStatus(
        int id, [FromBody] ChangeApplicationStatusRequest request, CancellationToken ct) =>
        Ok(await applicationsService.ChangeStatusAsync(id, request, ct));

    [HttpPatch("{id:int}/withdraw")]
    [Authorize(Roles = Roles.Candidate)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> Withdraw(int id, CancellationToken ct) =>
        Ok(await applicationsService.WithdrawAsync(id, ct));

    [HttpGet("{id:int}/cv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCv(int id, CancellationToken ct)
    {
        var result = await applicationsService.GetCvFileAsync(id, ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    [HttpGet("{id:int}/candidate-profile")]
    [Authorize(Roles = Roles.Employer)]
    [ProducesResponseType<ApplicantProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicantProfileDto>> GetApplicantProfile(int id, CancellationToken ct) =>
        Ok(await applicationsService.GetApplicantProfileByApplicationIdAsync(id, ct));
}
