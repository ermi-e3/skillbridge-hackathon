using Microsoft.EntityFrameworkCore;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Application.Common.Interfaces;

/// <summary>
/// What Application services see of the database. Add one DbSet<T> per entity here
/// and in AppDbContext. No repositories: one SaveChangesAsync call = one transaction.
/// </summary>
public interface IAppDbContext
{
    DbSet<Skill> Skills { get; }
    DbSet<CandidateProfile> CandidateProfiles { get; }
    DbSet<CandidateSkill> CandidateSkills { get; }
    DbSet<Job> Jobs { get; }
    DbSet<JobSkill> JobSkills { get; }
    DbSet<JobApplication> Applications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
