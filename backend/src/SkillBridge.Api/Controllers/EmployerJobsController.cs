using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;

namespace SkillBridge.Api.Controllers;

[ApiController]
[Route("api/employer/jobs")]
[Authorize(Roles = Roles.Employer)]
public sealed class EmployerJobsController(JobsService jobsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EmployerJobDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<EmployerJobDto>>> GetJobs(CancellationToken ct) =>
        Ok(await jobsService.GetEmployerJobsAsync(ct));

    [HttpPatch("{id:int}/close")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> CloseJob(int id, CancellationToken ct) =>
        Ok(await jobsService.CloseJobAsync(id, ct));

    [HttpPut("{id:int}")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> Update(int id, UpdateJobRequest request, CancellationToken ct) =>
        Ok(await jobsService.UpdateJobAsync(id, request, ct));
}
