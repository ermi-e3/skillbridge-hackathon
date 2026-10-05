using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Skills;

namespace SkillBridge.Api.Controllers;

[ApiController]
[Route("api/skills")]
[AllowAnonymous]
public sealed class SkillsController(ISkillCatalogService skillCatalogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SkillResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SkillResponse>>> Get(CancellationToken ct) =>
        Ok(await skillCatalogService.GetSkillsAsync(ct));
}
