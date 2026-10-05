These tests run the real API with ASP.NET Core Identity, JWT bearer middleware, and PostgreSQL. They apply migrations to the supplied database, seed the demo accounts, and create uniquely named test accounts. They do not delete or reset any database.

Use a dedicated PostgreSQL database whose name ends in `_tests` or `_test` and run from `backend` in PowerShell. The fixture refuses the application database `skillbridge`, explicitly replaces its database registration, and verifies the resolved host targets the supplied test database.

```powershell
$env:SKILLBRIDGE_TEST_CONNECTION = 'Host=localhost;Port=5433;Database=skillbridge_auth_tests;Username=postgres;Password=postgres'
dotnet test tests/SkillBridge.Auth.Tests/SkillBridge.Auth.Tests.csproj
```

The test host provides its own signing key, issuer, and audience. Its Candidate/Employer verification controller exists only in this test assembly. Production exposes no artificial authorization endpoints.
