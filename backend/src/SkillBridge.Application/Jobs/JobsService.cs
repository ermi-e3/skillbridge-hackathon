using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Application.Skills;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.Jobs;

public sealed class JobsService(
    IAppDbContext dbContext,
    IIdentityService identityService,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<CreateJobRequest> createJobValidator,
    IJobMatchingService jobMatchingService,
    IRealtimeNotifier? notifier = null,
    IValidator<UpdateJobRequest>? updateJobValidator = null)
{
    public async Task<PagedJobsResponse> GetJobsAsync(
        IEnumerable<int>? skillIds = null,
        string? search = null,
        string? sort = "newest",
        int page = 1,
        int pageSize = 20,
        JobType? jobType = null,
        WorkArrangement? workArrangement = null,
        ExperienceLevel? experienceLevel = null,
        string? location = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["page"] = ["Page must be greater than or equal to 1."]
            });
        }

        if (pageSize is < 1 or > 50)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["pageSize"] = ["PageSize must be between 1 and 50."]
            });
        }

        // Auto-close expired jobs
        var expiredJobs = await dbContext.Jobs
            .Where(j => j.IsOpen && j.Deadline != null && j.Deadline <= clock.UtcNow)
            .ToListAsync(cancellationToken);
        if (expiredJobs.Count > 0)
        {
            foreach (var expired in expiredJobs)
            {
                expired.Close();
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            if (notifier != null)
            {
                foreach (var expired in expiredJobs)
                {
                    await notifier.NotifyJobStatusChangedAsync(new JobStatusChangedRealtimeEvent(expired.Id, false, expired.Deadline, null), cancellationToken);
                }
            }
        }

        var filterSkillIds = skillIds?.Distinct().ToList();

        var query = dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.IsOpen)
            .Include(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .AsQueryable();

        // Optimized SQL filtering for skills
        if (filterSkillIds != null && filterSkillIds.Count > 0)
        {
            query = query.Where(j => j.RequiredSkills.Any(rs => filterSkillIds.Contains(rs.SkillId)));
        }

        // Optimized SQL filtering for title / description search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.Trim().ToLower();
            query = query.Where(j => j.Title.ToLower().Contains(lowerSearch) || j.Description.ToLower().Contains(lowerSearch));
        }

        // Optimized SQL filtering for hiring factors
        if (jobType.HasValue)
        {
            query = query.Where(j => j.JobType == jobType.Value);
        }

        if (workArrangement.HasValue)
        {
            query = query.Where(j => j.WorkArrangement == workArrangement.Value);
        }

        if (experienceLevel.HasValue)
        {
            query = query.Where(j => j.ExperienceLevel == experienceLevel.Value);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var lowerLoc = location.Trim().ToLower();
            query = query.Where(j => j.Location != null && j.Location.ToLower().Contains(lowerLoc));
        }

        var allMatchingJobs = await query.ToListAsync(cancellationToken);

        var employerIds = allMatchingJobs.Select(j => j.EmployerId).Distinct().ToList();
        var employers = await identityService.FindByIdsAsync(employerIds, cancellationToken);

        var isCandidate = currentUser.IsAuthenticated && currentUser.IsInRole(Roles.Candidate);
        CandidateProfile? candidateProfile = null;
        Dictionary<int, (ApplicationStatus Status, int Id)>? appliedJobMap = null;

        if (isCandidate)
        {
            candidateProfile = await dbContext.CandidateProfiles
                .AsNoTracking()
                .Include(cp => cp.Skills)
                .ThenInclude(cs => cs.Skill)
                .Include(cp => cp.ExperienceHistory)
                .FirstOrDefaultAsync(cp => cp.UserId == currentUser.UserId, cancellationToken);

            var applied = await dbContext.Applications
                .AsNoTracking()
                .Where(a => a.CandidateId == currentUser.UserId)
                .Select(a => new { a.JobId, a.Status, a.Id })
                .ToListAsync(cancellationToken);
            appliedJobMap = applied.ToDictionary(a => a.JobId, a => (a.Status, a.Id));
        }

        var jobDtos = allMatchingJobs.Select(job =>
        {
            employers.TryGetValue(job.EmployerId, out var employer);
            var companyName = employer?.CompanyName ?? "Unknown Company";

            var requiredSkills = job.RequiredSkills
                .Where(rs => rs.Skill != null && rs.IsRequired)
                .OrderBy(rs => rs.Skill.Name)
                .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                .ToList();

            // Fallback if none explicitly marked required
            if (requiredSkills.Count == 0)
            {
                requiredSkills = job.RequiredSkills
                    .Where(rs => rs.Skill != null)
                    .OrderBy(rs => rs.Skill.Name)
                    .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                    .ToList();
            }

            var preferredSkills = job.RequiredSkills
                .Where(rs => rs.Skill != null && !rs.IsRequired)
                .OrderBy(rs => rs.Skill.Name)
                .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                .ToList();

            JobMatchDto? match = null;
            int? myMatchPercent = null;
            bool? hasApplied = null;
            string? applicationStatus = null;
            int? applicationId = null;

            if (isCandidate)
            {
                if (candidateProfile != null)
                {
                    match = jobMatchingService.CalculateMatch(job, candidateProfile);
                    myMatchPercent = match.Percent;
                }

                if (appliedJobMap != null && appliedJobMap.TryGetValue(job.Id, out var appInfo))
                {
                    hasApplied = true;
                    applicationStatus = appInfo.Status.ToString();
                    applicationId = appInfo.Id;
                }
                else
                {
                    hasApplied = false;
                }
            }

            var isOwner = currentUser.IsAuthenticated && job.IsOwnedBy(currentUser.UserId);

            return new JobDto(
                job.Id,
                job.Title,
                companyName,
                job.Location,
                job.IsOpen,
                job.CreatedAt,
                requiredSkills,
                match,
                myMatchPercent,
                hasApplied,
                job.Description,
                isOwner,
                job.JobType,
                job.WorkArrangement,
                job.ExperienceLevel,
                job.MinExperienceYears,
                preferredSkills,
                applicationStatus,
                applicationId,
                job.Deadline);
        }).ToList();

        // Sort
        if (isCandidate && string.Equals(sort, "bestMatch", StringComparison.OrdinalIgnoreCase))
        {
            jobDtos = jobDtos
                .OrderByDescending(j => j.MyMatchPercent ?? 0)
                .ThenByDescending(j => j.CreatedAt)
                .ToList();
        }
        else
        {
            jobDtos = jobDtos.OrderByDescending(j => j.CreatedAt).ToList();
        }

        var totalCount = jobDtos.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var pagedItems = jobDtos.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedJobsResponse(pagedItems, page, pageSize, totalCount, totalPages);
    }

    public async Task<JobDto> GetJobByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .AsNoTracking()
            .Include(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException("jobs.not_found", "Job not found", "This job does not exist.");

        var isOpen = job.IsOpen;
        if (isOpen && job.Deadline.HasValue && clock.UtcNow >= job.Deadline.Value)
        {
            var trackedJob = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            if (trackedJob != null && trackedJob.IsOpen)
            {
                trackedJob.Close();
                await dbContext.SaveChangesAsync(cancellationToken);
                isOpen = false;
                if (notifier != null)
                {
                    await notifier.NotifyJobStatusChangedAsync(new JobStatusChangedRealtimeEvent(trackedJob.Id, false, trackedJob.Deadline, null), cancellationToken);
                }
            }
        }

        var employer = await identityService.FindByIdAsync(job.EmployerId, cancellationToken);
        var companyName = employer?.CompanyName ?? "Unknown Company";

        var requiredSkills = job.RequiredSkills
            .Where(rs => rs.Skill != null && rs.IsRequired)
            .OrderBy(rs => rs.Skill.Name)
            .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
            .ToList();

        if (requiredSkills.Count == 0)
        {
            requiredSkills = job.RequiredSkills
                .Where(rs => rs.Skill != null)
                .OrderBy(rs => rs.Skill.Name)
                .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                .ToList();
        }

        var preferredSkills = job.RequiredSkills
            .Where(rs => rs.Skill != null && !rs.IsRequired)
            .OrderBy(rs => rs.Skill.Name)
            .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
            .ToList();

        var isCandidate = currentUser.IsAuthenticated && currentUser.IsInRole(Roles.Candidate);
        JobMatchDto? match = null;
        int? myMatchPercent = null;
        bool? hasApplied = null;
        string? applicationStatus = null;
        int? applicationId = null;

        if (isCandidate)
        {
            var candidateProfile = await dbContext.CandidateProfiles
                .AsNoTracking()
                .Include(cp => cp.Skills)
                .ThenInclude(cs => cs.Skill)
                .Include(cp => cp.ExperienceHistory)
                .FirstOrDefaultAsync(cp => cp.UserId == currentUser.UserId, cancellationToken);

            if (candidateProfile != null)
            {
                match = jobMatchingService.CalculateMatch(job, candidateProfile);
                myMatchPercent = match.Percent;
            }

            var application = await dbContext.Applications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.JobId == id && a.CandidateId == currentUser.UserId, cancellationToken);

            if (application != null)
            {
                hasApplied = true;
                applicationStatus = application.Status.ToString();
                applicationId = application.Id;
            }
            else
            {
                hasApplied = false;
            }
        }

        var isOwner = currentUser.IsAuthenticated && job.IsOwnedBy(currentUser.UserId);

        return new JobDto(
            job.Id,
            job.Title,
            companyName,
            job.Location,
            isOpen,
            job.CreatedAt,
            requiredSkills,
            match,
            myMatchPercent,
            hasApplied,
            job.Description,
            isOwner,
            job.JobType,
            job.WorkArrangement,
            job.ExperienceLevel,
            job.MinExperienceYears,
            preferredSkills,
            applicationStatus,
            applicationId,
            job.Deadline);
    }

    public async Task<JobDto> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        var validationResult = await createJobValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
            throw new RequestValidationException(errors);
        }

        var customReqIds = await ResolveCustomSkillsAsync(request.CustomSkills, cancellationToken);
        var customPrefIds = await ResolveCustomSkillsAsync(request.CustomPreferredSkills, cancellationToken);

        var finalRequiredSkillIds = (request.RequiredSkillIds ?? []).Concat(customReqIds).Distinct().ToList();
        var finalPreferredSkillIds = (request.PreferredSkillIds ?? []).Concat(customPrefIds).Distinct().Where(id => !finalRequiredSkillIds.Contains(id)).ToList();

        var allSkillIds = new List<int>(finalRequiredSkillIds);
        if (finalPreferredSkillIds != null)
            allSkillIds.AddRange(finalPreferredSkillIds);
        if (request.SkillRequirements != null)
            allSkillIds.AddRange(request.SkillRequirements.Select(sr => sr.SkillId));

        var distinctSkillIds = allSkillIds.Distinct().ToList();
        var existingSkillCount = await dbContext.Skills
            .Where(s => distinctSkillIds.Contains(s.Id))
            .CountAsync(cancellationToken);

        if (existingSkillCount != distinctSkillIds.Count)
        {
            throw new BusinessRuleException("skills.unknown", "Unknown skills", "Some selected skills do not exist.");
        }

        IEnumerable<(int SkillId, bool IsRequired, SkillProficiency MinProficiency)>? skillDetails = null;
        if (request.SkillRequirements != null && request.SkillRequirements.Count > 0)
        {
            skillDetails = request.SkillRequirements.Select(sr => (sr.SkillId, sr.IsRequired, sr.MinProficiency));
        }

        var employerId = currentUser.UserId;
        var job = new Job(
            employerId,
            request.Title,
            request.Description,
            request.Location,
            finalRequiredSkillIds,
            clock.UtcNow,
            request.JobType,
            request.WorkArrangement,
            request.ExperienceLevel,
            request.MinExperienceYears,
            finalPreferredSkillIds,
            skillDetails,
            request.Deadline);

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await GetJobByIdAsync(job.Id, cancellationToken);
        if (notifier != null)
        {
            await notifier.NotifyJobCreatedAsync(new JobCreatedRealtimeEvent(created), cancellationToken);
        }
        return created;
    }

    public async Task<JobDto> UpdateJobAsync(int id, UpdateJobRequest request, CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        var job = await dbContext.Jobs
            .Include(j => j.RequiredSkills)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException("jobs.not_found", "Job not found", "This job does not exist.");

        if (job.EmployerId != currentUser.UserId)
        {
            throw new ForbiddenException("jobs.not_owner", "Not job owner", "You can only edit your own jobs.");
        }

        if (!job.IsEditingAllowed(clock.UtcNow))
        {
            throw new ForbiddenException("jobs.editing_expired", "Editing window expired", "Jobs can only be edited within the first 2 hours after posting.");
        }

        if (updateJobValidator != null)
        {
            var validationResult = await updateJobValidator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                throw new RequestValidationException(errors);
            }
        }

        var customReqIds = await ResolveCustomSkillsAsync(request.CustomSkills, cancellationToken);
        var customPrefIds = await ResolveCustomSkillsAsync(request.CustomPreferredSkills, cancellationToken);

        var finalRequiredSkillIds = (request.RequiredSkillIds ?? []).Concat(customReqIds).Distinct().ToList();
        var finalPreferredSkillIds = (request.PreferredSkillIds ?? []).Concat(customPrefIds).Distinct().Where(id => !finalRequiredSkillIds.Contains(id)).ToList();

        var allSkillIds = new List<int>(finalRequiredSkillIds);
        if (finalPreferredSkillIds != null)
            allSkillIds.AddRange(finalPreferredSkillIds);
        if (request.SkillRequirements != null)
            allSkillIds.AddRange(request.SkillRequirements.Select(sr => sr.SkillId));

        var distinctSkillIds = allSkillIds.Distinct().ToList();
        var existingSkillCount = await dbContext.Skills
            .Where(s => distinctSkillIds.Contains(s.Id))
            .CountAsync(cancellationToken);

        if (existingSkillCount != distinctSkillIds.Count)
        {
            throw new BusinessRuleException("skills.unknown", "Unknown skills", "Some selected skills do not exist.");
        }

        IEnumerable<(int SkillId, bool IsRequired, SkillProficiency MinProficiency)>? skillDetails = null;
        if (request.SkillRequirements != null && request.SkillRequirements.Count > 0)
        {
            skillDetails = request.SkillRequirements.Select(sr => (sr.SkillId, sr.IsRequired, sr.MinProficiency));
        }

        job.Update(
            request.Title,
            request.Description,
            request.Location,
            finalRequiredSkillIds,
            request.JobType,
            request.WorkArrangement,
            request.ExperienceLevel,
            request.MinExperienceYears,
            finalPreferredSkillIds,
            skillDetails,
            request.Deadline);

        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await GetJobByIdAsync(job.Id, cancellationToken);
        if (notifier != null)
        {
            await notifier.NotifyJobUpdatedAsync(new JobUpdatedRealtimeEvent(updated), cancellationToken);
            await notifier.NotifyJobStatusChangedAsync(new JobStatusChangedRealtimeEvent(job.Id, job.IsOpen, job.Deadline, updated), cancellationToken);
        }
        return updated;
    }

    private async Task<List<int>> ResolveCustomSkillsAsync(
        IReadOnlyList<string>? customSkills,
        CancellationToken cancellationToken)
    {
        var resolvedIds = new List<int>();
        if (customSkills == null || customSkills.Count == 0)
        {
            return resolvedIds;
        }

        foreach (var rawName in customSkills)
        {
            if (string.IsNullOrWhiteSpace(rawName)) continue;
            var trimmed = rawName.Trim();
            if (trimmed.Length > 50) trimmed = trimmed[..50];
            var normalized = Skill.Normalize(trimmed);

            var existing = await dbContext.Skills
                .FirstOrDefaultAsync(s => s.NormalizedName == normalized, cancellationToken)
                ?? dbContext.Skills.Local.FirstOrDefault(s => s.NormalizedName == normalized);

            if (existing == null)
            {
                existing = new Skill(trimmed);
                dbContext.Skills.Add(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            resolvedIds.Add(existing.Id);
        }

        return resolvedIds;
    }

    public async Task<IReadOnlyList<EmployerJobDto>> GetEmployerJobsAsync(CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        var employerId = currentUser.UserId;

        // Auto-close expired jobs
        var expiredJobs = await dbContext.Jobs
            .Where(j => j.IsOpen && j.Deadline != null && j.Deadline <= clock.UtcNow)
            .ToListAsync(cancellationToken);
        if (expiredJobs.Count > 0)
        {
            foreach (var expired in expiredJobs)
            {
                expired.Close();
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            if (notifier != null)
            {
                foreach (var expired in expiredJobs)
                {
                    await notifier.NotifyJobStatusChangedAsync(new JobStatusChangedRealtimeEvent(expired.Id, false, expired.Deadline, null), cancellationToken);
                }
            }
        }

        var jobs = await dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.EmployerId == employerId)
            .Include(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .Include(j => j.Applications)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

        return jobs.Select(job =>
        {
            var requiredSkills = job.RequiredSkills
                .Where(rs => rs.Skill != null && rs.IsRequired)
                .OrderBy(rs => rs.Skill.Name)
                .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                .ToList();

            if (requiredSkills.Count == 0)
            {
                requiredSkills = job.RequiredSkills
                    .Where(rs => rs.Skill != null)
                    .OrderBy(rs => rs.Skill.Name)
                    .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                    .ToList();
            }

            var preferredSkills = job.RequiredSkills
                .Where(rs => rs.Skill != null && !rs.IsRequired)
                .OrderBy(rs => rs.Skill.Name)
                .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                .ToList();

            var counts = new ApplicantCountsDto(
                Total: job.Applications.Count,
                Received: job.Applications.Count(a => a.Status == ApplicationStatus.Received),
                Shortlisted: job.Applications.Count(a => a.Status == ApplicationStatus.Shortlisted),
                Rejected: job.Applications.Count(a => a.Status == ApplicationStatus.Rejected),
                Withdrawn: job.Applications.Count(a => a.Status == ApplicationStatus.Withdrawn));

            return new EmployerJobDto(
                job.Id,
                job.Title,
                job.Location,
                job.IsOpen,
                job.CreatedAt,
                requiredSkills,
                counts,
                preferredSkills,
                job.JobType,
                job.WorkArrangement,
                job.ExperienceLevel,
                job.MinExperienceYears,
                job.Description,
                job.Deadline);
        }).ToList();
    }

    public async Task<JobDto> CloseJobAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        var job = await dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException("jobs.not_found", "Job not found", "This job does not exist.");

        if (job.EmployerId != currentUser.UserId)
        {
            throw new ForbiddenException("jobs.not_owner", "Not job owner", "You can only close your own jobs.");
        }

        if (job.IsOpen)
        {
            job.Close();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var jobDto = await GetJobByIdAsync(id, cancellationToken);
        if (notifier != null)
        {
            await notifier.NotifyJobStatusChangedAsync(new JobStatusChangedRealtimeEvent(id, false, job.Deadline, jobDto), cancellationToken);
        }

        return jobDto;
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
}
