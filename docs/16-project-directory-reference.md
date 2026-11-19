# 16. Complete Project Directory Reference

This document provides a structured, file-by-file reference detailing the responsibilities and architectural contributions of every important directory and file in the SkillBridge repository.

---

## 1. Root Repository Structure

```
skillbridge-hackathon/
├── backend/                  # Full-stack ASP.NET Core 10 Clean Architecture backend
├── frontend/                 # Standalone Angular 21 Single Page Application
├── docs/                     # Technical documentation suite (17 modules + index)
├── docker-compose.yml        # Multi-container orchestration (PostgreSQL database)
├── README.md                 # Repository landing page and quickstart guide
└── .env.example              # Documented environment variable template
```

- **`docker-compose.yml`:** Defines the local PostgreSQL 16 container (`postgres:16-alpine`), exposes port `5433:5432`, and configures the `postgres_data` volume mount.
- **`README.md`:** Project introduction, developer quickstart commands, and architectural summary.

---

## 2. Backend Directory Reference (`backend/`)

### 1. Solution Root & Configuration
- **`SkillBridge.sln`:** Visual Studio / .NET CLI solution linking all four production projects and the test suite.
- **`Directory.Build.props`:** Global MSBuild settings enforcing `net10.0` target framework, nullable reference types (`<Nullable>enable</Nullable>`), and latest C# compiler language features.

---

### 2. `SkillBridge.Domain` Project (Core Entities & Invariants)
- **`Common/AppException.cs`:** Base domain exception class.
- **`Common/Roles.cs`:** Static role definitions (`Roles.Candidate = "Candidate"`, `Roles.Employer = "Employer"`).
- **`Entities/Job.cs`:** Aggregate root representing an opening. Encapsulates 2-hour editing restrictions (`IsEditingAllowed()`), expiration checks (`IsExpired()`), and skill collection constraints.
- **`Entities/JobSkill.cs`:** Join entity associating a Job with a Skill, storing `IsRequired` flag and `MinProficiency`.
- **`Entities/JobApplication.cs`:** Aggregate representing an applicant. Enforces state transitions (`Received` -> `Shortlisted` / `Rejected` / `Withdrawn`) and holds attached CV metadata.
- **`Entities/CandidateProfile.cs`:** 1:1 entity linked to `AppUser`. Stores career preferences, headline, bio, experience history, and default CV details.
- **`Entities/CandidateSkill.cs`:** Join entity storing candidate skill proficiencies, years of experience, and `IsPreferred` flag.
- **`Entities/CandidateExperience.cs`:** Itemized work history records for experience matching.
- **`Entities/Skill.cs`:** Standardized skill taxonomy entity with normalized uppercase names.
- **`Entities/RefreshToken.cs`:** Entity storing SHA-256 hashed refresh tokens, `FamilyId`, rotation links, and revocation reasons.
- **`Enums/`:** Strongly typed enumerations (`ApplicationStatus`, `ExperienceLevel`, `JobType`, `SkillProficiency`, `WorkArrangement`).

---

### 3. `SkillBridge.Application` Project (Use Cases & Contracts)
- **`Common/Interfaces/IAppDbContext.cs`:** Abstraction exposing DbSets for domain entities, allowing services to query data without tight coupling to EF Core infrastructure.
- **`Common/Interfaces/IRealtimeNotifier.cs`:** Abstraction for dispatching real-time notifications (`ApplicationSubmitted`, `ApplicationStatusChanged`, etc.).
- **`Common/Interfaces/IEmailService.cs`:** Abstraction for transactional emails.
- **`Common/Interfaces/IFileStorageService.cs`:** Abstraction for reading, writing, and deleting uploaded files.
- **`Jobs/JobMatchingService.cs`:** Core matching algorithm calculating 0–100% compatibility scores and multi-factor breakdowns.
- **`Jobs/JobsService.cs`:** Use cases for creating, updating, closing, and paginating job postings.
- **`Applications/ApplicationsService.cs`:** Use cases for submitting applications, changing statuses, and withdrawing.
- **`CandidateProfiles/CandidateProfileService.cs`:** Use cases for retrieving profiles, updating skills, and managing default CV documents.
- **`Auth/AuthService.cs`:** Use cases for user registration, credential verification, token refresh rotation, and logout.
- **`Skills/SkillCatalogService.cs`:** Retrieves the global skills taxonomy.
- **Validators (`*Validator.cs`):** FluentValidation classes enforcing constraints on all request DTOs.

---

### 4. `SkillBridge.Infrastructure` Project (Data & External Integrations)
- **`Persistence/AppDbContext.cs`:** Subclasses `IdentityDbContext<AppUser>`, applies fluent configurations, and exposes database sets.
- **`Persistence/Configurations/`:** Fluent API mappings configuring table names, column lengths, composite primary keys, and foreign key cascade behaviors.
- **`Persistence/Migrations/`:** Sequential EF Core migration files tracking relational schema evolution.
- **`Identity/AppUser.cs`:** Extends `IdentityUser` with `FullName`, `CompanyName`, and audit timestamps.
- **`Identity/IdentitySeeder.cs`:** Automatically seeds demo candidate, demo employers across seven industries, and standardized skills.
- **`Identity/JwtTokenService.cs`:** Cryptographically generates JWT access tokens with user claims.
- **`Identity/RefreshTokenService.cs`:** Manages sliding refresh token creation, SHA-256 hashing, single-use rotation, and family reuse revocation.
- **`Services/BrevoEmailService.cs`:** Transactional email client integrating with Brevo REST API, featuring automatic fallback to mock logging.
- **`Services/LocalFileStorageService.cs`:** Manages file streams on disk with magic-byte signature validation for PDF and DOCX documents.

