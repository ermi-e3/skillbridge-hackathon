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

    [HttpPost("cv")]
    [ProducesResponseType<CandidateProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidateProfileResponse>> UploadDefaultCv(CancellationToken ct)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Multipart form data with 'cv' file is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var form = await Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("cv") ?? (form.Files.Count > 0 ? form.Files[0] : null);
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid file",
                Detail = "Please provide a valid CV file.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        using var stream = file.OpenReadStream();
        var profile = await profileService.UploadDefaultCvAsync(
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            ct);

        return Ok(profile);
    }

    [HttpGet("cv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDefaultCv(CancellationToken ct)
    {
        var result = await profileService.GetDefaultCvFileAsync(ct);
        return File(result.Stream, result.ContentType, result.FileName);
    }

    [HttpDelete("cv")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDefaultCv(CancellationToken ct)
    {
        await profileService.RemoveDefaultCvAsync(ct);
        return NoContent();
    }
}
