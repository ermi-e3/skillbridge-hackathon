# SkillBridge authentication and authorization

The backend implements registration, login, eight-hour JWT access tokens, the current-user endpoint, and Candidate/Employer role authorization. This guide covers authentication. Candidate profile endpoints are documented in [CANDIDATE-PROFILE.md](CANDIDATE-PROFILE.md).

The referenced solution blueprint and API contract were unavailable in `docs/`. The supplied “Authentication & Authorization First” prompt was used as the implementation contract.

## Run locally

Start PostgreSQL from the repository root:

```powershell
docker compose up -d postgres
```

Run the remaining commands from `backend`:

```powershell
./scripts/Initialize-LocalAuth.ps1
dotnet restore SkillBridge.sln
dotnet ef database update --project src/SkillBridge.Infrastructure --startup-project src/SkillBridge.Api
dotnet run --project src/SkillBridge.Api
```

The API listens at `http://localhost:5080`. In Development, Scalar is at `http://localhost:5080/scalar/` for browser testing, and Swagger is at `http://localhost:5080/swagger`. The IDE launch profile opens Scalar. PostgreSQL uses host port `5433`, database `skillbridge`, username `postgres`, and password `postgres` with the existing local Docker configuration.

`Initialize-LocalAuth.ps1` generates a signing key from 64 random bytes and writes it to the ignored `src/SkillBridge.Api/appsettings.Development.local.json`. Existing local keys are preserved. A signing secret is not committed to source control.

Apply migrations before starting the API. Startup seeds the two required roles idempotently. Demo users are seeded by default in Development:

| Email | Password | Role | Profile/company |
| --- | --- | --- | --- |
| `user@hackathon.local` | `User123!` | Candidate | Empty CandidateProfile |
| `admin@hackathon.local` | `Admin123!` | Employer | SkillBridge Demo Company; no CandidateProfile |

In production, configure `ConnectionStrings__Default`, `Jwt__Key`, `Jwt__Issuer`, and `Jwt__Audience` through deployment configuration. `Jwt__ExpirationHours` must be `8`; the signing key must contain at least 32 UTF-8 bytes. Demo users are disabled by default outside Development; `Seed__DemoUsers=false` disables them explicitly. Required role seeding still runs.

## Test in Scalar

1. Open `http://localhost:5080/scalar/` in your browser.
2. Select `POST /api/auth/login`, then **Test Request**. Enter the JSON body below and send it.

   ```json
   {
     "email": "user@hackathon.local",
     "password": "User123!"
   }
   ```

3. Copy `accessToken` from the `200` response.
4. Select `GET /api/auth/me`, open **Test Request**, and select the **Bearer** authentication scheme. Paste the raw token into the token field, then send the request. Do not add a second `Bearer` prefix.
5. Expect `200` with your Candidate account. Repeat using `admin@hackathon.local` / `Admin123!` to test the Employer account.
6. Clear the token and resend `/api/auth/me`; expect `401` with `auth.unauthenticated`.

Scalar uses the same OpenAPI document and Bearer scheme as Swagger. Its JavaScript is served by the API, and external default fonts are disabled. Tokens are not prefilled or configured for persistent browser storage. Scalar is available only in Development.

## Review in Swagger

1. Open `http://localhost:5080/swagger`.
2. Execute `POST /api/auth/login` with the Candidate demo credentials, or register a new Candidate.
3. Copy the returned `accessToken`.
4. Select **Authorize**, paste the token into the Bearer field, and authorize. Swagger adds the Bearer prefix.
5. Execute `GET /api/auth/me`; expect `200` and the Candidate's identity.
6. Repeat with the Employer demo credentials; expect `200` and the Employer's identity and company.
7. Remove authorization and execute `/api/auth/me`; expect `401` with `auth.unauthenticated`.

Registration examples:

```json
{
  "email": "candidate@example.com",
  "password": "Candidate123",
  "fullName": "Demo Candidate",
  "role": "Candidate",
  "companyName": null
}
```

```json
{
  "email": "employer@example.com",
  "password": "Employer123",
  "fullName": "Demo Employer",
  "role": "Employer",
  "companyName": "Example Company"
}
```

Registration and login return the same shape: `accessToken`, `expiresAt`, `userId`, `email`, `fullName`, `role`, and `companyName`. `/api/auth/me` returns those identity fields without the token and expiry.

Candidate/Employer access and wrong-role `403` are verified through controllers present only in the integration-test assembly. There are no artificial role-verification routes in the production API.

## Run integration tests

Use a dedicated PostgreSQL database whose name ends in `_tests` or `_test`. The fixture refuses the application database `skillbridge` and verifies the test host's resolved connection before issuing requests. These tests apply real migrations, seed demo accounts, and create uniquely named test accounts. They do not delete or reset the database.

