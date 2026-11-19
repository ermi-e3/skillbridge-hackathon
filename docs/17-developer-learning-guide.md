# 17. Developer Learning Guide

This guide is designed to help you quickly understand, maintain, debug, and extend the SkillBridge platform. It provides concrete walkthroughs using real examples from the existing codebase.

---

## 1. Where to Start When Reading the Codebase

To build an intuitive understanding of the project, follow this recommended reading sequence:

1. **Start with the Domain Entities:** Open `backend/src/SkillBridge.Domain/Entities/`. Read `Job.cs`, `JobApplication.cs`, and `CandidateProfile.cs`. Notice how business rules (such as the 2-hour editing window and status transitions) are encapsulated directly in domain methods rather than scattered across controllers.
2. **Review the DbContext & Mappings:** Inspect `backend/src/SkillBridge.Infrastructure/Persistence/AppDbContext.cs` and the configuration classes in `Configurations/` to understand table structures, primary keys, and foreign keys.
3. **Examine the Application Use Cases:** Look at `backend/src/SkillBridge.Application/Jobs/JobsService.cs` and `Applications/ApplicationsService.cs` to see how business operations are orchestrated.
4. **Follow the Frontend Routing & Shell:** Open `frontend/src/app/app.routes.ts` to see how lazy-loaded routes and guards are structured, then inspect `frontend/src/app/core/services/auth.service.ts` to see how user identity is maintained via signals.

---

## 2. Tracing an End-to-End Request (Frontend to Database)

Let's trace what happens when an Employer clicks **"Shortlist"** on an applicant:

```
[UI Button Click] ──> [Component Method] ──> [Angular Data Service] ──> [Auth Interceptor]
                                                                                │
[PostgreSQL Update] <── [EF Core SaveChanges] <── [Application Service] <── [HTTP Controller]
         │
         └──> [SignalR Hub] ──> [WebSocket Broadcast] ──> [Candidate Browser Toast]
```

1. **UI Click:** In `employer-job-applicants.component.html`, the employer clicks `<button (click)="updateStatus(applicant.id, 'Shortlisted')">`.
2. **Component Execution:** In `employer-job-applicants.component.ts`:
   ```typescript
   this.applicationsService.changeStatus(applicationId, newStatus).subscribe({
     next: (updated) => { /* Update local applicant signal */ }
   });
   ```
3. **HTTP Interceptor:** `auth.interceptor.ts` intercepts the outgoing `PATCH /api/applications/{id}/status`, injecting:
   - Header: `Authorization: Bearer <accessToken>`
   - Header: `X-XSRF-TOKEN: <token>`
4. **API Gateway & Controller:** Request reaches `ApplicationsController.ChangeStatus(int id, ChangeApplicationStatusRequest request)` in `SkillBridge.Api`:
   - Middleware verifies the caller possesses `Roles.Employer`.
   - Calls `applicationsService.ChangeApplicationStatusAsync(id, request.NewStatus)`.
5. **Application Service & Domain Invariants:**
   - In `ApplicationsService.cs`: Queries the database for the application, including the parent `Job`.
   - Verifies the employer owns the parent job (`job.IsOwnedBy(currentUser.UserId)`).
   - Calls `application.ChangeStatus(newStatus, clock.UtcNow)`. The domain entity validates that the current status is `Received`.
6. **Database Persistence:** Calls `await dbContext.SaveChangesAsync()`. EF Core generates parameterized SQL:
   ```sql
   UPDATE "JobApplications"
   SET "Status" = 'Shortlisted', "StatusChangedAt" = @p0
   WHERE "Id" = @p1;
   ```
7. **Real-Time Notification:** Service invokes `realtimeNotifier.NotifyApplicationStatusChangedAsync(event)`. SignalR dispatches a WebSocket message to the candidate's private connection:
   - Candidate's Angular app receives the event and renders a celebratory toast: *"Congratulations! You've been shortlisted for..."*

---

## 3. How to Add a New API Endpoint

Suppose you want to add an endpoint allowing employers to add an internal private note to an application:

### Step 1: Add Field to Domain Entity
In `SkillBridge.Domain/Entities/JobApplication.cs`:
```csharp
public string? EmployerNotes { get; private set; }

public void UpdateNotes(string? notes)
{
    EmployerNotes = notes?.Trim();
}
```

### Step 2: Create DTO & FluentValidation Rule
In `SkillBridge.Application/Applications/`:
```csharp
public sealed record UpdateNotesRequest(string Notes);

public sealed class UpdateNotesValidator : AbstractValidator<UpdateNotesRequest>
{
    public UpdateNotesValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}
```

