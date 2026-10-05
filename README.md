# SkillBridge

Monorepo: ASP.NET Core Web API (.NET 10, Clean Architecture) + Angular 21 + PostgreSQL 17.
Only PostgreSQL runs in Docker; the API and the Angular app run on your machine.

## Project structure

```text
skillbridge/
├── docker-compose.yml                  PostgreSQL 17 only (host port 5433)
├── .env.example                        Optional overrides for the database container
├── .gitignore  .gitattributes  .editorconfig
├── .vscode/extensions.json
├── docs/                               API contract, diagrams, notes
│
├── backend/
│   ├── SkillBridge.sln
│   ├── Directory.Build.props           net10.0, nullable, implicit usings for every project
│   ├── Directory.Packages.props        All NuGet versions in one place (EF Core 10, Npgsql 10)
│   ├── global.json                     Pins the .NET 10 SDK (10.0.100 or newer feature band)
│   ├── .config/dotnet-tools.json       dotnet-ef (run: dotnet tool restore)
│   ├── src/
│   │   ├── SkillBridge.Domain/         Entities, enums, business rules. No EF, no ASP.NET
│   │   │   ├── Common/                 AppException, BusinessRuleException
│   │   │   ├── Entities/
│   │   │   └── Enums/
│   │   ├── SkillBridge.Application/    Use cases (services + DTOs), one folder per feature
│   │   │   ├── Common/
│   │   │   │   ├── Interfaces/         IAppDbContext, IClock
│   │   │   │   ├── Exceptions/         NotFound, Forbidden, Unauthorized
│   │   │   │   └── Models/             Shared DTOs (paging, etc.)
│   │   │   └── DependencyInjection.cs  AddApplication()
│   │   ├── SkillBridge.Infrastructure/ EF Core + Npgsql, external services
│   │   │   ├── Persistence/
│   │   │   │   ├── AppDbContext.cs
│   │   │   │   ├── Configurations/     IEntityTypeConfiguration<T> per entity
│   │   │   │   ├── Migrations/
│   │   │   │   └── Seed/
│   │   │   ├── Services/               SystemClock
│   │   │   └── DependencyInjection.cs  AddInfrastructure()
│   │   └── SkillBridge.Api/            Slim controllers + HTTP concerns
│   │       ├── Controllers/            HealthController (API + DB check)
│   │       ├── Errors/                 AppExceptionHandler, ProblemDetailsWriter
│   │       ├── Properties/launchSettings.json   http://localhost:5080
│   │       ├── appsettings.json / appsettings.Development.json
│   │       └── Program.cs
│   └── tests/
│       └── SkillBridge.Domain.Tests/   xUnit
│
└── frontend/                           Angular 21, standalone components
    └── src/
        ├── environments/               apiUrl = http://localhost:5080/api
        └── app/
            ├── core/
            │   ├── api/                One service per backend resource
            │   ├── models/             TypeScript types matching the API contract
            │   ├── http/               Interceptors, error helpers
            │   └── guards/             Route guards
            ├── layout/                 App shell, navigation
            ├── shared/components/      Reusable UI pieces
            ├── features/               One folder per screen group
            ├── app.config.ts           Router + HttpClient
            └── app.routes.ts
```

Dependency direction: `Api → Application → Domain` and `Infrastructure → Application + Domain`.
Api references Infrastructure only to call `AddInfrastructure()` in `Program.cs`.

## Run locally

Prerequisites: Docker Desktop, .NET 10 SDK, Node.js 20.19+ or 22.12+.

```bash
# 1. Database
docker compose up -d

# 2. Backend
cd backend
dotnet restore
dotnet tool restore
dotnet run --project src/SkillBridge.Api
# Swagger: http://localhost:5080/swagger   Health: http://localhost:5080/api/health

# 3. Frontend
cd frontend
npm install
npx ng serve
# http://localhost:4200
```

## Everyday commands

| Task | Command (from `backend/`) |
| --- | --- |
| Add a migration | `dotnet ef migrations add <Name> --project src/SkillBridge.Infrastructure --startup-project src/SkillBridge.Api --output-dir Persistence/Migrations` |
| Apply migrations | `dotnet ef database update --project src/SkillBridge.Infrastructure --startup-project src/SkillBridge.Api` |
| Run tests | `dotnet test` |
| Reset the database | `docker compose down -v` then `docker compose up -d` (from the repo root) |
| psql shell | `docker exec -it skillbridge-postgres psql -U postgres -d skillbridge` |