If the test database does not exist, create it using the local Docker PostgreSQL container:

```powershell
docker exec skillbridge-postgres psql -U postgres -d postgres -c 'CREATE DATABASE skillbridge_auth_tests;'
```

From `backend`:

```powershell
$env:SKILLBRIDGE_TEST_CONNECTION = 'Host=localhost;Port=5433;Database=skillbridge_auth_tests;Username=postgres;Password=postgres'
dotnet test SkillBridge.sln
```

The test host supplies its own signing configuration. Tests exercise the real API, ASP.NET Core Identity, JWT bearer middleware, and PostgreSQL rather than replacing authentication with a mock.

## Files created

Paths below are relative to `backend`.

- `AUTHENTICATION.md`
- `scripts/Initialize-LocalAuth.ps1`
- `src/SkillBridge.Api/Controllers/AuthController.cs`
- `src/SkillBridge.Api/CurrentUser.cs`
- `src/SkillBridge.Api/Swagger/AuthorizationDocumentFilter.cs`
- `src/SkillBridge.Application/Auth/AccessToken.cs`
- `src/SkillBridge.Application/Auth/AuthResponse.cs`
- `src/SkillBridge.Application/Auth/AuthService.cs`
- `src/SkillBridge.Application/Auth/AuthUser.cs`
- `src/SkillBridge.Application/Auth/LoginRequest.cs`
- `src/SkillBridge.Application/Auth/LoginRequestValidator.cs`
- `src/SkillBridge.Application/Auth/RegisterRequest.cs`
- `src/SkillBridge.Application/Auth/RegisterRequestValidator.cs`
- `src/SkillBridge.Application/Common/Exceptions/RequestValidationException.cs`
- `src/SkillBridge.Application/Common/Interfaces/ICurrentUser.cs`
- `src/SkillBridge.Application/Common/Interfaces/IIdentityService.cs`
- `src/SkillBridge.Application/Common/Interfaces/IJwtTokenService.cs`
- `src/SkillBridge.Domain/Common/Roles.cs`
- `src/SkillBridge.Infrastructure/Identity/IdentitySeeder.cs`
- `src/SkillBridge.Infrastructure/Identity/IdentityService.cs`
- `src/SkillBridge.Infrastructure/Identity/JwtOptions.cs`
- `src/SkillBridge.Infrastructure/Identity/JwtTokenService.cs`
- `src/SkillBridge.Infrastructure/Persistence/Migrations/20261005125540_RemoveUnusedRefreshTokens.cs`
- `src/SkillBridge.Infrastructure/Persistence/Migrations/20261005125540_RemoveUnusedRefreshTokens.Designer.cs`
- `tests/SkillBridge.Auth.Tests/SkillBridge.Auth.Tests.csproj`
- `tests/SkillBridge.Auth.Tests/AuthFixture.cs`
- `tests/SkillBridge.Auth.Tests/AuthIntegrationTests.cs`
- `tests/SkillBridge.Auth.Tests/RoleVerificationController.cs`
- `tests/SkillBridge.Auth.Tests/README.md`

The generated `appsettings.Development.local.json` is a local setup artifact, ignored by Git.

## Files modified or removed

Modified:

- `Directory.Packages.props`: integration-test dependencies, Scalar.AspNetCore, and explicit updated Microsoft.OpenApi dependency.
- `SkillBridge.sln`: includes the implemented auth integration-test project.
- `src/SkillBridge.Api/Errors/AppExceptionHandler.cs`: validation errors use the common ProblemDetails format.
- `src/SkillBridge.Api/Program.cs`: Identity wiring, current-user abstraction, JWT authentication, role authorization, Scalar/Swagger, safe input errors, and startup seeding.
- `src/SkillBridge.Api/SkillBridge.Api.csproj`: explicit OpenAPI and Scalar dependencies.
- `src/SkillBridge.Api/Properties/launchSettings.json`: opens Scalar for browser testing.
- `src/SkillBridge.Api/appsettings.json`: JWT configuration with an empty signing-key placeholder.
- `src/SkillBridge.Application/DependencyInjection.cs`: registers AuthService and validators.
- `src/SkillBridge.Infrastructure/DependencyInjection.cs`: registers Identity, JWT settings/services, and seeding.
- `src/SkillBridge.Infrastructure/Persistence/AppDbContext.cs`: removes the unused RefreshTokens DbSet.
- `src/SkillBridge.Infrastructure/Persistence/AppDbContextFactory.cs`: supports EF commands from the documented working directories and environment configuration.
- `src/SkillBridge.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`: reflects removal of the legacy refresh-token model.

Removed:

