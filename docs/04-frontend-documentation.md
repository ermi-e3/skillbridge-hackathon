# 04. Frontend Documentation

## 1. Overview & Directory Structure

The SkillBridge frontend is built with **Angular 21.2.0** using **Standalone Components**, **Angular Signals**, **RxJS observables**, and an editorial design token system implemented in SCSS. The application requires zero legacy NgModules and compiles via the modern Vite-based `@angular/build` pipeline.

```
frontend/
├── public/                       # Static public assets (favicon.ico, branding assets)
├── src/
│   ├── app/
│   │   ├── core/                 # Core singletons, HTTP interceptors, guards, models
│   │   │   ├── api/              # Direct backend HTTP API client services
│   │   │   ├── guards/           # Functional routing guards (auth.guard.ts)
│   │   │   ├── http/             # auth.interceptor.ts (Bearer token & CSRF injection)
│   │   │   ├── models/           # TypeScript interfaces matching backend DTOs
│   │   │   ├── services/         # Stateful singleton services (Auth, Jobs, Realtime, Theme)
│   │   │   └── utils/            # Countdown timers, error formatters
│   │   ├── features/             # Lazy-loaded feature views
│   │   │   ├── account/          # Account security & settings
│   │   │   ├── auth/             # Login, register, confirm-email, reset-password
│   │   │   ├── candidate/        # Job search, job detail, onboarding, profile, applications
│   │   │   ├── employer/         # Employer job list, post job, applicant pipeline, applications
│   │   │   └── landing/          # Editorial landing page & walkthrough showcase
│   │   ├── shared/               # Reusable UI controls and utilities
│   │   │   ├── components/       # Pagination, ThemeToggle, UserProfileMenu
│   │   │   └── utils/            # Salary utilities, date filters, department categorizers
│   │   ├── app.config.ts         # Application providers, router, HTTP client, interceptors
│   │   ├── app.routes.ts         # Route declarations and role-guarded paths
│   │   └── app.ts                # Root shell component with router outlet
│   ├── index.html                # Entry HTML with custom font preloads & meta tags
│   ├── main.ts                   # Angular bootstrap entry point
│   └── styles.scss               # Global design tokens, typography, CSS variables
├── angular.json                  # Angular CLI build configuration
├── package.json                  # Dependencies and build scripts
└── tsconfig.json                 # TypeScript strict mode settings
```

---

## 2. Routing, Lazy Loading & Guard Hierarchy

All routes in [frontend/src/app/app.routes.ts](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/src/app/app.routes.ts) are lazily loaded using ES Dynamic Imports (`loadComponent: () => import(...).then(...)`), optimizing initial page load bundles.

### Complete Route Map

| Path | Component | Guard(s) | Target Role / Access |
| :--- | :--- | :--- | :--- |
| `/` | `LandingComponent` | None (Public) | Anonymous & All Users |
| `/login` | `LoginComponent` | None (Public) | Anonymous |
| `/register` | `RegisterComponent` | None (Public) | Anonymous |
| `/confirm-email` | `ConfirmEmailComponent` | None (Public) | Anonymous / Email Verify |
| `/forgot-password` | `ForgotPasswordComponent` | None (Public) | Anonymous |
| `/reset-password` | `ResetPasswordComponent` | None (Public) | Anonymous |
| `/unauthorized` | `UnauthorizedComponent` | None (Public) | Forbidden Users |
| `/account/security` | `AccountSecurityComponent` | `authGuard` | Authenticated Users |
| `/jobs` | `JobListComponent` | `authGuard`, `roleGuard('Candidate')` | Candidate Only |
| `/jobs/:id` | `JobDetailComponent` | `authGuard`, `roleGuard('Candidate')` | Candidate Only |
| `/candidate/onboarding/skills` | `CandidateSkillOnboardingComponent`| `authGuard`, `roleGuard('Candidate')` | Candidate Only |
| `/candidate/skill-registration` | *(Redirect to onboarding)* | N/A | Candidate Only |
| `/candidate/profile` | `CandidateProfileComponent` | `authGuard`, `roleGuard('Candidate')` | Candidate Only |
| `/profile` | *(Redirect to `/candidate/profile`)* | N/A | Candidate Only |
| `/my-applications` | `CandidateApplicationsComponent` | `authGuard`, `roleGuard('Candidate')` | Candidate Only |
| `/candidate/applications` | *(Redirect to `/my-applications`)* | N/A | Candidate Only |
| `/employer/jobs` | `EmployerJobsComponent` | `authGuard`, `roleGuard('Employer')` | Employer Only |
| `/employer/jobs/new` | `EmployerPostJobComponent` | `authGuard`, `roleGuard('Employer')` | Employer Only |
| `/employer/jobs/:id/edit`| `EmployerPostJobComponent` | `authGuard`, `roleGuard('Employer')` | Employer Only |
| `/employer/jobs/:id/applicants`| `EmployerJobApplicantsComponent` | `authGuard`, `roleGuard('Employer')` | Employer Only |
| `/employer/applications` | `EmployerApplicationsComponent` | `authGuard`, `roleGuard('Employer')` | Employer Only |
| `**` | *(Redirect to `/`)* | N/A | Catch-All Wildcard |

