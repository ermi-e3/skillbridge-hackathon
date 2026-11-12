using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Application.Auth;
using SkillBridge.Domain.Common;
using SkillBridge.Infrastructure.Identity;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class SecurityHardeningIntegrationTests(AuthFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Account_is_locked_after_5_consecutive_failed_logins_and_returns_423()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.1.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}");
        var email = $"lockout-{Guid.NewGuid():N}@test.com";
        const string correctPassword = "TestPassword123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = correctPassword,
            fullName = "Lockout Candidate",
            role = Roles.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        // First 4 failed attempts should return 401 Unauthorized
        for (var i = 1; i <= 4; i++)
        {
            var failedRes = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "WrongPassword999!"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failedRes.StatusCode);
        }

        // 5th failed attempt triggers lockout and returns 423 Locked
        var fifthAttempt = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword999!"
        });
        Assert.Equal((HttpStatusCode)423, fifthAttempt.StatusCode);

        // Subsequent attempt from another client/IP (fresh rate-limit bucket) is still locked and returns 423
        using var secondClient = fixture.Factory.CreateClient();
        secondClient.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.2.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}");
        var subsequentAttempt = await secondClient.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = correctPassword
        });
        Assert.Equal((HttpStatusCode)423, subsequentAttempt.StatusCode);
    }

    [Fact]
    public async Task Successful_login_resets_failed_access_count()
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.3.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}");
        var email = $"reset-count-{Guid.NewGuid():N}@test.com";
        const string correctPassword = "TestPassword123!";

        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = correctPassword,
            fullName = "Reset Count Candidate",
            role = Roles.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        // 3 failed attempts
        for (var i = 0; i < 3; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "WrongPassword!"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }

        // 4th attempt is successful
        var successRes = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = correctPassword
        });
        Assert.Equal(HttpStatusCode.OK, successRes.StatusCode);

        // Verify in Identity store that AccessFailedCount is 0
        using var scope = fixture.Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        var failedCount = await userManager.GetAccessFailedCountAsync(user);
        Assert.Equal(0, failedCount);
    }

    [Fact]
    public async Task Rate_limiting_returns_429_too_many_requests_when_exceeded_with_retry_after()
    {
        // Use a unique client with specific X-Forwarded-For IP to test its rate limit window
        using var client = fixture.Factory.CreateClient();
        var customIp = $"198.51.100.{Random.Shared.Next(1, 250)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", customIp);

        // Make 5 login attempts (status doesn't matter for rate limiting, but each consumes 1 permit)
        for (var i = 1; i <= 5; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = $"rate-{Guid.NewGuid():N}@test.com",
                password = "Password123!"
            });
            Assert.NotEqual((HttpStatusCode)429, res.StatusCode);
        }

        // 6th attempt within the 1-minute window must return 429 Too Many Requests
        var rateLimitedRes = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"rate-blocked-{Guid.NewGuid():N}@test.com",
            password = "Password123!"
        });

        Assert.Equal((HttpStatusCode)429, rateLimitedRes.StatusCode);
        Assert.True(rateLimitedRes.Headers.Contains("Retry-After") || rateLimitedRes.Headers.RetryAfter != null);
    }

    [Fact]
    public async Task Rate_limiting_works_independently_of_account_lockout()
    {
        // Rate limiting restricts client IP regardless of different user accounts
        using var client = fixture.Factory.CreateClient();
        var customIp = $"203.0.113.{Random.Shared.Next(1, 250)}";
        client.DefaultRequestHeaders.Add("X-Forwarded-For", customIp);

        for (var i = 1; i <= 5; i++)
        {
            var res = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = $"user-{i}-{Guid.NewGuid():N}@test.com",
                password = "Password123!"
            });
            Assert.NotEqual((HttpStatusCode)429, res.StatusCode);
        }

        // 6th attempt with yet another user is still throttled with 429
        var throttled = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"user-6-{Guid.NewGuid():N}@test.com",
            password = "Password123!"
        });
        Assert.Equal((HttpStatusCode)429, throttled.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_Bearer_token_and_reject_missing_or_invalid_tokens()
    {
        using var client = fixture.Factory.CreateClient();

        // 1. Missing Authorization header -> 401
        var noTokenRes = await client.GetAsync("/api/applications/my");
        Assert.Equal(HttpStatusCode.Unauthorized, noTokenRes.StatusCode);

        // 2. Invalid / forged token -> 401
        var invalidReq = new HttpRequestMessage(HttpMethod.Get, "/api/applications/my");
        invalidReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token.string");
        var invalidRes = await client.SendAsync(invalidReq);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidRes.StatusCode);
    }

    [Fact]
    public async Task Public_endpoints_permit_anonymous_access()
    {
        using var client = fixture.Factory.CreateClient();

        var healthRes = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);

        var skillsRes = await client.GetAsync("/api/skills");
        Assert.Equal(HttpStatusCode.OK, skillsRes.StatusCode);

        var csrfRes = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfRes.StatusCode);
    }

    [Fact]
    public async Task Api_responses_include_mandatory_security_headers()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify X-Frame-Options: DENY
        Assert.True(response.Headers.Contains("X-Frame-Options"), "Response missing X-Frame-Options header");
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());

        // Verify X-Content-Type-Options: nosniff
        Assert.True(response.Headers.Contains("X-Content-Type-Options"), "Response missing X-Content-Type-Options header");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }
}
