# 09. API Reference

This document provides a comprehensive REST API specification for all 26+ endpoints exposed by the SkillBridge backend. Interactive Scalar documentation is available at `/scalar/v1`, and Swagger UI is accessible at `/swagger`.

---

## 1. Authentication Endpoints (`/api/auth`)

### 1.1 Register Account
- **HTTP Method:** `POST`
- **Route:** `/api/auth/register`
- **Access:** Anonymous
- **Request Body:**
  ```json
  {
    "email": "candidate@example.com",
    "password": "Password123!",
    "fullName": "Abebe Bikila",
    "role": "Candidate",
    "companyName": null
  }
  ```
- **Response (`200 OK`):**
  ```json
  {
    "accessToken": "eyJhbGci...",
    "expiresAt": "2026-11-18T01:30:00Z",
    "user": {
      "id": "c1a2b3c4...",
      "email": "candidate@example.com",
      "fullName": "Abebe Bikila",
      "role": "Candidate",
      "companyName": null
    }
  }
  ```
- **Errors:** `400 Bad Request` (`validation.failed`, `auth.email_taken`).

---

### 1.2 User Login
- **HTTP Method:** `POST`
- **Route:** `/api/auth/login`
- **Access:** Anonymous (Rate limited: 5 req/min)
- **Request Body:**
  ```json
  {
    "email": "user@hackathon.local",
    "password": "User123!"
  }
  ```
- **Response (`200 OK`):** Returns `AuthResponse` + sets `refreshToken` and `XSRF-TOKEN` cookies.
- **Errors:** `400 Bad Request`, `401 Unauthorized` (`auth.invalid_credentials`), `423 Locked`, `429 Too Many Requests`.

---

### 1.3 Social Login
- **HTTP Method:** `POST`
- **Route:** `/api/auth/social-login`
- **Access:** Anonymous (Rate limited: 5 req/min)
- **Request Body:**
  ```json
  {
    "provider": "Google",
    "idToken": "eyJhbGci...",
    "role": "Candidate"
  }
  ```
- **Response (`200 OK`):** Returns `AuthResponse` with cookies.

---

### 1.4 Refresh Token (Sliding Rotation)
- **HTTP Method:** `POST`
- **Route:** `/api/auth/refresh`
- **Access:** Anonymous (Requires `refreshToken` cookie and `X-XSRF-TOKEN` header)
- **Request Body (Optional):** `{ "refreshToken": "..." }` (read from cookie if omitted)
- **Response (`200 OK`):** Returns fresh `AuthResponse` with rotated cookie.
- **Errors:** `401 Unauthorized` (`auth.refresh_token_reuse`, `auth.refresh_token_expired`), `403 Forbidden` (`auth.csrf_invalid`).

---

### 1.5 User Logout
- **HTTP Method:** `POST`
- **Route:** `/api/auth/logout`
- **Access:** Anonymous (Requires `X-XSRF-TOKEN` header)
- **Response (`204 No Content`):** Revokes token and clears cookies.

---

### 1.6 Current User Dossier
- **HTTP Method:** `GET`
- **Route:** `/api/auth/me`
- **Access:** Authenticated (`Authorization: Bearer <token>`)
- **Response (`200 OK`):**
  ```json
  {
    "id": "c1a2b3c4...",
    "email": "user@hackathon.local",
    "fullName": "Demo Candidate",
    "role": "Candidate",
    "companyName": null
  }
  ```

---

## 2. Jobs Endpoints (`/api/jobs`)

### 2.1 Search and List Jobs
- **HTTP Method:** `GET`
- **Route:** `/api/jobs`
- **Access:** Authenticated
- **Query Parameters:**
  - `page` (`int`, default `1`): Page number.
  - `pageSize` (`int`, default `10`): Items per page.
  - `query` (`string`, optional): Search keyword.
  - `location` (`string`, optional): City or Remote filter.
  - `category` (`string`, optional): Industry department category.
  - `jobType` (`JobType`, optional): FullTime, PartTime, etc.
  - `workArrangement` (`WorkArrangement`, optional): Remote, Hybrid, OnSite.
  - `experienceLevel` (`ExperienceLevel`, optional): Senior, Junior, etc.
  - `skillIds` (`int[]`, optional): Filter by required skill IDs.
  - `minMatch` (`int`, optional): Minimum match percentage (e.g. `100`).