### Step 3: Implement Service Method
In `SkillBridge.Application/Applications/ApplicationsService.cs`:
```csharp
public async Task<ApplicationDto> UpdateNotesAsync(int applicationId, string notes, CancellationToken ct)
{
    var app = await dbContext.Applications.Include(a => a.Job).FirstOrDefaultAsync(a => a.Id == applicationId, ct)
        ?? throw new NotFoundException("applications.not_found", "Not Found", "Application not found.");

    if (!app.Job.IsOwnedBy(currentUser.UserId))
        throw new ForbiddenException("jobs.not_owner", "Forbidden", "You do not own this opening.");

    app.UpdateNotes(notes);
    await dbContext.SaveChangesAsync(ct);
    return MapToDto(app);
}
```

### Step 4: Expose Controller Action
In `SkillBridge.Api/Controllers/ApplicationsController.cs`:
```csharp
[HttpPatch("{id:int}/notes")]
[Authorize(Roles = Roles.Employer)]
[ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
public async Task<ActionResult<ApplicationDto>> UpdateNotes(int id, UpdateNotesRequest request, CancellationToken ct) =>
    Ok(await applicationsService.UpdateNotesAsync(id, request.Notes, ct));
```

---

## 4. How to Create or Update a Database Model (EF Core Migrations)

Whenever you modify entities in `SkillBridge.Domain`:

1. Open your terminal in `backend/src/SkillBridge.Api`:
   ```bash
   cd backend/src/SkillBridge.Api
   ```
2. Generate the migration:
   ```bash
   dotnet ef migrations add AddEmployerNotesToApplications --project ../SkillBridge.Infrastructure --startup-project .
   ```
3. Inspect the generated migration in `backend/src/SkillBridge.Infrastructure/Persistence/Migrations/` to verify that `Up()` and `Down()` methods reflect your intent.
4. Apply the migration to the database:
   ```bash
   dotnet ef database update --project ../SkillBridge.Infrastructure --startup-project .
   ```

---

## 5. How to Add a New Frontend Page

Suppose you want to add an Employer Analytics page at `/employer/analytics`:

### Step 1: Generate Standalone Component
Create `frontend/src/app/features/employer/analytics/employer-analytics.component.ts`:
```typescript
import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-employer-analytics',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="analytics-container">
      <h1>Hiring Analytics</h1>
      <p>Total applications reviewed this month: {{ metrics().reviewed }}</p>
    </div>
  `,
  styleUrls: ['./employer-analytics.component.scss']
})
export class EmployerAnalyticsComponent implements OnInit {
  readonly metrics = signal({ reviewed: 42 });
  ngOnInit() {}
}
```

### Step 2: Register in Route Table
In `frontend/src/app/app.routes.ts`:
```typescript
{
  path: 'employer/analytics',
  canActivate: [authGuard, roleGuard('Employer')],
  loadComponent: () =>
    import('./features/employer/analytics/employer-analytics.component').then(
      (m) => m.EmployerAnalyticsComponent
    ),
}
```

### Step 3: Add Navigation Link
In `frontend/src/app/shared/components/user-profile-menu/user-profile-menu.component.html`, add `<a routerLink="/employer/analytics">Analytics</a>` inside the Employer navigation section.

---

## 6. How to Test Changes Safely

### Running the Frontend Unit Test Suite:
```bash
cd frontend
npm test
```
All 200 unit tests must pass. When creating a new component, always generate a corresponding `.spec.ts` file covering:
- Component creation.
- Initial signal states.
- User click interactions and service spy dispatching.

### Running Backend Unit Tests:
```bash
dotnet test backend/tests/SkillBridge.Auth.Tests/SkillBridge.Auth.Tests.csproj --filter "FullyQualifiedName~JobMatchingServiceTests"
```

---

## 7. Troubleshooting Common Development Issues

### 1. `401 Unauthorized` Loop on Frontend
- **Cause:** Access token expired (15m) and background refresh failed.
- **Solution:** Inspect `Application -> Cookies` in browser DevTools. Verify that the `refreshToken` cookie exists with `Path=/api/auth` and that the `XSRF-TOKEN` cookie is present. If missing, log in again.

### 2. `403 Forbidden` (`auth.forbidden`)
- **Cause:** Attempting to call an Employer endpoint while signed in with a Candidate account (or vice versa).
- **Solution:** Check the `role` claim in your current user dossier (`GET /api/auth/me`).

### 3. Database Connection Failure (`Npgsql.NpgsqlException`)
- **Cause:** PostgreSQL container is stopped or port 5433 is blocked.
- **Solution:** Run `docker compose ps` to verify the container is healthy. Ensure the connection string specifies `Port=5433`.

### 4. SignalR WebSocket Fails to Connect
- **Cause:** Missing access token in handshake query string.
- **Solution:** Verify `RealtimeService.connect()` correctly passes `accessTokenFactory: () => this.authService.getAccessToken()`. In `Program.cs`, check that `OnMessageReceived` reads `context.Request.Query["access_token"]` for paths starting with `/hubs`.

