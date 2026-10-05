using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Application.Jobs;
using SkillBridge.Application.Skills;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Applications;

public sealed class ApplicationsService(
    IAppDbContext dbContext,
    IIdentityService identityService,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<ApplicationDto> ApplyAsync(
        int jobId,
        ApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCandidate();

        var job = await dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            ?? throw new NotFoundException("jobs.not_found", "Job not found", "This job does not exist.");

        if (!job.IsOpen)
        {
            throw new BusinessRuleException("jobs.closed", "Job closed", "This job is no longer accepting applications.");
        }

        var candidateSkills = await dbContext.CandidateSkills
            .AsNoTracking()
            .Where(cs => cs.CandidateId == currentUser.UserId)
            .Include(cs => cs.Skill)
            .ToListAsync(cancellationToken);

        if (candidateSkills.Count == 0)
        {
            throw new BusinessRuleException(
                "profile.no_skills", "No skills in profile", "Add at least one skill to your profile before applying.");
        }

        var alreadyApplied = await dbContext.Applications
            .AsNoTracking()
            .AnyAsync(a => a.JobId == jobId && a.CandidateId == currentUser.UserId, cancellationToken);

        if (alreadyApplied)
        {
            throw new BusinessRuleException(
                "applications.duplicate", "Already applied", "You have already applied to this job.");
        }

        if (!string.IsNullOrEmpty(request.CoverNote) && request.CoverNote.Length > JobApplication.CoverNoteMaxLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["coverNote"] = [$"Cover note cannot exceed {JobApplication.CoverNoteMaxLength} characters."]
            });
        }

        var application = new JobApplication(jobId, currentUser.UserId, request.CoverNote, clock.UtcNow);
        dbContext.Applications.Add(application);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new BusinessRuleException(
                "applications.duplicate", "Already applied", "You have already applied to this job.");
        }

        var employer = await identityService.FindByIdAsync(job.EmployerId, cancellationToken);
        var match = ComputeMatch(job.RequiredSkills, candidateSkills.Select(cs => cs.SkillId).ToHashSet());

        return new ApplicationDto(
            application.Id,
            application.JobId,
            job.Title,
            employer?.CompanyName,
            application.Status.ToString(),
            application.AppliedAt,
            application.StatusChangedAt,
            application.CoverNote,
            match.Percent,
            match);
    }

    public async Task<IReadOnlyList<ApplicationDto>> GetMyApplicationsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCandidate();

        ApplicationStatus? filterStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<ApplicationStatus>(status, true, out var parsedStatus))
            {
                filterStatus = parsedStatus;
            }
            else
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["status"] = ["Unknown application status."]
                });
            }
        }

        var query = dbContext.Applications
            .AsNoTracking()
            .Where(a => a.CandidateId == currentUser.UserId);

        if (filterStatus.HasValue)
        {
            query = query.Where(a => a.Status == filterStatus.Value);
        }

        var applications = await query
            .Include(a => a.Job)
            .ThenInclude(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(cancellationToken);

        if (applications.Count == 0)
        {
            return Array.Empty<ApplicationDto>();
        }

        var employerIds = applications.Select(a => a.Job.EmployerId).Distinct().ToList();
        var employers = await identityService.FindByIdsAsync(employerIds, cancellationToken);

        var candidateSkills = await dbContext.CandidateSkills
            .AsNoTracking()
            .Where(cs => cs.CandidateId == currentUser.UserId)
            .Select(cs => cs.SkillId)
            .ToListAsync(cancellationToken);
        var candidateSkillSet = candidateSkills.ToHashSet();

        return applications.Select(a =>
        {
            employers.TryGetValue(a.Job.EmployerId, out var employer);
            var match = ComputeMatch(a.Job.RequiredSkills, candidateSkillSet);

            return new ApplicationDto(
                a.Id,
                a.JobId,
                a.Job.Title,
                employer?.CompanyName,
                a.Status.ToString(),
                a.AppliedAt,
                a.StatusChangedAt,
                a.CoverNote,
                match.Percent,
                match);
        }).ToList();
    }

    public async Task<ApplicationDto> GetApplicationByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException("auth.unauthenticated", "Authentication required", "Please sign in to continue.");
        }

        var application = await dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Job)
            .ThenInclude(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("applications.not_found", "Application not found", "This application does not exist.");

        var isApplicant = application.CandidateId == currentUser.UserId;
        var isJobOwner = application.Job.EmployerId == currentUser.UserId;

        if (!isApplicant && !isJobOwner)
        {
            throw new ForbiddenException("applications.not_owner", "Forbidden", "You cannot access this application.");
        }

        var employer = await identityService.FindByIdAsync(application.Job.EmployerId, cancellationToken);

        var candidateSkills = await dbContext.CandidateSkills
            .AsNoTracking()
            .Where(cs => cs.CandidateId == application.CandidateId)
            .Select(cs => cs.SkillId)
            .ToListAsync(cancellationToken);
        var match = ComputeMatch(application.Job.RequiredSkills, candidateSkills.ToHashSet());

        return new ApplicationDto(
            application.Id,
            application.JobId,
            application.Job.Title,
            employer?.CompanyName,
            application.Status.ToString(),
            application.AppliedAt,
            application.StatusChangedAt,
            application.CoverNote,
            match.Percent,
            match);
    }

    public async Task<JobApplicantsDto> GetJobApplicationsAsync(
        int jobId,
        string? status = null,
        bool fullMatchOnly = false,
        CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        var job = await dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            ?? throw new NotFoundException("jobs.not_found", "Job not found", "This job does not exist.");

        if (job.EmployerId != currentUser.UserId)
        {
            throw new ForbiddenException("jobs.not_owner", "Not job owner", "You can only view applicants for your own jobs.");
        }

        ApplicationStatus? filterStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<ApplicationStatus>(status, true, out var parsedStatus))
            {
                filterStatus = parsedStatus;
            }
            else
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["status"] = ["Unknown application status."]
                });
            }
        }

        var query = dbContext.Applications
            .AsNoTracking()
            .Where(a => a.JobId == jobId);

        if (filterStatus.HasValue)
        {
            query = query.Where(a => a.Status == filterStatus.Value);
        }

        var applications = await query.ToListAsync(cancellationToken);

        var candidateIds = applications.Select(a => a.CandidateId).Distinct().ToList();
        var candidates = await identityService.FindByIdsAsync(candidateIds, cancellationToken);

        var candidateProfiles = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Where(p => candidateIds.Contains(p.UserId))
            .Include(p => p.Skills)
            .ThenInclude(cs => cs.Skill)
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var jobRequiredSkills = job.RequiredSkills
            .Where(rs => rs.Skill != null)
            .OrderBy(rs => rs.Skill.Name)
            .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
            .ToList();

        var applicantItems = new List<JobApplicantItemDto>();

        foreach (var app in applications)
        {
            candidates.TryGetValue(app.CandidateId, out var candidateUser);
            candidateProfiles.TryGetValue(app.CandidateId, out var profile);

            var candidateSkills = profile?.Skills
                .Where(cs => cs.Skill != null)
                .OrderBy(cs => cs.Skill.Name)
                .Select(cs => new SkillDto(cs.SkillId, cs.Skill.Name))
                .ToList() ?? new List<SkillDto>();

            var match = ComputeMatch(job.RequiredSkills, candidateSkills.Select(s => s.Id).ToHashSet());

            if (fullMatchOnly && match.Percent < 100)
            {
                continue;
            }

            var summary = new CandidateSummaryDto(
                app.CandidateId,
                candidateUser?.FullName ?? "Unknown",
                candidateUser?.Email ?? string.Empty,
                profile?.Headline,
                profile?.GitHubUrl,
                candidateSkills);

            applicantItems.Add(new JobApplicantItemDto(
                app.Id,
                summary,
                app.Status.ToString(),
                app.AppliedAt,
                app.StatusChangedAt,
                app.CoverNote,
                match));
        }

        // Sorted by match % desc, then earliest applied
        applicantItems = applicantItems
            .OrderByDescending(a => a.Match.Percent)
            .ThenBy(a => a.AppliedAt)
            .ToList();

        var jobSummary = new JobSummaryDto(job.Id, job.Title, job.IsOpen, jobRequiredSkills);
        return new JobApplicantsDto(jobSummary, applicantItems);
    }

    public async Task<ApplicationDto> ChangeStatusAsync(
        int id,
        ChangeApplicationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["status"] = ["Status is required."]
            });
        }

        if (!string.Equals(request.Status, "Shortlisted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "applications.invalid_status", "Invalid status", "Status must be Shortlisted or Rejected.");
        }

        var targetStatus = string.Equals(request.Status, "Shortlisted", StringComparison.OrdinalIgnoreCase)
            ? ApplicationStatus.Shortlisted
            : ApplicationStatus.Rejected;

        var application = await dbContext.Applications
            .Include(a => a.Job)
            .ThenInclude(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("applications.not_found", "Application not found", "This application does not exist.");

        if (application.Job.EmployerId != currentUser.UserId)
        {
            throw new ForbiddenException("applications.not_owner", "Forbidden", "You cannot access this application.");
        }

        application.ChangeStatus(targetStatus, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var employer = await identityService.FindByIdAsync(application.Job.EmployerId, cancellationToken);
        var candidateSkills = await dbContext.CandidateSkills
            .AsNoTracking()
            .Where(cs => cs.CandidateId == application.CandidateId)
            .Select(cs => cs.SkillId)
            .ToListAsync(cancellationToken);
        var match = ComputeMatch(application.Job.RequiredSkills, candidateSkills.ToHashSet());

        return new ApplicationDto(
            application.Id,
            application.JobId,
            application.Job.Title,
            employer?.CompanyName,
            application.Status.ToString(),
            application.AppliedAt,
            application.StatusChangedAt,
            application.CoverNote,
            match.Percent,
            match);
    }

    public async Task<ApplicationDto> WithdrawAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        EnsureCandidate();

        var application = await dbContext.Applications
            .Include(a => a.Job)
            .ThenInclude(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("applications.not_found", "Application not found", "This application does not exist.");

        if (application.CandidateId != currentUser.UserId)
        {
            throw new ForbiddenException("applications.not_owner", "Forbidden", "You cannot access this application.");
        }

        application.Withdraw(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        var employer = await identityService.FindByIdAsync(application.Job.EmployerId, cancellationToken);
        var candidateSkills = await dbContext.CandidateSkills
            .AsNoTracking()
            .Where(cs => cs.CandidateId == application.CandidateId)
            .Select(cs => cs.SkillId)
            .ToListAsync(cancellationToken);
        var match = ComputeMatch(application.Job.RequiredSkills, candidateSkills.ToHashSet());

        return new ApplicationDto(
            application.Id,
            application.JobId,
            application.Job.Title,
            employer?.CompanyName,
            application.Status.ToString(),
            application.AppliedAt,
            application.StatusChangedAt,
            application.CoverNote,
            match.Percent,
            match);
    }

    private void EnsureCandidate()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException("auth.unauthenticated", "Authentication required", "Please sign in to continue.");
        }

        if (!currentUser.IsInRole(Roles.Candidate))
        {
            throw new ForbiddenException("auth.forbidden", "Forbidden", "Your account type cannot do this.");
        }
    }

    private void EnsureEmployer()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException("auth.unauthenticated", "Authentication required", "Please sign in to continue.");
        }

        if (!currentUser.IsInRole(Roles.Employer))
        {
            throw new ForbiddenException("auth.forbidden", "Forbidden", "Your account type cannot do this.");
        }
    }

    private static JobMatchDto ComputeMatch(
        IEnumerable<JobSkill> jobSkills,
        HashSet<int> candidateSkillIds)
    {
        var requiredSkills = jobSkills
            .Where(rs => rs.Skill != null)
            .OrderBy(rs => rs.Skill.Name)
            .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
            .ToList();

        var matched = requiredSkills.Where(s => candidateSkillIds.Contains(s.Id)).ToList();
        var missing = requiredSkills.Where(s => !candidateSkillIds.Contains(s.Id)).ToList();
        var percent = requiredSkills.Count == 0
            ? 0
            : (int)Math.Round((double)matched.Count / requiredSkills.Count * 100.0, MidpointRounding.AwayFromZero);

        return new JobMatchDto(percent, matched, missing);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("IX_Applications_JobId_CandidateId", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("23505", StringComparison.OrdinalIgnoreCase);
    }
}
