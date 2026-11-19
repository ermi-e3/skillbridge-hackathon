# 07. Complete Feature Documentation

This document provides exhaustive, end-to-end documentation for every feature implemented in the SkillBridge application.

---

## Feature Matrix

| # | Feature Name | Primary Actor | Target Module |
| :--- | :--- | :--- | :--- |
| **01** | User Registration & Dual-Role Selection | Public / Anonymous | Auth (`/register`) |
| **02** | User Authentication & IP Rate Limiting | Public / Anonymous | Auth (`/login`) |
| **03** | OAuth Social Login (Google & GitHub) | Public / Anonymous | Auth (`/login`, `/register`) |
| **04** | Sliding Refresh Token Rotation & Family Revocation | System / Authenticated | Core Auth Engine |
| **05** | Candidate Skill Onboarding & Proficiency Setup | Candidate | Candidate (`/candidate/onboarding/skills`) |
| **06** | Candidate Profile Dossier & Career Preferences | Candidate | Candidate (`/candidate/profile`) |
| **07** | CV Upload, Magic-Byte Validation & Preview | Candidate & Employer | Profile & Application Modules |
| **08** | Candidate Work Experience History Management | Candidate | Candidate Profile (`/candidate/profile`) |
| **09** | Employer Job Creation Studio & ETB Compensation | Employer | Employer Jobs (`/employer/jobs/new`) |
| **10** | 2-Hour Job Editing Window & Requirement Locking | Employer | Employer Jobs (`/employer/jobs/:id/edit`) |
| **11** | Automated Job Expiration Background Worker | System / Worker | Backend Hosted Service |
| **12** | Multi-Factor Compatibility Matching Engine | System / Real-Time | Application Core |
| **13** | Candidate Job Discovery & 100% Match Filter | Candidate | Candidate Jobs (`/jobs`) |
| **14** | One-Click Application & Self-Service Withdrawal | Candidate | Job Detail (`/jobs/:id`) |
| **15** | Employer Applicant Pipeline & Status Decisions | Employer | Employer Applicants (`/employer/jobs/:id/applicants`) |
| **16** | Real-Time Push Notifications & Telemetry (SignalR) | Candidate & Employer | Real-Time Subsystem |
| **17** | Account Security, Password & Email Management | Authenticated Users | Account (`/account/security`) |
| **18** | Editorial Light / Dark / System Theme Toggle | All Users | Global Shared UI |

---

## Feature 01: User Registration & Dual-Role Selection

### 1. Purpose
Allows new users to create a SkillBridge account, select their account type (**Candidate** or **Employer**), and establish credentials with email verification support.

### 2. Access & Interactivity
- **Access:** Public / Unauthenticated users.
- **Interaction:** User visits `/register`, selects their account role via a tactile segmented control, provides their Full Name, Email, Password, and (if Employer) Company Name, and submits.

### 3. Step-by-Step Lifecycle
1. User enters data into the reactive form. Client-side validators verify email format, password complexity (min 8 chars, 1 uppercase, 1 digit, 1 special char), and conditional `companyName` requirement.
2. Form submits `POST /api/auth/register`.
3. Backend `RegisterRequestValidator` performs server-side validation.
4. `IdentityService` checks for email collisions; if unique, creates an `AppUser` and assigns the requested role (`Roles.Candidate` or `Roles.Employer`).
5. If Candidate, automatically creates an empty `CandidateProfile` record.
6. Issues access token and HttpOnly refresh token cookie, sets `XSRF-TOKEN` cookie, and dispatches an email confirmation link via `BrevoEmailService`.
7. Candidate is redirected to onboarding; Employer is redirected to the jobs dashboard.

### 4. Technical Grounding
- **Frontend:** [frontend/src/app/features/auth/register/register.component.ts](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/src/app/features/auth/register/register.component.ts)
- **Backend:** `AuthController.Register` in `backend/src/SkillBridge.Api/Controllers/AuthController.cs`
- **Endpoints:** `POST /api/auth/register`
- **Database Tables:** `AspNetUsers`, `AspNetUserRoles`, `AspNetRoles`, `CandidateProfiles`, `RefreshTokens`

---

## Feature 02: User Authentication & IP Rate Limiting

