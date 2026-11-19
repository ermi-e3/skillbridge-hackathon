# 15. Technical Debt & Architecture Recommendations

This document presents an objective technical review of the SkillBridge codebase, identifying architectural limitations, operational risks, security considerations, and prioritized recommendations for production hardening.

---

## 1. Prioritized Findings Summary

| Priority | Finding | Category | Nature |
| :--- | :--- | :--- | :--- |
| **CRITICAL** | Local Disk Storage for CV Files | Scalability & Infrastructure | Confirmed Limitation |
| **HIGH** | In-Memory SignalR Without Redis Backplane | Scalability & Horizontal Scaling | Confirmed Limitation |
| **HIGH** | Reverse Proxy IP Forwarding Configuration | Security & Rate Limiting | Potential Risk |
| **MEDIUM** | Refresh Tokens Table Pruning Job | Database Performance | Architectural Debt |
| **MEDIUM** | In-Memory Client Access Token Persistence | Client Resilience | Architectural Recommendation |
| **MEDIUM** | Content Security Policy (CSP) Header | Web Security | Missing Hardening |
| **LOW** | SCSS Bundle Budget Ceilings | Frontend Build | Build Optimization |
| **LOW** | Integration Test Database Orchestration | Developer Experience | Testing Debt |

---

## 2. Detailed Technical Findings

### Finding 1: Local Disk Storage for CV Files [CRITICAL]
- **Confirmed Limitation:** `LocalFileStorageService` writes uploaded candidate CV files (`.pdf`, `.docx`) directly to the local server filesystem.
- **Impact:**
  - If the backend is deployed as multiple container replicas behind a load balancer, an employer connected to Replica B will fail to download or preview a CV uploaded to Replica A (`404 File Not Found`).
  - Container restarts without persistent volume mounts will result in permanent CV data loss.
- **Practical Recommendation:**
  - Abstract `IFileStorageService` to support cloud object storage:
    ```csharp
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(Stream content, string extension, CancellationToken ct);
        Task<Stream> ReadFileAsync(string fileKey, CancellationToken ct);
        Task DeleteFileAsync(string fileKey, CancellationToken ct);
    }
    ```
  - Implement an AWS S3, Cloudflare R2, or Azure Blob Storage provider using pre-signed URLs for direct client uploads and downloads.

---

### Finding 2: In-Memory SignalR Without Redis Backplane [HIGH]
- **Confirmed Limitation:** SignalR hub connections are managed purely in server process memory (`builder.Services.AddSignalR()`).
- **Impact:**
  - In a clustered or horizontally scaled production environment, an application submitted by a candidate on Server 1 will not push real-time WebSocket events to an employer connected to Server 2.
- **Practical Recommendation:**
  - Introduce Redis backplane package:
    ```bash
    dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
    ```
  - Configure in `Program.cs`:
    ```csharp
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis"));
    ```

---

### Finding 3: Reverse Proxy Client IP Rate Limiting [HIGH]
- **Potential Risk:** The rate limiter policy `"LoginRateLimit"` retrieves the client IP via:
  ```csharp
  var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
      ?? httpContext.Connection.RemoteIpAddress?.ToString();
  ```
- **Impact:**
  - An attacker could spoof the `X-Forwarded-For` header to bypass rate limits or exhaust quotas for legitimate user IPs unless ASP.NET Core's `ForwardedHeadersOptions` is explicitly configured to trust only the known upstream reverse proxy IP.
- **Practical Recommendation:**
  - Configure `ForwardedHeadersOptions` in `Program.cs`:
    ```csharp
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        // Add trusted proxy CIDR or IP
    });
    app.UseForwardedHeaders();
    ```

---

### Finding 4: Refresh Tokens Table Pruning Job [MEDIUM]
- **Architectural Debt:** The `RefreshTokens` table persists every token created, rotated, or revoked. Over months of production traffic, expired and rotated tokens accumulate indefinitely.
- **Impact:** Slows down index lookups on `TokenHash` and inflates database storage size.
- **Practical Recommendation:**
  - Add a scheduled maintenance worker or PostgreSQL cron job (`pg_cron`) to delete expired tokens older than 30 days:
    ```sql
    DELETE FROM "RefreshTokens" WHERE "ExpiresAt" < NOW() - INTERVAL '30 days';
    ```

---

### Finding 5: Missing Content Security Policy (CSP) Header [MEDIUM]
- **Missing Hardening:** While `X-Frame-Options: DENY` and `X-Content-Type-Options: nosniff` are applied, a modern `Content-Security-Policy` header is absent.
- **Impact:** Increases vulnerability surface if an inline script injection ever succeeds in bypassing Angular's sanitization.
- **Practical Recommendation:**
  - Inject a strict CSP header in the global middleware in `Program.cs`:
    ```csharp
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; font-src 'self' https://fonts.gstatic.com; connect-src 'self' wss:;";
    ```

---

### Finding 6: Integration Test Database Orchestration [LOW]
- **Testing Debt:** `SkillBridge.Auth.Tests` requires an out-of-band PostgreSQL instance running on port 5433 with a dedicated `_tests` database. Developers running `dotnet test` without Docker running encounter failing tests.
- **Practical Recommendation:**
  - Adopt **Testcontainers for .NET** (`Testcontainers.PostgreSql`), which automatically spins up an ephemeral PostgreSQL container during test execution and tears it down upon completion.

