# 05. Backend Documentation

## 1. Backend Architecture & Solution Structure

The SkillBridge backend is built as an enterprise **Clean Architecture** solution in **ASP.NET Core 10** (`net10.0`) and **C# 13**. The solution (`backend/SkillBridge.sln`) is composed of four main projects:

```
backend/
├── src/
│   ├── SkillBridge.Domain/            # Entities, enums, value objects, business rules
│   │   ├── Common/                    # AppException, BusinessRuleException, Roles
│   │   ├── Entities/                  # Job, JobSkill, JobApplication, CandidateProfile, etc.
│   │   └── Enums/                     # ApplicationStatus, JobType, WorkArrangement, etc.
│   ├── SkillBridge.Application/       # Use cases, application services, DTOs, validators
│   │   ├── Applications/              # ApplicationsService, ApplyRequest, DTOs
│   │   ├── Auth/                      # AuthService, LoginRequest, RegisterRequest
│   │   ├── CandidateProfiles/         # CandidateProfileService, profile DTOs
│   │   ├── Common/                    # Interfaces (IAppDbContext, IRealtimeNotifier, etc.)
│   │   ├── Jobs/                      # JobsService, JobMatchingService, CreateJobRequest
│   │   └── Skills/                    # SkillCatalogService, SkillDto
│   ├── SkillBridge.Infrastructure/    # EF Core, Identity, Brevo email, File storage
│   │   ├── Identity/                  # AppUser, JwtTokenService, RefreshTokenService
│   │   ├── Persistence/               # AppDbContext, entity configurations, migrations
│   │   └── Services/                  # BrevoEmailService, LocalFileStorageService, SystemClock
│   └── SkillBridge.Api/               # Web API host, controllers, middleware, SignalR hub
│       ├── Controllers/               # Auth, Jobs, EmployerJobs, Applications, CandidateProfile
│       ├── Errors/                    # AppExceptionHandler, ProblemDetailsWriter
│       ├── Hubs/                      # SkillBridgeHub, CustomUserIdProvider
│       ├── Realtime/                  # SignalRNotifier
│       ├── Services/                  # JobExpirationWorker (BackgroundService)
│       ├── Swagger/                   # OpenAPI filters and scalar setup
│       └── Program.cs                 # Host builder & middleware pipeline
└── tests/
    └── SkillBridge.Auth.Tests/        # xUnit integration and unit tests
```

---

## 2. API Controllers & Routing Overview

The API exposes 26+ endpoints organized into RESTful controllers:

| Controller | Route Base | Responsibilities | Default Authorization |
| :--- | :--- | :--- | :--- |
| `AuthController` | `/api/auth` | Register, login, social auth, tokens, email confirmation, password reset | Anonymous (with `[Authorize]` on `/me`) |
| `JobsController` | `/api/jobs` | Public/Candidate job search, detail view, candidate apply | Authenticated (Candidate for `/apply`) |
| `EmployerJobsController` | `/api/employer/jobs` | Employer jobs list, job closing, job editing | `Roles.Employer` |
| `ApplicationsController` | `/api/applications` | Application detail, employer status change, candidate withdraw, CV download | Authenticated (`Roles.Employer` / `Roles.Candidate`) |
| `CandidateProfileController` | `/api/candidates/me/profile` | Candidate profile retrieval, profile updates, default CV upload/download/deletion | `Roles.Candidate` |
| `CandidateApplicationsController`| `/api/candidates` | Candidate applications history (`/me/applications`) | `Roles.Candidate` |
| `SkillsController` | `/api/skills` | Global skill taxonomy catalog retrieval | Anonymous / Authenticated |
| `AccountController` | `/api/account` | Profile update, password change, email change, session revocation, account deletion | Authenticated |
| `HealthController` | `/api/health` | Service and database health check probe | Anonymous |

---

## 3. Core Application Services & Business Logic

### 1. `JobMatchingService` (`SkillBridge.Application.Jobs`)
The computational core of SkillBridge. Implements `IJobMatchingService` to evaluate any `Job` entity against a `CandidateProfile` entity.

