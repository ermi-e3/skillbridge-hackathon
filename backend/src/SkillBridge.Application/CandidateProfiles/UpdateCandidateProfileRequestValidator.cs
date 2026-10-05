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
    }

    private static bool BeAbsoluteHttpUrl(string? value) =>
        value is null ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
}
