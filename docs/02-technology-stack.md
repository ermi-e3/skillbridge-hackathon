# 02. Technology Stack

This document provides a comprehensive inventory of all technologies, frameworks, libraries, and tools used across the SkillBridge application. Every item listed is verified against project configuration files, dependency manifests, and source code.

---

## 1. Technology Classification Matrix

| Category | Primary Technology / Tool | Version | Implementation Scope |
| :--- | :--- | :--- | :--- |
| **Frontend Framework** | Angular | `21.2.0` | Client Single Page Application (`frontend/`) |
| **Frontend CLI & Build** | Angular CLI / Angular Build | `21.2.24` | Vite-based build pipeline and dev server |
| **Frontend Language** | TypeScript | `5.9.2` | Strong typing across all components and services |
| **Frontend State & Async** | Angular Signals & RxJS | `7.8.0` | Reactive component state & HTTP observables |
| **Real-Time Client** | `@microsoft/signalr` | `10.0.11` | Persistent WebSocket connection to backend hub |
| **Frontend Testing** | Vitest & jsdom | `4.0.8` / `28.0.0` | Unit and component testing (25 test suites, 200 tests) |
| **Backend Framework** | ASP.NET Core Web API | `10.0` (`net10.0`) | REST API and background workers (`backend/src/SkillBridge.Api`) |
| **Backend Language** | C# | `13.0` / Latest | Type-safe, object-oriented domain and service logic |
| **ORM & Data Access** | Entity Framework Core 10 | `10.0.0` | Relational modeling, LINQ queries, and migrations |
| **Database Provider** | `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.0` | PostgreSQL provider for EF Core 10 |
| **Database Engine** | PostgreSQL | `16-alpine` | Relational ACID database running via Docker |
| **Identity & Auth** | ASP.NET Core Identity | `10.0.0` | User account management, password hashing, roles |
| **Token Authentication** | `Microsoft.AspNetCore.Authentication.JwtBearer`| `10.0.0` | Stateless JWT token validation middleware |
| **Real-Time Server** | ASP.NET Core SignalR | `10.0.0` | Real-time push hub and room management |
| **Validation Engine** | FluentValidation | `12.0.0` | Strongly typed validation rules for request DTOs |
| **API Documentation** | Scalar & Swashbuckle OpenAPI | `Scalar.AspNetCore` / `Swashbuckle 7.x` | Modern interactive API reference and Swagger UI |
| **Email Service** | Brevo (Sendinblue) REST API | Custom HTTP Client | Transactional emails (confirmations, password resets) |
| **File Storage** | Local File Storage Service | .NET IO Streams | Sanitized local disk storage for CV documents |
| **Containerization** | Docker & Docker Compose | Compose v2 | Multi-container orchestration (PostgreSQL database) |

---

## 2. Frontend Technologies

### 1. Angular (`@angular/core`, `@angular/common`, `@angular/forms`, `@angular/router`)
- **Version:** `21.2.0`
- **What it does:** Serves as the primary client-side web application framework.
- **Why it was chosen:** Provides enterprise-grade modularity, high-performance compilation, native dependency injection, and modern signal-based reactivity for rapid state updates without Zone.js overhead.
- **Where & How it is implemented:**
  - Configured in [frontend/package.json](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/package.json) and [frontend/angular.json](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/angular.json).
  - All components are built as modern **Standalone Components** (`standalone: true`).
  - Reactive forms (`ReactiveFormsModule`) handle complex validation on registration, login, profile updates, and job creation.
- **Dependencies:** Relies on TypeScript 5.9.2 and RxJS 7.8.0. Every feature component in `frontend/src/app/features` depends on Angular.

### 2. TypeScript
- **Version:** `~5.9.2`
- **What it does:** Provides static typing, interfaces, enums, and compile-time verification for JavaScript.
- **Why it was chosen:** Enforces strict contract alignment between frontend models and backend DTOs, preventing runtime `undefined` errors.
- **Where & How it is implemented:**
  - Configured via [frontend/tsconfig.json](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/tsconfig.json) and [frontend/tsconfig.app.json](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/tsconfig.app.json) with `strict: true`.
  - Strong typing applied to API responses in `frontend/src/app/core/models/`.
- **Dependencies:** All frontend code depends on TypeScript.

### 3. Angular Signals & RxJS
- **Version:** Angular Signals (built-in) / RxJS `~7.8.0`
- **What it does:**
  - **Signals (`signal()`, `computed()`, `effect()`):** Manages local and shared component reactivity, filtering criteria, and dynamic UI state.
  - **RxJS (`Observable`, `Subject`, `tap`, `switchMap`):** Manages asynchronous HTTP requests, event streams, and real-time push subscriptions.
