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
    IValidator<CreateJobRequest> createJobValidator)
{
    public async Task<PagedJobsResponse> GetJobsAsync(
        IEnumerable<int>? skillIds = null,
        string? search = null,
        string? sort = "newest",
        int page = 1,
        int pageSize = 20,
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

        var filterSkillIds = skillIds?.Distinct().ToList();

        var query = dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.IsOpen)
            .Include(j => j.RequiredSkills)
            .ThenInclude(rs => rs.Skill)
            .AsQueryable();

        if (filterSkillIds != null && filterSkillIds.Count > 0)
        {
            query = query.Where(j => j.RequiredSkills.Any(rs => filterSkillIds.Contains(rs.SkillId)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.Trim().ToLower();
            query = query.Where(j => j.Title.ToLower().Contains(lowerSearch));
        }

        var allMatchingJobs = await query.ToListAsync(cancellationToken);

        var employerIds = allMatchingJobs.Select(j => j.EmployerId).Distinct().ToList();
        var employers = await identityService.FindByIdsAsync(employerIds, cancellationToken);

        var isCandidate = currentUser.IsAuthenticated && currentUser.IsInRole(Roles.Candidate);
        HashSet<int>? candidateSkillIds = null;
        HashSet<int>? appliedJobIds = null;

        if (isCandidate)
        {
            var candidateSkills = await dbContext.CandidateSkills
                .AsNoTracking()
                .Where(cs => cs.CandidateId == currentUser.UserId)
                .Select(cs => cs.SkillId)
                .ToListAsync(cancellationToken);
            candidateSkillIds = candidateSkills.ToHashSet();

            var applied = await dbContext.Applications
                .AsNoTracking()
                .Where(a => a.CandidateId == currentUser.UserId)
                .Select(a => a.JobId)
                .ToListAsync(cancellationToken);
            appliedJobIds = applied.ToHashSet();
        }

        var jobDtos = allMatchingJobs.Select(job =>
        {
            employers.TryGetValue(job.EmployerId, out var employer);
            var companyName = employer?.CompanyName ?? "Unknown Company";

            var requiredSkills = job.RequiredSkills
                .Where(rs => rs.Skill != null)
                .OrderBy(rs => rs.Skill.Name)
                .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
                .ToList();

            JobMatchDto? match = null;
            int? myMatchPercent = null;
            bool? hasApplied = null;

            if (isCandidate && candidateSkillIds != null)
            {
                var matchedSkills = requiredSkills.Where(s => candidateSkillIds.Contains(s.Id)).ToList();
                var missingSkills = requiredSkills.Where(s => !candidateSkillIds.Contains(s.Id)).ToList();
                var percent = requiredSkills.Count == 0
                    ? 0
                    : (int)Math.Round((double)matchedSkills.Count / requiredSkills.Count * 100.0, MidpointRounding.AwayFromZero);

                match = new JobMatchDto(percent, matchedSkills, missingSkills);
                myMatchPercent = percent;
                hasApplied = appliedJobIds?.Contains(job.Id) ?? false;
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
                isOwner);
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

        var employer = await identityService.FindByIdAsync(job.EmployerId, cancellationToken);
        var companyName = employer?.CompanyName ?? "Unknown Company";

        var requiredSkills = job.RequiredSkills
            .Where(rs => rs.Skill != null)
            .OrderBy(rs => rs.Skill.Name)
            .Select(rs => new SkillDto(rs.SkillId, rs.Skill.Name))
            .ToList();

        var isCandidate = currentUser.IsAuthenticated && currentUser.IsInRole(Roles.Candidate);
        JobMatchDto? match = null;
        int? myMatchPercent = null;
        bool? hasApplied = null;

        if (isCandidate)
        {
            var candidateSkills = await dbContext.CandidateSkills
                .AsNoTracking()
                .Where(cs => cs.CandidateId == currentUser.UserId)
                .Select(cs => cs.SkillId)
                .ToListAsync(cancellationToken);
            var candidateSkillIds = candidateSkills.ToHashSet();

            var matchedSkills = requiredSkills.Where(s => candidateSkillIds.Contains(s.Id)).ToList();
            var missingSkills = requiredSkills.Where(s => !candidateSkillIds.Contains(s.Id)).ToList();
            var percent = requiredSkills.Count == 0
                ? 0
                : (int)Math.Round((double)matchedSkills.Count / requiredSkills.Count * 100.0, MidpointRounding.AwayFromZero);

            match = new JobMatchDto(percent, matchedSkills, missingSkills);
            myMatchPercent = percent;

            hasApplied = await dbContext.Applications
                .AsNoTracking()
                .AnyAsync(a => a.JobId == id && a.CandidateId == currentUser.UserId, cancellationToken);
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
            isOwner);
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

        var distinctSkillIds = request.RequiredSkillIds.Distinct().ToList();
        var existingSkillCount = await dbContext.Skills
            .Where(s => distinctSkillIds.Contains(s.Id))
            .CountAsync(cancellationToken);

        if (existingSkillCount != distinctSkillIds.Count)
        {
            throw new BusinessRuleException("skills.unknown", "Unknown skills", "Some selected skills do not exist.");
        }

        var employerId = currentUser.UserId;
        var job = new Job(
            employerId,
            request.Title,
            request.Description,
            request.Location,
            distinctSkillIds,
            clock.UtcNow);

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Fetch back with skills
        return await GetJobByIdAsync(job.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<EmployerJobDto>> GetEmployerJobsAsync(CancellationToken cancellationToken = default)
    {
        EnsureEmployer();

        var employerId = currentUser.UserId;

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
                .Where(rs => rs.Skill != null)
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
                counts);
        }).ToList();
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
