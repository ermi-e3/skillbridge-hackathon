namespace SkillBridge.Domain.Enums;

/// <summary>Stored as text in the database (see JobApplicationConfiguration).</summary>
public enum ApplicationStatus
{
    Received = 0,
    Shortlisted = 1,
    Rejected = 2,
    Withdrawn = 3
}