### Route Guard Implementation
Located in [frontend/src/app/core/guards/auth.guard.ts](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/src/app/core/guards/auth.guard.ts):
- **`authGuard`:** Checks `authService.isAuthenticated()`. If false, preserves the attempted URL as a `returnUrl` query parameter and redirects to `/login`.
- **`roleGuard(expectedRole)`:** Checks `authService.hasRole(expectedRole)`. If the user is authenticated but possesses the wrong role (e.g. an Employer attempting to access `/jobs`), redirects them to `/unauthorized`.

---

## 3. Core Feature Components Breakdown

### 1. `LandingComponent` & `WalkthroughShowcaseComponent`
- **Location:** `features/landing/`
- **Purpose:** Cinematic editorial landing page showcasing the platform value proposition to both candidates and employers.
- **Key Functionality:**
  - Audience Perspective Switcher: Toggle between *"I Have Skills"* (Candidate persona) and *"I Need Talent"* (Employer persona).
  - Dynamic Hero Skill Compatibility Calculator: Users can click interactive skill tags to simulate real-time match scoring recalculations.
  - Live Walkthrough Showcase: Auto-playing motion video previewing the SkillBridge UI without intrusive controls.

### 2. `JobListComponent` (`/jobs`)
- **Location:** `features/candidate/jobs/job-list/`
- **Purpose:** Primary job discovery portal for candidates with live compatibility telemetry.
- **Key Functionality:**
  - Computes and renders a radial telemetry badge (`93% Match`) for each opening.
  - Multi-Criteria Filtering: Filter by search keyword, department category (Engineering, Healthcare, Finance, etc.), work arrangement (Remote, Hybrid, On-Site), experience level, and a one-click **"100% Match Only"** toggle.
  - Monospace countdown timer indicating remaining time before the application deadline.
  - SignalR Integration: Automatically updates job availability and statuses in real time.

### 3. `JobDetailComponent` (`/jobs/:id`)
- **Location:** `features/candidate/jobs/job-detail/`
- **Purpose:** Detailed view of an individual job opening with full requirement breakdown and one-click application submission.
- **Key Functionality:**
  - Comprehensive Match Breakdown: Visualizes points earned across Required Skills, Preferred Skills, Experience, Location, and Work Arrangement.
  - One-Click Application: If the candidate already has a default profile CV, they can apply with a single click. Alternatively, they can attach a bespoke CV file.
  - Self-Service Withdraw: If the application status is `Received`, candidates can withdraw directly from this view.

### 4. `CandidateSkillOnboardingComponent` (`/candidate/onboarding/skills`)
- **Location:** `features/candidate/onboarding/`
- **Purpose:** Guided onboarding workflow for newly registered candidates.
- **Key Functionality:**
  - Interactive skill selection from the categorized catalog (up to 30 skills).
  - Interactive proficiency selector (`Beginner`, `Intermediate`, `Advanced`, `Expert`) and years-of-experience slider for each chosen skill.
  - Headline and career preference configuration (preferred job type and work arrangement).

### 5. `CandidateProfileComponent` (`/candidate/profile`)
- **Location:** `features/candidate/profile/`
- **Purpose:** Comprehensive profile management dossier for candidates.
- **Key Functionality:**
  - Tabbed interface: Overview, Skills & Proficiencies, Work Experience History, and CV Management.
  - Default CV Upload: Drag-and-drop or file selector supporting PDF and DOCX documents up to 5 MB, with instant preview and download controls.
  - Experience Timeline: Add, edit, and delete past employment records.

### 6. `EmployerJobsComponent` (`/employer/jobs`)
- **Location:** `features/employer/jobs/`
- **Purpose:** Central management console for employers to review their active and closed openings.
- **Key Functionality:**
  - 3-Card Telemetry KPI Strip: Active Openings, Total Applicants Received, Shortlisted Candidates.
  - 2-Hour Edit Window Indicator: Shows countdown timer for active openings; switches to *"Locked (2h passed)"* once the editing window elapses.
  - One-Click Close Job action.

