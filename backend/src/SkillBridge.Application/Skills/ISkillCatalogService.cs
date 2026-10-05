namespace SkillBridge.Application.Skills;

public interface ISkillCatalogService
{
    Task<IReadOnlyList<SkillResponse>> GetSkillsAsync(CancellationToken cancellationToken = default);
}
