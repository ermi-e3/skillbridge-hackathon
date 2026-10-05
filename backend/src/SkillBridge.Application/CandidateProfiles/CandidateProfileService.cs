using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Application.CandidateProfiles;

public sealed class CandidateProfileService(
    IAppDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<UpdateCandidateProfileRequest> validator) : ICandidateProfileService
{
    public async Task<CandidateProfileResponse> GetMyProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCandidateUserId();
        var profile = await dbContext.CandidateProfiles
            .AsNoTracking()
            .Include(candidate => candidate.Skills)
            .ThenInclude(candidateSkill => candidateSkill.Skill)
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw ProfileNotFound();

        return CreateResponse(profile, profile.Skills
            .OrderBy(skill => skill.SkillId)
            .Select(skill => new CandidateProfileSkillResponse(skill.SkillId, skill.Skill.Name))
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
            GitHubUrl = NormalizeOptional(request.GitHubUrl)
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
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw ProfileNotFound();

        var selectedSkills = await dbContext.Skills
            .AsNoTracking()
            .Where(skill => normalized.SkillIds.Contains(skill.Id))
            .OrderBy(skill => skill.Id)
            .Select(skill => new CandidateProfileSkillResponse(skill.Id, skill.Name))
            .ToArrayAsync(cancellationToken);

        if (selectedSkills.Length != normalized.SkillIds.Count)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["skillIds"] = ["One or more selected skills do not exist."]
            });
        }

        profile.Update(normalized.Headline, normalized.Bio, normalized.GitHubUrl,
            normalized.SkillIds, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateResponse(profile, selectedSkills);
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
        CandidateProfile profile, IReadOnlyList<CandidateProfileSkillResponse> skills) =>
        new(profile.UserId, profile.Headline, profile.Bio, profile.GitHubUrl, profile.UpdatedAt, skills);
}
