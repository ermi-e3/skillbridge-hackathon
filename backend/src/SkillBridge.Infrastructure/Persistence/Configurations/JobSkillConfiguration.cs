using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class JobSkillConfiguration : IEntityTypeConfiguration<JobSkill>
{
    public void Configure(EntityTypeBuilder<JobSkill> builder)
    {
        builder.ToTable("JobSkills");
        builder.HasKey(js => new { js.JobId, js.SkillId }); // no duplicate required skill per job

        builder.HasOne(js => js.Skill)
            .WithMany()
            .HasForeignKey(js => js.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(js => js.SkillId); // "filter jobs by skill"
    }
}
