# SkillBridge — Digital Internship & Job Matching

SkillBridge connects candidates with internships and entry-level technology jobs using a shared skill catalogue and server-computed match percentages.

## Team Members & Responsibilities

| Team member | Responsibility |
| --- | --- |
| Abebe Mihiretu | Product ownership, pitch, demo coordination |
| Nebiyu Daniel; Abdulmenan Amin | Angular screens, reactive forms, responsive UI |
| Beka Aman; Ermiyas Eshetu | ASP.NET Core API, services, validation, authorization |
| Ermiyas Eshetu and all team members | Database, full-stack integration, QA, testing and demo verification |

## Tech Stack Used

- Angular 21 with TypeScript and standalone components
- ASP.NET Core Web API on .NET 10
- Entity Framework Core 10
- PostgreSQL 17
- Docker Compose for PostgreSQL
- JWT authentication with Candidate and Employer roles

## How to Run Locally

### Prerequisites

- Docker Desktop
- .NET 10 SDK
- Node.js 20.19+ or 22.12+

### 1. Start PostgreSQL

From the repository root:

```powershell
docker compose up -d
```

PostgreSQL is available on `localhost:5433`.

### 2. Start the backend

Open a terminal in `backend`:

```powershell
dotnet restore
dotnet tool restore
dotnet ef database update --project src/SkillBridge.Infrastructure --startup-project src/SkillBridge.Api
$env:Jwt__Key = "skillbridge-local-development-signing-key-2026"
$env:ConnectionStrings__Default = "Host=localhost;Port=5433;Database=skillbridge;Username=postgres;Password=postgres"
dotnet run --project src/SkillBridge.Api --launch-profile http
```

Backend URLs:

- API: http://localhost:5080
- Health check: http://localhost:5080/api/health
- Scalar API documentation: http://localhost:5080/scalar

The JWT key must contain at least 32 UTF-8 bytes. Use an environment variable or a local, uncommitted configuration file.

### 3. Start the frontend

Open another terminal in `frontend`:

```powershell
npm install
npm start
```

Frontend URL: http://localhost:4200

## Test Accounts & Demo Credentials

Demo users are seeded automatically in Development:

| Role | Email | Password |
| --- | --- | --- |
| Employer | `admin@hackathon.local` | `Admin123!` |
| Candidate | `user@hackathon.local` | `User123!` |

## Working Features

- Candidate and Employer registration and login
- JWT authentication and role-based API authorization
- Candidate profile with headline, biography, GitHub URL and technical skills
- Seeded shared skill catalogue
- Employer job creation with required skill tags
- Candidate job browsing, title search and skill filtering
- Server-side match percentage calculation
- Candidate match percentage and matched/missing skills
- Candidate application submission with optional cover note
- Database and service protection against duplicate applications
- Candidate “My Applications” view with application status
- Candidate application withdrawal while status is `Received`
- Employer “My Jobs” dashboard with applicant counts
- Employer applicant list ranked by match percentage
- Applicant status filtering and full-match filtering
- Employer status changes from `Received` to `Shortlisted` or `Rejected`
- GitHub profile links on applicant cards
- Validation, friendly ProblemDetails errors, loading states and empty states
- PostgreSQL persistence through EF Core migrations

## Core Server Rules

- Match percentage is calculated as overlapping skills divided by required job skills.
- Percentages are rounded to the nearest whole number using midpoint-away-from-zero rounding.
- A candidate cannot apply to the same job twice.
- Only candidates can apply to jobs.
- Only employers can post jobs and manage their job applicants.
- Employers can only view and update applicants for their own jobs.
- Application status can only move from `Received` to `Shortlisted` or `Rejected`.
- A candidate must have at least one skill before applying.
- Job creation requires between 1 and 15 valid required skills.

## Architecture

The backend follows Clean Architecture:

```text
API -> Application -> Domain
Infrastructure -> Application + Domain
```

- `SkillBridge.Domain`: entities, value rules and match calculation
- `SkillBridge.Application`: use-case services, DTOs and business workflows
- `SkillBridge.Infrastructure`: EF Core, PostgreSQL, Identity and seed data
- `SkillBridge.Api`: controllers, authentication, error handling and HTTP concerns
- `frontend`: Angular components, services, routing, forms and API integration

The frontend communicates with the backend through Angular `HttpClient`; it does not use mock JSON data.

## Validation and Testing

Run backend tests from `backend`:

```powershell
dotnet test
```

Run frontend tests from `frontend`:

```powershell
npm test
```

Before the demo, verify this journey:

1. Employer logs in and posts a job with required skills.
2. Candidate logs in and updates their skills.
3. Candidate filters jobs and opens the employer's job.
4. Candidate applies once, then attempts to apply again.
5. The second application is rejected with HTTP 400.
6. Employer opens the applicant list and sees the server-computed match.
7. Employer shortlists or rejects the application.
8. Candidate refreshes “My Applications” and sees the new status.

## Known Limitations & Bugs

- The official brief specifies .NET 8 or .NET 9; this implementation currently uses .NET 10.
- Full status-history records are not stored; only the latest `StatusChangedAt` value is retained.
- There is no SignalR/live push notification; refreshing the page retrieves updated data.
- Refresh tokens are not implemented; the demo uses an eight-hour JWT.
- Pagination is supported by the jobs API, but the frontend does not yet expose page controls.
- Profile photo, CV upload and file upload are outside the MVP.
- Job closing is represented by `IsOpen` in the data model, but a complete employer close-job workflow is not yet exposed in the UI.

## Useful Commands

| Task | Command |
| --- | --- |
| Add a migration | `dotnet ef migrations add <Name> --project src/SkillBridge.Infrastructure --startup-project src/SkillBridge.Api --output-dir Persistence/Migrations` |
| Apply migrations | `dotnet ef database update --project src/SkillBridge.Infrastructure --startup-project src/SkillBridge.Api` |
| Reset the database | `docker compose down -v` then `docker compose up -d` |
| PostgreSQL shell | `docker exec -it skillbridge-postgres psql -U postgres -d skillbridge` |
