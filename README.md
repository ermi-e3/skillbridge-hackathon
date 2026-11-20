# SkillBridge

> **The Modern, Skills-First Talent Marketplace & Real-Time Hiring Platform**  
> *Transforming recruitment from keyword-guessing into verified, deterministic skill matching.*

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-21.2-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time-orange?logo=signalr&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![TailwindCSS](https://img.shields.io/badge/Tailwind-CSS-38B2AC?logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)
[![Vitest](https://img.shields.io/badge/Vitest-Passed%20(217)-6E9F18?logo=vitest&logoColor=white)](https://vitest.dev/)

---

## Table of Contents

- [Project Overview](#project-overview)
- [The Problem Statement](#the-problem-statement)
- [The Proposed Solution](#the-proposed-solution)
- [Key Features & Capabilities](#key-features--capabilities)
- [Technology Stack](#technology-stack)
- [System Architecture](#system-architecture)
- [Getting Started — Run Locally](#getting-started--run-locally)
  - [1. Prerequisites](#1-prerequisites)
  - [2. Clone & Directory Navigation](#2-clone--directory-navigation)
  - [3. Start Database (PostgreSQL)](#3-start-database-postgresql)
  - [4. Start Backend (ASP.NET Core API)](#4-start-backend-aspnet-core-api)
  - [5. Start Frontend (Angular Client)](#5-start-frontend-angular-client)
- [User Roles & Guided Workflows](#user-roles--guided-workflows)
- [Environment Variables Reference](#environment-variables-reference)
- [Project Directory Structure](#project-directory-structure)
- [API Reference & Interactive Consoles](#api-reference--interactive-consoles)

---

## Project Overview

**SkillBridge** is a modern, full-stack, skills-first talent marketplace and hiring management web platform. Traditional recruitment portals rely heavily on unstructured resume text parsing, keyword hunting, and opaque black-box machine-learning algorithms that unfairly disqualify qualified talent while rewarding keyword-stuffed resumes.

SkillBridge fundamentally redesigns this dynamic by treating technical and domain skills as **first-class relational entities** with explicit proficiency ratings, experience tiers, and mathematical requirement weights. 

The platform delivers a dual-sided, real-time ecosystem connecting **Candidates** (job seekers seeking transparent, merit-based career alignment) with **Employers** (hiring managers and recruiters demanding calibrated candidate pipelines). Powered by a deterministic 6-factor matching engine (0–100%), real-time bidirectional WebSockets via ASP.NET Core SignalR, automated background job expiration, and security-hardened account protection, SkillBridge brings trust, transparency, and velocity to modern technical and professional hiring.

---

## The Problem Statement

Traditional hiring platforms and legacy Applicant Tracking Systems (ATS) suffer from three systemic failures:

1. **Keyword Stuffing & Algorithmic Opacity:**
   - Conventional ATS tools parse PDF/Word resumes using crude string matching. Candidates who lack arbitrary synonyms are silently filtered out, while those who game algorithms with white-text keyword stuffing pass through regardless of true technical competence.
   - Candidates receive zero feedback on why they were rejected or how well their background matched the job description.

2. **The "Application Black Hole":**
   - Job seekers submit applications into an asynchronous vacuum, rarely knowing if their profile was ever opened, reviewed, or passed over.
   - Employers waste countless hours manually sifting through uncalibrated applicant inboxes without objective ranking or real-time pipeline status telemetry.

3. **Stale Postings & Shifting Goalposts:**
   - Legacy job boards keep expired listings active for months to artificially inflate traffic metrics, consuming applicant time on closed vacancies.
   - Employers often alter job requirements midway through an active recruitment drive, invalidating previously submitted applications without notice.

---

## The Proposed Solution

SkillBridge eliminates resume guesswork through a structured, verified, and real-time hiring paradigm:

- **Relational Skill Architecture:** A curated catalog of 170+ standardized skills across Engineering, Healthcare, Finance, Design, HR, Legal, and Skilled Trades. Candidates declare exact proficiencies (`Beginner`, `Intermediate`, `Advanced`, `Expert`), eliminating fuzzy string matching.
- **Deterministic 6-Factor Matching Engine:** Every job opportunity computes an objective 0–100% compatibility score broken down across Required Skills, Preferred Skills, Experience Longevity, Work Arrangement (Remote/Hybrid/On-Site), Geographic Proximity, and Job Type.
- **Bi-Directional Real-Time SignalR WebSockets:** Employers receive immediate push toasts the second an applicant applies; candidates receive instant status updates (`Received` &rarr; `Shortlisted` / `Rejected`) without needing to refresh their browser.
- **Built-in 2-Hour Editing Lock Invariant:** Employers can fine-tune requirements within 2 hours of creation. After 2 hours, core requirements lock automatically to protect applicant evaluation integrity.
- **Automated Lifecycle Background Worker:** An autonomous background worker (`JobExpirationWorker`) verifies application deadlines every 15 seconds, automatically closing expired postings and broadcasting closure events across all active client feeds.

---

## Key Features & Capabilities

### Candidate Experience
- **Tactile Onboarding & Profile Studio:** Guided step-by-step setup selecting skills, configuring proficiencies, crafting headlines, and setting work arrangement preferences.
- **Algorithmic Career Discovery:** Filter and sort job feeds by verified mathematical match percentage, industry departments, work arrangements, job types, and experience levels.
- **Interactive Match Breakdown:** Inspect exactly how match scores are computed via visual telemetry bars (Required Skills fit, Preferred Skills bonus, Experience alignment, Location, and Work Arrangement).
- **One-Click Application & CV Engine:** Submit applications using a verified profile with an attached default CV (or opening-specific CV) in PDF or DOCX format (validated server-side with magic-byte inspection).
- **Self-Service Application Withdrawal:** Candidates retain agency to withdraw submitted applications at any time while in `Received` status.

### Employer Studio & Applicant Pipeline
- **Job Posting Studio:** Compose comprehensive openings with distinct Required (must-have) vs. Preferred (nice-to-have) skills, experience floors, deadlines, ETB salary ranges, and work arrangements.
- **Live Markdown & Card Preview:** Real-time dual-pane preview reflecting exactly how the opening appears to candidates before publishing.
- **Applicant Telemetry Pipeline:** Review candidate rosters ranked by algorithmic match score with a dedicated *"100% Match Only"* toggle.
- **In-Drawer Candidate Dossier & CV Viewer:** Inspect full candidate profiles, skill histories, and resume attachments directly within an interactive slide-over drawer without page navigation.
- **Kanban Status Transitions:** Move applicants smoothly through `Received` &rarr; `Shortlisted` or `Rejected` with immediate candidate push notifications.

### Platform & Security Operations
- **Real-Time Notification Hub:** SignalR WebSockets broadcast live status updates, new application toasts, and job closure countdowns.
- **Stateless JWT & Refresh Token Rotation:** Secure authentication utilizing short-lived access tokens, sliding-window refresh token rotation, and HttpOnly/SameSite cookie security.
- **Audit Trails & Security Controls:** Active session management, full account deletion with cascading cleanups, and granular role-based access control (`Candidate` vs. `Employer`).

---

## Technology Stack

| Category | Technology | Version | Purpose in SkillBridge |
| :--- | :--- | :--- | :--- |
| **Frontend Framework** | **Angular** | `21.2.0` | Client Single Page Application with Standalone Components & Signals |
| **Frontend CLI & Build** | **Angular CLI / Vite** | `21.2.24` | Ultra-fast ESM compilation, development server, and production bundling |
| **Frontend Language** | **TypeScript** | `5.9.2` | Compile-time type safety across models, services, and reactive forms |
| **Styling & Design System**| **Tailwind CSS & SCSS** | Modern | Custom design tokens, dark/light theme engine, and mobile-responsive layouts |
| **Real-Time Client** | **@microsoft/signalr** | `10.0.11` | Persistent WebSocket client connection to backend SignalR hub |
| **Frontend Testing** | **Vitest & JSDOM** | `4.0.8` | Lightning-fast unit testing (28 test suites, 217 unit tests passing) |
| **Backend Framework** | **ASP.NET Core Web API**| `10.0` (`net10.0`)| High-performance REST API, background workers, and middleware pipeline |
| **Backend Language** | **C# 13** | Latest | Type-safe domain models, records, primary constructors, and pattern matching |
| **ORM & Data Access** | **Entity Framework Core** | `10.0.0` | Relational entity mappings, LINQ queries, and PostgreSQL migrations |
| **Database Provider** | **Npgsql.EntityFrameworkCore** | `10.0.0` | High-throughput PostgreSQL driver for EF Core 10 |
| **Database Engine** | **PostgreSQL** | `17-alpine` | Relational ACID database running containerized via Docker |
| **Identity & Security** | **ASP.NET Core Identity** | `10.0.0` | Password hashing (PBKDF2), role management, security stamps |
| **Token Authentication** | **JwtBearer Authentication** | `10.0.0` | Stateless HMAC-SHA256 JWT validation and cookie inspection |
| **Real-Time Server** | **ASP.NET Core SignalR** | `10.0.0` | Bidirectional WebSocket hub managing client groups and event streams |
| **Validation Engine** | **FluentValidation** | `12.0.0` | Strongly-typed validation rules and business invariant checks |
| **API Documentation** | **Scalar & OpenAPI/Swagger** | `v1` / `7.x` | Modern interactive API documentation and testing consoles |
| **Email Service** | **Brevo REST API** | Custom HTTP | Transactional email delivery for password resets and verification |
| **Containerization** | **Docker & Docker Compose** | Compose v2 | Local database service provisioning on host port `5433` |

---

## System Architecture

SkillBridge is built following **Clean Architecture (Onion / Hexagonal)** on the backend and a **Reactive, Component-Driven Architecture** on the frontend:

```mermaid
graph TB
    subgraph ClientLayer["Frontend Client Layer (Angular 21 SPA)"]
        UI["Standalone Components & Signals<br/>(Jobs Feed • Employer Studio • Dossier Drawer)"]
        AuthInt["Auth Interceptor<br/>(Bearer Token Injection • XSRF Header Sync)"]
        RTClient["Realtime Service<br/>(@microsoft/signalr • WebSocket Handshake)"]
    end

    subgraph APILayer["Backend Host Layer (SkillBridge.Api)"]
        MW["Security Middleware Pipeline<br/>(RateLimiter • CORS • JwtBearer • ExceptionHandler)"]
        Controllers["API Controllers<br/>(Auth • Jobs • Applications • Profiles • Account)"]
        Hub["SignalR Hub (/hubs/app)<br/>(User Channels • Status Broadcasts)"]
        Worker["JobExpirationWorker<br/>(BackgroundService • 15s Timer)"]
    end

    subgraph AppLayer["Application Layer (SkillBridge.Application)"]
        Services["Application Use Cases<br/>(JobsService • MatchingService • ApplicationsService)"]
        Validators["FluentValidation Rules<br/>(Registration • Job Creation • Profile Updates)"]
        Interfaces["Abstractions<br/>(IAppDbContext • IRealtimeNotifier • IEmailService)"]
    end

    subgraph DomainLayer["Domain Layer (SkillBridge.Domain)"]
        Entities["Domain Entities & Invariants<br/>(Job • JobSkill • CandidateProfile • Skill • Application)"]
        Rules["Business Invariants<br/>(2-Hour Edit Lock • Status Transitions • Skill Bounds)"]
    end

    subgraph InfraLayer["Infrastructure Layer (SkillBridge.Infrastructure)"]
        DbContext["AppDbContext (EF Core 10)<br/>(Relational Mappings • Migrations • IdentitySeeder)"]
        Identity["Identity Services<br/>(UserManager • JwtTokenService • RefreshTokenService)"]
        Storage["LocalFileStorageService<br/>(Stream Validation • Magic Bytes)"]
        Brevo["BrevoEmailService<br/>(REST API Client)"]
    end

    subgraph DataStore["Persistence & Storage Layer"]
        Postgres[("PostgreSQL 17 Database<br/>(Host Port: 5433)")]
        Disk[("Local Secure Storage<br/>(Candidate CV Uploads: PDF/DOCX)")]
    end

    UI --> AuthInt
    UI --> RTClient
    AuthInt -->|HTTP / JSON ProblemDetails| MW
    RTClient <-->|WebSockets (WSS)| Hub
    MW --> Controllers
    Controllers --> Services
    Services --> Validators
    Services --> Interfaces
    Services --> Entities
    Services --> Rules
    Interfaces -.->|Implemented by| InfraLayer
    DbContext --> Postgres
    Identity --> DbContext
    Storage --> Disk
    Worker --> Services
    Services --> Hub
```

---

## Getting Started — Run Locally

Follow this step-by-step guide to run SkillBridge locally.

### 1. Prerequisites

Ensure the following runtimes and tools are installed:
- **Git:** Version 2.40 or newer
- **Docker & Docker Compose:** Docker Desktop (running)
- **.NET SDK:** .NET 10 SDK (`10.0.100` feature band or newer) — verify with `dotnet --version`
- **Node.js:** Node.js `20.19+` or `22.12+` with npm — verify with `node -v`

---

### 2. Clone & Directory Navigation

Clone the repository and enter the project root directory:

```bash
git clone https://github.com/your-username/skillbridge-hackathon.git
cd skillbridge-hackathon
```

---

### 3. Start Database (PostgreSQL)

SkillBridge runs PostgreSQL 17 in a lightweight Docker container mapped to host port **`5433`** (avoiding collisions with any existing local PostgreSQL instances on port 5432):

```bash
docker compose up -d
```

Verify that the database container is healthy:
```bash
docker compose ps
```

---

### 4. Start Backend (ASP.NET Core API)

Open a **new terminal window** in the repository root:

```bash
# Navigate to the backend directory
cd backend

# Restore dependencies and local tools
dotnet restore
dotnet tool restore

# Run the API project
dotnet run --project src/SkillBridge.Api
```

On first startup, the backend automatically:
1. Applies all pending Entity Framework Core database migrations to PostgreSQL.
2. Runs the idempotent seeder, populating the 170+ skills catalog, demo employer organizations, and **25 realistic job postings across 5 industry departments**.

**Backend Service URLs:**
- **API Base URL:** `http://localhost:5080` (or `https://localhost:7080`)
- **Health Check Endpoint:** [http://localhost:5080/api/health](http://localhost:5080/api/health)
- **Interactive Swagger UI:** [http://localhost:5080/swagger](http://localhost:5080/swagger)
- **Interactive Scalar API Reference:** [http://localhost:5080/scalar/v1](http://localhost:5080/scalar/v1)

---

### 5. Start Frontend (Angular Client)

Open a **second terminal window** in the repository root:

```bash
# Navigate to the frontend directory
cd frontend

# Install npm packages
npm install

# Start the Angular development server
npx ng serve
```

Once compilation completes, open your browser and navigate to:
**[http://localhost:4200](http://localhost:4200)**

---


## User Roles & Guided Workflows

### Candidate Experience Journey
1. **Sign In:** Log in as `user@hackathon.local` (or register a fresh Candidate account).
2. **Onboarding / Profile Review:** Visit `/candidate/profile` to view or edit registered skills, proficiencies, headline, and uploaded CV document.
3. **Explore Jobs (`/jobs`):**
   - Use the **Department** dropdown to filter by *Engineering & Architecture*, *Human Resources*, *Legal & Compliance*, *Creative & Design*, or *Other / General*.
   - Toggle **Highest Match First** to sort opportunities by mathematical fit.
   - Use the `⌘K` or `Ctrl+K` command search to filter by role or company name.
4. **Inspect Match Breakdown & Apply:**
   - Click any job card to open `/jobs/:id`.
   - Inspect the real-time **Skill Fit Telemetry breakdown** showing Required Skills fit, Preferred bonus points, and Experience alignment.
   - Select your profile default CV (or upload an alternative CV) and click **Submit Application**.
5. **Track Telemetry (`/candidate/applications`):**
   - View your live application roster.
   - Test self-service application withdrawal while in `Received` status.

### Employer Hiring Studio Journey
1. **Sign In:** Log in as any Employer (e.g., `eng@hackathon.local`, `hr@hackathon.local`, or `admin@hackathon.local`).
2. **Post a New Opening (`/employer/jobs/new`):**
   - Fill in Job Title, Department, Work Arrangement, and ETB Salary range.
   - Select Required Skills (must-have) and Preferred Skills (nice-to-have) from the catalog picker.
   - Review the live card preview and click **Publish Job Opening**.
3. **Application Management (`/employer/applications`):**
   - View executive KPI metric boxes: *Total Applications*, *Pending Review*, and *Shortlisted*.
   - Filter openings by Active, Closed, or Has Applicants.
4. **Review Applicants Pipeline (`/employer/jobs/:id/applicants`):**
   - Click **Review Applicants** on any active opening.
   - Sort candidates by **Match %**, toggle the **100% Match Only** filter.
   - Click **View Profile &rarr;** to expand the slide-over candidate dossier, review declared skills, and preview their attached CV.
   - Click **Shortlist** or **Reject** to update hiring status. If the candidate is logged in on another window, they will receive a real-time SignalR status toast immediately!

---

## Environment Variables Reference

SkillBridge is configured out-of-the-box for seamless local evaluation. For production or custom deployments, the following configuration parameters are supported:

| Configuration Key | Component | Required | Purpose | Local Default |
| :--- | :--- | :--- | :--- | :--- |
| `ConnectionStrings:Default` | Backend | **Yes** | PostgreSQL connection string | `Host=localhost;Port=5433;Database=skillbridge;Username=postgres;Password=postgres` |
| `Jwt:Key` | Backend | **Yes** | Secret signing key for HMAC-SHA256 JWT tokens | `Min 32-character development secret` |
| `Jwt:Issuer` | Backend | **Yes** | Token issuer claim identifier | `SkillBridge` |
| `Jwt:Audience` | Backend | **Yes** | Token audience claim identifier | `SkillBridge.Client` |
| `Jwt:ExpirationHours` | Backend | No | Access token lifespan in hours | `8` |
| `Seed:RunInitialData` | Backend | No | Enables database migrations and demo seed on launch | `true` (in Development) |
| `Brevo:ApiKey` | Backend | No | Brevo transactional email delivery API key | Safe development placeholder |
| `Brevo:UseMock` | Backend | No | Skips external email dispatch when true | `true` / Development mock |
| `apiUrl` | Frontend | **Yes** | REST API base URL consumed by Angular HttpClient | `http://localhost:5080/api` |
| `hubUrl` | Frontend | **Yes** | SignalR WebSocket hub URL | `http://localhost:5080/hubs/app` |

---

## Project Directory Structure

```text
skillbridge-hackathon/
├── docker-compose.yml                  # PostgreSQL 17 container on host port 5433
├── README.md                           # Master project documentation & judges guide
├── docs/                               # 17 comprehensive architectural and API specifications
│   ├── 01-project-overview.md
│   ├── 02-technology-stack.md
│   ├── 03-system-architecture.md
│   └── 09-api-reference.md
│
├── backend/                            # ASP.NET Core 10 Clean Architecture Monorepo
│   ├── SkillBridge.sln
│   ├── Directory.Build.props           # Common .NET 10 compilation settings
│   ├── Directory.Packages.props        # Centralized package version management
│   ├── src/
│   │   ├── SkillBridge.Domain/         # Core business entities, enums, and domain invariants
│   │   ├── SkillBridge.Application/    # Use cases, DTOs, matching engine, and validators
│   │   ├── SkillBridge.Infrastructure/ # EF Core 10, PostgreSQL persistence, Identity & Seeder
│   │   │   └── Persistence/Seed/       # Standalone JSON seed catalog (jobs-seed-data.json)
│   │   └── SkillBridge.Api/            # REST controllers, SignalR hub, middleware pipeline
│   └── tests/
│       └── SkillBridge.Auth.Tests/     # Integration test suite for auth, jobs & real-time events
│
└── frontend/                           # Angular 21 Single Page Application
    ├── angular.json                    # Angular CLI & build configuration
    ├── package.json                    # Dependencies (@angular 21, @microsoft/signalr, vitest)
    └── src/
        ├── app/
        │   ├── core/                   # Auth, HTTP interceptors, API services, SignalR client
        │   ├── features/
        │   │   ├── auth/               # Login, Register, Password Reset, Unauthorized
        │   │   ├── candidate/          # Onboarding, Job Explorer, Profile, Applications
        │   │   ├── employer/           # Job Post Studio, My Jobs, Applications Management
        │   │   └── landing/            # Interactive Hero, Audience Switcher, Walkthrough Video
        │   └── shared/                 # Design tokens, user menus, navigation, pagination
        └── styles/                     # Tailwind CSS imports and global design variables
```

---

## API Reference & Interactive Consoles

The backend exposes 26+ strongly-typed REST endpoints documented via **OpenAPI / Swagger** and **Scalar**:

| HTTP Method | Route | Description | Auth Required |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/register` | Register new Candidate or Employer account | Anonymous |
| `POST` | `/api/auth/login` | Authenticate with credentials and receive JWT | Anonymous |
| `POST` | `/api/auth/refresh-token` | Exchange rotating refresh token for a fresh JWT | Cookie-based |
| `GET` | `/api/skills` | Retrieve the standardized 170+ skills catalog | Anonymous |
| `GET` | `/api/jobs` | Browse active openings with multi-factor filters & pagination | Authenticated |
| `GET` | `/api/jobs/{id}` | Retrieve job details with computed candidate skill fit breakdown | Authenticated |
| `POST` | `/api/jobs` | Publish a new job opening (Enforces 2-Hour Edit Rule) | Employer Only |
| `PUT` | `/api/jobs/{id}` | Update job opening (Allowed within 2-hour window only) | Employer Only |
| `POST` | `/api/jobs/{id}/close` | Manually close an active opening | Employer Only |
| `POST` | `/api/jobs/{id}/apply` | Submit verified skills application with CV attachment | Candidate Only |
| `POST` | `/api/applications/{id}/withdraw` | Withdraw application while in `Received` status | Candidate Only |
| `GET` | `/api/employer/jobs` | Retrieve all openings posted by the authenticated employer | Employer Only |
| `GET` | `/api/employer/jobs/{id}/applicants`| View applicant roster with algorithmic match scores | Employer Only |
| `PATCH`| `/api/employer/applications/{id}/status`| Advance status (`Received` &rarr; `Shortlisted` / `Rejected`)| Employer Only |
| `GET` | `/api/candidate/profile` | Retrieve candidate skills dossier and preferences | Candidate Only |
| `PUT` | `/api/candidate/profile` | Update candidate headline, skills list, and preferences | Candidate Only |
| `POST` | `/api/candidate/cv` | Upload candidate CV document (PDF/DOCX, magic bytes checked)| Candidate Only |
| `GET` | `/api/candidate/cv` | Stream/download candidate CV attachment | Authenticated |

**Interactive API Consoles:**
- **Swagger UI:** [http://localhost:5080/swagger](http://localhost:5080/swagger)
- **Scalar Documentation:** [http://localhost:5080/scalar/v1](http://localhost:5080/scalar/v1)

---

### License

This project is developed for hackathon presentation and open-source evaluation under the MIT License.