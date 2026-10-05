using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Application.Skills;

public sealed class SkillCatalogService(IAppDbContext dbContext) : ISkillCatalogService
{
    public async Task<IReadOnlyList<SkillResponse>> GetSkillsAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.Skills
            .AsNoTracking()
            .OrderBy(skill => skill.Name)
            .ThenBy(skill => skill.Id)
            .Select(skill => new SkillResponse(skill.Id, skill.Name))
            .ToListAsync(cancellationToken);
}
