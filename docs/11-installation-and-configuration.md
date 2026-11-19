# 11. Installation & Configuration

This guide provides step-by-step instructions for installing, configuring, and launching the complete SkillBridge platform in a local development environment.

---

## 1. System Prerequisites & Software Requirements

Before beginning, ensure the following developer tools are installed on your machine:

| Software / Tool | Minimum Version | Verification Command |
| :--- | :--- | :--- |
| **.NET SDK** | `10.0` or higher | `dotnet --version` |
| **Node.js** | `v20.18.0` (LTS) or higher | `node -v` |
| **npm** | `10.x` or higher | `npm -v` |
| **Docker Desktop / Engine** | Docker Compose v2 | `docker compose version` |
| **Git** | `2.40+` | `git --version` |

---

## 2. Documented Configuration Template (`.env.example`)

The backend reads settings from `appsettings.json`, `appsettings.Development.json`, and environment variables. Create a local configuration file or populate your environment using the template below:

```bash
# ==============================================================================
# SkillBridge Platform — Environment Variables Configuration Template
# ==============================================================================

# 1. Database Connection String (PostgreSQL)
# Format: Host=<host>;Port=<port>;Database=<dbname>;Username=<user>;Password=<password>
ConnectionStrings__Default=Host=localhost;Port=5433;Database=skillbridge;Username=postgres;Password=postgres

# 2. JWT Bearer Token Security Configuration
Jwt__Issuer=SkillBridgeApi
Jwt__Audience=SkillBridgeApp
Jwt__Key=super-secret-development-signing-key-minimum-256-bits-length-required-here!
Jwt__ExpirationMinutes=15
Jwt__RefreshTokenExpirationDays=7

# 3. CORS Allowed Origins (Comma-separated array or JSON list)
Cors__Origins__0=http://localhost:4200

# 4. Brevo (Sendinblue) Transactional Email Service
# Set UseMock=true for local development to log emails to the console instead of sending
Brevo__ApiKey=your_brevo_api_key_here
Brevo__SenderName=SkillBridge Platform
Brevo__SenderEmail=no-reply@skillbridge.local
Brevo__UseMock=true

# 5. Third-Party Social Authentication (Optional)
Authentication__Google__ClientId=your-google-client-id.apps.googleusercontent.com
Authentication__Google__ClientSecret=your-google-client-secret
Authentication__GitHub__ClientId=your-github-client-id
Authentication__GitHub__ClientSecret=your-github-client-secret

# 6. Database Seeding Controls
# When true, automatically seeds demo candidate, demo employers, skills, and vacancies
Seed__DemoUsers=true
```

---

## 3. Step-by-Step Local Setup Guide

### Step 1: Start PostgreSQL via Docker Compose
From the project root directory, launch the PostgreSQL database container:

```bash
# Start PostgreSQL container in background
docker compose up -d

# Verify container is running on port 5433
docker compose ps
```

The database container runs on external host port `5433` (mapped from container port `5432`). Database storage is persisted in the named Docker volume `postgres_data`.

---

### Step 2: Install Backend Dependencies & Run Database Migrations
Open a terminal in the backend API project directory:

```bash
cd backend/src/SkillBridge.Api

# Restore NuGet dependencies
dotnet restore

# Run the API (Migrations and Demo Seed Data apply automatically on startup)
dotnet run
```

> [!NOTE]
> When `SkillBridge.Api` starts up, `Program.cs` automatically invokes `await dbContext.Database.MigrateAsync()` to apply all pending EF Core migrations, followed by `IdentitySeeder.SeedAsync()` to populate demo accounts and initial skills.

#### (Optional) Manual Migration Commands
If you prefer running EF Core tools manually:
```bash
# Install EF Core CLI global tool if not already present
dotnet tool install --global dotnet-ef

# Apply migrations manually
dotnet ef database update --project ../SkillBridge.Infrastructure --startup-project .
```

---

### Step 3: Install Frontend Dependencies & Start Angular Client
Open a second terminal in the `frontend` directory:

```bash
cd frontend

# Install Node.js packages
npm install

# Start Angular development server
npm start
```

The Angular development server starts at `http://localhost:4200` with hot-module reloading enabled.

---

## 4. Verification & Default Demo Accounts

Once both backend and frontend are running, open your browser and navigate to `http://localhost:4200`. You can log in using any of the pre-seeded demo accounts:

### Pre-Seeded Employer Accounts:
- **Technology:** `admin@hackathon.local` / `Admin123!` (*SkillBridge Enterprise Partners*)
- **Healthcare:** `health@hackathon.local` / `Health123!` (*St. Paul General Hospital*)
- **Finance:** `finance@hackathon.local` / `Finance123!` (*Abyssinia Capital & Financial Group*)
- **Education:** `edu@hackathon.local` / `Education123!` (*Addis International Academy*)
- **Engineering:** `eng@hackathon.local` / `Engineer123!` (*Apex Engineering & Infrastructure*)
- **Hospitality:** `hospitality@hackathon.local` / `Hospitality123!` (*Grand Palace Hotel*)
- **Marketing:** `marketing@hackathon.local` / `Marketing123!` (*Horizon Creative Media*)

### Pre-Seeded Candidate Account:
- **Candidate:** `user@hackathon.local` / `User123!` (*Demo Candidate*)

---

## 5. Development vs. Production Builds

### Compiling the Frontend for Production
```bash
cd frontend
npm run build
```
Compiled static assets are generated in `frontend/dist/frontend/browser/`, ready for serving via Nginx or Cloudflare Pages.

### Publishing the Backend for Production
```bash
cd backend/src/SkillBridge.Api
dotnet publish -c Release -o ../../publish
```
Compiled binaries are output to `backend/publish/`.

