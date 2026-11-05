using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class JobSkillConfiguration : IEntityTypeConfiguration<JobSkill>
{
    public void Configure(EntityTypeBuilder<JobSkill> builder)
    {
        builder.ToTable("JobSkills");
        builder.HasKey(js => new { js.JobId, js.SkillId }); // no duplicate skill per job

        builder.Property(js => js.IsRequired).HasDefaultValue(true);
        builder.Property(js => js.MinProficiency).HasDefaultValue(SkillProficiency.Intermediate);

        builder.HasOne(js => js.Skill)
            .WithMany()
            .HasForeignKey(js => js.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(js => js.SkillId); // "filter jobs by skill"
        builder.HasIndex(js => new { js.JobId, js.IsRequired });
    }
}