- **Response (`200 OK`):**
  ```json
  {
    "items": [
      {
        "id": 1,
        "title": "Full-Stack Software Engineer",
        "companyName": "SkillBridge Enterprise Partners",
        "location": "Addis Ababa, Ethiopia",
        "isOpen": true,
        "createdAt": "2026-11-18T00:00:00Z",
        "deadline": "2026-12-31T23:59:59Z",
        "myMatchPercent": 93,
        "hasApplied": false,
        "requiredSkills": [
          { "id": 1, "name": "C#" },
          { "id": 2, "name": ".NET Core" }
        ],
        "preferredSkills": [
          { "id": 4, "name": "Docker" }
        ]
      }
    ],
    "page": 1,
    "pageSize": 10,
    "totalCount": 24,
    "totalPages": 3
  }
  ```

---

### 2.2 Get Job by ID
- **HTTP Method:** `GET`
- **Route:** `/api/jobs/{id}`
- **Access:** Authenticated
- **Response (`200 OK`):** Returns complete `JobDto` including detailed `JobMatchDto` with multi-factor breakdown.

---

### 2.3 Create Job Opening
- **HTTP Method:** `POST`
- **Route:** `/api/jobs`
- **Access:** `Roles.Employer`
- **Request Body:**
  ```json
  {
    "title": "Lead DevOps Architect",
    "description": "Oversee enterprise Kubernetes infrastructure. [Salary: 80,000 – 120,000 ETB / mo]",
    "location": "Addis Ababa",
    "jobType": "FullTime",
    "workArrangement": "Hybrid",
    "experienceLevel": "Lead",
    "minExperienceYears": 7,
    "deadline": "2026-12-31T23:59:59Z",
    "category": "Engineering",
    "requiredSkillIds": [4, 7],
    "preferredSkillIds": [12]
  }
  ```
- **Response (`201 Created`):** Returns created `JobDto`.

---

### 2.4 Apply to Job
- **HTTP Method:** `POST`
- **Route:** `/api/jobs/{jobId}/applications`
- **Access:** `Roles.Candidate`
- **Content-Type:** `multipart/form-data` or `application/json`
- **Payload:**
  - `cv` (`IFormFile`, optional): Bespoke CV document (up to 5 MB, PDF/DOCX). If omitted, uses default profile CV.
- **Response (`201 Created`):** Returns `ApplicationDto`.
- **Errors:** `400 Bad Request` (`applications.already_applied`, `jobs.closed`, `applications.cv_missing`).

---

### 2.5 Get Applicants for Job
- **HTTP Method:** `GET`
- **Route:** `/api/jobs/{jobId}/applications`
- **Access:** `Roles.Employer` (Must own the job)
- **Response (`200 OK`):** Returns `JobApplicantsDto` containing job details, telemetry counters, and candidate application records with match scores and skill alignments.

---

## 3. Applications Endpoints (`/api/applications`)

### 3.1 Get Application by ID
- **HTTP Method:** `GET`
- **Route:** `/api/applications/{id}`
- **Access:** Authenticated (Owner Candidate or Job Employer)
- **Response (`200 OK`):** Returns `ApplicationDto`.

---

### 3.2 Change Application Status
- **HTTP Method:** `PATCH`
- **Route:** `/api/applications/{id}/status`
- **Access:** `Roles.Employer`
- **Request Body:**
  ```json
  {
    "newStatus": "Shortlisted"
  }
  ```
- **Response (`200 OK`):** Returns updated `ApplicationDto`.
- **Errors:** `400 Bad Request` (`applications.invalid_status`, `applications.invalid_transition`).

---

### 3.3 Withdraw Application
- **HTTP Method:** `PATCH`
- **Route:** `/api/applications/{id}/withdraw`
- **Access:** `Roles.Candidate` (Owner only)
- **Response (`200 OK`):** Returns `ApplicationDto` with `status: "Withdrawn"`.
- **Errors:** `400 Bad Request` (`applications.cannot_withdraw` if already reviewed).

---

