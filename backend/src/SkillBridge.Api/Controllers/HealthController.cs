using Microsoft.AspNetCore.Mvc;
using SkillBridge.Infrastructure.Persistence;

namespace SkillBridge.Api.Controllers;

/// <summary>Smoke test for the whole chain: Angular → API → PostgreSQL in Docker.</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Ok(new { api = "up", database = "up" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { api = "up", database = "down" });
}
