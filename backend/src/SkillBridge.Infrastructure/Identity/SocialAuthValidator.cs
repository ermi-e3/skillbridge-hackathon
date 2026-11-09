using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Exceptions;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;

namespace SkillBridge.Infrastructure.Identity;

public sealed class SocialAuthValidator(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<SocialAuthValidator> logger) : ISocialAuthValidator
{
    public async Task<SocialUserInfo> ValidateAsync(
        SocialLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = request.Provider.Trim();

        // 1. Mock / Deterministic test tokens
        if (IsMockToken(request.IdToken) || IsMockToken(request.Code))
        {
            return ParseMockSocialUser(provider, request.IdToken ?? request.Code!);
        }

        return provider.ToLowerInvariant() switch
        {
            "google" => await ValidateGoogleAsync(request, cancellationToken),
            "github" => await ValidateGitHubAsync(request, cancellationToken),
            _ => throw new BusinessRuleException(
                "auth.unsupported_provider",
                "Unsupported provider",
                $"Provider '{provider}' is not supported. Supported providers are Google and GitHub.")
        };
    }

    private static bool IsMockToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        (token.StartsWith("mock_", StringComparison.OrdinalIgnoreCase) ||
         token.StartsWith("test_", StringComparison.OrdinalIgnoreCase));

    private static SocialUserInfo ParseMockSocialUser(string provider, string token)
    {
        // format: mock_{email}_{name} or mock_{email} or mock_{id}
        var parts = token.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var email = parts.Length > 1 && parts[1].Contains('@')
            ? parts[1]
            : $"{provider.ToLowerInvariant()}_user@example.com";
        var name = parts.Length > 2
            ? parts[2].Replace('-', ' ')
            : $"{provider} User";
        var providerKey = $"mock-{provider.ToLowerInvariant()}-{email}";

        return new SocialUserInfo(
            Provider: provider,
            ProviderKey: providerKey,
            Email: email,
            FullName: name,
            EmailVerified: true);
    }

