using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class CandidateSkillConfiguration : IEntityTypeConfiguration<CandidateSkill>
{
    public void Configure(EntityTypeBuilder<CandidateSkill> builder)
    {
        builder.ToTable("CandidateSkills");
        builder.HasKey(cs => new { cs.CandidateId, cs.SkillId }); // no duplicate skill per candidate

        builder.Property(cs => cs.Proficiency).HasDefaultValue(SkillProficiency.Intermediate);
        builder.Property(cs => cs.YearsOfExperience).HasDefaultValue(1);
        builder.Property(cs => cs.IsPreferred).HasDefaultValue(false);

        builder.HasOne(cs => cs.Skill)
            .WithMany()
            .HasForeignKey(cs => cs.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cs => cs.SkillId);
    }
}