### 1. Purpose
Authenticates existing users, protects accounts from brute-force password guessing, and establishes secure session state.

### 2. Access & Interactivity
- **Access:** Public / Unauthenticated users.
- **Interaction:** User navigates to `/login`, enters email and password, and submits.

### 3. Step-by-Step Lifecycle
1. Request arrives at `POST /api/auth/login`.
2. **Fixed-Window Rate Limiter:** Evaluates client IP (or `X-Forwarded-For`). If more than 5 attempts occur within 60 seconds, rejects immediately with `429 Too Many Requests` and `Retry-After: 60`.
3. If allowed, `AuthService` verifies user existence and validates password hash.
4. If invalid: increments `AccessFailedCount`. If threshold reached, enables temporary lockout (`423 Locked`).
5. If valid: resets failed count, issues JWT access token (valid for 15 minutes) and cryptographically secure refresh token (stored in HttpOnly cookie valid for 7 days).
6. Frontend updates `authService.currentUser` signal and redirects user to their intended destination or role dashboard.

### 4. Technical Grounding
- **Frontend:** [frontend/src/app/features/auth/login/login.component.ts](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/src/app/features/auth/login/login.component.ts)
- **Backend:** `Program.cs` (rate limiter policy `"LoginRateLimit"`), `AuthController.Login`
- **Endpoints:** `POST /api/auth/login`
- **Database Tables:** `AspNetUsers`, `RefreshTokens`

---

## Feature 03: OAuth Social Login (Google & GitHub)

### 1. Purpose
Provides one-click sign-in using third-party identity providers without requiring separate password management.

### 2. Access & Interactivity
- **Access:** Public / Unauthenticated users.
- **Interaction:** User clicks "Sign in with Google" or "Sign in with GitHub" buttons on `/login` or `/register`.

### 3. Step-by-Step Lifecycle
1. Client completes OAuth handshake and sends provider id token to `POST /api/auth/social-login`.
2. `SocialAuthValidator` verifies token authenticity directly with Google or GitHub APIs.
3. If user exists: logs them in.
4. If user does not exist: creates new user, marks email as verified, assigns default role, and creates associated candidate profile.

---

## Feature 04: Sliding Refresh Token Rotation & Family Revocation

### 1. Purpose
Maintains seamless user sessions across page reloads without persistent storage of access tokens, while guarding against token theft and replay attacks.

### 2. Implementation & Security Model
- Follows **RFC 6819** guidance. Refresh tokens are organized into cryptographic **Token Families** (`FamilyId`).
- Each refresh token can be used **exactly once**.
- When `POST /api/auth/refresh` is called:
  - The incoming token hash is matched in `RefreshTokens`.
  - If the token is already marked **Revoked**, **Token Reuse is Detected**. The backend immediately revokes **all active tokens in that entire family** and rejects the request with `401 Unauthorized` (`auth.refresh_token_reuse`).
  - If valid: revokes the used token, generates a new refresh token within the same family, updates the HttpOnly cookie, and returns a fresh JWT access token.
- Requires matching double-submit CSRF token (`X-XSRF-TOKEN` header matching `XSRF-TOKEN` cookie).

---

## Feature 05: Candidate Skill Onboarding & Proficiency Setup

### 1. Purpose
Guides new candidates through skill selection and proficiency declaration to ensure their profile generates accurate compatibility ratings.

### 2. Access & Interactivity
- **Access:** Candidate role only (`/candidate/onboarding/skills`).
- **Interaction:** Candidate browses curated skills, searches by keyword, selects up to 30 skills, assigns proficiency badges (`Beginner`, `Intermediate`, `Advanced`, `Expert`), selects years of experience, sets their professional headline, and completes setup.

### 3. Technical Grounding
- **Frontend:** [frontend/src/app/features/candidate/onboarding/candidate-skill-onboarding.component.ts](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/src/app/features/candidate/onboarding/candidate-skill-onboarding.component.ts)
- **Backend:** `CandidateProfileController.Update`
- **Endpoints:** `GET /api/skills`, `GET /api/candidates/me/profile`, `PUT /api/candidates/me/profile`
- **Database Tables:** `Skills`, `CandidateProfiles`, `CandidateSkills`

---

