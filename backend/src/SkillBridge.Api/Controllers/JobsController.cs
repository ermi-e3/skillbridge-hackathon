using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Applications;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;

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
        [FromQuery] JobType? jobType = null,
        [FromQuery] WorkArrangement? workArrangement = null,
        [FromQuery] ExperienceLevel? experienceLevel = null,
        [FromQuery] string? location = null,
        CancellationToken ct = default)
    {
        var skillIdList = ParseSkillIds(Request.Query["skillIds"]);
        var response = await jobsService.GetJobsAsync(
            skillIdList, search, sort, page, pageSize, jobType, workArrangement, experienceLevel, location, ct);
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

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Employer)]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> Update(int id, UpdateJobRequest request, CancellationToken ct) =>
        Ok(await jobsService.UpdateJobAsync(id, request, ct));

    [HttpPatch("{id:int}/close")]
    [Authorize(Roles = Roles.Employer)]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> CloseJob(int id, CancellationToken ct) =>
        Ok(await jobsService.CloseJobAsync(id, ct));

    [HttpPost("{jobId:int}/applications")]
    [Authorize(Roles = Roles.Candidate)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> Apply(
        int jobId, CancellationToken ct)
    {
        string? coverNote = null;
        bool useDefaultCv = false;
        Stream? cvStream = null;
        string? cvOriginalFileName = null;
        string? cvContentType = null;
        long? cvFileSizeBytes = null;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            coverNote = form["coverNote"].FirstOrDefault();
            if (bool.TryParse(form["useDefaultCv"].FirstOrDefault(), out var parsedUseDefault))
            {
                useDefaultCv = parsedUseDefault;
            }
            var file = form.Files.GetFile("cv") ?? (form.Files.Count > 0 ? form.Files[0] : null);
            if (file != null && file.Length > 0)
            {
                cvStream = file.OpenReadStream();
                cvOriginalFileName = file.FileName;
                cvContentType = file.ContentType;
                cvFileSizeBytes = file.Length;
            }
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var jsonRequest = System.Text.Json.JsonSerializer.Deserialize<ApplyRequest>(
                    body,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                coverNote = jsonRequest?.CoverNote;
                useDefaultCv = jsonRequest?.UseDefaultCv ?? false;
            }
        }

        var application = await applicationsService.ApplyAsync(
            jobId,
            new ApplyRequest(coverNote, useDefaultCv),
            cvStream,
            cvOriginalFileName,
            cvContentType,
            cvFileSizeBytes,
            ct);

        return Created($"/api/applications/{application.Id}", application);
    }

    [HttpGet("{jobId:int}/applicants/{candidateId}/profile")]
    [Authorize(Roles = Roles.Employer)]
    [ProducesResponseType<ApplicantProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicantProfileDto>> GetApplicantProfile(
        int jobId, string candidateId, CancellationToken ct) =>
        Ok(await applicationsService.GetApplicantProfileAsync(jobId, candidateId, ct));

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
