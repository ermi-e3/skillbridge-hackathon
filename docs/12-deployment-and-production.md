# 12. Deployment & Production Architecture

This document describes the recommended production deployment architecture, compilation procedures, infrastructure topologies, containerization patterns, and operational maintenance strategies for SkillBridge.

---

## 1. Production Architecture Overview

In a production environment, SkillBridge deploys as a containerized, decoupled web application behind an SSL-terminating reverse proxy (such as Nginx, Caddy, or Cloudflare):

```mermaid
graph TD
    User["End Users / Mobile & Desktop Browsers"] -->|HTTPS (Port 443)| Cloudflare["Cloudflare / CDN & DDoS Protection"]
    Cloudflare -->|SSL Termination| Proxy["Nginx Reverse Proxy / Ingress Controller"]

    subgraph InternalNetwork["Internal Production VPC / Docker Network"]
        Proxy -->|Serve Static HTML/JS/CSS| WebSPA["Angular Production Bundle<br/>(Nginx Alpine Container)"]
        Proxy -->|Reverse Proxy /api & /hubs| API["ASP.NET Core 10 Web API<br/>(Kestrel Linux Container)"]

        API <-->|Port 5432 (SSL)| PostgresManaged[("Managed PostgreSQL 16<br/>(AWS RDS / Azure Flexible Server)")]
        API <-->|Persistent Volume Mount| CVStorage["Encrypted Persistent Volume<br/>(/app/uploads/cvs)"]
        API -->|HTTPS Outbound| BrevoAPI["Brevo Email Delivery API"]
    end
```

---

## 2. Compilation & Build Procedures

### 1. Frontend Production Build
From `frontend/`:
```bash
npm ci
npm run build -- --configuration production
```
- **Output:** Stored in `frontend/dist/frontend/browser/`.
- **Optimizations Applied:** Minification, tree-shaking, CSS dead-code elimination, and asset hashing.

### 2. Backend Production Compilation
From `backend/src/SkillBridge.Api`:
```bash
dotnet publish -c Release -o ./publish /p:UseAppHost=false
```
- **Output:** Stored in `backend/src/SkillBridge.Api/publish/`.
- **Runtime:** Optimized IL compiled against `net10.0` runtime.

---

## 3. Production Dockerfile Recommendations

### Multi-Stage Backend Dockerfile (`backend/Dockerfile`)
```dockerfile
# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["src/SkillBridge.Domain/SkillBridge.Domain.csproj", "src/SkillBridge.Domain/"]
COPY ["src/SkillBridge.Application/SkillBridge.Application.csproj", "src/SkillBridge.Application/"]
COPY ["src/SkillBridge.Infrastructure/SkillBridge.Infrastructure.csproj", "src/SkillBridge.Infrastructure/"]
COPY ["src/SkillBridge.Api/SkillBridge.Api.csproj", "src/SkillBridge.Api/"]

RUN dotnet restore "src/SkillBridge.Api/SkillBridge.Api.csproj"

COPY . .
WORKDIR "/src/src/SkillBridge.Api"
RUN dotnet publish "SkillBridge.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Install ICU libraries for non-English sorting
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Create non-root user and persistent CV directory
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
RUN mkdir -p /app/uploads/cvs && chown -R appuser:appgroup /app/uploads

USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SkillBridge.Api.dll"]
```

---

## 4. Nginx Reverse Proxy Configuration

Below is a production Nginx configuration snippet routing API traffic, WebSocket upgrades (for SignalR), and frontend static files:

```nginx
server {
    listen 80;
    server_name skillbridge.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name skillbridge.example.com;

    ssl_certificate /etc/letsencrypt/live/skillbridge.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/skillbridge.example.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    # Security Headers
    add_header X-Frame-Options "DENY" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # 1. Frontend Angular Static Single Page Application
    location / {
        root /var/www/skillbridge/browser;
        index index.html;
        try_files $uri $uri/ /index.html;
    }

    # 2. Backend REST API
    location /api/ {
        proxy_pass http://localhost:8080/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # 3. SignalR WebSockets Gateway
    location /hubs/ {
        proxy_pass http://localhost:8080/hubs/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}
```

---

## 5. Production Hardening Checklist

1. **Reverse Proxy Header Forwarding:** Ensure `builder.Services.Configure<ForwardedHeadersOptions>(...)` is enabled in `Program.cs` so client IP rate limiting correctly evaluates incoming client IPs instead of the reverse proxy loopback address.
2. **CORS Origin Lockdown:** Change `Cors:Origins` from `http://localhost:4200` to the explicit production domain (`https://skillbridge.example.com`).
3. **Database Security:**
   - Enforce SSL mode in PostgreSQL connection string: `SSL Mode=Require;Trust Server Certificate=false`.
   - Never use the `postgres` superuser for the application connection string; provision a restricted `skillbridge_app` user with DDL/DML permissions limited to the application schema.
4. **Secret Management:** Store `Jwt:Key`, `Brevo:ApiKey`, and database connection strings in environment variables injected via Kubernetes Secrets, Docker Swarm Secrets, or Azure Key Vault. Never commit real keys to source control.
5. **Horizontal Scalability Path:**
   - **SignalR Redis Backplane:** When scaling `SkillBridge.Api` across multiple container instances, add `Microsoft.AspNetCore.SignalR.StackExchangeRedis` so push notifications reach clients connected to different server nodes.
   - **Object Storage Migration:** Replace `LocalFileStorageService` with an AWS S3, Cloudflare R2, or Azure Blob Storage implementation so uploaded CV files are accessible across distributed instances.

