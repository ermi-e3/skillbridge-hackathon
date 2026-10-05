using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Applications;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;

namespace SkillBridge.Api.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize]
public sealed class JobsController(
    JobsService jobsService,
    ApplicationsService applicationsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedJobsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedJobsResponse>> GetJobs(
        [FromQuery] string? search,
        [FromQuery] string? sort = "newest",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var skillIdList = ParseSkillIds(Request.Query["skillIds"]);
        var response = await jobsService.GetJobsAsync(skillIdList, search, sort, page, pageSize, ct);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    [ActionName(nameof(GetById))]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> GetById(int id, CancellationToken ct) =>
        Ok(await jobsService.GetJobByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Employer)]
    [ProducesResponseType<JobDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<JobDto>> Create(CreateJobRequest request, CancellationToken ct)
    {
        var job = await jobsService.CreateJobAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    [HttpPost("{jobId:int}/applications")]
    [Authorize(Roles = Roles.Candidate)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> Apply(
        int jobId, [FromBody] ApplyRequest request, CancellationToken ct)
    {
        var application = await applicationsService.ApplyAsync(jobId, request, ct);
        return Created($"/api/applications/{application.Id}", application);
    }

    [HttpGet("{jobId:int}/applications")]
    [Authorize(Roles = Roles.Employer)]
    [ProducesResponseType<JobApplicantsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobApplicantsDto>> GetApplicants(
        int jobId,
        [FromQuery] string? status,
        [FromQuery] bool fullMatchOnly = false,
        CancellationToken ct = default) =>
        Ok(await applicationsService.GetJobApplicationsAsync(jobId, status, fullMatchOnly, ct));

    private static List<int>? ParseSkillIds(Microsoft.Extensions.Primitives.StringValues values)
    {
        if (values.Count == 0)
            return null;

        var result = new List<int>();
        foreach (var val in values)
        {
            if (string.IsNullOrWhiteSpace(val))
                continue;

            var parts = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var id))
                {
                    result.Add(id);
                }
            }
        }

        return result.Count > 0 ? result : null;
    }
}
