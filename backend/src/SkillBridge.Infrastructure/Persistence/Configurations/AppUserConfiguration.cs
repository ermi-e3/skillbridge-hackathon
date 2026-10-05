using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Infrastructure.Identity;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        // Table stays AspNetUsers (Identity default).
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.CompanyName).HasMaxLength(120);

        // Identity's email index is not unique by default; make it unique.
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
