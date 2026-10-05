using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    // Ids 1..40 in this order (1 = ASP.NET Core, 2 = Angular, 3 = C#, 5 = PostgreSQL).
    private static readonly string[] SeedNames =
    [
        "ASP.NET Core", "Angular", "C#", "TypeScript", "PostgreSQL",
        "Entity Framework Core", "JavaScript", "HTML", "CSS", "Tailwind CSS",
        "SQL Server", "Docker", "Git", "Linux", "REST APIs",
        "SignalR", "React", "Vue.js", "Node.js", "Express.js",
        "Python", "Django", "Flask", "Java", "Spring Boot",
        "Kotlin", "Flutter", "Dart", "Swift", "PHP",
        "Laravel", "MySQL", "MongoDB", "Redis", "Azure",
        "AWS", "Figma", "Unit Testing", "CI/CD", "Go"
    ];

    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(s => s.Id);

        // Seeded rows use explicit ids; start the sequence above them.
        builder.Property(s => s.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 1000);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(50);
        builder.Property(s => s.NormalizedName).IsRequired().HasMaxLength(50);

        builder.HasIndex(s => s.NormalizedName).IsUnique();

        builder.HasData(SeedNames.Select((name, i) => new Skill(i + 1, name)));
    }
}
