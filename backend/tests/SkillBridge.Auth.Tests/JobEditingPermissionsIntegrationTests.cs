using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Application.Applications;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;
using SkillBridge.Infrastructure.Persistence;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class JobEditingPermissionsIntegrationTests(AuthFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Employer_can_create_job_with_custom_skills_and_query_them()
    {
        using var client = fixture.Factory.CreateClient();
        var employerEmail = $"emp-customskill-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        await RegisterAndLoginAsync(client, employerEmail, password, Roles.Employer, "Custom Skills Tech");

        var customSkill1 = $"FastAPI-{Guid.NewGuid():N}"[..18];
        var customSkill2 = $"Solidity-{Guid.NewGuid():N}"[..18];

        var createRes = await client.PostAsJsonAsync("/api/jobs", new
        {
            title = "Web3 Backend Engineer",
            description = "Build decentralized smart contracts and high-speed API services.",
            location = "Remote",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Remote,
            experienceLevel = ExperienceLevel.Senior,
            minExperienceYears = 4,
            requiredSkillIds = new[] { 1 }, // ASP.NET Core
            customSkills = new[] { customSkill1, customSkill2 }
        });

        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdJob = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(createdJob);
        Assert.Contains(createdJob.RequiredSkills, s => s.Name.Equals(customSkill1, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(createdJob.RequiredSkills, s => s.Name.Equals(customSkill2, StringComparison.OrdinalIgnoreCase));

        // Query job by ID
        var getRes = await client.GetAsync($"/api/jobs/{createdJob.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetchedJob = await getRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(fetchedJob);
        Assert.Contains(fetchedJob.RequiredSkills, s => s.Name.Equals(customSkill1, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fetchedJob.RequiredSkills, s => s.Name.Equals(customSkill2, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Employer_can_update_their_job_within_2_hours()
    {
        using var client = fixture.Factory.CreateClient();
        var employerEmail = $"emp-editowner-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        await RegisterAndLoginAsync(client, employerEmail, password, Roles.Employer, "Owner Corp");

        var createRes = await client.PostAsJsonAsync("/api/jobs", new
        {
            title = "Initial Job Title",
            description = "This is the initial description for the job opening before edits.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 } // ASP.NET Core
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdJob = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(createdJob);

        var newCustomSkill = $"K8s-{Guid.NewGuid():N}"[..12];

        // Edit job details and skills within 2 hours
        var updateRes = await client.PutAsJsonAsync($"/api/jobs/{createdJob.Id}", new
        {
            title = "Updated Senior Platform Architect",
            description = "This is the updated description with detailed architecture requirements.",
            location = "Bole, Addis Ababa",
            jobType = JobType.Contract,
            workArrangement = WorkArrangement.Remote,
            experienceLevel = ExperienceLevel.Senior,
            minExperienceYears = 5,
            requiredSkillIds = new[] { 1, 3 }, // ASP.NET Core, C#
            customSkills = new[] { newCustomSkill }
        });

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedJob = await updateRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(updatedJob);
        Assert.Equal("Updated Senior Platform Architect", updatedJob.Title);
        Assert.Equal("Bole, Addis Ababa", updatedJob.Location);
        Assert.Equal(JobType.Contract, updatedJob.JobType);
        Assert.Equal(WorkArrangement.Remote, updatedJob.WorkArrangement);
        Assert.Equal(ExperienceLevel.Senior, updatedJob.ExperienceLevel);
        Assert.Equal(5, updatedJob.MinExperienceYears);
        Assert.Contains(updatedJob.RequiredSkills, s => s.Name.Equals(newCustomSkill, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Non_owner_employer_cannot_edit_another_employers_job()
    {
        using var ownerClient = fixture.Factory.CreateClient();
        using var otherClient = fixture.Factory.CreateClient();
        const string password = "TestPassword123!";

        var ownerEmail = $"emp-own1-{Guid.NewGuid():N}@test.com";
        var otherEmail = $"emp-other2-{Guid.NewGuid():N}@test.com";

        await RegisterAndLoginAsync(ownerClient, ownerEmail, password, Roles.Employer, "Owner Corp");
        await RegisterAndLoginAsync(otherClient, otherEmail, password, Roles.Employer, "Other Corp");

        var createRes = await ownerClient.PostAsJsonAsync("/api/jobs", new
        {
            title = "Protected Job Role",
            description = "Job that should not be editable by other employers.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.OnSite,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 }
        });
        var createdJob = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(createdJob);

        // Other employer attempts to update owner's job
        var updateRes = await otherClient.PutAsJsonAsync($"/api/jobs/{createdJob.Id}", new
        {
            title = "Malicious Hijacked Job Title",
            description = "Attempting to change another employer's job details.",
            location = "Unknown",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.OnSite,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 }
        });

        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);
        var errBody = await updateRes.Content.ReadAsStringAsync();
        Assert.Contains("not_owner", errBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employer_cannot_edit_job_after_2_hours()
    {
        using var client = fixture.Factory.CreateClient();
        var employerEmail = $"emp-expirededit-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        await RegisterAndLoginAsync(client, employerEmail, password, Roles.Employer, "Time Limit Corp");

        var createRes = await client.PostAsJsonAsync("/api/jobs", new
        {
            title = "Time Limited Job Opening",
            description = "Job for checking 2-hour editing restriction enforcement.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 }
        });
        var createdJob = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(createdJob);

        // Simulate 2 hours passing: update CreatedAt in database to 3 hours ago
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Jobs\" SET \"CreatedAt\" = {0} WHERE \"Id\" = {1}",
                DateTime.UtcNow.AddHours(-3),
                createdJob.Id);
        }

        // Employer attempts to edit job details after 2 hours
        var updateRes = await client.PutAsJsonAsync($"/api/jobs/{createdJob.Id}", new
        {
            title = "Should Not Update Title",
            description = "This update should be blocked by the 2-hour backend restriction.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 }
        });

        Assert.Equal(HttpStatusCode.Forbidden, updateRes.StatusCode);
        var errBody = await updateRes.Content.ReadAsStringAsync();
        Assert.Contains("editing_expired", errBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employer_can_manage_applications_within_2_hours_but_not_after_2_hours()
    {
        using var employerClient = fixture.Factory.CreateClient();
        using var candidateClient = fixture.Factory.CreateClient();
        const string password = "TestPassword123!";

        var employerEmail = $"emp-appmanage-{Guid.NewGuid():N}@test.com";
        var candidateEmail = $"cand-appmanage-{Guid.NewGuid():N}@test.com";

        await RegisterAndLoginAsync(employerClient, employerEmail, password, Roles.Employer, "App Manager Corp");
        await RegisterAndLoginAsync(candidateClient, candidateEmail, password, Roles.Candidate);

        // 1. Employer creates job
        var createRes = await employerClient.PostAsJsonAsync("/api/jobs", new
        {
            title = "Application Management Role",
            description = "Job to test application management within and after 2-hour window.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 }
        });
        var job = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);

        // 2. Candidate applies
        var applyRes = await candidateClient.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new
        {
            coverNote = "I am excited to apply for this position."
        });
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var application = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(application);

        // 3. Within 2 hours: employer updates status to Shortlisted -> Succeeds
        var shortlistRes = await employerClient.PatchAsJsonAsync($"/api/applications/{application.Id}/status", new
        {
            status = "Shortlisted"
        });
        Assert.Equal(HttpStatusCode.OK, shortlistRes.StatusCode);
        var updatedApp = await shortlistRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(updatedApp);
        Assert.Equal("Shortlisted", updatedApp.Status);

        // 4. Simulate 2 hours passing: update Job's CreatedAt to 3 hours ago
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Jobs\" SET \"CreatedAt\" = {0} WHERE \"Id\" = {1}",
                DateTime.UtcNow.AddHours(-3),
                job.Id);
        }

        // 5. After 2 hours: employer attempts to change status to Rejected -> Rejected with 403 Forbidden
        var rejectRes = await employerClient.PatchAsJsonAsync($"/api/applications/{application.Id}/status", new
        {
            status = "Rejected"
        });
        Assert.Equal(HttpStatusCode.Forbidden, rejectRes.StatusCode);
        var errBody = await rejectRes.Content.ReadAsStringAsync();
        Assert.Contains("editing_expired", errBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Non_owner_employer_cannot_manage_applications_for_another_employers_job()
    {
        using var ownerClient = fixture.Factory.CreateClient();
        using var otherClient = fixture.Factory.CreateClient();
        using var candidateClient = fixture.Factory.CreateClient();
        const string password = "TestPassword123!";

        var ownerEmail = $"emp-appown1-{Guid.NewGuid():N}@test.com";
        var otherEmail = $"emp-appother2-{Guid.NewGuid():N}@test.com";
        var candEmail = $"cand-appown-{Guid.NewGuid():N}@test.com";

        await RegisterAndLoginAsync(ownerClient, ownerEmail, password, Roles.Employer, "Owner Corp");
        await RegisterAndLoginAsync(otherClient, otherEmail, password, Roles.Employer, "Other Corp");
        await RegisterAndLoginAsync(candidateClient, candEmail, password, Roles.Candidate);

        // Owner creates job
        var createRes = await ownerClient.PostAsJsonAsync("/api/jobs", new
        {
            title = "Cross Employer Job",
            description = "Job for checking application authorization boundary.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 }
        });
        var job = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);

        // Candidate applies
        var applyRes = await candidateClient.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new
        {
            coverNote = "Application"
        });
        var application = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(application);

        // Other employer attempts to manage status
        var statusRes = await otherClient.PatchAsJsonAsync($"/api/applications/{application.Id}/status", new
        {
            status = "Shortlisted"
        });
        Assert.Equal(HttpStatusCode.Forbidden, statusRes.StatusCode);
        var errBody = await statusRes.Content.ReadAsStringAsync();
        Assert.Contains("not_owner", errBody, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RegisterAndLoginAsync(
        HttpClient client,
        string email,
        string password,
        string role,
        string? companyName = null)
    {
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = role == Roles.Employer ? "Employer User" : "Candidate User",
            role,
            companyName
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        if (role == Roles.Candidate)
        {
            await client.PutAsJsonAsync("/api/candidates/me/profile", new
            {
                headline = "Software Developer",
                bio = "Passionate engineer",
                skillIds = new[] { 1 } // ASP.NET Core
            });
        }
    }
}
