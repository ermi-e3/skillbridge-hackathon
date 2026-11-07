using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.CandidateProfiles;
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
    IClock clock,
    IJobMatchingService jobMatchingService,
    IFileStorageService fileStorageService,
    IRealtimeNotifier? notifier = null)
{
    public async Task<ApplicationDto> ApplyAsync(
        int jobId,
        ApplyRequest request,
        Stream? cvFileStream = null,
        string? cvOriginalFileName = null,
        string? cvContentType = null,
        long? cvFileSizeBytes = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCandidate();

        var job = await dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            ?? throw new NotFoundException("jobs.not_found", "Job not found", "This job does not exist.");

        if (!job.IsOpen || (job.Deadline.HasValue && clock.UtcNow >= job.Deadline.Value))
        {
            if (job.IsOpen && job.Deadline.HasValue && clock.UtcNow >= job.Deadline.Value)
            {
                var trackedJob = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
                if (trackedJob != null && trackedJob.IsOpen)
                {
                    trackedJob.Close();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    if (notifier != null)
                    {
                        await notifier.NotifyJobStatusChangedAsync(new JobStatusChangedRealtimeEvent(jobId, false, trackedJob.Deadline, null), cancellationToken);
                    }
                }
            }
            throw new BusinessRuleException("jobs.closed", "Job closed", "This job is no longer accepting applications.");
        }

        var candidateProfile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(p => p.Skills)
            .ThenInclude(cs => cs.Skill)
            .Include(p => p.ExperienceHistory)
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, cancellationToken);

        var candidateSkills = candidateProfile?.Skills
            ?? await dbContext.CandidateSkills
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

        string? storedCvFileName = null;
        if (cvFileStream != null && !string.IsNullOrWhiteSpace(cvOriginalFileName))
        {
            CvFileValidator.Validate(cvOriginalFileName, cvFileSizeBytes ?? cvFileStream.Length, cvFileStream, cvContentType);
            storedCvFileName = await fileStorageService.SaveFileAsync(
                cvFileStream,
                cvOriginalFileName,
                cvContentType ?? "application/octet-stream",
                cancellationToken);
        }
        else if (request.UseDefaultCv || (candidateProfile != null && candidateProfile.HasDefaultCv))
        {
            if (candidateProfile == null || !candidateProfile.HasDefaultCv || string.IsNullOrWhiteSpace(candidateProfile.DefaultCvFileName))
            {
                throw new BusinessRuleException(
                    "applications.no_default_cv", "No default CV found", "You do not have a default CV in your profile. Please upload a CV.");
            }

            using var defaultCvStream = await fileStorageService.GetFileAsync(candidateProfile.DefaultCvFileName, cancellationToken);
            if (defaultCvStream == null)
            {
                throw new BusinessRuleException(
                    "applications.default_cv_missing", "Default CV file missing", "The default CV file could not be found in storage.");
            }

            cvOriginalFileName = candidateProfile.DefaultCvOriginalFileName ?? "resume.pdf";
            cvContentType = candidateProfile.DefaultCvContentType ?? "application/pdf";
            cvFileSizeBytes = candidateProfile.DefaultCvFileSizeBytes;

            storedCvFileName = await fileStorageService.SaveFileAsync(
                defaultCvStream,
                cvOriginalFileName,
                cvContentType,
                cancellationToken);
        }
        else
        {
            throw new BusinessRuleException(
                "applications.cv_required", "CV required", "A valid CV must always be attached before an application can be submitted.");
        }

        var application = new JobApplication(
            jobId,
            currentUser.UserId,
            request.CoverNote,
            clock.UtcNow,
            cvFileName: storedCvFileName,
            cvOriginalFileName: cvOriginalFileName,
            cvContentType: cvContentType ?? "application/octet-stream",
            cvFileSizeBytes: cvFileSizeBytes ?? cvFileStream?.Length,
            cvUploadedAt: clock.UtcNow);

        dbContext.Applications.Add(application);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            if (!string.IsNullOrEmpty(storedCvFileName))
            {
                await fileStorageService.DeleteFileAsync(storedCvFileName, CancellationToken.None);
            }
            throw new BusinessRuleException(
                "applications.duplicate", "Already applied", "You have already applied to this job.");
        }

        var employer = await identityService.FindByIdAsync(job.EmployerId, cancellationToken);
        var match = jobMatchingService.CalculateMatch(job, candidateProfile);

        if (notifier != null)
        {
            var jobApps = await dbContext.Applications.AsNoTracking().Where(a => a.JobId == jobId).ToListAsync(cancellationToken);
            var counts = new ApplicantCountsDto(
                Total: jobApps.Count,
                Received: jobApps.Count(a => a.Status == ApplicationStatus.Received),
                Shortlisted: jobApps.Count(a => a.Status == ApplicationStatus.Shortlisted),
                Rejected: jobApps.Count(a => a.Status == ApplicationStatus.Rejected),
                Withdrawn: jobApps.Count(a => a.Status == ApplicationStatus.Withdrawn));

            var candidateUser = await identityService.FindByIdAsync(currentUser.UserId, cancellationToken);
            var profileSkills = candidateProfile?.Skills
                .Where(cs => cs.Skill != null)
                .OrderBy(cs => cs.Skill.Name)
                .Select(cs => new SkillDto(cs.SkillId, cs.Skill.Name))
                .ToList() ?? new List<SkillDto>();

            var summary = new CandidateSummaryDto(
                currentUser.UserId,
                candidateUser?.FullName ?? "Applicant",
                candidateUser?.Email ?? string.Empty,
                candidateProfile?.Headline,
                candidateProfile?.GitHubUrl,
                profileSkills);

            var applicantItem = new JobApplicantItemDto(
                application.Id,
                summary,
                application.Status.ToString(),
                application.AppliedAt,
                application.StatusChangedAt,
                application.CoverNote,
                match,
                HasCv: !string.IsNullOrEmpty(application.CvFileName),
                CvOriginalFileName: application.CvOriginalFileName,
                CvFileSizeBytes: application.CvFileSizeBytes,
                CvUploadedAt: application.CvUploadedAt,
                CvDownloadUrl: !string.IsNullOrEmpty(application.CvFileName) ? $"/api/applications/{application.Id}/cv" : null);

            await notifier.NotifyApplicationSubmittedAsync(new ApplicationSubmittedRealtimeEvent(
                application.Id,
                job.Id,
                job.Title,
                job.EmployerId,
                currentUser.UserId,
                candidateUser?.FullName ?? "Applicant",
                match.Percent,
                applicantItem,
                counts), cancellationToken);
        }

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
            match,
            HasCv: !string.IsNullOrEmpty(application.CvFileName),
            CvOriginalFileName: application.CvOriginalFileName,
            CvFileSizeBytes: application.CvFileSizeBytes,
            CvUploadedAt: application.CvUploadedAt,
            CvDownloadUrl: !string.IsNullOrEmpty(application.CvFileName) ? $"/api/applications/{application.Id}/cv" : null);
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

        var candidateProfile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(p => p.Skills)
            .ThenInclude(cs => cs.Skill)
            .Include(p => p.ExperienceHistory)
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId, cancellationToken);

        return applications.Select(a =>
        {
            employers.TryGetValue(a.Job.EmployerId, out var employer);
            var match = jobMatchingService.CalculateMatch(a.Job, candidateProfile);

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
                match,
                HasCv: !string.IsNullOrEmpty(a.CvFileName),
                CvOriginalFileName: a.CvOriginalFileName,
                CvFileSizeBytes: a.CvFileSizeBytes,
                CvUploadedAt: a.CvUploadedAt,
                CvDownloadUrl: !string.IsNullOrEmpty(a.CvFileName) ? $"/api/applications/{a.Id}/cv" : null);
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

        var candidateProfile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(p => p.Skills)
            .ThenInclude(cs => cs.Skill)
            .Include(p => p.ExperienceHistory)
            .FirstOrDefaultAsync(p => p.UserId == application.CandidateId, cancellationToken);

        var match = jobMatchingService.CalculateMatch(application.Job, candidateProfile);

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
            match,
            HasCv: !string.IsNullOrEmpty(application.CvFileName),
            CvOriginalFileName: application.CvOriginalFileName,
            CvFileSizeBytes: application.CvFileSizeBytes,
            CvUploadedAt: application.CvUploadedAt,
            CvDownloadUrl: !string.IsNullOrEmpty(application.CvFileName) ? $"/api/applications/{application.Id}/cv" : null);
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
            .Include(p => p.ExperienceHistory)
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var jobRequiredSkills = job.RequiredSkills
            .Where(rs => rs.Skill != null && rs.IsRequired)
            .OrderBy(rs => rs.Skill.Name)
            .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
            .ToList();

        if (jobRequiredSkills.Count == 0)
        {
            jobRequiredSkills = job.RequiredSkills
                .Where(rs => rs.Skill != null)
                .OrderBy(rs => rs.Skill.Name)
                .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                .ToList();
        }

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

            var match = jobMatchingService.CalculateMatch(job, profile);

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
                match,
                HasCv: !string.IsNullOrEmpty(app.CvFileName),
                CvOriginalFileName: app.CvOriginalFileName,
                CvFileSizeBytes: app.CvFileSizeBytes,
                CvUploadedAt: app.CvUploadedAt,
                CvDownloadUrl: !string.IsNullOrEmpty(app.CvFileName) ? $"/api/applications/{app.Id}/cv" : null));
        }

        // Sorted by match % desc, then earliest applied
        applicantItems = applicantItems
            .OrderByDescending(a => a.Match.Percent)
            .ThenBy(a => a.AppliedAt)
            .ToList();

        var jobPreferredSkills = job.RequiredSkills
            .Where(rs => rs.Skill != null && !rs.IsRequired)
            .OrderBy(rs => rs.Skill.Name)
            .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
            .ToList();

        var canEditJob = job.IsEditingAllowed(clock.UtcNow);
        var jobSummary = new JobSummaryDto(job.Id, job.Title, job.IsOpen, jobRequiredSkills, jobPreferredSkills, job.CreatedAt, CanManageApplications: true, CanEditJob: canEditJob);
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

        if (notifier != null)
        {
            var jobApps = await dbContext.Applications.AsNoTracking().Where(a => a.JobId == application.JobId).ToListAsync(cancellationToken);
            var counts = new ApplicantCountsDto(
                Total: jobApps.Count,
                Received: jobApps.Count(a => a.Status == ApplicationStatus.Received),
                Shortlisted: jobApps.Count(a => a.Status == ApplicationStatus.Shortlisted),
                Rejected: jobApps.Count(a => a.Status == ApplicationStatus.Rejected),
                Withdrawn: jobApps.Count(a => a.Status == ApplicationStatus.Withdrawn));

            await notifier.NotifyApplicationStatusChangedAsync(new ApplicationStatusChangedRealtimeEvent(
                application.Id,
                application.JobId,
                application.Job.Title,
                application.Job.EmployerId,
                application.CandidateId,
                application.Status.ToString(),
                counts), cancellationToken);
        }

        var employer = await identityService.FindByIdAsync(application.Job.EmployerId, cancellationToken);
        var candidateProfile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(p => p.Skills)
            .ThenInclude(cs => cs.Skill)
            .Include(p => p.ExperienceHistory)
            .FirstOrDefaultAsync(p => p.UserId == application.CandidateId, cancellationToken);
        var match = jobMatchingService.CalculateMatch(application.Job, candidateProfile);

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
            match,
            HasCv: !string.IsNullOrEmpty(application.CvFileName),
            CvOriginalFileName: application.CvOriginalFileName,
            CvFileSizeBytes: application.CvFileSizeBytes,
            CvUploadedAt: application.CvUploadedAt,
            CvDownloadUrl: !string.IsNullOrEmpty(application.CvFileName) ? $"/api/applications/{application.Id}/cv" : null);
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

        if (notifier != null)
        {
            var jobApps = await dbContext.Applications.AsNoTracking().Where(a => a.JobId == application.JobId).ToListAsync(cancellationToken);
            var counts = new ApplicantCountsDto(
                Total: jobApps.Count,
                Received: jobApps.Count(a => a.Status == ApplicationStatus.Received),
                Shortlisted: jobApps.Count(a => a.Status == ApplicationStatus.Shortlisted),
                Rejected: jobApps.Count(a => a.Status == ApplicationStatus.Rejected),
                Withdrawn: jobApps.Count(a => a.Status == ApplicationStatus.Withdrawn));

            await notifier.NotifyApplicationWithdrawnAsync(new ApplicationWithdrawnRealtimeEvent(
                application.Id,
                application.JobId,
                application.Job.Title,
                application.Job.EmployerId,
                currentUser.UserId,
                counts), cancellationToken);
        }

        var employer = await identityService.FindByIdAsync(application.Job.EmployerId, cancellationToken);
        var candidateProfile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(p => p.Skills)
            .ThenInclude(cs => cs.Skill)
            .Include(p => p.ExperienceHistory)
            .FirstOrDefaultAsync(p => p.UserId == application.CandidateId, cancellationToken);
        var match = jobMatchingService.CalculateMatch(application.Job, candidateProfile);

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
            match,
            HasCv: !string.IsNullOrEmpty(application.CvFileName),
            CvOriginalFileName: application.CvOriginalFileName,
            CvFileSizeBytes: application.CvFileSizeBytes,
            CvUploadedAt: application.CvUploadedAt,
            CvDownloadUrl: !string.IsNullOrEmpty(application.CvFileName) ? $"/api/applications/{application.Id}/cv" : null);
    }

    public async Task<ApplicantProfileDto> GetApplicantProfileAsync(
        int jobId,
        string candidateId,
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
            throw new ForbiddenException("jobs.not_owner", "Not job owner", "You can only view applicant profiles for your own jobs.");
        }

        var application = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.JobId == jobId && a.CandidateId == candidateId, cancellationToken)
            ?? throw new NotFoundException("applications.not_found", "Applicant not found", "This candidate has not applied to this job.");

        var candidateUser = await identityService.FindByIdAsync(candidateId, cancellationToken);

        var candidateProfile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(p => p.Skills)
            .ThenInclude(cs => cs.Skill)
            .Include(p => p.ExperienceHistory)
            .FirstOrDefaultAsync(p => p.UserId == candidateId, cancellationToken);

        var match = jobMatchingService.CalculateMatch(job, candidateProfile);

        var skills = candidateProfile?.Skills
            .Where(s => s.Skill != null)
            .OrderByDescending(s => s.IsPreferred)
            .ThenByDescending(s => (int)s.Proficiency)
            .ThenBy(s => s.Skill!.Name)
            .Select(s => new CandidateProfileSkillResponse(
                s.SkillId,
                s.Skill!.Name,
                s.Proficiency,
                s.YearsOfExperience,
                s.IsPreferred))
            .ToList() ?? new List<CandidateProfileSkillResponse>();

        var experiences = candidateProfile?.ExperienceHistory
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.StartDate)
            .Select(e => new CandidateExperienceDto(
                e.Id,
                e.Title,
                e.Company,
                e.Location,
                e.StartDate,
                e.EndDate,
                e.IsCurrent,
                e.Description))
            .ToList() ?? new List<CandidateExperienceDto>();

        return new ApplicantProfileDto(
            CandidateId: candidateId,
            FullName: candidateUser?.FullName ?? "Applicant",
            Email: candidateUser?.Email ?? string.Empty,
            Headline: candidateProfile?.Headline,
            Bio: candidateProfile?.Bio,
            GitHubUrl: candidateProfile?.GitHubUrl,
            Location: candidateProfile?.Location,
            ExperienceLevel: candidateProfile?.ExperienceLevel,
            YearsOfExperience: candidateProfile?.YearsOfExperience,
            PreferredWorkArrangement: candidateProfile?.PreferredWorkArrangement,
            PreferredJobType: candidateProfile?.PreferredJobType,
            Skills: skills,
            ExperienceHistory: experiences,
            ApplicationId: application.Id,
            ApplicationStatus: application.Status.ToString(),
            AppliedAt: application.AppliedAt,
            StatusChangedAt: application.StatusChangedAt,
            CoverNote: application.CoverNote,
            Match: match,
            HasCv: !string.IsNullOrEmpty(application.CvFileName),
            CvOriginalFileName: application.CvOriginalFileName,
            CvFileSizeBytes: application.CvFileSizeBytes,
            CvUploadedAt: application.CvUploadedAt,
            CvDownloadUrl: !string.IsNullOrEmpty(application.CvFileName) ? $"/api/applications/{application.Id}/cv" : null);
    }

    public async Task<ApplicantProfileDto> GetApplicantProfileByApplicationIdAsync(
        int applicationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        var application = await dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
            ?? throw new NotFoundException("applications.not_found", "Application not found", "This application does not exist.");

        return await GetApplicantProfileAsync(application.JobId, application.CandidateId, cancellationToken);
    }

    public async Task<CvDownloadResult> GetCvFileAsync(
        int applicationId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException("auth.unauthenticated", "Authentication required", "Please sign in to continue.");
        }

        var application = await dbContext.Applications
            .AsNoTracking()
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
            ?? throw new NotFoundException("applications.not_found", "Application not found", "This application does not exist.");

        var isApplicant = application.CandidateId == currentUser.UserId;
        var isJobOwner = application.Job.EmployerId == currentUser.UserId;

        if (!isApplicant && !isJobOwner)
        {
            throw new ForbiddenException("applications.not_owner", "Forbidden", "You cannot access this CV.");
        }

        if (string.IsNullOrEmpty(application.CvFileName))
        {
            throw new NotFoundException("applications.cv_not_found", "CV not found", "No CV was uploaded for this application.");
        }

        var stream = await fileStorageService.GetFileAsync(application.CvFileName, cancellationToken);
        if (stream == null)
        {
            throw new NotFoundException("applications.cv_not_found", "CV file not found", "The CV file could not be found in storage.");
        }

        var contentType = application.CvContentType ?? "application/octet-stream";
        var fileName = application.CvOriginalFileName ?? "cv";

        return new CvDownloadResult(stream, contentType, fileName);
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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("IX_Applications_JobId_CandidateId", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("23505", StringComparison.OrdinalIgnoreCase);
    }
}
