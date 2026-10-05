using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SkillBridge.Infrastructure.Persistence;

public sealed class AppDbContextFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var apiDirectory = new[]
        {
            Path.Combine(currentDirectory, "src", "SkillBridge.Api"),
            Path.Combine(currentDirectory, "backend", "src", "SkillBridge.Api"),
            Path.Combine(currentDirectory, "..", "SkillBridge.Api")
        }.FirstOrDefault(path => File.Exists(Path.Combine(path, "appsettings.json")))
            ?? throw new InvalidOperationException("Run EF commands from the repository, backend, or Infrastructure directory.");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile(
                "appsettings.json",
                optional: false)
            .AddJsonFile(
                "appsettings.Development.json",
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is missing.");

        var options =
            new DbContextOptionsBuilder<AppDbContext>();

        options.UseNpgsql(connectionString);

        return new AppDbContext(options.Options);
    }
}