### 3.4 Download Application CV
- **HTTP Method:** `GET`
- **Route:** `/api/applications/{id}/cv`
- **Access:** Authenticated (Owner Candidate or Job Employer)
- **Response (`200 OK`):** File binary stream with `Content-Disposition: inline` (for preview) or `attachment`.

---

## 4. Candidate Profile Endpoints (`/api/candidates/me/profile`)

### 4.1 Get Profile
- **HTTP Method:** `GET`
- **Route:** `/api/candidates/me/profile`
- **Access:** `Roles.Candidate`
- **Response (`200 OK`):** Returns `CandidateProfileResponse` including skills, experiences, and default CV status.

---

### 4.2 Update Profile
- **HTTP Method:** `PUT`
- **Route:** `/api/candidates/me/profile`
- **Access:** `Roles.Candidate`
- **Request Body:**
  ```json
  {
    "headline": "Senior Full-Stack Engineer",
    "bio": "Specializing in .NET 10 and Angular 21.",
    "location": "Addis Ababa, Ethiopia",
    "gitHubUrl": "https://github.com/example",
    "experienceLevel": "Senior",
    "yearsOfExperience": 6,
    "preferredWorkArrangement": "Hybrid",
    "preferredJobType": "FullTime",
    "skills": [
      { "skillId": 1, "proficiency": "Expert", "yearsOfExperience": 6, "isPreferred": true },
      { "skillId": 2, "proficiency": "Expert", "yearsOfExperience": 6, "isPreferred": true }
    ],
    "experienceHistory": [
      {
        "title": "Senior Engineer",
        "company": "TechCorp",
        "startDate": "2021-01-01T00:00:00Z",
        "isCurrent": true
      }
    ]
  }
  ```
- **Response (`200 OK`):** Returns updated `CandidateProfileResponse`.

---

### 4.3 Upload Default CV
- **HTTP Method:** `POST`
- **Route:** `/api/candidates/me/profile/cv`
- **Access:** `Roles.Candidate`
- **Content-Type:** `multipart/form-data` (`cv` file)
- **Response (`200 OK`):** Returns updated `CandidateProfileResponse`.

---

### 4.4 Download Default CV
- **HTTP Method:** `GET`
- **Route:** `/api/candidates/me/profile/cv`
- **Access:** `Roles.Candidate`
- **Response (`200 OK`):** Binary stream with original filename.

---

### 4.5 Remove Default CV
- **HTTP Method:** `DELETE`
- **Route:** `/api/candidates/me/profile/cv`
- **Access:** `Roles.Candidate`
- **Response (`204 No Content`)**

---

## 5. Account Administration Endpoints (`/api/account`)

### 5.1 Get Security Status
- **HTTP Method:** `GET`
- **Route:** `/api/account/security-status`
- **Access:** Authenticated
- **Response (`200 OK`):** Password presence, 2FA status, linked social logins, active session count.

---

### 5.2 Change Password
- **HTTP Method:** `POST`
- **Route:** `/api/account/change-password`
- **Access:** Authenticated
- **Request Body:** `{ "currentPassword": "...", "newPassword": "..." }`
- **Response (`200 OK`):** `{ "message": "Password changed successfully." }`

---

### 5.3 Logout All Other Devices
- **HTTP Method:** `POST`
- **Route:** `/api/account/logout-all`
- **Access:** Authenticated
- **Response (`200 OK`):** Revokes all active refresh tokens for the current user.

---

### 5.4 Permanent Account Deletion
- **HTTP Method:** `POST`
- **Route:** `/api/account/delete`
- **Access:** Authenticated
- **Request Body:** `{ "password": "..." }`
- **Response (`200 OK`):** Deletes user account and cascades to profile, applications, and sessions.

---

## 6. Skills & Health Endpoints

### 6.1 Get Global Skills Catalog
- **HTTP Method:** `GET`
- **Route:** `/api/skills`
- **Access:** Anonymous
- **Response (`200 OK`):** Array of `{ "id": 1, "name": "C#" }`.

---

### 6.2 Health Probe
- **HTTP Method:** `GET`
- **Route:** `/api/health`
- **Access:** Anonymous
- **Response (`200 OK`):** `{ "status": "Healthy", "database": "Connected", "timestamp": "..." }`.

