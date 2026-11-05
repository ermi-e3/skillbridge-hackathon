using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;
using SkillBridge.Infrastructure.Persistence;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class JobStatusManagementIntegrationTests(AuthFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Employer_can_create_job_with_deadline_and_query_it()
    {
        using var client = fixture.Factory.CreateClient();
        var employerEmail = $"emp-deadline-{Guid.NewGuid():N}@deadline.test";
        const string password = "TestPassword123!";

        // Register employer
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = employerEmail,
            password,
            fullName = "Deadline Employer",
            role = Roles.Employer,
            companyName = "Deadline Corp"
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email = employerEmail, password });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // 1. Valid future deadline
        var futureDeadline = DateTime.UtcNow.AddDays(7);
        var createJobRes = await client.PostAsJsonAsync("/api/jobs", new
        {
            title = "Senior Platform Engineer",
            description = "Lead our cloud infrastructure and deployment pipelines.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.Senior,
            minExperienceYears = 5,
            requiredSkillIds = new[] { 1 },
            deadline = futureDeadline
        });

        Assert.Equal(HttpStatusCode.Created, createJobRes.StatusCode);
        var createdJob = await createJobRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(createdJob);
        Assert.True(createdJob.IsOpen);
        Assert.NotNull(createdJob.Deadline);

        // 2. Query as job details
        var getJobRes = await client.GetAsync($"/api/jobs/{createdJob.Id}");
        Assert.Equal(HttpStatusCode.OK, getJobRes.StatusCode);
        var fetchedJob = await getJobRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(fetchedJob);
        Assert.Equal(createdJob.Id, fetchedJob.Id);
        Assert.NotNull(fetchedJob.Deadline);
        Assert.True(fetchedJob.IsOpen);

        // 3. Query employer jobs
        var employerJobsRes = await client.GetAsync("/api/employer/jobs");
        Assert.Equal(HttpStatusCode.OK, employerJobsRes.StatusCode);
        var employerJobs = await employerJobsRes.Content.ReadFromJsonAsync<List<EmployerJobDto>>(JsonOptions);
        Assert.NotNull(employerJobs);
        var empJob = employerJobs.FirstOrDefault(j => j.Id == createdJob.Id);
        Assert.NotNull(empJob);
        Assert.NotNull(empJob.Deadline);
        Assert.True(empJob.IsOpen);
    }

    [Fact]
    public async Task Creating_job_with_past_deadline_returns_400_BadRequest()
    {
        using var client = fixture.Factory.CreateClient();
        var employerEmail = $"emp-pastdeadline-{Guid.NewGuid():N}@deadline.test";
        const string password = "TestPassword123!";

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = employerEmail,
            password,
            fullName = "Past Deadline Employer",
            role = Roles.Employer,
            companyName = "Past Corp"
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email = employerEmail, password });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Past deadline
        var pastDeadline = DateTime.UtcNow.AddDays(-2);
        var createJobRes = await client.PostAsJsonAsync("/api/jobs", new
        {
            title = "Invalid Deadline Job",
            description = "Job with past deadline that should fail validation.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 },
            deadline = pastDeadline
        });

        Assert.Equal(HttpStatusCode.BadRequest, createJobRes.StatusCode);
        var errorBody = await createJobRes.Content.ReadAsStringAsync();
        Assert.Contains("future", errorBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employer_can_manually_close_job_broadcasting_realtime_event_and_restricting_applications()
    {
        var testNotifier = new TestRealtimeNotifier();
        var customFactory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IRealtimeNotifier>(_ => testNotifier);
            });
        });

        using var employerClient = customFactory.CreateClient();
        using var candidateClient = customFactory.CreateClient();

        var employerEmail = $"emp-close-{Guid.NewGuid():N}@deadline.test";
        var candidateEmail = $"cand-close-{Guid.NewGuid():N}@deadline.test";
        const string password = "TestPassword123!";

        // Register & login employer
        await employerClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = employerEmail,
            password,
            fullName = "Closing Employer",
            role = Roles.Employer,
            companyName = "Close Corp"
        });
        var empLogin = await employerClient.PostAsJsonAsync("/api/auth/login", new { email = employerEmail, password });
        var empAuth = await empLogin.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        employerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empAuth!.AccessToken);

        // Register & login candidate
        await candidateClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = candidateEmail,
            password,
            fullName = "Candidate Closer",
            role = Roles.Candidate
        });
        var candLogin = await candidateClient.PostAsJsonAsync("/api/auth/login", new { email = candidateEmail, password });
        var candAuth = await candLogin.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        candidateClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candAuth!.AccessToken);

        // Create job
        var createRes = await employerClient.PostAsJsonAsync("/api/jobs", new
        {
            title = "Manual Close Test Role",
            description = "Job to test manual closure feature.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 },
            deadline = DateTime.UtcNow.AddDays(5)
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var job = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);
        Assert.True(job.IsOpen);

        // 1. Employer manually closes job via PATCH /api/jobs/{id}/close
        var closeRes = await employerClient.PatchAsync($"/api/jobs/{job.Id}/close", null);
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);
        var closedJobDto = await closeRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(closedJobDto);
        Assert.False(closedJobDto.IsOpen);

        // 2. Real-time event was dispatched
        var statusEvent = testNotifier.JobStatusChangedEvents.FirstOrDefault(e => e.JobId == job.Id);
        Assert.NotNull(statusEvent);
        Assert.False(statusEvent.IsOpen);

        // 3. Querying job returns IsOpen = false
        var fetchedRes = await candidateClient.GetAsync($"/api/jobs/{job.Id}");
        var fetchedDto = await fetchedRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(fetchedDto);
        Assert.False(fetchedDto.IsOpen);

        // 4. Candidate attempts to apply to closed job: Rejected with 400 jobs.closed
        var applyRes = await candidateClient.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new
        {
            coverNote = "I want to apply please!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, applyRes.StatusCode);
        var applyErr = await applyRes.Content.ReadAsStringAsync();
        Assert.Contains("closed", applyErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Non_owner_cannot_close_job()
    {
        using var client = fixture.Factory.CreateClient();
        var employer1Email = $"emp-owner-{Guid.NewGuid():N}@deadline.test";
        var employer2Email = $"emp-other-{Guid.NewGuid():N}@deadline.test";
        const string password = "TestPassword123!";

        // Register employer 1 (owner)
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = employer1Email,
            password,
            fullName = "Owner Employer",
            role = Roles.Employer,
            companyName = "Owner Corp"
        });
        var login1 = await client.PostAsJsonAsync("/api/auth/login", new { email = employer1Email, password });
        var auth1 = await login1.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth1!.AccessToken);

        // Create job
        var createRes = await client.PostAsJsonAsync("/api/jobs", new
        {
            title = "Ownership Test Job",
            description = "Job for checking close authorization.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 },
            deadline = DateTime.UtcNow.AddDays(3)
        });
        var job = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);

        // Register employer 2 (non-owner)
        using var otherClient = fixture.Factory.CreateClient();
        await otherClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = employer2Email,
            password,
            fullName = "Other Employer",
            role = Roles.Employer,
            companyName = "Other Corp"
        });
        var login2 = await otherClient.PostAsJsonAsync("/api/auth/login", new { email = employer2Email, password });
        var auth2 = await login2.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2!.AccessToken);

        // Attempt to close employer 1's job
        var closeRes = await otherClient.PatchAsync($"/api/jobs/{job.Id}/close", null);
        Assert.Equal(HttpStatusCode.Forbidden, closeRes.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_close_job()
    {
        using var client = fixture.Factory.CreateClient();
        var employerEmail = $"emp-candclose-{Guid.NewGuid():N}@deadline.test";
        var candidateEmail = $"cand-candclose-{Guid.NewGuid():N}@deadline.test";
        const string password = "TestPassword123!";

        // Register employer
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = employerEmail,
            password,
            fullName = "Owner Employer",
            role = Roles.Employer,
            companyName = "Owner Corp"
        });
        var login1 = await client.PostAsJsonAsync("/api/auth/login", new { email = employerEmail, password });
        var auth1 = await login1.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth1!.AccessToken);

        // Create job
        var createRes = await client.PostAsJsonAsync("/api/jobs", new
        {
            title = "Candidate Close Test Job",
            description = "Job for checking close authorization.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 },
            deadline = DateTime.UtcNow.AddDays(3)
        });
        var job = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);

        // Register candidate
        using var candClient = fixture.Factory.CreateClient();
        await candClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = candidateEmail,
            password,
            fullName = "Candidate Try Close",
            role = Roles.Candidate
        });
        var login2 = await candClient.PostAsJsonAsync("/api/auth/login", new { email = candidateEmail, password });
        var auth2 = await login2.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        candClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2!.AccessToken);

        // Candidate attempts to close job
        var closeRes = await candClient.PatchAsync($"/api/jobs/{job.Id}/close", null);
        Assert.Equal(HttpStatusCode.Forbidden, closeRes.StatusCode);
    }

    [Fact]
    public async Task Expired_job_automatically_closes_on_query_and_rejects_applications()
    {
        var testNotifier = new TestRealtimeNotifier();
        var customFactory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IRealtimeNotifier>(_ => testNotifier);
            });
        });

        using var employerClient = customFactory.CreateClient();
        using var candidateClient = customFactory.CreateClient();

        var employerEmail = $"emp-expired-{Guid.NewGuid():N}@deadline.test";
        var candidateEmail = $"cand-expired-{Guid.NewGuid():N}@deadline.test";
        const string password = "TestPassword123!";

        // Register employer
        await employerClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = employerEmail,
            password,
            fullName = "Expire Employer",
            role = Roles.Employer,
            companyName = "Expire Corp"
        });
        var empLogin = await employerClient.PostAsJsonAsync("/api/auth/login", new { email = employerEmail, password });
        var empAuth = await empLogin.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        employerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empAuth!.AccessToken);

        // Register candidate
        await candidateClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = candidateEmail,
            password,
            fullName = "Candidate Expire",
            role = Roles.Candidate
        });
        var candLogin = await candidateClient.PostAsJsonAsync("/api/auth/login", new { email = candidateEmail, password });
        var candAuth = await candLogin.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        candidateClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candAuth!.AccessToken);

        // Create job with future deadline
        var createRes = await employerClient.PostAsJsonAsync("/api/jobs", new
        {
            title = "Auto-Expire Role",
            description = "Job that will be artificially expired in database.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            requiredSkillIds = new[] { 1 },
            deadline = DateTime.UtcNow.AddMinutes(5)
        });
        var job = await createRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);
        Assert.True(job.IsOpen);

        // Artificially simulate that deadline passed in the DB
        using (var scope = customFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbJob = await db.Jobs.FindAsync(job.Id);
            Assert.NotNull(dbJob);

            // Set deadline to past using raw SQL / EF property
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Jobs\" SET \"Deadline\" = {0} WHERE \"Id\" = {1}",
                DateTime.UtcNow.AddHours(-1),
                job.Id);
        }

        // Candidate fetches job: System detects deadline <= UtcNow and auto-closes it!
        var getJobRes = await candidateClient.GetAsync($"/api/jobs/{job.Id}");
        Assert.Equal(HttpStatusCode.OK, getJobRes.StatusCode);
        var autoClosedJob = await getJobRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(autoClosedJob);
        Assert.False(autoClosedJob.IsOpen);

        // SignalR event was dispatched
        var statusEvent = testNotifier.JobStatusChangedEvents.FirstOrDefault(e => e.JobId == job.Id);
        Assert.NotNull(statusEvent);
        Assert.False(statusEvent.IsOpen);

        // Candidate attempts to apply: rejected with 400 jobs.closed
        var applyRes = await candidateClient.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new
        {
            coverNote = "Apply to expired job"
        });
        Assert.Equal(HttpStatusCode.BadRequest, applyRes.StatusCode);
        var applyErr = await applyRes.Content.ReadAsStringAsync();
        Assert.Contains("closed", applyErr, StringComparison.OrdinalIgnoreCase);
    }
}