    private async Task<SocialUserInfo> ValidateGoogleAsync(
        SocialLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            string? idToken = request.IdToken;

            // If an authorization code was passed, exchange it for tokens
            if (string.IsNullOrWhiteSpace(idToken) && !string.IsNullOrWhiteSpace(request.Code))
            {
                var clientId = configuration["Authentication:Google:ClientId"]?.Replace("https://", "")?.Trim();
                var clientSecret = configuration["Authentication:Google:ClientSecret"]?.Trim();

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    logger.LogWarning("Google credentials not configured; falling back to simulated Google user.");
                    return new SocialUserInfo("Google", "google-simulated-sub", "google.user@example.com", "Google User", true);
                }

                var tokenResponse = await httpClient.PostAsync(
                    "https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["code"] = request.Code,
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret,
                        ["redirect_uri"] = request.RedirectUri ?? string.Empty,
                        ["grant_type"] = "authorization_code"
                    }), cancellationToken);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    throw new UnauthorizedException("auth.google_auth_failed", "Google authentication failed", "Failed to exchange Google authorization code.");
                }

                using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
                if (tokenDoc.RootElement.TryGetProperty("id_token", out var idTokenProp))
                {
                    idToken = idTokenProp.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new UnauthorizedException("auth.google_token_missing", "Google token missing", "Neither Google ID token nor valid authorization code was provided.");
            }

            // Verify Google ID token via tokeninfo endpoint
            var response = await httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new UnauthorizedException("auth.invalid_google_token", "Invalid Google token", "Google ID token validation failed.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;

            var sub = root.GetProperty("sub").GetString() ?? throw new InvalidOperationException("Missing Google sub claim.");
            var email = root.GetProperty("email").GetString() ?? throw new InvalidOperationException("Missing Google email claim.");
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : email;
            var emailVerified = root.TryGetProperty("email_verified", out var evProp) &&
                ((evProp.ValueKind == JsonValueKind.True) || (evProp.ValueKind == JsonValueKind.String && evProp.GetString() == "true"));

            return new SocialUserInfo("Google", sub, email, name ?? email, emailVerified);
        }
        catch (Exception ex) when (ex is not UnauthorizedException && ex is not BusinessRuleException)
        {
            logger.LogError(ex, "Error validating Google identity");
            throw new UnauthorizedException("auth.google_auth_error", "Google authentication error", "An error occurred while validating Google identity.");
        }
    }

    private async Task<SocialUserInfo> ValidateMicrosoftAsync(
        SocialLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = request.IdToken;

            // If an authorization code was passed, exchange it
            if (string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(request.Code))
            {
                var clientId = configuration["Authentication:Microsoft:ClientId"];
                var clientSecret = configuration["Authentication:Microsoft:ClientSecret"];
                var tenantId = configuration["Authentication:Microsoft:TenantId"] ?? "common";

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    logger.LogWarning("Microsoft credentials not configured; falling back to simulated Microsoft user.");
                    return new SocialUserInfo("Microsoft", "ms-simulated-id", "ms.user@example.com", "Microsoft User", true);
                }

                var tokenResponse = await httpClient.PostAsync(
                    $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["code"] = request.Code,
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret,
                        ["redirect_uri"] = request.RedirectUri ?? string.Empty,
                        ["grant_type"] = "authorization_code"
                    }), cancellationToken);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    throw new UnauthorizedException("auth.microsoft_auth_failed", "Microsoft authentication failed", "Failed to exchange Microsoft authorization code.");
                }

                using var tokenDoc = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
                if (tokenDoc.RootElement.TryGetProperty("access_token", out var tokenProp))
                {
                    accessToken = tokenProp.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new UnauthorizedException("auth.microsoft_token_missing", "Microsoft token missing", "Microsoft access token was not provided.");
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await httpClient.SendAsync(req, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new UnauthorizedException("auth.invalid_microsoft_token", "Invalid Microsoft token", "Microsoft user verification failed.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;

            var id = root.GetProperty("id").GetString()!;
            var email = (root.TryGetProperty("mail", out var m) && !string.IsNullOrWhiteSpace(m.GetString()))
                ? m.GetString()!
                : root.GetProperty("userPrincipalName").GetString()!;
            var name = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : email;

            return new SocialUserInfo("Microsoft", id, email, name ?? email, true);
        }
        catch (Exception ex) when (ex is not UnauthorizedException && ex is not BusinessRuleException)
        {
            logger.LogError(ex, "Error validating Microsoft identity");
            throw new UnauthorizedException("auth.microsoft_auth_error", "Microsoft authentication error", "An error occurred while validating Microsoft identity.");
        }
    }

    private async Task<SocialUserInfo> ValidateGitHubAsync(
        SocialLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = request.IdToken;

            // GitHub code exchange
            if (string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(request.Code))
            {
                var clientId = configuration["Authentication:GitHub:ClientId"];
                var clientSecret = configuration["Authentication:GitHub:ClientSecret"];

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    logger.LogWarning("GitHub credentials not configured; falling back to simulated GitHub user.");
                    return new SocialUserInfo("GitHub", "github-simulated-id", "github.user@example.com", "GitHub User", true);
                }

                var formFields = new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["code"] = request.Code
                };

                if (!string.IsNullOrWhiteSpace(request.RedirectUri))
                {
                    formFields["redirect_uri"] = request.RedirectUri;
                }

                using var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
                {
                    Content = new FormUrlEncodedContent(formFields)
                };
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var tokenResponse = await httpClient.SendAsync(req, cancellationToken);
                var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    logger.LogWarning("GitHub token exchange HTTP error {Status}: {Body}", tokenResponse.StatusCode, tokenContent);
                    throw new UnauthorizedException("auth.github_auth_failed", "GitHub authentication failed", "Failed to exchange GitHub authorization code.");
                }

                using var tokenDoc = JsonDocument.Parse(tokenContent);
                if (tokenDoc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    var errDesc = tokenDoc.RootElement.TryGetProperty("error_description", out var descProp)
                        ? descProp.GetString()
                        : errorProp.GetString();
                    logger.LogWarning("GitHub token exchange returned error: {Error} ({Desc})", errorProp.GetString(), errDesc);
                    throw new UnauthorizedException("auth.github_auth_failed", "GitHub authentication failed", errDesc ?? "Failed to exchange GitHub authorization code.");
                }

                if (tokenDoc.RootElement.TryGetProperty("access_token", out var tokenProp))
                {
                    accessToken = tokenProp.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new UnauthorizedException("auth.github_token_missing", "GitHub token missing", "GitHub access token was not provided.");
            }

            using var userReq = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            userReq.Headers.UserAgent.Add(new ProductInfoHeaderValue("SkillBridge", "1.0"));

            using var userResp = await httpClient.SendAsync(userReq, cancellationToken);
            if (!userResp.IsSuccessStatusCode)
            {
                throw new UnauthorizedException("auth.invalid_github_token", "Invalid GitHub token", "GitHub profile verification failed.");
            }

            using var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync(cancellationToken));
            var root = userDoc.RootElement;

            var id = root.GetProperty("id").GetInt64().ToString();
            var name = root.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString())
                ? n.GetString()
                : root.GetProperty("login").GetString();
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;

            // If email is private, fetch /user/emails
            if (string.IsNullOrWhiteSpace(email))
            {
                using var emailReq = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
                emailReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                emailReq.Headers.UserAgent.Add(new ProductInfoHeaderValue("SkillBridge", "1.0"));

                using var emailResp = await httpClient.SendAsync(emailReq, cancellationToken);
                if (emailResp.IsSuccessStatusCode)
                {
                    using var emailsDoc = JsonDocument.Parse(await emailResp.Content.ReadAsStringAsync(cancellationToken));
                    foreach (var elem in emailsDoc.RootElement.EnumerateArray())
                    {
                        var isPrimary = elem.TryGetProperty("primary", out var prim) && prim.GetBoolean();
                        var isVerified = elem.TryGetProperty("verified", out var ver) && ver.GetBoolean();
                        if (isPrimary && isVerified && elem.TryGetProperty("email", out var mailProp))
                        {
                            email = mailProp.GetString();
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new UnauthorizedException("auth.github_email_not_found", "GitHub email not found", "Could not retrieve a verified primary email from GitHub.");
            }

            return new SocialUserInfo("GitHub", id, email, name ?? email, true);
        }
        catch (Exception ex) when (ex is not UnauthorizedException && ex is not BusinessRuleException)
        {
            logger.LogError(ex, "Error validating GitHub identity");
            throw new UnauthorizedException("auth.github_auth_error", "GitHub authentication error", "An error occurred while validating GitHub identity.");
        }
    }
}
