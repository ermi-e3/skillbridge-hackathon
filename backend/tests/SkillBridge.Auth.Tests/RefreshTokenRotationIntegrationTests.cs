using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Application.Auth;
using SkillBridge.Domain.Common;
using SkillBridge.Infrastructure.Persistence;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class RefreshTokenRotationIntegrationTests(AuthFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Login_generates_15min_access_token_and_sets_HttpOnly_refresh_token_cookie()
    {
        using var client = fixture.Factory.CreateClient();
        var email = $"login-ref-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Refresh Tester",
            role = Roles.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth?.AccessToken);

        // Access token expires in ~15 minutes
        var now = DateTime.UtcNow;
        var tokenLifetime = auth.ExpiresAt - now;
        Assert.True(tokenLifetime.TotalMinutes >= 14 && tokenLifetime.TotalMinutes <= 16,
            $"Expected ~15 minute lifetime, got {tokenLifetime.TotalMinutes} minutes.");

        // Cookie headers
        Assert.True(loginRes.Headers.Contains("Set-Cookie"));
        var cookies = loginRes.Headers.GetValues("Set-Cookie").ToList();

        var refreshCookie = cookies.FirstOrDefault(c => c.StartsWith("refreshToken="));
        Assert.NotNull(refreshCookie);
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", refreshCookie, StringComparison.OrdinalIgnoreCase);

        var xsrfCookie = cookies.FirstOrDefault(c => c.StartsWith("XSRF-TOKEN="));
        Assert.NotNull(xsrfCookie);
        Assert.Contains("path=/", xsrfCookie, StringComparison.OrdinalIgnoreCase);

        // Verify database record
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedToken = await db.RefreshTokens
            .Where(t => t.UserId == auth.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(storedToken);
        Assert.Null(storedToken.RevokedAt);
        Assert.NotEqual(Guid.Empty, storedToken.FamilyId);
        // Refresh token expires in ~7 days
        var refreshLifetime = storedToken.ExpiresAt - storedToken.CreatedAt;
        Assert.True(refreshLifetime.TotalDays >= 6.9 && refreshLifetime.TotalDays <= 7.1);
    }

    [Fact]
    public async Task Refresh_rotation_succeeds_revokes_old_token_and_issues_new_token_in_same_family()
    {
        using var client = fixture.Factory.CreateClient();
        var email = $"rotate-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Rotate Tester",
            role = Roles.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        var (firstRefreshToken, firstCsrfToken) = ExtractCookies(registerRes);
        Assert.NotNull(firstRefreshToken);
        Assert.NotNull(firstCsrfToken);

        // Perform Refresh Request
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refreshToken={firstRefreshToken}; XSRF-TOKEN={firstCsrfToken}");
        refreshRequest.Headers.Add("X-XSRF-TOKEN", firstCsrfToken);
        refreshRequest.Content = JsonContent.Create(new { });

        var refreshRes = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);

        var newAuth = await refreshRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(newAuth?.AccessToken);

        var (secondRefreshToken, secondCsrfToken) = ExtractCookies(refreshRes);
        Assert.NotNull(secondRefreshToken);
        Assert.NotEqual(firstRefreshToken, secondRefreshToken);

        // Verify in database: first token is revoked with "Rotated", second token is active in the same family
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userTokens = await db.RefreshTokens
            .Where(t => t.UserId == newAuth.UserId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        Assert.True(userTokens.Count >= 2);
        var oldToken = userTokens[^2];
        var latestToken = userTokens[^1];

        Assert.NotNull(oldToken.RevokedAt);
        Assert.Equal("Rotated", oldToken.RevokedReason);
        Assert.Equal(latestToken.Id, oldToken.ReplacedByTokenId);

        Assert.Null(latestToken.RevokedAt);
        Assert.Equal(oldToken.FamilyId, latestToken.FamilyId);
    }

    [Fact]
    public async Task Refresh_token_reuse_detection_revokes_entire_token_family()
    {
        using var client = fixture.Factory.CreateClient();
        var email = $"reuse-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Reuse Tester",
            role = Roles.Candidate
        });
        var (t1, csrf1) = ExtractCookies(registerRes);

        // Rotate t1 -> t2
        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req1.Headers.Add("Cookie", $"refreshToken={t1}; XSRF-TOKEN={csrf1}");
        req1.Headers.Add("X-XSRF-TOKEN", csrf1);
        req1.Content = JsonContent.Create(new { });

        var res1 = await client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var (t2, csrf2) = ExtractCookies(res1);

        // Now an attacker attempts to reuse the already-rotated t1!
        using var reuseReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        reuseReq.Headers.Add("Cookie", $"XSRF-TOKEN={csrf2}");
        reuseReq.Headers.Add("X-XSRF-TOKEN", csrf2);
        reuseReq.Content = JsonContent.Create(new { refreshToken = t1 });

        var reuseRes = await client.SendAsync(reuseReq);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseRes.StatusCode);
        var body = await reuseRes.Content.ReadAsStringAsync();
        Assert.Contains("auth.refresh_token_reuse", body);

        // Verify in DB that the entire family (including t2) has been revoked
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        var familyTokens = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        Assert.All(familyTokens, token =>
        {
            Assert.NotNull(token.RevokedAt);
        });

        // Even t2 (the latest token) should now fail to refresh because its family was revoked
        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req2.Headers.Add("Cookie", $"XSRF-TOKEN={csrf2}");
        req2.Headers.Add("X-XSRF-TOKEN", csrf2);
        req2.Content = JsonContent.Create(new { refreshToken = t2 });

        var res2 = await client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Unauthorized, res2.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_clears_cookies()
    {
        using var client = fixture.Factory.CreateClient();
        var email = $"logout-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Logout Tester",
            role = Roles.Candidate
        });
        var (refreshToken, csrfToken) = ExtractCookies(registerRes);

        // Call Logout
        using var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutReq.Headers.Add("Cookie", $"refreshToken={refreshToken}; XSRF-TOKEN={csrfToken}");
        logoutReq.Headers.Add("X-XSRF-TOKEN", csrfToken);

        var logoutRes = await client.SendAsync(logoutReq);
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        // Verify Set-Cookie clears the refresh token cookie (expires in past)
        var cookies = logoutRes.Headers.GetValues("Set-Cookie").ToList();
        var clearRefreshCookie = cookies.FirstOrDefault(c => c.StartsWith("refreshToken="));
        Assert.NotNull(clearRefreshCookie);

        // Verify token in database is revoked with "Logout"
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userToken = await db.RefreshTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(userToken);
        Assert.NotNull(userToken.RevokedAt);
        Assert.Equal("Logout", userToken.RevokedReason);
    }

    [Fact]
    public async Task Refresh_fails_with_403_when_csrf_token_mismatches()
    {
        using var client = fixture.Factory.CreateClient();
        var email = $"csrf-fail-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "CSRF Tester",
            role = Roles.Candidate
        });
        var (refreshToken, csrfToken) = ExtractCookies(registerRes);

        // Send refresh with mismatched X-XSRF-TOKEN
        using var refreshReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshReq.Headers.Add("Cookie", $"refreshToken={refreshToken}; XSRF-TOKEN={csrfToken}");
        refreshReq.Headers.Add("X-XSRF-TOKEN", "invalid-csrf-token");
        refreshReq.Content = JsonContent.Create(new { });

        var refreshRes = await client.SendAsync(refreshReq);
        Assert.Equal(HttpStatusCode.Forbidden, refreshRes.StatusCode);
    }

    private static (string? RefreshToken, string? CsrfToken) ExtractCookies(HttpResponseMessage response)
    {
        string? refreshToken = null;
        string? csrfToken = null;

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                var parts = cookie.Split(';')[0].Split('=');
                if (parts.Length == 2)
                {
                    if (parts[0].Trim() == "refreshToken")
                        refreshToken = parts[1].Trim();
                    else if (parts[0].Trim() == "XSRF-TOKEN")
                        csrfToken = parts[1].Trim();
                }
            }
        }

        return (refreshToken, csrfToken);
    }
}