---

### 5. `SkillBridge.Api` Project (Gateway & Host)
- **`Program.cs`:** Host entry point configuring services, rate limiting, JWT bearer options, CORS, middleware pipeline, and background workers.
- **`Controllers/AuthController.cs`:** Endpoints for register, login, social auth, token refresh, logout, and password recovery.
- **`Controllers/JobsController.cs`:** Endpoints for public job search, job details, and candidate application submission.
- **`Controllers/EmployerJobsController.cs`:** Endpoints for employer job listings, job editing, and closing.
- **`Controllers/ApplicationsController.cs`:** Endpoints for status transitions, withdrawals, and CV streaming.
- **`Controllers/CandidateProfileController.cs`:** Endpoints for profile management and CV document uploads.
- **`Controllers/AccountController.cs`:** Endpoints for security audits, password changes, session revocation, and account deletion.
- **`Hubs/SkillBridgeHub.cs`:** SignalR WebSocket hub managing client connections, role groups, and job rooms.
- **`Realtime/SignalRNotifier.cs`:** Implements `IRealtimeNotifier`, pushing typed events through the SignalR hub.
- **`Services/JobExpirationWorker.cs`:** Background service executing every 15 seconds to auto-close expired postings.
- **`Errors/AppExceptionHandler.cs`:** Global exception handler translating domain errors into standard RFC 7807 ProblemDetails.

---

## 3. Frontend Directory Reference (`frontend/`)

### 1. `src/app/core/` (Singletons, Interceptors & Infrastructure)
- **`http/auth.interceptor.ts`:** Functional HTTP interceptor injecting `Authorization: Bearer <token>` and `X-XSRF-TOKEN` headers on outgoing requests, and catching 401 errors to trigger token refresh.
- **`guards/auth.guard.ts`:** `authGuard` (verifies authentication) and `roleGuard` (verifies specific role access).
- **`services/auth.service.ts`:** Central authentication facade tracking `currentUser` signal and token state.
- **`services/realtime.service.ts`:** Manages persistent SignalR WebSocket connection to `/hubs/app` and exposes reactive RxJS event observables.
- **`services/jobs.service.ts`:** Data service for job retrieval, caching, and mutation.
- **`services/applications.service.ts`:** Data service for application lifecycle actions and CV downloads.
- **`services/theme.service.ts`:** Theme management service persisting user preference to `localStorage` and toggling `data-theme`.
- **`utils/countdown.ts`:** Monospace countdown timer computing remaining hours and minutes until deadlines.
- **`utils/error-formatter.ts`:** Parses RFC 7807 ProblemDetails responses into user-friendly error strings.

---

### 2. `src/app/features/` (Feature Views & Workflows)
- **`landing/`:** Marketing landing page (`landing.component.ts`) featuring the audience perspective switcher and live showcase (`walkthrough-showcase.component.ts`).
- **`auth/`:** Authentication views (`login/`, `register/`, `confirm-email/`, `forgot-password/`, `reset-password/`, `unauthorized/`).
- **`candidate/jobs/job-list/`:** Candidate job discovery dashboard with multi-criteria filtering, search docks, and 100% match toggling.
- **`candidate/jobs/job-detail/`:** Detailed opening view with compatibility scoring breakdown and one-click application submission.
- **`candidate/onboarding/`:** Step-by-step onboarding wizard for newly registered candidates.
- **`candidate/profile/`:** Candidate profile dossier, skill proficiencies, experience timeline, and CV upload manager.
- **`candidate/applications/`:** Candidate application tracking pipeline with status badges and self-service withdrawal.
- **`employer/jobs/`:** Central employer management console displaying active postings and 2-hour edit lock indicators.
- **`employer/jobs/employer-post-job/`:** Split-screen editorial job studio with sticky live candidate preview and ETB compensation module.
- **`employer/jobs/employer-job-applicants/`:** Applicant evaluation dossier with Kanban status advancement, CV previews, and 100% match filtering.
- **`employer/applications/`:** Cross-opening candidate pipeline with Bento search dock and pagination footer.
- **`account/`:** Account security center for password changes, email updates, session revocation, and account deletion.

---

### 3. `src/app/shared/` (Reusable Controls & Utilities)
- **`components/pagination/`:** Monospace pagination dock with page buttons and items-per-page selector.
- **`components/theme-toggle/`:** Icon button or segmented control for Light, Dark, and System theme switching.
- **`components/user-profile-menu/`:** Header user avatar pill, initials generator, role indicators, and sign-out dropdown.
- **`utils/salary.util.ts`:** Parses, formats, and validates Ethiopian Birr (ETB) compensation with comma formatting and pay periods.