## Feature 06: Candidate Profile Dossier & Career Preferences

### 1. Purpose
Provides a central hub for candidates to manage their public presentation, biography, GitHub link, location, target work arrangement, target job type, and skills.

### 2. Access & Interactivity
- **Access:** Candidate role only (`/candidate/profile`).
- **Interaction:** Tabbed interface with inline editing, skill addition/removal, experience history timeline, and CV management.

---

## Feature 07: CV Document Upload, Magic-Byte Validation & Preview

### 1. Purpose
Allows candidates to upload a resume (PDF or DOCX format) for default profile storage and job application attachment, with instant in-browser preview for employers.

### 2. Technical Implementation & Security
- **File Constraints:** Maximum file size `5 MB` (`5,242,880 bytes`). Allowed extensions: `.pdf`, `.docx`.
- **Magic-Byte Signature Verification:**
  - Files are inspected at stream byte offsets to prevent extension spoofing:
    - **PDF:** Must begin with bytes `%PDF-` (`0x25, 0x50, 0x44, 0x46, 0x2D`).
    - **DOCX:** Must begin with ZIP archive signature `PK\x03\x04` (`0x50, 0x4B, 0x03, 0x04`).
- **Storage:** Saved locally with a randomized GUID filename into candidate-isolated disk directories. Original filenames are preserved in metadata for download presentation.
- **Endpoints:** `POST /api/candidates/me/profile/cv`, `GET /api/candidates/me/profile/cv`, `DELETE /api/candidates/me/profile/cv`, `GET /api/applications/{id}/cv`.

---

## Feature 08: Candidate Work Experience History Management

### 1. Purpose
Allows candidates to maintain an itemized chronology of previous employment positions.

### 2. Functionality
- Fields: Job Title, Company Name, Location, Start Date, End Date, `IsCurrent` flag, Description.
- If `IsCurrent` is true, `EndDate` is automatically nulled out.
- Directly influences the matching engine's experience score calculation.

---

## Feature 09: Employer Job Creation Studio & ETB Compensation Module

### 1. Purpose
Enables employers to draft and publish detailed job openings with granular skill requirements, proficiency thresholds, deadlines, and Ethiopian Birr (ETB) compensation terms.

### 2. User Experience & Real-Time Preview
- **Split-Screen Studio:** Desktop layout features an editorial form on the left and a sticky **Live Job Preview Card** on the right that renders exactly how candidates will see the posting.
- **Compensation Module:**
  - Segmented toggle: `Fixed / Range (ETB)` vs `Negotiable`.
  - When Fixed/Range: Provides Min Salary, Max Salary (with automatic comma formatting as typed), and pay period selector (`Per Month`, `Per Year`, `Per Project`).
  - When Negotiable: Automatically renders a verified confirmation banner.
- **Skill Requirements:** Employers distinguish between **Required Skills** (mandatory) and **Preferred Skills** (+15% bonus weight), assigning minimum proficiency requirements to each.

