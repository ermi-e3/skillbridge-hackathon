using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Domain.Entities;

/// <summary>
/// A candidate's application to a job. Mapped to the "Applications" table.
/// Named JobApplication (not Application) so it never collides with the SkillBridge.Application namespace.
/// </summary>
public class JobApplication
{
    public const int CoverNoteMaxLength = 1000;

    private JobApplication() { } // EF

    public int Id { get; private set; }
    public int JobId { get; private set; }
    public string CandidateId { get; private set; } = null!;
    public ApplicationStatus Status { get; private set; }
    public string? CoverNote { get; private set; }
    public DateTime AppliedAt { get; private set; }
    public DateTime? StatusChangedAt { get; private set; }

    /// <summary>Concurrency token, mapped to PostgreSQL's xmin system column.</summary>
    public uint Version { get; private set; }

    public Job Job { get; private set; } = null!;
    //public AppUser Candidate { get; private set; } = null!;

    public bool BelongsToCandidate(string userId) => CandidateId == userId;

    
}
