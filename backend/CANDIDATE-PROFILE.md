# Candidate profile API

Candidates can read and update their own profile through:

| Method | Endpoint | Result |
| --- | --- | --- |
| GET | `/api/candidates/me/profile` | Current candidate's profile, including selected skills |
| PUT | `/api/candidates/me/profile` | Replace editable profile fields and skill selection; return the saved profile |

Both endpoints require a Candidate Bearer token. The current user ID comes from `ICurrentUser`, which reads the authenticated JWT principal. A user ID supplied in the body or query string cannot change whose profile is accessed. Employers receive `403`; requests without a valid token receive `401`.

## Interface and service pattern

```text
CandidateProfileController
  -> ICandidateProfileService
  -> CandidateProfileService
  -> ICurrentUser + IAppDbContext + IClock
  -> CandidateProfile domain entity
  -> EF Core / PostgreSQL
```

The controller receives requests and returns service results. The Application service handles current-user checks, input validation, skill lookup, and persistence. The domain entity applies profile changes and replaces skill associations while retaining unchanged associations. The implementation is registered through `AddScoped<ICandidateProfileService, CandidateProfileService>()`.

Existing `CandidateProfiles`, `CandidateSkills`, and `Skills` tables are reused. No database migration is required. Profile and skill changes are saved together through one `SaveChangesAsync` call.

## Test in Scalar

Start the backend as described in [AUTHENTICATION.md](AUTHENTICATION.md), then open [Scalar](http://localhost:5080/scalar/).

1. Log in with `user@hackathon.local` / `User123!`.
2. Copy `accessToken` into the Bearer authentication field.
3. Send `GET /api/candidates/me/profile`. A newly registered Candidate has an empty profile with no selected skills.
4. Send `PUT /api/candidates/me/profile` with:

   ```json
   {
     "headline": "Backend developer",
     "bio": "I build APIs using C# and PostgreSQL.",
     "gitHubUrl": "https://github.com/example",
     "skillIds": [1, 3, 5]
   }
   ```

5. Send GET again to see the persisted fields, UTC `updatedAt`, and skills with their IDs and names.

Retrieve the shared catalog through public [`GET /api/skills`](SKILL-CATALOG.md). The existing catalog includes `1 = ASP.NET Core`, `2 = Angular`, `3 = C#`, and `5 = PostgreSQL`. Updates select existing catalog entries; they do not create new skills.

## Validation and errors

| Field | Rules |
| --- | --- |
| `headline` | Required, trimmed, maximum 120 characters |
| `bio` | Optional, trimmed, maximum 2000 characters; blank values become null |
| `gitHubUrl` | Optional, trimmed, maximum 300 characters; must be an absolute HTTP or HTTPS URL when present; blank values become null |
| `skillIds` | Required list of 1-30 distinct positive IDs, all present in the existing skill catalog |

PUT replaces all editable fields and the skill selection. Omitted optional fields are cleared. Invalid input is rejected before profile changes are applied.

Responses contain `userId`, `headline`, `bio`, `gitHubUrl`, `updatedAt`, and `skills` (`id` and `name`, ordered by ID). A profile created during registration can have null text fields and `updatedAt`, with an empty skill list; a completed update requires at least one skill.

| Status | Error code | Meaning |
| --- | --- | --- |
| 400 | `validation.failed` | Invalid fields or unknown skill IDs; `errors` contains field messages |
| 401 | `auth.unauthenticated` | Missing or invalid authentication |
| 403 | `auth.forbidden` | The current account is not a Candidate |
| 404 | `candidate.profile_not_found` | The current user's CandidateProfile is missing |

Errors use the existing ProblemDetails format with `code` and `traceId`.

## Verification

Profile integration tests use the existing real-PostgreSQL fixture and a dedicated database ending in `_tests` or `_test`. See [the test guide](tests/SkillBridge.Auth.Tests/README.md) for the connection variable and test command.

Verified with a clean build (zero warnings or errors) and 88 passing integration tests: 32 profile cases plus the 56 existing authentication tests. Coverage includes Candidate-only access, ownership isolation, persisted updates, skill replacement, validation without partial changes, and Scalar/OpenAPI availability. EF reports no pending model changes.
