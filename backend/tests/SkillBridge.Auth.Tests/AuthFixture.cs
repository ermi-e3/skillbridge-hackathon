using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SkillBridge.Infrastructure.Persistence;
using Xunit;

namespace SkillBridge.Auth.Tests;

/// <summary>Runs the real API, Identity stores, migrations, and PostgreSQL provider.</summary>
public sealed class AuthFixture : IAsyncLifetime
{
    public const string SigningKey = "SkillBridge-integration-tests-only-signing-key-64-characters-long!!";
    public const string Issuer = "SkillBridge.Auth.Tests";
    public const string Audience = "SkillBridge.Auth.Tests.Client";

    public AuthApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable("SKILLBRIDGE_TEST_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set SKILLBRIDGE_TEST_CONNECTION to a dedicated PostgreSQL test database before running auth integration tests. " +
                "The fixture applies migrations and creates test users; it never deletes or resets the database.");

        var expectedDatabase = RequireTestDatabase(ConnectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options;
        await using (var database = new AppDbContext(options))
            await database.Database.MigrateAsync();

        Factory = new AuthApiFactory(ConnectionString);
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var scope = Factory.Services.CreateScope();
        var resolvedDatabase = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(expectedDatabase, resolvedDatabase.Database.GetDbConnection().Database);
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        Assert.Equal(expectedDatabase,
            new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Default")).Database);
    }

    internal static string RequireTestDatabase(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database;
        if (string.IsNullOrWhiteSpace(name) || name.Equals("skillbridge", StringComparison.OrdinalIgnoreCase)
            || !(name.EndsWith("_tests", StringComparison.OrdinalIgnoreCase)
                 || name.EndsWith("_test", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Auth integration tests require a dedicated PostgreSQL database named with an _tests or _test suffix. " +
                "The application database skillbridge cannot be used. No migration or test data was written.");
        }
        return name;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
    }
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;

    public AuthApiFactory(string testConnectionString)
    {
        AuthFixture.RequireTestDatabase(testConnectionString);
        connectionString = testConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Jwt:Key"] = AuthFixture.SigningKey,
                ["Jwt:Issuer"] = AuthFixture.Issuer,
                ["Jwt:Audience"] = AuthFixture.Audience,
                ["Jwt:ExpirationHours"] = "8",
                ["Seed:DemoUsers"] = "true"
            }));
        builder.ConfigureServices(services =>
        {
            // Minimal hosting can read the production connection before the factory's
            // configuration callback. Replace those captured EF registrations explicitly.
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
            services.AddControllers().AddApplicationPart(typeof(RoleVerificationController).Assembly);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class AuthCollection : ICollectionFixture<AuthFixture>
{
    public const string Name = "PostgreSQL authentication";
}
