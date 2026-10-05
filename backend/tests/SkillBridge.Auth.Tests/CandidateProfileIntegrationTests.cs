using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Domain.Common;
using SkillBridge.Infrastructure.Persistence;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class CandidateProfileIntegrationTests(AuthFixture fixture)
{
    private const string ProfilePath = "/api/candidates/me/profile";

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Profile_requires_authentication(string method)
    {
        using var response = await SendAsync(method, null, ValidInput());
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Employer_cannot_access_candidate_profile(string method)
    {
        var employer = await RegisterAsync(Roles.Employer);
        using var response = await SendAsync(method, employer.AccessToken, ValidInput());
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Fact]
    public async Task Registered_candidate_can_read_their_initial_empty_profile()
    {
        var candidate = await RegisterAsync();
        var profile = await GetProfileAsync(candidate);

        Assert.Equal(candidate.UserId, profile.UserId);
        Assert.Null(profile.Headline);
        Assert.Null(profile.Bio);
        Assert.Null(profile.GitHubUrl);
        Assert.Null(profile.UpdatedAt);
        Assert.Empty(profile.Skills);
    }

    [Fact]
    public async Task Update_trims_fields_persists_a_UTC_timestamp_and_returns_sorted_skill_names()
    {
        var candidate = await RegisterAsync();
        var before = DateTimeOffset.UtcNow;
        using var response = await SendAsync("PUT", candidate.AccessToken, new ProfileInput(
            "  Backend developer  ", "  Builds APIs  ", "  https://github.com/test-candidate  ", [3, 1, 2]));
        var profile = await ReadProfileAsync(response);

        Assert.Equal(candidate.UserId, profile.UserId);
        Assert.Equal("Backend developer", profile.Headline);
        Assert.Equal("Builds APIs", profile.Bio);
        Assert.Equal("https://github.com/test-candidate", profile.GitHubUrl);
        Assert.NotNull(profile.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, profile.UpdatedAt.Value.Offset);
        Assert.InRange(profile.UpdatedAt.Value, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal([1, 2, 3], profile.Skills.Select(skill => skill.Id).ToArray());
        Assert.Equal(["ASP.NET Core", "Angular", "C#"], profile.Skills.Select(skill => skill.Name).ToArray());
        AssertSameProfile(profile, await GetProfileAsync(candidate));
        await AssertPersistedAsync(profile);
    }

    [Fact]
    public async Task Replacing_skills_removes_orphans_preserves_overlap_and_repeated_updates_do_not_duplicate_rows()
    {
        var candidate = await RegisterAsync();
        using (var initial = await SendAsync("PUT", candidate.AccessToken,
                   ValidInput() with { SkillIds = [1, 2, 3] }))
            await ReadProfileAsync(initial);

        var replacement = ValidInput() with { SkillIds = [5, 2] };
        using var replaced = await SendAsync("PUT", candidate.AccessToken, replacement);
        var profile = await ReadProfileAsync(replaced);
        Assert.Equal([2, 5], profile.Skills.Select(skill => skill.Id).ToArray());
        Assert.Equal(["Angular", "PostgreSQL"], profile.Skills.Select(skill => skill.Name).ToArray());
        await AssertPersistedAsync(profile);

        using var repeated = await SendAsync("PUT", candidate.AccessToken, replacement);
        var repeatedProfile = await ReadProfileAsync(repeated);
        Assert.Equal(profile.Headline, repeatedProfile.Headline);
        Assert.Equal(profile.Skills.Select(skill => skill.Id), repeatedProfile.Skills.Select(skill => skill.Id));
        await AssertPersistedAsync(repeatedProfile);
    }

    public static IEnumerable<object[]> InvalidUpdates()
    {
        var valid = ValidInput();
        yield return [valid with { Headline = null }, "headline"];
        yield return [valid with { Headline = "   " }, "headline"];
        yield return [valid with { Headline = new string('h', 121) }, "headline"];
        yield return [valid with { Bio = new string('b', 2001) }, "bio"];
        yield return [valid with { GitHubUrl = "https://example.com/" + new string('x', 282) }, "gitHubUrl"];
        yield return [valid with { GitHubUrl = "/relative/profile" }, "gitHubUrl"];
        yield return [valid with { GitHubUrl = "ftp://example.com/profile" }, "gitHubUrl"];
        yield return [valid with { GitHubUrl = "javascript:alert(1)" }, "gitHubUrl"];
        yield return [valid with { SkillIds = null }, "skillIds"];
        yield return [valid with { SkillIds = [] }, "skillIds"];
        yield return [valid with { SkillIds = Enumerable.Range(1, 31).ToArray() }, "skillIds"];
        yield return [valid with { SkillIds = [1, 1] }, "skillIds"];
        yield return [valid with { SkillIds = [0] }, "skillIds"];
        yield return [valid with { SkillIds = [-1] }, "skillIds"];
        yield return [valid with { SkillIds = [1, int.MaxValue] }, "skillIds"];
    }

    [Theory]
    [MemberData(nameof(InvalidUpdates))]
    public async Task Invalid_update_returns_field_errors_and_leaves_the_entire_profile_unchanged(
        ProfileInput input, string field)
    {
        var candidate = await RegisterAsync();
        using var initial = await SendAsync("PUT", candidate.AccessToken, ValidInput());
        var before = await ReadProfileAsync(initial);

        using var response = await SendAsync("PUT", candidate.AccessToken, input);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed", field);
        AssertSameProfile(before, await GetProfileAsync(candidate));
        await AssertPersistedAsync(before);
    }

    [Theory]
    [InlineData("headline")]
    [InlineData("skillIds")]
    public async Task Missing_required_properties_return_field_validation_errors(string field)
    {
        var candidate = await RegisterAsync();
        var body = new Dictionary<string, object?>
        {
            ["headline"] = "Backend developer", ["bio"] = null,
            ["gitHubUrl"] = null, ["skillIds"] = new[] { 1 }
        };
        body.Remove(field);
        using var response = await SendAsync("PUT", candidate.AccessToken, body);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed", field);
        var profile = await GetProfileAsync(candidate);
        Assert.Null(profile.Headline);
        Assert.Null(profile.UpdatedAt);
        Assert.Empty(profile.Skills);
    }

    [Fact]
    public async Task Optional_blank_fields_are_normalized_to_null()
    {
        var candidate = await RegisterAsync();
        using var response = await SendAsync("PUT", candidate.AccessToken,
            ValidInput() with { Bio = "  \t\r\n ", GitHubUrl = "  " });
        var profile = await ReadProfileAsync(response);
        Assert.Null(profile.Bio);
        Assert.Null(profile.GitHubUrl);
        await AssertPersistedAsync(profile);
    }

    [Fact]
    public async Task Maximum_field_lengths_and_thirty_existing_skills_are_accepted()
    {
        var candidate = await RegisterAsync();
        var input = new ProfileInput(new string('h', 120), new string('b', 2000),
            "https://example.com/" + new string('x', 280), Enumerable.Range(1, 30).Reverse().ToArray());
        Assert.Equal(300, input.GitHubUrl!.Length);
        using var response = await SendAsync("PUT", candidate.AccessToken, input);
        var profile = await ReadProfileAsync(response);
        Assert.Equal(input.Headline, profile.Headline);
        Assert.Equal(input.Bio, profile.Bio);
        Assert.Equal(input.GitHubUrl, profile.GitHubUrl);
        Assert.Equal(Enumerable.Range(1, 30), profile.Skills.Select(skill => skill.Id));
        Assert.All(profile.Skills, skill => Assert.False(string.IsNullOrWhiteSpace(skill.Name)));
        await AssertPersistedAsync(profile);
    }

    [Theory]
    [InlineData("http://example.com/portfolio")]
    [InlineData("https://example.com/portfolio")]
    public async Task Profile_URL_accepts_absolute_HTTP_and_HTTPS_without_requiring_a_GitHub_host(string url)
    {
        var candidate = await RegisterAsync();
        using var response = await SendAsync("PUT", candidate.AccessToken, ValidInput() with { GitHubUrl = url });
        var profile = await ReadProfileAsync(response);
        Assert.Equal(url, profile.GitHubUrl);
        await AssertPersistedAsync(profile);
    }

    [Fact]
    public async Task Candidate_access_is_isolated_to_token_identity_and_ignores_spoofed_user_IDs()
    {
        var first = await RegisterAsync();
        var second = await RegisterAsync();
        using var secondUpdate = await SendAsync("PUT", second.AccessToken,
            ValidInput() with { Headline = "Other candidate", SkillIds = [5] });
        var secondBefore = await ReadProfileAsync(secondUpdate);

        var queryPath = $"{ProfilePath}?userId={Uri.EscapeDataString(second.UserId)}";
        using var firstGet = await SendAsync("GET", first.AccessToken, path: queryPath);
        var firstInitial = await ReadProfileAsync(firstGet);
        Assert.Equal(first.UserId, firstInitial.UserId);
        Assert.Null(firstInitial.Headline);

        using var firstUpdate = await SendAsync("PUT", first.AccessToken, new
        {
            userId = second.UserId, headline = "First candidate", bio = "Self only",
            gitHubUrl = (string?)null, skillIds = new[] { 1, 2 }
        }, queryPath);
        var firstAfter = await ReadProfileAsync(firstUpdate);
        Assert.Equal(first.UserId, firstAfter.UserId);
        Assert.Equal("First candidate", firstAfter.Headline);
        await AssertPersistedAsync(firstAfter);
        AssertSameProfile(secondBefore, await GetProfileAsync(second));
        await AssertPersistedAsync(secondBefore);

        using var otherUserPath = await SendAsync("GET", first.AccessToken,
            path: $"/api/candidates/{Uri.EscapeDataString(second.UserId)}/profile");
        Assert.Equal(HttpStatusCode.NotFound, otherUserPath.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Missing_current_candidate_profile_returns_a_safe_not_found_error(string method)
    {
        var candidate = await RegisterAsync();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Remove only this test's newly registered, still-empty profile.
            var ownedProfile = await database.CandidateProfiles.SingleAsync(p => p.UserId == candidate.UserId);
            database.CandidateProfiles.Remove(ownedProfile);
            await database.SaveChangesAsync();
        }

        using var response = await SendAsync(method, candidate.AccessToken, ValidInput());
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "candidate.profile_not_found");
    }

    [Fact]
    public async Task Swagger_documents_both_protected_profile_operations_and_Scalar_uses_the_same_document()
    {
        using var response = await fixture.Client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var bearer = root.GetProperty("components").GetProperty("securitySchemes").EnumerateObject()
            .Single(scheme => scheme.Value.GetProperty("type").GetString() == "http"
                              && scheme.Value.GetProperty("scheme").GetString() == "bearer");
        var path = root.GetProperty("paths").GetProperty(ProfilePath);
        foreach (var method in new[] { "get", "put" })
        {
            var operation = path.GetProperty(method);
            Assert.Contains(operation.GetProperty("security").EnumerateArray(),
                requirement => requirement.TryGetProperty(bearer.Name, out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        }

        using var scalar = await fixture.Client.GetAsync("/scalar/");
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        Assert.Contains("swagger/v1/swagger.json", await scalar.Content.ReadAsStringAsync());
    }

    private static ProfileInput ValidInput() => new("Backend developer", "Builds APIs", null, [1, 2]);

    private async Task<Account> RegisterAsync(string role = Roles.Candidate)
    {
        using var response = await fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"candidate-profile-{Guid.NewGuid():N}@tests.local", password = "Test1234",
            fullName = "Profile Test User", role, companyName = role == Roles.Employer ? "Profile Test Company" : null
        });
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Registration returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Account>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string? token, object? body = null,
        string path = ProfilePath)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method == "PUT")
            request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private async Task<ProfileResponse> GetProfileAsync(Account account)
    {
        using var response = await SendAsync("GET", account.AccessToken);
        return await ReadProfileAsync(response);
    }

    private static async Task<ProfileResponse> ReadProfileAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Profile returned {(int)response.StatusCode}: {json}");
        using var document = JsonDocument.Parse(json);
        foreach (var field in new[] { "userId", "headline", "bio", "gitHubUrl", "updatedAt", "skills" })
            Assert.True(document.RootElement.TryGetProperty(field, out _), $"Profile is missing {field}.");
        var profile = JsonSerializer.Deserialize<ProfileResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(profile);
        Assert.NotNull(profile.Skills);
        Assert.Equal(profile.Skills.Select(skill => skill.Id).OrderBy(id => id), profile.Skills.Select(skill => skill.Id));
        return profile;
    }

    private async Task AssertPersistedAsync(ProfileResponse expected)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actual = await database.CandidateProfiles.AsNoTracking().Include(p => p.Skills).ThenInclude(s => s.Skill)
            .SingleAsync(p => p.UserId == expected.UserId);
        Assert.Equal(expected.Headline, actual.Headline);
        Assert.Equal(expected.Bio, actual.Bio);
        Assert.Equal(expected.GitHubUrl, actual.GitHubUrl);
        AssertSameTimestamp(expected.UpdatedAt,
            actual.UpdatedAt is null ? null : new DateTimeOffset(actual.UpdatedAt.Value));
        if (actual.UpdatedAt is not null)
            Assert.Equal(DateTimeKind.Utc, actual.UpdatedAt.Value.Kind);
        Assert.Equal(expected.Skills.Select(skill => skill.Id), actual.Skills.OrderBy(skill => skill.SkillId).Select(skill => skill.SkillId));
        Assert.Equal(expected.Skills.Select(skill => skill.Name), actual.Skills.OrderBy(skill => skill.SkillId).Select(skill => skill.Skill.Name));
        Assert.Equal(expected.Skills.Length,
            await database.CandidateSkills.CountAsync(skill => skill.CandidateId == expected.UserId));
    }

    private static void AssertSameProfile(ProfileResponse expected, ProfileResponse actual)
    {
        Assert.Equal(expected.UserId, actual.UserId);
        Assert.Equal(expected.Headline, actual.Headline);
        Assert.Equal(expected.Bio, actual.Bio);
        Assert.Equal(expected.GitHubUrl, actual.GitHubUrl);
        AssertSameTimestamp(expected.UpdatedAt, actual.UpdatedAt);
        Assert.Equal(expected.Skills.Select(skill => skill.Id), actual.Skills.Select(skill => skill.Id));
        Assert.Equal(expected.Skills.Select(skill => skill.Name), actual.Skills.Select(skill => skill.Name));
    }

    private static void AssertSameTimestamp(DateTimeOffset? expected, DateTimeOffset? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }
        Assert.NotNull(actual);
        // PostgreSQL stores microseconds; DateTime can contain finer 100ns ticks.
        Assert.InRange(Math.Abs((expected.Value - actual.Value).Ticks), 0, 10);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status,
        string code, string? field = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        if (field is not null)
        {
            var errors = problem.GetProperty("errors");
            Assert.True(errors.TryGetProperty(field, out var messages), $"Expected errors for {field}: {errors}");
            Assert.NotEmpty(messages.EnumerateArray());
        }
    }

    public sealed record ProfileInput(string? Headline, string? Bio, string? GitHubUrl, int[]? SkillIds);
    private sealed record Account(string UserId, string AccessToken);
    private sealed record ProfileResponse(string UserId, string? Headline, string? Bio, string? GitHubUrl,
        DateTimeOffset? UpdatedAt, SkillResponse[] Skills);
    private sealed record SkillResponse(int Id, string Name);
}