- `src/SkillBridge.Domain/Entities/RefreshToken.cs`
- `src/SkillBridge.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`

## Database changes

The existing Identity database design is retained. `AppDbContext` remains the shared Identity/application context with standard string user IDs:

- `AspNetUsers` stores both account types, Identity password hashes, FullName, and optional CompanyName. The existing unique NormalizedEmail index prevents duplicate accounts, including concurrent registration.
- `AspNetRoles` contains Candidate and Employer, created through RoleManager.
- `AspNetUserRoles` assigns the registered account its single supported role.
- The related standard Identity claims, logins, and tokens tables remain intact.
- `CandidateProfiles.UserId` remains both its primary key and foreign key to `AspNetUsers.Id`, with a one-to-one optional relationship.

The original `20261005113719_InitialMigration` is preserved. The forward migration `20261005125540_RemoveUnusedRefreshTokens` drops the unused legacy RefreshTokens table to match the supplied no-refresh-token contract. That table had zero rows in the reviewed development database before the migration. On another database, applying this migration also removes any rows in that table. No unrelated application-table design changes were made.

Candidate registration saves the Identity user, Candidate role assignment, and empty CandidateProfile in one database transaction. Employer registration saves the Identity user, Employer role assignment, and company name, without creating a CandidateProfile. A failure rolls back the account creation.

## Authentication flow

```text
Register/Login request
  -> AuthService: normalize and validate input
  -> ASP.NET Core Identity: create user or check password
  -> AppUser + Candidate/Employer role
  -> JwtTokenService: signed eight-hour JWT
  -> AuthResponse
```

Identity handles password hashing and verification. Registration requires an 8–100 character password with uppercase, lowercase, and a digit; a symbol is optional. Emails and names are trimmed; passwords are preserved exactly. Candidate company names are ignored, and Employer company names are required with a maximum of 120 characters.

JWTs contain the Identity user ID in `sub` and NameIdentifier, plus email, name, and the standard role claim. JWT bearer authentication validates issuer, audience, signing key, signature, allowed signing algorithm, and expiration.

## Authorization flow

```text
Authorization: Bearer JWT
  -> JWT bearer authentication
  -> ClaimsPrincipal
  -> [Authorize]: authenticated user required
  -> [Authorize(Roles = "Candidate" / "Employer")]: required account type
  -> allowed request, or 403 auth.forbidden
```

`ICurrentUser` reads the authenticated principal; frontend user IDs, emails, or roles are not accepted as the current identity. `/api/auth/me` loads the account using that trusted user ID. Future ownership checks belong in Application services after controller role checks.

Expected auth errors use ProblemDetails with `code` and `traceId`:

| Case | HTTP | Code |
| --- | --- | --- |
| Duplicate email | 400 | `auth.email_taken` |
| Unsupported role | 400 | `auth.invalid_role` |
| Employer without company | 400 | `auth.company_required` |
| Invalid input | 400 | `validation.failed` |
| Unknown email or wrong password | 401 | `auth.invalid_credentials` |
| Missing, invalid, or expired JWT | 401 | `auth.unauthenticated` |
| Correct JWT with wrong role | 403 | `auth.forbidden` |

## Verification status

Verified on October 5, 2026: the solution built with zero warnings and errors, and all 56 integration test cases passed against real PostgreSQL. Migrations succeeded on the development and dedicated test databases. A live Development API at port 5080 also returned `200` for both demo logins and their authenticated `/api/auth/me` requests, and `401` for `/api/auth/me` without a token.

| Check | Status |
| --- | --- |
| Solution build | PASS — zero warnings and errors |
| Migration | PASS — development and test databases |
| Identity tables | PASS |
| Role seed | PASS |
| Candidate seed | PASS |
| Employer seed | PASS |
| CandidateProfile creation | PASS |
| Candidate registration | PASS |
| Employer registration | PASS |
| Duplicate email and concurrent registration | PASS |
| Invalid role and missing Employer company | PASS |
| Login and invalid credentials | PASS |
| JWT generation, claims, and eight-hour lifetime | PASS |
| JWT validation | PASS |
| `/api/auth/me` | PASS — tests and live demo accounts |
| Candidate authorization | PASS |
| Employer authorization | PASS |
| Wrong-role 403 | PASS |
| Swagger bearer document and UI assets over HTTP | PASS |
| Scalar page, local JavaScript assets, and Bearer configuration over HTTP | PASS |
| Interactive Swagger browser flow | NOT RUN — browser automation reported NoBrowserAvailable |
| Interactive Scalar browser flow | NOT RUN — manual browser review remains available |

The automated Swagger check validates the bearer security scheme, protected-route security requirements, and served authorization UI assets. Interactive browser verification is a separate manual check; it is not claimed as completed.
