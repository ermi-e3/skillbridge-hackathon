using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;
using SkillBridge.Infrastructure.Identity;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("CandidateProfiles");
        builder.HasKey(p => p.UserId); // the profile's key IS the user's id

        builder.Property(p => p.Headline).HasMaxLength(120);
        builder.Property(p => p.Bio).HasMaxLength(2000);
        builder.Property(p => p.GitHubUrl).HasMaxLength(300);
        builder.Property(p => p.Location).HasMaxLength(100);
        builder.Property(p => p.ExperienceLevel).HasDefaultValue(ExperienceLevel.EntryLevel);
        builder.Property(p => p.YearsOfExperience).HasDefaultValue(0);
        builder.Property(p => p.PreferredWorkArrangement).HasDefaultValue(WorkArrangement.Remote);
        builder.Property(p => p.PreferredJobType).HasDefaultValue(JobType.FullTime);

        builder.Ignore(p => p.HasSkills);

        // 1 : 0..1 with the Identity user. FK only — no navigation, so Domain stays Identity-free.
        builder.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<CandidateProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Skills)
            .WithOne(cs => cs.Candidate)
            .HasForeignKey(cs => cs.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ExperienceHistory)
            .WithOne(e => e.Candidate)
            .HasForeignKey(e => e.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Skills).HasField("_skills").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.ExperienceHistory).HasField("_experienceHistory").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
