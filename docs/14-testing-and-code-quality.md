# 14. Testing & Code Quality

This document outlines the testing architecture, quality assurance procedures, code standards, and verification metrics implemented across SkillBridge.

---

## 1. Test Suite Summary & Verification Metrics

| Test Suite | Runner / Framework | Target Projects | File Count | Test Count | Status |
| :--- | :--- | :--- | :---: | :---: | :--- |
| **Frontend Unit & Component** | Vitest `4.0.8` + JSDOM | `frontend/src/app` | **25 files** | **200 tests** | **100% Passing** :white_check_mark: |
| **Backend Matching Algorithm** | xUnit `2.9.x` | `SkillBridge.Application` | 1 file | 6 tests | Passing |
| **Backend Integration Suite** | xUnit + `WebApplicationFactory` | `SkillBridge.Api` + PostgreSQL | 11 files | 40+ tests | Requires dedicated test database |

---

## 2. Frontend Test Architecture (Vitest)

The frontend test suite is executed using Angular 21's unit-test builder (`@angular/build:unit-test`) powered by **Vitest** and **jsdom**:

```bash
# Execute the full frontend test suite
npm test
# Or directly via Angular CLI
node ./node_modules/@angular/cli/bin/ng.js test --watch=false
```

### Verified Passing Test Suites (200 Tests Across 25 Files):
1. `src/app/app.spec.ts` (1 test)
2. `src/app/core/guards/auth.guard.spec.ts` (6 tests) — Tests `authGuard` returnUrl redirect and `roleGuard` permission checking.
3. `src/app/core/http/auth.interceptor.spec.ts` (6 tests) — Tests JWT header attachment, CSRF header mirroring, and refresh triggering.
4. `src/app/core/services/applications.service.spec.ts` (10 tests)
5. `src/app/core/services/auth.service.spec.ts` (12 tests) — Tests signal state transitions, login/logout, and role checks.
6. `src/app/core/services/jobs.service.spec.ts` (6 tests)
7. `src/app/core/services/realtime.service.spec.ts` (3 tests) — Tests SignalR connection lifecycle and event routing.
8. `src/app/core/services/theme.service.spec.ts` (3 tests) — Tests localStorage persistence and DOM `data-theme` attribute toggling.
9. `src/app/core/utils/countdown.spec.ts` (6 tests) — Tests monospace deadline calculations and expiration indicators.
10. `src/app/features/auth/login/login.component.spec.ts` (5 tests)
11. `src/app/features/auth/register/register.component.spec.ts` (6 tests)
12. `src/app/features/candidate/applications/candidate-applications.component.spec.ts` (11 tests)
13. `src/app/features/candidate/jobs/job-detail/job-detail.component.spec.ts` (17 tests)
14. `src/app/features/candidate/jobs/job-list/job-list.component.spec.ts` (19 tests) — Tests multi-criteria filtering, search docks, and 100% match toggling.
15. `src/app/features/candidate/onboarding/candidate-skill-onboarding.component.spec.ts` (13 tests)
16. `src/app/features/candidate/profile/candidate-profile.component.spec.ts` (15 tests)
17. `src/app/features/employer/applications/employer-applications.component.spec.ts` (8 tests) — Tests KPI strip calculations and pagination dock.
18. `src/app/features/employer/jobs/employer-job-applicants/employer-job-applicants.component.spec.ts` (10 tests)
19. `src/app/features/employer/jobs/employer-jobs.component.spec.ts` (9 tests)
20. `src/app/features/employer/jobs/employer-post-job/employer-post-job.component.spec.ts` (14 tests) — Tests split studio live preview and ETB compensation module.
21. `src/app/features/landing/landing.component.spec.ts` (5 tests)
22. `src/app/features/landing/walkthrough-showcase/walkthrough-showcase.component.spec.ts` (3 tests)
23. `src/app/shared/components/pagination/pagination.component.spec.ts` (4 tests)
24. `src/app/shared/components/theme-toggle/theme-toggle.component.spec.ts` (2 tests)
25. `src/app/shared/components/user-profile-menu/user-profile-menu.component.spec.ts` (6 tests)

---

## 3. Backend Test Architecture (xUnit)

Located in `backend/tests/SkillBridge.Auth.Tests/`:

### 1. Algorithmic Unit Tests (`JobMatchingServiceTests.cs`)
Unit tests validating mathematical scoring rules without database dependencies:
- `CalculateMatch_PerfectMatch_Returns100Percent`: Asserts that a candidate matching all required skills, preferred skills, experience, location, and arrangement receives exactly 100%.
- `CalculateMatch_LowerProficiency_ReducesRequiredScore`: Asserts that candidates with proficiencies below the requirement receive fractional multipliers (e.g. 0.85x or 0.70x).
- `CalculateMatch_WorkArrangement_RemoteJobIsFullyCompatibleForAnyPreference`: Asserts that Remote jobs award full points regardless of candidate preference.
- `CalculateMatch_ZeroRequiredSkillsMatched_CapsScoreAt25Percent`: Verifies the 25% cap rule when zero required skills are matched.
- `CalculateMatch_CandidateWithIncompleteProfile_ReturnsZeroPercent`: Verifies graceful failure for uninitialized profiles.

### 2. Live API Integration Tests
Tests running full HTTP pipelines against ASP.NET Core Identity, JWT Bearer middleware, and PostgreSQL:
- `AuthFixture.cs`: Configures `WebApplicationFactory<Program>` with isolated database registration.
- `AuthIntegrationTests.cs`: End-to-end registration and login.
- `RefreshTokenRotationIntegrationTests.cs`: Verifies RFC 6819 token rotation and family revocation on reuse.
- `JobEditingPermissionsIntegrationTests.cs`: Verifies the 2-hour editing window restriction.
- `CvUploadAndProfileViewingIntegrationTests.cs`: Verifies magic-byte validation and PDF/DOCX uploads.

> [!NOTE]
> The integration tests require a dedicated test database whose name ends in `_test` or `_tests` (e.g. `skillbridge_auth_tests`) configured via `$env:SKILLBRIDGE_TEST_CONNECTION`. The test fixture deliberately refuses to run against the production database `skillbridge` to prevent data loss.

---

## 4. Code Quality & Static Analysis Standards

1. **C# Nullable Reference Types:** Enabled project-wide via `<Nullable>enable</Nullable>`. Eliminates `NullReferenceException` at compile time.
2. **TypeScript Strict Mode:** Enabled via `strict: true` in `tsconfig.json`. Enforces `noImplicitAny`, `strictNullChecks`, and `strictPropertyInitialization`.
3. **Encapsulated Domain Invariants:** Entities use private setters (`public int Id { get; private set; }`) and mutated exclusively through explicit domain methods (`job.Close()`, `application.Withdraw()`).
4. **FluentValidation Schema Enforcement:** Input DTOs are validated prior to service execution, ensuring dirty data never enters the database.

