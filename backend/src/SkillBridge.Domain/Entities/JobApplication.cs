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

    public string? CvFileName { get; private set; }
    public string? CvOriginalFileName { get; private set; }
    public string? CvContentType { get; private set; }
    public long? CvFileSizeBytes { get; private set; }
    public DateTime? CvUploadedAt { get; private set; }

    /// <summary>Concurrency token, mapped to PostgreSQL's xmin system column.</summary>
    public uint Version { get; private set; }

    public Job Job { get; private set; } = null!;
    //public AppUser Candidate { get; private set; } = null!;

    public JobApplication(
        int jobId,
        string candidateId,
        string? coverNote,
        DateTime appliedAt,
        string? cvFileName = null,
        string? cvOriginalFileName = null,
        string? cvContentType = null,
        long? cvFileSizeBytes = null,
        DateTime? cvUploadedAt = null)
    {
        JobId = jobId;
        CandidateId = candidateId;
        CoverNote = string.IsNullOrWhiteSpace(coverNote) ? null : coverNote.Trim();
        Status = ApplicationStatus.Received;
        AppliedAt = appliedAt;
        CvFileName = cvFileName;
        CvOriginalFileName = cvOriginalFileName;
        CvContentType = cvContentType;
        CvFileSizeBytes = cvFileSizeBytes;
        CvUploadedAt = cvUploadedAt;
    }

    public void AttachCv(
        string fileName,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        DateTime uploadedAt)
    {
        CvFileName = fileName;
        CvOriginalFileName = originalFileName;
        CvContentType = contentType;
        CvFileSizeBytes = fileSizeBytes;
        CvUploadedAt = uploadedAt;
    }

    public bool BelongsToCandidate(string userId) => CandidateId == userId;

    public void ChangeStatus(ApplicationStatus newStatus, DateTime changedAt)
    {
        if (newStatus is not (ApplicationStatus.Shortlisted or ApplicationStatus.Rejected))
        {
            throw new BusinessRuleException("applications.invalid_status", "Invalid status", "Status must be Shortlisted or Rejected.");
        }

        if (Status != ApplicationStatus.Received)
        {
            throw new BusinessRuleException("applications.invalid_transition", "Invalid transition",
                $"Cannot change status from {Status} to {newStatus}.");
        }

        Status = newStatus;
        StatusChangedAt = changedAt;
    }

    public void Withdraw(DateTime withdrawnAt)
    {
        if (Status != ApplicationStatus.Received)
        {
            throw new BusinessRuleException("applications.cannot_withdraw", "Cannot withdraw",
                "Only applications still in Received can be withdrawn.");
        }

        Status = ApplicationStatus.Withdrawn;
        StatusChangedAt = withdrawnAt;
    }
}
