namespace SkillBridge.Application.Common.Interfaces;

/// <summary>
/// What Application services see of the database. Add one DbSet&lt;T&gt; per entity here
/// and in AppDbContext. No repositories: one SaveChangesAsync call = one transaction.
/// </summary>
public interface IAppDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
