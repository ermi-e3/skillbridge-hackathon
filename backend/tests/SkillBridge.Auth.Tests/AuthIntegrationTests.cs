using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SkillBridge.Domain.Common;
using SkillBridge.Infrastructure.Identity;
using SkillBridge.Infrastructure.Persistence;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class AuthIntegrationTests(AuthFixture fixture)
{
    private const string Password = "Test1234"; // Symbols are intentionally optional in the contract.

    [Theory]
    [InlineData(Roles.Candidate)]
    [InlineData(Roles.Employer)]
    public async Task Registration_persists_identity_and_correct_profile_then_login_and_me_work(string role)
    {
        var input = ValidRegistration(role);
        // Candidates cannot supply employer data through the same registration endpoint.
        input = input with { CompanyName = "SkillBridge Test Company" };
        var registered = await RegisterAsync(input);

        Assert.Equal(input.Email, registered.Email);
        Assert.Equal(input.FullName, registered.FullName);
        Assert.Equal(role, registered.Role);
        Assert.Equal(role == Roles.Employer ? input.CompanyName : null, registered.CompanyName);
        Assert.False(string.IsNullOrWhiteSpace(registered.UserId));
        AssertToken(registered);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await manager.FindByIdAsync(registered.UserId);
            Assert.NotNull(user);
            Assert.True(await manager.CheckPasswordAsync(user, Password));
            Assert.NotEqual(Password, user.PasswordHash);
            Assert.Equal([role], await manager.GetRolesAsync(user));
            Assert.Equal(role == Roles.Candidate,
                await database.CandidateProfiles.AnyAsync(profile => profile.UserId == registered.UserId));
            if (role == Roles.Candidate)
            {
                var profile = await database.CandidateProfiles.SingleAsync(p => p.UserId == registered.UserId);
                Assert.True(string.IsNullOrEmpty(profile.Headline));
                Assert.Null(profile.Bio);
                Assert.Null(profile.GitHubUrl);
                Assert.Empty(profile.Skills);
            }
        }

        var login = await LoginAsync(input.Email!.ToUpperInvariant(), Password);
        Assert.Equal(registered.UserId, login.UserId);
        Assert.Equal(registered.Role, login.Role);
        AssertToken(login);

        using var me = await GetAuthorizedAsync("/api/auth/me", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var current = await me.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(current);
        Assert.Equal(registered.UserId, current.UserId);
        Assert.Equal(registered.Email, current.Email);
        Assert.Equal(registered.FullName, current.FullName);
        Assert.Equal(registered.Role, current.Role);
        Assert.Equal(registered.CompanyName, current.CompanyName);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_case_insensitively_without_an_extra_profile()
    {
        var input = ValidRegistration(Roles.Candidate);
        var registered = await RegisterAsync(input);
        using var duplicate = await fixture.Client.PostAsJsonAsync("/api/auth/register",
            input with { Email = input.Email!.ToUpperInvariant() });
        await AssertProblemAsync(duplicate, HttpStatusCode.BadRequest, "auth.email_taken",
            "This email is already registered.");
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await database.Users.CountAsync(u => u.NormalizedEmail == input.Email!.ToUpperInvariant()));
        Assert.Equal(1, await database.CandidateProfiles.CountAsync(p => p.UserId == registered.UserId));
    }

    [Fact]
    public async Task Concurrent_case_insensitive_registration_has_one_winner_and_one_complete_account()
    {
        var input = ValidRegistration(Roles.Candidate);
        var requests = await Task.WhenAll(
            fixture.Client.PostAsJsonAsync("/api/auth/register", input),
            fixture.Client.PostAsJsonAsync("/api/auth/register", input with { Email = input.Email!.ToUpperInvariant() }));
        using var first = requests[0];
        using var second = requests[1];
        var success = Assert.Single(requests, response => response.StatusCode == HttpStatusCode.OK);
        var duplicate = Assert.Single(requests, response => response.StatusCode == HttpStatusCode.BadRequest);
        await AssertProblemAsync(duplicate, HttpStatusCode.BadRequest, "auth.email_taken",
            "This email is already registered.");
        var account = await success.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(account);
        AssertToken(account);

        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await database.Users.CountAsync(u => u.NormalizedEmail == input.Email!.ToUpperInvariant()));
        Assert.Equal(1, await database.CandidateProfiles.CountAsync(p => p.UserId == account.UserId));
        Assert.Equal(1, await database.UserRoles.CountAsync(r => r.UserId == account.UserId));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("candidate")]
    [InlineData("Manager")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Registration_rejects_roles_outside_the_exact_whitelist(string? role)
    {
        var input = ValidRegistration(Roles.Candidate) with { Role = role };
        using var result = await fixture.Client.PostAsJsonAsync("/api/auth/register", input);
        await AssertProblemAsync(result, HttpStatusCode.BadRequest, "auth.invalid_role",
            "Role must be Candidate or Employer.");
        await AssertNoUserAsync(input.Email!);
    }

    [Fact]
    public async Task Registration_requires_role_when_the_json_property_is_missing()
    {
        var input = ValidRegistration(Roles.Candidate);
        using var response = await fixture.Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = input.Email,
            password = input.Password,
            fullName = input.FullName
        });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "auth.invalid_role",
            "Role must be Candidate or Employer.");
        await AssertNoUserAsync(input.Email!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Employer_registration_requires_a_company(string? company)
    {
        var input = ValidRegistration(Roles.Employer) with { CompanyName = company };
        using var result = await fixture.Client.PostAsJsonAsync("/api/auth/register", input);
        await AssertProblemAsync(result, HttpStatusCode.BadRequest, "auth.company_required",
            "Employers must provide a company name.");
        await AssertNoUserAsync(input.Email!);
    }

    public static IEnumerable<object[]> InvalidRegistrations()
    {
        var valid = new RegisterInput("validation@tests.local", Password, "Test Candidate", Roles.Candidate, null);
        yield return [valid with { Email = null }];
        yield return [valid with { Email = "not-an-email" }];
        yield return [valid with { Email = new string('a', 250) + "@tests.local" }];
        yield return [valid with { Password = null }];
        yield return [valid with { Password = "Short1" }];
        yield return [valid with { Password = "lowercase1" }];
        yield return [valid with { Password = "UPPERCASE1" }];
        yield return [valid with { Password = "NoDigitsHere" }];
        yield return [valid with { Password = "A1" + new string('b', 99) }];
        yield return [valid with { FullName = null }];
        yield return [valid with { FullName = " " }];
        yield return [valid with { FullName = "X" }];
        yield return [valid with { FullName = new string('a', 101) }];
        yield return [valid with { Role = Roles.Employer, CompanyName = new string('a', 121) }];
    }

    [Theory]
    [MemberData(nameof(InvalidRegistrations))]
    public async Task Registration_validates_contract_limits_before_persisting(RegisterInput input)
    {
        using var response = await fixture.Client.PostAsJsonAsync("/api/auth/register", input);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        if (input.Email is not null)
            await AssertNoUserAsync(input.Email);
    }

    [Theory]
    [InlineData("/api/auth/register", "email", false)]
    [InlineData("/api/auth/register", "password", false)]
    [InlineData("/api/auth/register", "fullName", false)]
    [InlineData("/api/auth/login", "email", false)]
    [InlineData("/api/auth/login", "password", false)]
    [InlineData("/api/auth/login", "email", true)]
    [InlineData("/api/auth/login", "password", true)]
    public async Task Missing_or_null_required_fields_return_safe_validation_ProblemDetails(
        string path, string field, bool useNull)
    {
        var email = $"missing-{Guid.NewGuid():N}@tests.local";
        var request = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = Password,
            ["fullName"] = "Test User",
            ["role"] = Roles.Candidate
        };
        if (useNull)
            request[field] = null;
        else
            request.Remove(field);
        using var response = await fixture.Client.PostAsJsonAsync(path, request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        await AssertNoUserAsync(email);
    }

    [Theory]
    [InlineData("/api/auth/register", "{\"email\":")]
    [InlineData("/api/auth/login", "{\"password\":\"BodySecret1234\",\"email\":")]
    [InlineData("/api/auth/register", "null")]
    [InlineData("/api/auth/login", "null")]
    public async Task Malformed_or_null_json_returns_safe_validation_ProblemDetails(string path, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await fixture.Client.PostAsync(path, body);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        var error = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("BodySecret1234", error);
        Assert.DoesNotContain("System.Text.Json", error);
        Assert.DoesNotContain("JsonException", error);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_return_the_same_safe_error()
    {
        using var unknown = await fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = $"unknown-{Guid.NewGuid():N}@tests.local",
            password = Password
        });
        using var wrong = await fixture.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "user@hackathon.local",
            password = "Wrong1234"
        });
        await AssertProblemAsync(unknown, HttpStatusCode.Unauthorized, "auth.invalid_credentials",
            "Invalid email or password.");
        await AssertProblemAsync(wrong, HttpStatusCode.Unauthorized, "auth.invalid_credentials",
            "Invalid email or password.");
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/__tests/authorization/candidate")]
    [InlineData("/__tests/authorization/employer")]
    public async Task Missing_token_is_rejected_as_ProblemDetails(string path)
    {
        using var response = await fixture.Client.GetAsync(path);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated",
            "Please sign in to continue.");
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("malformed")]
    [InlineData("unsigned")]
    [InlineData("missing-expiry")]
    [InlineData("future-nbf")]
    [InlineData("algorithm")]
    public async Task Jwt_middleware_rejects_invalid_tokens(string kind)
    {
        var login = await LoginAsync("user@hackathon.local", "User123!");
        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            kind == "signature" ? AuthFixture.SigningKey + "-wrong" : AuthFixture.SigningKey)),
            kind == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: kind == "issuer" ? "AnotherIssuer" : AuthFixture.Issuer,
            audience: kind == "audience" ? "AnotherAudience" : AuthFixture.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, login.UserId), new Claim(ClaimTypes.Role, Roles.Candidate)],
            notBefore: kind == "future-nbf" ? now.AddHours(1) : now.AddHours(-9),
            expires: kind == "missing-expiry" ? null : kind == "expired" ? now.AddMinutes(-20) : now.AddHours(2),
            signingCredentials: kind == "unsigned" ? null : credentials);
        var encoded = kind == "malformed" ? "not-a-jwt" : new JwtSecurityTokenHandler().WriteToken(token);
        using var response = await GetAuthorizedAsync("/api/auth/me", encoded);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "auth.unauthenticated",
            "Please sign in to continue.");
    }

    [Fact]
    public async Task Me_reads_the_token_identity_and_ignores_spoofed_query_parameters()
    {
        var candidate = await LoginAsync("user@hackathon.local", "User123!");
        var employer = await LoginAsync("admin@hackathon.local", "Admin123!");
        var path = $"/api/auth/me?userId={Uri.EscapeDataString(employer.UserId)}" +
                   $"&email={Uri.EscapeDataString(employer.Email)}&role={Roles.Employer}";
        using var response = await GetAuthorizedAsync(path, candidate.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(current);
        Assert.Equal(candidate.UserId, current.UserId);
        Assert.Equal(candidate.Email, current.Email);
        Assert.Equal(Roles.Candidate, current.Role);
        Assert.Null(current.CompanyName);
    }

    [Theory]
    [InlineData("user@hackathon.local", "User123!", Roles.Candidate, "candidate", "employer")]
    [InlineData("admin@hackathon.local", "Admin123!", Roles.Employer, "employer", "candidate")]
    public async Task Roles_allow_own_endpoint_and_forbid_other_role(
        string email, string password, string role, string allowed, string forbidden)
    {
        var login = await LoginAsync(email, password);
        Assert.Equal(role, login.Role);
        using var accepted = await GetAuthorizedAsync($"/__tests/authorization/{allowed}", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var identity = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal(login.UserId, identity.RootElement.GetProperty("userId").GetString());
        Assert.Equal(login.UserId, identity.RootElement.GetProperty("claimsUserId").GetString());
        Assert.True(identity.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(role == Roles.Candidate, identity.RootElement.GetProperty("isCandidate").GetBoolean());
        Assert.Equal(role == Roles.Employer, identity.RootElement.GetProperty("isEmployer").GetBoolean());
        using var denied = await GetAuthorizedAsync($"/__tests/authorization/{forbidden}", login.AccessToken);
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "auth.forbidden",
            "Your account type cannot do this.");
    }

    [Fact]
    public async Task Migrations_and_repeat_startup_preserve_Identity_schema_and_idempotent_demo_seeds()
    {
        using var secondFactory = new AuthApiFactory(fixture.ConnectionString);
        using var secondClient = secondFactory.CreateClient();
        using var health = await secondClient.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var scope = secondFactory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Empty(await database.Database.GetPendingMigrationsAsync());
        Assert.Equal(new[] { Roles.Candidate, Roles.Employer },
            await database.Roles.Select(r => r.Name!).OrderBy(n => n).ToArrayAsync());

        await database.Database.OpenConnectionAsync();
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        foreach (var table in new[] { "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims",
                     "AspNetRoleClaims", "AspNetUserLogins", "AspNetUserTokens", "CandidateProfiles" })
            Assert.Contains(table, tables);
        Assert.DoesNotContain("RefreshTokens", tables);
        await reader.DisposeAsync();

        foreach (var demo in new[]
                 {
                     (Email: "user@hackathon.local", Password: "User123!", Role: Roles.Candidate),
                     (Email: "admin@hackathon.local", Password: "Admin123!", Role: Roles.Employer)
                 })
        {
            Assert.Equal(1, await database.Users.CountAsync(u => u.NormalizedEmail == demo.Email.ToUpperInvariant()));
            var user = await users.FindByEmailAsync(demo.Email);
            Assert.NotNull(user);
            Assert.True(await users.CheckPasswordAsync(user, demo.Password));
            Assert.Equal(new[] { demo.Role }, await users.GetRolesAsync(user));
            Assert.Equal(demo.Role == Roles.Candidate,
                await database.CandidateProfiles.AnyAsync(p => p.UserId == user.Id));
            if (demo.Role == Roles.Employer)
                Assert.False(string.IsNullOrWhiteSpace(user.CompanyName));
        }
    }

    [Fact]
    public async Task Swagger_documents_Bearer_authentication_for_me_and_serves_authorization_ui()
    {
        using var response = await fixture.Client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        var bearer = schemes.EnumerateObject().Single(s => s.Value.GetProperty("type").GetString() == "http"
            && s.Value.GetProperty("scheme").GetString() == "bearer");
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/auth/me").GetProperty("get");
        Assert.Contains(operation.GetProperty("security").EnumerateArray(), requirement =>
            requirement.TryGetProperty(bearer.Name, out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/auth/register", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/auth/login", out _));
        using var ui = await fixture.Client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Contains("swagger-ui-bundle.js", await ui.Content.ReadAsStringAsync());
        using var script = await fixture.Client.GetAsync("/swagger/swagger-ui-bundle.js");
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Contains("SwaggerUIBundle", await script.Content.ReadAsStringAsync());
    }

    private static RegisterInput ValidRegistration(string role) => new(
        $"auth-{Guid.NewGuid():N}@tests.local", Password, "Test User", role,
        role == Roles.Employer ? "Test Company" : null);

    private async Task<AuthResponse> RegisterAsync(RegisterInput input)
    {
        using var response = await fixture.Client.PostAsJsonAsync("/api/auth/register", input);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Registration returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<AuthResponse> LoginAsync(string email, string password)
    {
        using var response = await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Login returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<HttpResponseMessage> GetAuthorizedAsync(string path, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await fixture.Client.SendAsync(request);
    }

    private async Task AssertNoUserAsync(string email)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
    }

    private static void AssertToken(AuthResponse response)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
        Assert.Equal(response.UserId, token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(response.Email, token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(response.FullName, token.Claims.Single(c => c.Type == "name").Value);
        Assert.Contains(token.Claims, c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == response.Role);
        Assert.Equal(AuthFixture.Issuer, token.Issuer);
        Assert.Contains(AuthFixture.Audience, token.Audiences);
        Assert.Equal(TimeSpan.FromHours(8), token.ValidTo - token.ValidFrom);
        Assert.InRange((token.ValidTo - DateTime.UtcNow).TotalSeconds,
            TimeSpan.FromHours(8).TotalSeconds - 10, TimeSpan.FromHours(8).TotalSeconds + 2);
        Assert.InRange(Math.Abs((response.ExpiresAt.UtcDateTime - token.ValidTo).TotalSeconds), 0, 1);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status,
        string? code = null, string? detail = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        if (code is not null)
            Assert.Equal(code, body.GetProperty("code").GetString());
        if (detail is not null)
            Assert.Equal(detail, body.GetProperty("detail").GetString());
    }

    public sealed record RegisterInput(string? Email, string? Password, string? FullName, string? Role, string? CompanyName);
    private sealed record UserResponse(string UserId, string Email, string FullName, string Role, string? CompanyName);
    private sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, string UserId,
        string Email, string FullName, string Role, string? CompanyName);
}