#### Compatibility Algorithm Breakdown:
- **Total Possible Score:** `100%`
- **1. Required Skills Score (50–65 points):**
  - If the job has preferred skills: max weight is `50 points`.
  - If the job has no preferred skills: max weight is `65 points`.
  - For each required skill:
    - If candidate meets or exceeds required proficiency: `1.0x` credit.
    - If candidate is 1 level below (e.g. Advanced required, Intermediate possessed): `0.85x` credit.
    - If candidate is 2 levels below: `0.70x` credit.
    - If candidate lacks the skill entirely: `0.0x` credit.
  - **Zero-Match Cap:** If the job defines required skills and the candidate matches **zero** of them, the total overall match score is hard-capped at **25%**, regardless of other match points.
- **2. Preferred Skills Score (0–15 points):**
  - If the job defines preferred skills, candidates receive proportional credit for each possessed skill up to `15 points`.
- **3. Experience Level & History (15 points):**
  - Points awarded based on candidate years of experience vs. job minimum, plus seniority tier alignment.
- **4. Work Arrangement Compatibility (10 points):**
  - Exact match (`Remote == Remote`, `Hybrid == Hybrid`, `OnSite == OnSite`) = 10 points.
  - **Remote Job Rule:** If the job is `Remote`, it is treated as **100% compatible (10 points)** for any candidate preference.
- **5. Location Match (5 points):**
  - Case-insensitive city/location match = 5 points. Remote jobs automatically receive 5 points.
- **6. Job Type Match (5 points):**
  - Full-time, part-time, or contract match = 5 points.

### 2. `JobsService` (`SkillBridge.Application.Jobs`)
- Handles creation, modification, closing, and pagination of job postings.
- **2-Hour Edit Window Rule:** Enforces `job.IsEditingAllowed(clock.UtcNow)` (`CreatedAt <= 2 hours`). If an employer attempts to update a job after 2 hours, throws `BusinessRuleException("jobs.editing_locked", "Job editing locked", "Job requirements can only be edited within 2 hours of posting.")`.
- **Skill Validation:** Ensures between 1 and 25 required skills are attached to every posting. Automatically categorizes custom user-typed skills.

### 3. `ApplicationsService` (`SkillBridge.Application.Applications`)
- Handles the candidate application submission and state machine:
  - Validates that the candidate has not already applied to the specified job.
  - Validates that the job is currently `IsOpen` and not expired past its `Deadline`.
  - Supports using either the candidate's default profile CV or an uploaded bespoke CV.
- Manages status transitions:
  - `Received` -> `Shortlisted` (Employer only)
  - `Received` -> `Rejected` (Employer only)
  - `Received` -> `Withdrawn` (Candidate only)
  - Any attempt to transition from already finalized states (`Shortlisted`, `Rejected`, `Withdrawn`) throws `BusinessRuleException("applications.invalid_transition")`.
- Dispatches typed notifications via `IRealtimeNotifier`.

### 4. `AuthService` (`SkillBridge.Application.Auth`)
- Orchestrates user registration, authentication, email confirmation, password resets, and session management.
- Coordinates between `IIdentityService`, `IJwtTokenService`, `IRefreshTokenService`, and `IEmailService`.
- Coordinates refresh token issuance, rotation, and revocation.

---

## 4. Background Services: `JobExpirationWorker`

Defined in [backend/src/SkillBridge.Api/Services/JobExpirationWorker.cs](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Api/Services/JobExpirationWorker.cs).

### Operational Lifecycle:
1. **Startup Delay:** Waits 3 seconds on host startup to ensure database migrations and seeders finish executing.
2. **Execution Loop:** Runs continuously every **15 seconds** until application shutdown is requested.
3. **Execution Logic:**
   ```csharp
   var expiredJobs = await dbContext.Jobs
       .Where(j => j.IsOpen && j.Deadline != null && j.Deadline <= clock.UtcNow)
       .ToListAsync(stoppingToken);

   foreach (var job in expiredJobs)
   {
       job.Close();
       // Broadcasts real-time event to connected employers and candidates
       await notifier.NotifyJobClosedAsync(new JobClosedRealtimeEvent(job.Id, job.Title, "DeadlineExpired"));
   }
   await dbContext.SaveChangesAsync(stoppingToken);
   ```