- **Why it was chosen:** Angular Signals provide granular, glitch-free DOM updates with zero boilerplate, while RxJS excels at composing asynchronous network streams.
- **Where & How it is implemented:**
  - Used in `RealtimeService`, `JobListComponent`, `EmployerJobApplicantsComponent`, and `ThemeService`.
- **Dependencies:** Integrated directly into Angular services and feature components.

### 4. `@microsoft/signalr`
- **Version:** `^10.0.11`
- **What it does:** Client library establishing and maintaining persistent WebSocket (or Long Polling fallback) connections to ASP.NET Core SignalR hubs.
- **Why it was chosen:** Enables real-time, bi-directional communication between client and server with automatic reconnection logic.
- **Where & How it is implemented:**
  - Implemented in [frontend/src/app/core/services/realtime.service.ts](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/src/app/core/services/realtime.service.ts).
  - Connects to `/hubs/app` passing the JWT bearer token as an access query token during connection handshake.
- **Dependencies:** Consumed by `JobListComponent`, `EmployerJobsComponent`, `EmployerJobApplicantsComponent`, and `CandidateApplicationsComponent`.

### 5. Vitest & JSDOM
- **Version:** Vitest `^4.0.8`, JSDOM `^28.0.0`
- **What it does:** Ultra-fast, Vite-native unit testing runner executing in a headless browser-like DOM environment.
- **Why it was chosen:** Delivers instant test execution (sub-second suite runs) compared to legacy Karma/Jasmine runners, with native support for ESM and TypeScript.
- **Where & How it is implemented:**
  - Configured in [frontend/angular.json](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/angular.json) using the builder `@angular/build:unit-test`.
  - Executed via `ng test` (25 test files, 200 unit tests passing).
- **Dependencies:** Tests all core services, guards, pipes, and feature components.

---

## 3. Backend Technologies

### 1. ASP.NET Core 10 (`Microsoft.NET.Sdk.Web`, `net10.0`)
- **Version:** .NET 10.0
- **What it does:** The high-performance, cross-platform backend web application host providing HTTP routing, dependency injection, middleware pipeline, and hosting services.
- **Why it was chosen:** Offers unmatched throughput, minimal memory overhead, native AOT support, and robust enterprise architectural primitives.
- **Where & How it is implemented:**
  - Configured in [backend/Directory.Build.props](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/Directory.Build.props) and [backend/src/SkillBridge.Api/SkillBridge.Api.csproj](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Api/SkillBridge.Api.csproj).
  - Orchestrated in [backend/src/SkillBridge.Api/Program.cs](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Api/Program.cs).
- **Dependencies:** All backend projects (`SkillBridge.Domain`, `SkillBridge.Application`, `SkillBridge.Infrastructure`, `SkillBridge.Api`) target `net10.0`.

### 2. C# 13 / Latest
- **Version:** C# 13 (`<LangVersion>latest</LangVersion>`)
- **What it does:** Primary programming language used across the entire backend.
- **Why it was chosen:** Modern features including primary constructors, collection expressions, pattern matching, records, and nullable reference types (`<Nullable>enable</Nullable>`) ensure expressive, clean, and bug-resistant domain models.
- **Where & How it is implemented:**
  - Primary constructors applied throughout services and controllers (e.g., `public sealed class AuthController(AuthService authService)`).
  - Strongly typed records for all DTOs and requests.
- **Dependencies:** Entire backend codebase.

### 3. Entity Framework Core 10 (`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Version:** `10.0.0`
- **What it does:** Object-Relational Mapper (ORM) facilitating relational database interactions via strongly typed C# LINQ queries.
- **Why it was chosen:** Eliminates manual SQL writing while maintaining full control over relational mappings, composite keys, cascading delete behaviors, and schema migrations.
- **Where & How it is implemented:**
  - DbContext configured in [backend/src/SkillBridge.Infrastructure/Persistence/AppDbContext.cs](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Infrastructure/Persistence/AppDbContext.cs).
  - Fluent entity configurations in `backend/src/SkillBridge.Infrastructure/Persistence/Configurations/`.
  - Migrations compiled in `backend/src/SkillBridge.Infrastructure/Persistence/Migrations/`.
- **Dependencies:** Consumed by `IAppDbContext` interface implemented in `SkillBridge.Application` services.

### 4. ASP.NET Core Identity & JWT Bearer Authentication
- **Version:** `10.0.0`
- **What it does:**
  - **Identity:** Provides user storage (`AppUser`), role management (`IdentityRole`), PBKDF2 password hashing, lockout logic, and email verification tokens.
  - **JWT Bearer:** Validates cryptographic signatures on HTTP `Authorization: Bearer <token>` headers, populating `ClaimsPrincipal` for authorization.
