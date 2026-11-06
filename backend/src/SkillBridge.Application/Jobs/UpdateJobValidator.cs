using FluentValidation;

namespace SkillBridge.Application.Jobs;

public sealed class UpdateJobValidator : AbstractValidator<UpdateJobRequest>
{
    public UpdateJobValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .Length(3, 120).WithMessage("Title must be between 3 and 120 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .Length(20, 4000).WithMessage("Description must be between 20 and 4000 characters.");

        RuleFor(x => x.Location)
            .MaximumLength(100).WithMessage("Location cannot exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.RequiredSkillIds)
            .Must((req, skills) =>
            {
                var total = (skills?.Distinct().Count() ?? 0) + (req.CustomSkills?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Count() ?? 0);
                return total is >= 1 and <= 25;
            })
            .WithMessage("Select or add between 1 and 25 required skills.");

        RuleFor(x => x.PreferredSkillIds)
            .Must(skills => skills == null || skills.Distinct().Count() <= 25)
            .WithMessage("Select at most 25 preferred skills.")
            .When(x => x.PreferredSkillIds != null);

        RuleFor(x => x.Deadline)
            .Must(d => !d.HasValue || d.Value > DateTime.UtcNow)
            .WithMessage("Deadline must be in the future.")
            .When(x => x.Deadline.HasValue);
    }
}