---

## 5. Global Error Handling & ProblemDetails Format

SkillBridge conforms to the **RFC 7807 ProblemDetails** standard for all API error responses.

### Handled in `AppExceptionHandler.cs`:
When an uncaught exception occurs during request execution, `AppExceptionHandler` catches it and formats a consistent JSON payload:

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Invalid transition",
  "status": 400,
  "detail": "Cannot change status from Shortlisted to Rejected.",
  "code": "applications.invalid_transition",
  "traceId": "0HN1A2B3C4D5E:00000001"
}
```

### Exception Mapping Table:

| Exception Type | HTTP Status Code | Machine Code | Description |
| :--- | :--- | :--- | :--- |
| `RequestValidationException` | `400 Bad Request` | `validation.failed` | FluentValidation schema failure with `errors` dictionary |
| `BusinessRuleException` | `400 Bad Request` | Custom (e.g. `jobs.editing_locked`) | Domain rule invariant violated |
| `UnauthorizedException` | `401 Unauthorized` | `auth.unauthorized` | Missing or invalid credentials |
| `ForbiddenException` | `403 Forbidden` | `auth.forbidden` | User lacks the required role or permission |
| `NotFoundException` | `404 Not Found` | `resource.not_found` | Target entity does not exist |
| `ConflictException` | `409 Conflict` | `resource.conflict` | Duplicate key or concurrency collision |
| Unhandled Exception | `500 Internal Error` | `server.error` | Unexpected internal exception (details masked in prod) |

---

## 6. Practical API Request & Response Examples

### Example 1: User Login
- **Endpoint:** `POST /api/auth/login`
- **Request Body:**
  ```json
  {
    "email": "user@hackathon.local",
    "password": "User123!"
  }
  ```
- **Response (`200 OK`):**
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2026-11-18T01:30:00Z",
    "user": {
      "id": "c1a2b3c4-d5e6-7890-abcd-ef1234567890",
      "email": "user@hackathon.local",
      "fullName": "Demo Candidate",
      "role": "Candidate",
      "companyName": null
    }
  }
  ```
- **Response Headers:**
  - `Set-Cookie: refreshToken=...; Path=/api/auth; HttpOnly; SameSite=Lax; Secure`
  - `Set-Cookie: XSRF-TOKEN=...; Path=/; SameSite=Lax; Secure`

### Example 2: Create a Job Opening
- **Endpoint:** `POST /api/jobs`
- **Headers:** `Authorization: Bearer <token>`
- **Request Body:**
  ```json
  {
    "title": "Senior Cloud Infrastructure Engineer",
    "description": "Lead our AWS and Kubernetes migration. [Salary: 60,000 – 90,000 ETB / mo]",
    "location": "Addis Ababa, Ethiopia",
    "jobType": "FullTime",
    "workArrangement": "Hybrid",
    "experienceLevel": "Senior",
    "minExperienceYears": 5,
    "deadline": "2026-12-31T23:59:59Z",
    "category": "Engineering",
    "requiredSkillIds": [1, 2, 4],
    "preferredSkillIds": [7, 12]
  }
  ```
- **Response (`201 Created`):**
  ```json
  {
    "id": 14,
    "title": "Senior Cloud Infrastructure Engineer",
    "companyName": "SkillBridge Enterprise Partners",
    "location": "Addis Ababa, Ethiopia",
    "isOpen": true,
    "createdAt": "2026-11-18T00:30:00Z",
    "deadline": "2026-12-31T23:59:59Z",
    "jobType": "FullTime",
    "workArrangement": "Hybrid",
    "experienceLevel": "Senior",
    "minExperienceYears": 5,
    "category": "Engineering",
    "requiredSkills": [
      { "id": 1, "name": "C#" },
      { "id": 2, "name": ".NET Core" },
      { "id": 4, "name": "Docker" }
    ],
    "preferredSkills": [
      { "id": 7, "name": "Kubernetes" },
      { "id": 12, "name": "AWS" }
    ],
    "isOwner": true
  }
  ```

