using Microsoft.EntityFrameworkCore;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    // Add DbSets here, e.g. public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Picks up every IEntityTypeConfiguration<T> in Persistence/Configurations.
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
