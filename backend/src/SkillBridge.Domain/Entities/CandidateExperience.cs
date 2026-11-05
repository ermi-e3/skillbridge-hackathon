using SkillBridge.Domain.Common;

namespace SkillBridge.Domain.Entities;

/// <summary>
/// A candidate's past or current work history entry used for relevance and experience matching.
/// </summary>
public class CandidateExperience
{
    private CandidateExperience() { } // EF

    public CandidateExperience(
        string candidateId,
        string title,
        string company,
        string? location,
        DateTime startDate,
        DateTime? endDate,
        bool isCurrent,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("validation.failed", "Title is required", "Job title is required.");

        if (string.IsNullOrWhiteSpace(company))
            throw new BusinessRuleException("validation.failed", "Company is required", "Company name is required.");

        CandidateId = candidateId;
        Title = title.Trim();
        Company = company.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        StartDate = startDate;
        EndDate = isCurrent ? null : endDate;
        IsCurrent = isCurrent;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public int Id { get; private set; }
    public string CandidateId { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Company { get; private set; } = null!;
    public string? Location { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsCurrent { get; private set; }
    public string? Description { get; private set; }

    public CandidateProfile Candidate { get; private set; } = null!;

    public void Update(
        string title,
        string company,
        string? location,
        DateTime startDate,
        DateTime? endDate,
        bool isCurrent,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessRuleException("validation.failed", "Title is required", "Job title is required.");

        if (string.IsNullOrWhiteSpace(company))
            throw new BusinessRuleException("validation.failed", "Company is required", "Company name is required.");

        Title = title.Trim();
        Company = company.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        StartDate = startDate;
        EndDate = isCurrent ? null : endDate;
        IsCurrent = isCurrent;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }
}

