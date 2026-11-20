using FluentValidation;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Application.CandidateProfiles;

public sealed class UpdateCandidateProfileRequestValidator : AbstractValidator<UpdateCandidateProfileRequest>
{
    public UpdateCandidateProfileRequestValidator()
    {
        RuleFor(request => request.Headline)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(request => request.Bio)
            .MaximumLength(2000);

        RuleFor(request => request.Location)
            .MaximumLength(100);

        RuleFor(request => request.YearsOfExperience)
            .GreaterThanOrEqualTo(0)
            .When(r => r.YearsOfExperience.HasValue);

        RuleFor(request => request.PreferredWorkArrangement)
            .IsInEnum()
            .When(r => r.PreferredWorkArrangement.HasValue);

        RuleFor(request => request.PreferredJobType)
            .IsInEnum()
            .When(r => r.PreferredJobType.HasValue);

        RuleForEach(request => request.PreferredJobTypes)
            .IsInEnum()
            .When(r => r.PreferredJobTypes != null);

        RuleFor(request => request.ExperienceLevel)
            .IsInEnum()
            .When(r => r.ExperienceLevel.HasValue);

        RuleFor(request => request.LinkedInUrl)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(300)
            .Must(BeValidLinkedInUrl)
            .WithMessage("LinkedIn URL must start with https:// and be a linkedin.com address.");

        RuleFor(request => request.SkillIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Skills are required.")
            .Must(ids => ids.Count is >= 1 and <= CandidateProfile.MaxSkills)
            .WithMessage($"Pick between 1 and {CandidateProfile.MaxSkills} skills.")
            .Must(ids => ids.All(id => id > 0))
            .WithMessage("Skill IDs must be positive.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Skill IDs must be unique.");

        RuleFor(request => request.PreferredSkillIds)
            .Cascade(CascadeMode.Stop)
            .Must(ids => ids == null || ids.Distinct().Count() <= 15)
            .WithMessage("Select at most 15 preferred skills.")
            .Must(ids => ids == null || ids.All(id => id > 0))
            .WithMessage("Preferred skill IDs must be positive.")
            .Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Preferred skill IDs must be unique.")
            .When(request => request.PreferredSkillIds != null);
    }

    private static bool BeValidLinkedInUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host.ToLowerInvariant();
        return host == "linkedin.com" || host.EndsWith(".linkedin.com");
    }
}
