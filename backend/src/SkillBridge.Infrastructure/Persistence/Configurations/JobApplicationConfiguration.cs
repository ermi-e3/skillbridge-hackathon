using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;
using SkillBridge.Infrastructure.Identity;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    /// <summary>The exception handler matches on this name to return applications.duplicate.</summary>
    public const string UniqueJobCandidateIndex = "IX_Applications_JobId_CandidateId";

    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("Applications", t =>
            t.HasCheckConstraint("CK_Applications_Status",
                "\"Status\" IN ('Received', 'Shortlisted', 'Rejected', 'Withdrawn')"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.CandidateId).IsRequired();
        builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.CoverNote).HasMaxLength(JobApplication.CoverNoteMaxLength);
        builder.Property(a => a.AppliedAt).IsRequired();
        builder.Property(a => a.Version).IsRowVersion(); // PostgreSQL xmin

        // CandidateId → AspNetUsers.Id. A user with applications cannot be deleted.
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(a => a.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        // HARD RULE: one application per candidate per job.
        builder.HasIndex(a => new { a.JobId, a.CandidateId })
            .IsUnique()
            .HasDatabaseName(UniqueJobCandidateIndex);

        builder.HasIndex(a => a.CandidateId); // "my applications"
    }
}