### 3. Technical Grounding
- **Frontend:** [frontend/src/app/features/employer/jobs/employer-post-job/employer-post-job.component.ts](file:///c:/Users/User/SkillBridge/skillbridge-hackathon/frontend/src/app/features/employer/jobs/employer-post-job/employer-post-job.component.ts)
- **Backend:** `JobsController.Create`
- **Endpoints:** `POST /api/jobs`
- **Database Tables:** `Jobs`, `JobSkills`, `Skills`

---

## Feature 10: 2-Hour Job Editing Window & Requirement Locking

### 1. Purpose
Prevents employers from moving the goalposts on candidates by locking job requirements after a 2-hour grace period.

### 2. Business Rule Enforcement
- When an employer creates a job, `CreatedAt` is stamped in UTC.
- For the first 2 hours (`utcNow - CreatedAt <= TimeSpan.FromHours(2)`), the employer can edit title, description, skills, and parameters freely via `PUT /api/jobs/{id}` or `PUT /api/employer/jobs/{id}`.
- Once 2 hours pass:
  - Frontend displays `Job Details Locked (2h passed)` status pill and disables editing controls.
  - Backend `JobsService.UpdateJobAsync` checks `job.IsEditingAllowed(clock.UtcNow)`. If violated, rejects with `400 Bad Request` (`jobs.editing_locked`).

---

## Feature 11: Automated Job Expiration Background Worker

### 1. Purpose
Automatically transitions expired job postings from open to closed without requiring manual employer intervention.

### 2. Execution Details
- Implemented as an ASP.NET Core `BackgroundService` (`JobExpirationWorker`).
- Wakes up every 15 seconds, queries PostgreSQL for open jobs with `Deadline <= DateTime.UtcNow`.
- Closes the jobs in database and emits `JobClosedRealtimeEvent` over SignalR.

---

## Feature 12: Multi-Factor Compatibility Matching Engine

### 1. Purpose
Calculates dynamic 0–100% compatibility scores for any job-candidate pair.

### 2. Six-Factor Mathematical Model
```
MatchScore = RequiredSkillsScore (0-50/65)
           + PreferredSkillsScore (0-15)
           + ExperienceScore (0-15)
           + WorkArrangementScore (0-10)
           + LocationScore (0-5)
           + JobTypeScore (0-5)
```
- **Proficiency Multipliers:** 1.0x (equal/exceeds), 0.85x (1 level below), 0.70x (2 levels below), 0.0x (unmatched).
- **Zero-Match Guardrail:** If candidate matches 0 required skills, total score is capped at `25%`.
- **Remote Flexibility:** Remote jobs award 100% credit for location (5 pts) and work arrangement (10 pts).

---

## Feature 13: Candidate Job Discovery & 100% Match Filter

### 1. Purpose
Allows candidates to search, filter, and sort openings based on verified fit.

### 2. Interaction
- Live keyword search across title, company, description, and required skills.
- Category filters: All, Technology, Healthcare, Finance, Education, Engineering, Hospitality, Marketing.
- **"100% Match Only" Toggle:** Instantly isolates positions where the candidate satisfies every single criterion.

---

## Feature 14: One-Click Application & Self-Service Withdrawal

### 1. Purpose
Minimizes candidate friction when applying, while granting candidates the autonomy to withdraw mistakes before employer review.

### 2. Functionality
- If candidate has an uploaded default CV, clicking "Submit Application" completes application submission in one click without redundant file re-uploads.
- While the application remains in `Received` status, a "Withdraw Application" button allows the candidate to cancel.
- Once an employer advances or rejects the application, withdrawal is disabled.

---

## Feature 15: Employer Applicant Review Pipeline & Status Transitions

### 1. Purpose
Provides employers with a Kanban-style pipeline to review, inspect, and evaluate candidates.

### 2. Pipeline Stages
- `Received`: Newly submitted applicant awaiting initial review.
- `Shortlisted`: Qualified candidate advanced to interview/screening stage.
- `Rejected`: Archived applicant not proceeding further.
- `Withdrawn`: Self-cancelled by candidate.
- Real-time status transitions trigger instant SignalR push notifications to the candidate.

---

## Feature 16: Real-Time Push Notifications & Telemetry (SignalR)

### 1. Purpose
Delivers zero-latency notifications and UI state synchronization across all connected clients.

### 2. Subscribed Events
- `ApplicationSubmitted`: Updates employer KPI counts and applicant tables.
- `ApplicationWithdrawn`: Alerts employer of candidate withdrawal.
- `ApplicationStatusChanged`: Displays celebratory toast to candidate upon shortlisting.
- `JobClosed`: Updates candidate job lists when deadlines expire.

---

## Feature 17: Account Security, Password & Email Management

### 1. Purpose
Empowers users to audit and harden their credentials.

### 2. Functionality
- **Change Password:** Requires validation of existing password before applying new PBKDF2 hash.
- **Change Email:** Sends confirmation token to the new address before updating normalized identity records.
- **Log Out All Other Devices:** Revokes all active refresh token families for the user account.
- **Account Deletion:** Permanently removes account, profile, and associated records.

---

## Feature 18: Editorial Light / Dark / System Theme Toggle

### 1. Purpose
Provides seamless day and night visual accessibility.

### 2. Implementation
- `ThemeService` monitors system `prefers-color-scheme` media query.
- Allows explicit user overrides (`light` or `dark`), persisted in `localStorage`.
- Sets `data-theme` attribute on `<html>`, instantly switching CSS design tokens across all components.