- **Why it was chosen:** Industry-standard security architecture supporting role-based access control (RBAC) with zero custom cryptographic vulnerabilities.
- **Where & How it is implemented:**
  - Configured in `Program.cs` and `SkillBridge.Infrastructure/Identity/`.
  - Custom `RefreshTokenService` implements OAuth 2.0-style sliding refresh token rotation with family revocation on reuse.
- **Dependencies:** Protects all authenticated API routes and SignalR connections.

### 5. ASP.NET Core SignalR
- **Version:** `10.0.0` (in-box framework)
- **What it does:** Real-time bidirectional RPC over WebSockets.
- **Why it was chosen:** Enables real-time UI synchronization without costly client polling.
- **Where & How it is implemented:**
  - Hub defined in [backend/src/SkillBridge.Api/Hubs/SkillBridgeHub.cs](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Api/Hubs/SkillBridgeHub.cs) mapped to `/hubs/app`.
  - Notifier implemented in [backend/src/SkillBridge.Api/Realtime/SignalRNotifier.cs](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Api/Realtime/SignalRNotifier.cs) satisfying `IRealtimeNotifier`.
- **Dependencies:** Consumed whenever applications are submitted, withdrawn, or status-changed, or when jobs expire.

### 6. FluentValidation
- **Version:** `12.0.0` (`FluentValidation.DependencyInjectionExtensions`)
- **What it does:** Strongly typed validation library for incoming request DTOs.
- **Why it was chosen:** Keeps controllers and domain entities free of defensive conditional checks, enforcing business constraints with expressive fluent rules.
- **Where & How it is implemented:**
  - Registered in [backend/src/SkillBridge.Application/DependencyInjection.cs](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Application/DependencyInjection.cs).
  - Validators defined in `SkillBridge.Application/Auth/`, `Jobs/`, `CandidateProfiles/`, and `Applications/`.
- **Dependencies:** Invoked before service execution; invalid requests produce standard RFC 7807 400 Bad Request responses.

### 7. Scalar & OpenAPI / Swashbuckle
- **Version:** `Scalar.AspNetCore` (latest), `Swashbuckle.AspNetCore`
- **What it does:** Generates OpenAPI 3.0 specifications and serves a modern, interactive API documentation interface.
- **Why it was chosen:** Scalar provides a clean, fast API exploration tool supporting Bearer token authentication directly in the browser.
- **Where & How it is implemented:**
  - Configured in `Program.cs` via `app.MapScalarApiReference()` and `app.UseSwaggerUI()`.
  - Accessible at `/scalar/v1` and `/swagger`.
- **Dependencies:** Inspects all API controllers and produces standard OpenAPI JSON.

### 8. Brevo (Sendinblue) Transactional Email
- **Version:** REST API Integration via `HttpClient`
- **What it does:** Dispatches transactional HTML emails for email verification, password reset, and security notifications.
- **Why it was chosen:** Highly reliable cloud SMTP/REST delivery provider with support for local offline mock mode when API keys are not configured.
- **Where & How it is implemented:**
  - Implemented in [backend/src/SkillBridge.Infrastructure/Services/BrevoEmailService.cs](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/backend/src/SkillBridge.Infrastructure/Services/BrevoEmailService.cs) satisfying `IEmailService`.
  - Configured via `BrevoOptions` in `appsettings.json`.
- **Dependencies:** Consumed by `AuthService` during registration, password reset, and email changes.

---

## 4. Database & Infrastructure

### 1. PostgreSQL Engine
- **Version:** `16-alpine`
- **What it does:** Primary relational data store providing ACID compliance, complex foreign key constraints, and relational indexes.
- **Why it was chosen:** High performance, open-source reliability, excellent EF Core provider support, and JSON/indexing capabilities.
- **Where & How it is implemented:**
  - Defined in [docker-compose.yml](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/docker-compose.yml) running on port `5433` (mapped to internal `5432`).
- **Dependencies:** Backend API requires PostgreSQL to store all users, jobs, applications, profiles, and tokens.

### 2. Docker & Docker Compose
- **Version:** Docker Compose v2
- **What it does:** Containerizes supporting services for consistent local development and production orchestration.
- **Where & How it is implemented:**
  - Defined in root [docker-compose.yml](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/docker-compose.yml) with named volume `postgres_data` for database persistence.
- **Dependencies:** Used by developers to run PostgreSQL without manual local software installation.

