using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Application.Applications;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;
using Xunit;

namespace SkillBridge.Auth.Tests;

public sealed class TestRealtimeNotifier : IRealtimeNotifier
{
    public List<ApplicationSubmittedRealtimeEvent> SubmittedEvents { get; } = [];
    public List<ApplicationStatusChangedRealtimeEvent> StatusChangedEvents { get; } = [];
    public List<ApplicationWithdrawnRealtimeEvent> WithdrawnEvents { get; } = [];
    public List<JobCreatedRealtimeEvent> JobCreatedEvents { get; } = [];
    public List<CandidateProfileUpdatedRealtimeEvent> ProfileUpdatedEvents { get; } = [];

    public Task NotifyApplicationSubmittedAsync(ApplicationSubmittedRealtimeEvent @event, CancellationToken cancellationToken = default)
    {
        SubmittedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public Task NotifyApplicationWithdrawnAsync(ApplicationWithdrawnRealtimeEvent @event, CancellationToken cancellationToken = default)
    {
        WithdrawnEvents.Add(@event);
        return Task.CompletedTask;
    }

    public Task NotifyApplicationStatusChangedAsync(ApplicationStatusChangedRealtimeEvent @event, CancellationToken cancellationToken = default)
    {
        StatusChangedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public Task NotifyJobCreatedAsync(JobCreatedRealtimeEvent @event, CancellationToken cancellationToken = default)
    {
        JobCreatedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public Task NotifyCandidateProfileUpdatedAsync(CandidateProfileUpdatedRealtimeEvent @event, CancellationToken cancellationToken = default)
    {
        ProfileUpdatedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public Task SendNotificationAsync(string userId, NotificationRealtimeEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

[Collection(AuthCollection.Name)]
public sealed class RealtimeInteractionsTests(AuthFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task SignalR_negotiate_endpoint_succeeds_with_auth_token()
    {
        // 1. Authenticate a user
        var candidateEmail = $"signalr-{Guid.NewGuid():N}@realtime.test";
        const string password = "TestPassword123!";
        using var client = fixture.Factory.CreateClient();

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = candidateEmail,
            password,
            fullName = "Realtime Tester",
            role = Roles.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email = candidateEmail, password });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth?.AccessToken);

        // 2. Perform SignalR negotiation with access token
        using var negotiateRequest = new HttpRequestMessage(HttpMethod.Post, $"/hubs/app/negotiate?negotiateVersion=1&access_token={auth.AccessToken}");
        negotiateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var negotiateResponse = await client.SendAsync(negotiateRequest);
        Assert.Equal(HttpStatusCode.OK, negotiateResponse.StatusCode);

        var json = await negotiateResponse.Content.ReadAsStringAsync();
        Assert.Contains("connectionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("availableTransports", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Realtime_events_dispatched_with_live_counts_during_application_lifecycle()
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

        var employerEmail = $"emp-{Guid.NewGuid():N}@eventlifecycle.test";
        var candidateEmail = $"cand-{Guid.NewGuid():N}@eventlifecycle.test";
        const string password = "TestPassword123!";

        // Register & login
        await employerClient.PostAsJsonAsync("/api/auth/register", new { email = employerEmail, password, fullName = "Emp Realtime", role = Roles.Employer, companyName = "Realtime Corp" });
        var empLoginRes = await employerClient.PostAsJsonAsync("/api/auth/login", new { email = employerEmail, password });
        var empAuth = await empLoginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        employerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empAuth!.AccessToken);

        await candidateClient.PostAsJsonAsync("/api/auth/register", new { email = candidateEmail, password, fullName = "Cand Realtime", role = Roles.Candidate });
        var candLoginRes = await candidateClient.PostAsJsonAsync("/api/auth/login", new { email = candidateEmail, password });
        var candAuth = await candLoginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        candidateClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candAuth!.AccessToken);

        // Candidate sets profile skills
        var profileRes = await candidateClient.PutAsJsonAsync("/api/candidates/me/profile", new
        {
            headline = "Full Stack Engineer",
            preferredJobType = JobType.FullTime,
            skillIds = new[] { 1, 3 }
        });
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);

        // Employer creates job
        var createJobRes = await employerClient.PostAsJsonAsync("/api/jobs", new
        {
            title = "Realtime Senior Engineer",
            description = "Building real-time event-driven apps.",
            requiredSkillIds = new[] { 1 }
        });
        var createdJob = await createJobRes.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(createdJob);

        Assert.Single(testNotifier.JobCreatedEvents);
        Assert.Equal(createdJob.Id, testNotifier.JobCreatedEvents[0].Job.Id);

        // Candidate applies -> should trigger NotifyApplicationSubmittedAsync
        var applyRes = await candidateClient.PostAsJsonAsync($"/api/jobs/{createdJob.Id}/applications", new { coverNote = "Excited to join!" });
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);

        Assert.Single(testNotifier.SubmittedEvents);
        Assert.Equal(createdJob.Id, testNotifier.SubmittedEvents[0].JobId);
        Assert.Equal(1, testNotifier.SubmittedEvents[0].Counts.Total);
        Assert.Equal(1, testNotifier.SubmittedEvents[0].Counts.Received);
        Assert.Equal(0, testNotifier.SubmittedEvents[0].Counts.Shortlisted);
        Assert.Equal(0, testNotifier.SubmittedEvents[0].Counts.Withdrawn);

        // Candidate withdraws application while in Received status -> should trigger NotifyApplicationWithdrawnAsync
        var withdrawRes = await candidateClient.PatchAsync($"/api/applications/{app.Id}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdrawRes.StatusCode);

        Assert.Single(testNotifier.WithdrawnEvents);
        Assert.Equal(app.Id, testNotifier.WithdrawnEvents[0].ApplicationId);
        Assert.Equal(1, testNotifier.WithdrawnEvents[0].Counts.Total);
        Assert.Equal(1, testNotifier.WithdrawnEvents[0].Counts.Withdrawn);
        Assert.Equal(0, testNotifier.WithdrawnEvents[0].Counts.Received);

        // Candidate 2 applies -> Employer shortlists -> should trigger NotifyApplicationStatusChangedAsync
        var candidate2Email = $"cand2-{Guid.NewGuid():N}@eventlifecycle.test";
        await candidateClient.PostAsJsonAsync("/api/auth/register", new { email = candidate2Email, password, fullName = "Cand 2 Realtime", role = Roles.Candidate });
        var cand2LoginRes = await candidateClient.PostAsJsonAsync("/api/auth/login", new { email = candidate2Email, password });
        var cand2Auth = await cand2LoginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        candidateClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cand2Auth!.AccessToken);

        await candidateClient.PutAsJsonAsync("/api/candidates/me/profile", new
        {
            headline = "Senior Developer",
            preferredJobType = JobType.FullTime,
            skillIds = new[] { 1, 3 }
        });

        var apply2Res = await candidateClient.PostAsJsonAsync($"/api/jobs/{createdJob.Id}/applications", new { coverNote = "Ready to start!" });
        Assert.Equal(HttpStatusCode.Created, apply2Res.StatusCode);
        var app2 = await apply2Res.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app2);

        // Employer shortlists Candidate 2
        var shortlistRes = await employerClient.PatchAsJsonAsync($"/api/applications/{app2.Id}/status", new { status = "Shortlisted" });
        Assert.Equal(HttpStatusCode.OK, shortlistRes.StatusCode);

        Assert.Single(testNotifier.StatusChangedEvents);
        Assert.Equal(app2.Id, testNotifier.StatusChangedEvents[0].ApplicationId);
        Assert.Equal("Shortlisted", testNotifier.StatusChangedEvents[0].NewStatus);
        Assert.Equal(2, testNotifier.StatusChangedEvents[0].Counts.Total);
        Assert.Equal(1, testNotifier.StatusChangedEvents[0].Counts.Shortlisted);
        Assert.Equal(1, testNotifier.StatusChangedEvents[0].Counts.Withdrawn);
        Assert.Equal(0, testNotifier.StatusChangedEvents[0].Counts.Received);
    }
}
