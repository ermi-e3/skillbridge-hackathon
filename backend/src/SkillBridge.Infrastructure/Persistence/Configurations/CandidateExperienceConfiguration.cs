using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class CandidateExperienceConfiguration : IEntityTypeConfiguration<CandidateExperience>
{
    public void Configure(EntityTypeBuilder<CandidateExperience> builder)
    {
        builder.ToTable("CandidateExperiences");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.CandidateId).IsRequired();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Company).IsRequired().HasMaxLength(120);
        builder.Property(e => e.Location).HasMaxLength(100);
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.IsCurrent).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);

        builder.HasIndex(e => e.CandidateId);
    }
}

