using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.Applications;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Application.CandidateProfiles;

public sealed class CandidateProfileService(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<UpdateCandidateProfileRequest> validator,
    IFileStorageService fileStorageService,
    IRealtimeNotifier? notifier = null) : ICandidateProfileService
{
    public async Task<CandidateProfileResponse> GetMyProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCandidateUserId();
        var profile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(candidate => candidate.Skills)
            .ThenInclude(candidateSkill => candidateSkill.Skill)
            .Include(candidate => candidate.ExperienceHistory)
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw ProfileNotFound();

        return CreateResponse(profile, profile.Skills
            .OrderBy(skill => skill.SkillId)
            .Select(skill => new CandidateProfileSkillResponse(
                skill.SkillId,
                skill.Skill?.Name ?? "Unknown Skill",
                skill.Proficiency,
                skill.YearsOfExperience,
                skill.IsPreferred))
            .ToArray());
    }

    public async Task<CandidateProfileResponse> UpdateMyProfileAsync(
        UpdateCandidateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetCandidateUserId();
        var normalized = request with
        {
            Headline = (request.Headline ?? string.Empty).Trim(),
            Bio = NormalizeOptional(request.Bio),
            GitHubUrl = NormalizeOptional(request.GitHubUrl),
            Location = NormalizeOptional(request.Location),
            SkillIds = request.SkillIds ?? []
        };

        var validation = await validator.ValidateAsync(normalized, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(error => char.ToLowerInvariant(error.PropertyName[0]) + error.PropertyName[1..])
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray());
            throw new RequestValidationException(errors);
        }

        var profile = await dbContext.CandidateProfiles
            .Include(candidate => candidate.Skills)
            .Include(candidate => candidate.ExperienceHistory)
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw ProfileNotFound();

        var preferredIds = request.PreferredSkillIds ?? [];
        var allRequestedSkillIds = normalized.SkillIds.Concat(preferredIds).Distinct().ToList();

        var selectedSkills = await dbContext.Skills
            .AsNoTracking()
            .Where(skill => allRequestedSkillIds.Contains(skill.Id))
            .OrderBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        if (selectedSkills.Count != allRequestedSkillIds.Count)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["skillIds"] = ["One or more selected skills do not exist."]
            });
        }

        IEnumerable<(int SkillId, SkillProficiency Proficiency, int Years, bool IsPreferred)>? skillDetails = null;
        if (request.SkillDetails != null && request.SkillDetails.Count > 0)
        {
            skillDetails = request.SkillDetails.Select(sd => (
                sd.SkillId,
                sd.Proficiency,
                sd.YearsOfExperience,
                sd.IsPreferred || preferredIds.Contains(sd.SkillId)));
        }

        IEnumerable<CandidateExperience>? experiences = null;
        if (request.ExperienceHistory != null)
        {
            experiences = request.ExperienceHistory.Select(e => new CandidateExperience(
                userId,
                e.Title,
                e.Company,
                e.Location,
                e.StartDate,
                e.EndDate,
                e.IsCurrent,
                e.Description));
        }

        profile.Update(
            normalized.Headline,
            normalized.Bio,
            normalized.GitHubUrl,
            normalized.SkillIds,
            normalized.Location,
            normalized.ExperienceLevel,
            normalized.YearsOfExperience,
            normalized.PreferredWorkArrangement,
            normalized.PreferredJobType,
            clock.UtcNow,
            skillDetails,
            experiences,
            request.PreferredSkillIds);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (notifier != null)
        {
            await notifier.NotifyCandidateProfileUpdatedAsync(new CandidateProfileUpdatedRealtimeEvent(userId), cancellationToken);
        }

        var skillResponses = profile.Skills
            .OrderBy(s => s.SkillId)
            .Select(s =>
            {
                var skillName = selectedSkills.FirstOrDefault(sk => sk.Id == s.SkillId)?.Name ?? "Skill";
                return new CandidateProfileSkillResponse(s.SkillId, skillName, s.Proficiency, s.YearsOfExperience, s.IsPreferred);
            })
            .ToArray();

        return CreateResponse(profile, skillResponses);
    }

    public async Task<CandidateProfileResponse> UploadDefaultCvAsync(
        Stream cvStream, string originalFileName, string contentType, long fileSizeBytes, CancellationToken cancellationToken = default)
    {
        var userId = GetCandidateUserId();
        CvFileValidator.Validate(originalFileName, fileSizeBytes, cvStream, contentType);

        var profile = await dbContext.CandidateProfiles
            .Include(candidate => candidate.Skills)
            .ThenInclude(candidateSkill => candidateSkill.Skill)
            .Include(candidate => candidate.ExperienceHistory)
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw ProfileNotFound();

        var storedFileName = await fileStorageService.SaveFileAsync(cvStream, originalFileName, contentType, cancellationToken);

        if (!string.IsNullOrWhiteSpace(profile.DefaultCvFileName))
        {
            await fileStorageService.DeleteFileAsync(profile.DefaultCvFileName, cancellationToken);
        }

        profile.SetDefaultCv(storedFileName, originalFileName, contentType, fileSizeBytes, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (notifier != null)
        {
            await notifier.NotifyCandidateProfileUpdatedAsync(new CandidateProfileUpdatedRealtimeEvent(userId), cancellationToken);
        }

        var skillResponses = profile.Skills
            .OrderBy(s => s.SkillId)
            .Select(s => new CandidateProfileSkillResponse(s.SkillId, s.Skill?.Name ?? "Skill", s.Proficiency, s.YearsOfExperience, s.IsPreferred))
            .ToArray();

        return CreateResponse(profile, skillResponses);
    }

    public async Task<CvDownloadResult> GetDefaultCvFileAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCandidateUserId();
        var profile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw ProfileNotFound();

        if (string.IsNullOrWhiteSpace(profile.DefaultCvFileName))
        {
            throw new NotFoundException("candidate.no_default_cv", "Default CV not found", "No default CV is associated with this profile.");
        }

        var stream = await fileStorageService.GetFileAsync(profile.DefaultCvFileName, cancellationToken)
            ?? throw new NotFoundException("candidate.default_cv_missing", "Default CV file missing", "The default CV file could not be found in storage.");

        return new CvDownloadResult(
            stream,
            profile.DefaultCvContentType ?? "application/octet-stream",
            profile.DefaultCvOriginalFileName ?? "resume.pdf");
    }

    public async Task RemoveDefaultCvAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCandidateUserId();
        var profile = await dbContext.CandidateProfiles
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw ProfileNotFound();

        if (!string.IsNullOrWhiteSpace(profile.DefaultCvFileName))
        {
            await fileStorageService.DeleteFileAsync(profile.DefaultCvFileName, cancellationToken);
            profile.RemoveDefaultCv(clock.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (notifier != null)
            {
                await notifier.NotifyCandidateProfileUpdatedAsync(new CandidateProfileUpdatedRealtimeEvent(userId), cancellationToken);
            }
        }
    }

    private string GetCandidateUserId()
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new UnauthorizedException(
                "auth.unauthenticated", "Authentication required", "Please sign in to continue.");
        }

        if (!currentUser.IsInRole(Roles.Candidate))
        {
            throw new ForbiddenException(
                "auth.forbidden", "Forbidden", "Your account type cannot do this.");
        }

        return currentUser.UserId;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NotFoundException ProfileNotFound() =>
        new("candidate.profile_not_found", "Profile not found", "Candidate profile not found.");

    private static CandidateProfileResponse CreateResponse(
        CandidateProfile profile, IReadOnlyList<CandidateProfileSkillResponse> skills)
    {
        var experiences = profile.ExperienceHistory
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
            .ToList();

        var preferredSkills = skills.Where(s => s.IsPreferred).ToList();

        return new(
            profile.UserId,
            profile.Headline,
            profile.Bio,
            profile.GitHubUrl,
            profile.UpdatedAt,
            skills,
            profile.Location,
            profile.ExperienceLevel,
            profile.YearsOfExperience,
            profile.PreferredWorkArrangement,
            profile.PreferredJobType,
            experiences,
            preferredSkills,
            profile.HasDefaultCv,
            profile.DefaultCvOriginalFileName,
            profile.DefaultCvFileSizeBytes,
            profile.DefaultCvUploadedAt);
    }
}
