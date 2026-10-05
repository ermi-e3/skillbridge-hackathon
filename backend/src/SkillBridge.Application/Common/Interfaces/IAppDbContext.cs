using Microsoft.EntityFrameworkCore;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Application.Common.Interfaces;

/// <summary>
/// What Application services see of the database. Add one DbSet&lt;T&gt; per entity here
/// and in AppDbContext. No repositories: one SaveChangesAsync call = one transaction.
/// </summary>
public interface IAppDbContext
{
    DbSet<CandidateProfile> CandidateProfiles { get; }
    DbSet<Skill> Skills { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