### 7. `EmployerPostJobComponent` (`/employer/jobs/new`, `/employer/jobs/:id/edit`)
- **Location:** `features/employer/jobs/employer-post-job/`
- **Purpose:** Split-screen editorial job creation and editing studio.
- **Key Functionality:**
  - Left Column: Structured input modules for Job Details, Compensation (ETB), Required Skills (with minimum proficiency thresholds), Preferred Bonus Skills, and Application Deadline.
  - Right Column (Desktop): Sticky live candidate-view preview card that updates in real time as the employer types.
  - Compensation Module: Tactile segmented pill toggle between `Fixed / Range (ETB)` (with comma formatting) and `Negotiable`.
  - Enforces 2-hour editing restrictions when loaded in edit mode.

### 8. `EmployerJobApplicantsComponent` (`/employer/jobs/:id/applicants`)
- **Location:** `features/employer/jobs/employer-job-applicants/`
- **Purpose:** Applicant review dossier and candidate evaluation dashboard.
- **Key Functionality:**
  - Telemetry KPI strip for Total Applicants, Received, Shortlisted, Rejected, and Withdrawn.
  - Filtering by applicant status and **"100% Match Only"** toggle.
  - Applicant Cards: Displays candidate match score, verified matching skills, missing skills, and years of experience.
  - Decision Triggers: One-click actions to advance applicants to **Shortlisted** or mark as **Rejected**.
  - Modal Profile & CV Preview: Direct in-app preview of candidate CV documents and full dossiers without page navigation.

### 9. `EmployerApplicationsComponent` (`/employer/applications`)
- **Location:** `features/employer/applications/`
- **Purpose:** Aggregate candidate pipeline management across all posted openings.
- **Key Functionality:**
  - Bento layout featuring search dock, status tabs, and monospace pagination footer.
  - Direct navigation to specific opening applicant pipelines.

### 10. `AccountSecurityComponent` (`/account/security`)
- **Location:** `features/account/`
- **Purpose:** Centralized security and account administration center.
- **Key Functionality:**
  - Change password with current password verification.
  - Change email address with verification link dispatch.
  - Session Management: One-click *"Log Out All Other Devices"* action that revokes all active refresh tokens in the database.
  - Danger Zone: Permanent account deletion with password re-entry confirmation.

---

## 4. Reusable Shared Components

### 1. `PaginationComponent` (`<app-pagination>`)
- **Location:** `shared/components/pagination/`
- **Purpose:** Monospace editorial pagination control with page-number buttons, ellipsis compression, and items-per-page selectors.

### 2. `ThemeToggleComponent` (`<app-theme-toggle>`)
- **Location:** `shared/components/theme-toggle/`
- **Purpose:** Supports two variants: an icon button cycling through light and dark mode, and a segmented control (`Light` / `Dark` / `System`).
- **Implementation:** Integrates with `ThemeService` to persist settings to `localStorage` and toggle the `data-theme` attribute on the `<html>` root element.

### 3. `UserProfileMenuComponent` (`<app-user-profile-menu>`)
- **Location:** `shared/components/user-profile-menu/`
- **Purpose:** Header user pill and dropdown menu.
- **Functionality:** Computes dynamic initials (e.g. `DE • Demo Employer`), displays current role, renders role-appropriate navigation links, and triggers secure sign-out.

---

## 5. State Management & Design System

### Reactive State via Angular Signals
SkillBridge uses Angular Signals for clean, component-scoped reactive state:
```typescript
// Example from JobListComponent
readonly jobs = signal<JobDto[]>([]);
readonly searchQuery = signal<string>('');
readonly selectedCategory = signal<string>('All');
readonly matchOnly100 = signal<boolean>(false);

// Derived state automatically re-computed with zero manual subscriptions
readonly filteredJobs = computed(() => {
  let list = this.jobs();
  const q = this.searchQuery().toLowerCase().trim();
  if (q) list = list.filter(j => j.title.toLowerCase().includes(q));
  if (this.matchOnly100()) list = list.filter(j => (j.myMatchPercent ?? 0) === 100);
  return list;
});
```

### Editorial SCSS Design Token Palette
The application adheres to an editorial, high-contrast palette inspired by Linear, Cron, and Arc:
- **Canvas (`--bg-canvas`):** Slate-50 (`#F8FAFC`) in light mode; Slate-950 (`#0B0F19`) in dark mode.
- **Surfaces (`--bg-card`):** Pure White (`#FFFFFF`) / Slate-900 (`#111827`) with hairline borders (`rgba(30, 41, 59, 0.08)`).
- **Brand Signal (`--brand-teal`):** Teal-600 (`#0D9488`) for active opening tags, 90%+ telemetry rings, and selection rings.
- **Action Accent (`--brand-orange`):** Orange-500 (`#F97316`) for primary call-to-actions, review buttons, and bonus match indicators.
- **Typography:** Space Grotesk / Plus Jakarta Sans for headings, Inter for body copy, and JetBrains Mono for telemetry codes and KPIs.

