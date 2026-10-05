using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;
using SkillBridge.Infrastructure.Identity;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.EmployerId).IsRequired();
        builder.Property(j => j.Title).IsRequired().HasMaxLength(120);
        builder.Property(j => j.Description).IsRequired().HasMaxLength(4000);
        builder.Property(j => j.Location).HasMaxLength(100);
        builder.Property(j => j.IsOpen).IsRequired();
        builder.Property(j => j.CreatedAt).IsRequired();

        // EmployerId → AspNetUsers.Id. An employer with jobs cannot be deleted.
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(j => j.EmployerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(j => j.RequiredSkills)
            .WithOne(js => js.Job)
            .HasForeignKey(js => js.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.Applications)
            .WithOne(a => a.Job)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(j => j.RequiredSkills).HasField("_requiredSkills").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(j => j.Applications).HasField("_applications").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(j => new { j.IsOpen, j.CreatedAt }); // public job list: open, newest first
    }
}
