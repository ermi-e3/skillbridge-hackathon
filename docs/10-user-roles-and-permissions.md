# 10. User Roles & Permissions

## 1. Overview of Platform Roles

SkillBridge enforces **Role-Based Access Control (RBAC)** across both the Angular client and the ASP.NET Core Web API. User roles are defined as static string constants in `SkillBridge.Domain.Common.Roles`:

```csharp
namespace SkillBridge.Domain.Common;

public static class Roles
{
    public const string Candidate = "Candidate";
    public const string Employer = "Employer";
}
```

A user is assigned exactly one role during registration. Roles are stored in the ASP.NET Core Identity database table `AspNetUserRoles` and embedded as a standard claim (`ClaimTypes.Role`) in issued JWT access tokens.

---

## 2. Role-Permission Matrix

| Functional Capability | Anonymous | Candidate (`Roles.Candidate`) | Employer (`Roles.Employer`) | Enforcement Mechanism |
| :--- | :---: | :---: | :---: | :--- |
| **View Marketing Landing Page** | :white_check_mark: | :white_check_mark: | :white_check_mark: | Public route |
| **Browse Skills Catalog** | :white_check_mark: | :white_check_mark: | :white_check_mark: | `[AllowAnonymous]` |
| **Register / Login / Reset Password** | :white_check_mark: | :x: | :x: | Public / Rate Limited |
| **Candidate Skill Onboarding** | :x: | :white_check_mark: | :x: | `roleGuard('Candidate')` & `[Authorize(Roles = "Candidate")]` |
| **View & Edit Candidate Profile** | :x: | :white_check_mark: | :x: | `roleGuard('Candidate')` & Controller Role Check |
| **Upload / Download Default CV** | :x: | :white_check_mark: | :x: | `[Authorize(Roles = "Candidate")]` |
| **Search & Discover Jobs** | :x: | :white_check_mark: | :x: | `roleGuard('Candidate')` |
| **View Job Compatibility Score** | :x: | :white_check_mark: | :x: | Computed dynamically per candidate |
| **Apply to a Job Opening** | :x: | :white_check_mark: | :x: | `[Authorize(Roles = "Candidate")]` |
| **Withdraw Own Application** | :x: | :white_check_mark: (If Received) | :x: | Candidate ownership & Status check |
| **View Own Submitted Applications** | :x: | :white_check_mark: | :x: | Candidate ownership filter |
| **Post a New Job Opening** | :x: | :x: | :white_check_mark: | `roleGuard('Employer')` & `[Authorize(Roles = "Employer")]` |
| **Edit Job Opening (Within 2 Hours)**| :x: | :x: | :white_check_mark: (Owner only) | Employer role + `job.IsOwnedBy()` + 2h rule |
| **Close Job Opening** | :x: | :x: | :white_check_mark: (Owner only) | Employer role + `job.IsOwnedBy()` |
| **Review Applicants for an Opening** | :x: | :x: | :white_check_mark: (Owner only) | Employer role + `job.IsOwnedBy()` |
| **Preview / Download Applicant CV** | :x: | :white_check_mark: (Own CV) | :white_check_mark: (Job Owner) | Resource ownership verification |
| **Advance Status to Shortlisted/Rejected**| :x: | :x: | :white_check_mark: | `[Authorize(Roles = "Employer")]` + Job Ownership |
| **Account Security Settings** | :x: | :white_check_mark: | :white_check_mark: | `[Authorize]` |
| **Session Revocation (Logout All)** | :x: | :white_check_mark: | :white_check_mark: | `[Authorize]` |
| **Permanent Account Deletion** | :x: | :white_check_mark: | :white_check_mark: | `[Authorize]` with password check |

---

## 3. Enforcement Mechanisms

### 1. Frontend Route Guards
Angular routes enforce role restrictions via functional guards configured in `app.routes.ts`:

```typescript
// Example: Restricting Job Discovery to Candidates
{
  path: 'jobs',
  canActivate: [authGuard, roleGuard('Candidate')],
  loadComponent: () => import('./features/candidate/jobs/job-list/job-list.component').then(m => m.JobListComponent)
}

// Example: Restricting Job Creation to Employers
{
  path: 'employer/jobs/new',
  canActivate: [authGuard, roleGuard('Employer')],
  loadComponent: () => import('./features/employer/jobs/employer-post-job/employer-post-job.component').then(m => m.EmployerPostJobComponent)
}
```

- If an unauthenticated user navigates to a protected route, `authGuard` preserves the return URL and redirects to `/login`.
- If an authenticated user navigates to a route for the opposite role (e.g. an Employer clicking on `/jobs`), `roleGuard` redirects them immediately to `/unauthorized`.

### 2. Backend Controller Role Declarations
Controllers enforce role constraints declaratively at the class or method level:

```csharp
[ApiController]
[Route("api/employer/jobs")]
[Authorize(Roles = Roles.Employer)]
public sealed class EmployerJobsController(JobsService jobsService) : ControllerBase
{ ... }

[ApiController]
[Route("api/candidates/me/profile")]
[Authorize(Roles = Roles.Candidate)]
public sealed class CandidateProfileController(ICandidateProfileService profileService) : ControllerBase
{ ... }
```

When an authenticated token lacks the required role claim, the ASP.NET Core authorization middleware invokes `JwtBearerEvents.OnForbidden`, which returns a structured RFC 7807 response:
```json
{
  "type": "https://httpstatuses.io/403",
  "title": "Forbidden",
  "status": 403,
  "detail": "Your account type cannot do this.",
  "code": "auth.forbidden"
}
```

### 3. Resource-Level Ownership Validation
Role authorization prevents cross-role access, but resource-level checks prevent **IDOR (Insecure Direct Object Reference)** attacks within the same role:
- **Employer Resource Validation:** When an employer attempts to view applicants (`/api/jobs/{jobId}/applications`) or close a job, the service queries:
  ```csharp
  var job = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
  if (job == null) throw new NotFoundException();
  if (!job.IsOwnedBy(currentUser.UserId))
      throw new ForbiddenException("jobs.not_owner", "Forbidden", "You do not own this job opening.");
  ```
- **Candidate Resource Validation:** When a candidate views or withdraws an application, the service ensures `application.CandidateId == currentUser.UserId`.

