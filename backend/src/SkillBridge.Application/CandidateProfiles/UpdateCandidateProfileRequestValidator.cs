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

        RuleFor(request => request.ExperienceLevel)
            .IsInEnum()
            .When(r => r.ExperienceLevel.HasValue);

        RuleFor(request => request.GitHubUrl)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(300)
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("GitHub URL must be an absolute HTTP or HTTPS URL.");

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

    private static bool BeAbsoluteHttpUrl(string? value) =>
        value is null ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
