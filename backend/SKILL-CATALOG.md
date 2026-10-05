# Skill catalog API

`GET /api/skills` returns the shared skill catalog. The endpoint is public: Candidates, Employers, and callers without a Bearer token can retrieve the same list.

The response is a JSON array containing `id` and `name`, ordered alphabetically by name, then by ID. Response shape, with one entry shown:

```json
[
  {
    "id": 1,
    "name": "ASP.NET Core"
  }
]
```

The catalog uses the 40 existing seeded skills. Use these IDs in `skillIds` when [updating a candidate profile](CANDIDATE-PROFILE.md). This endpoint reads existing entries; it does not add or change skills.

## Interface and service pattern

```text
SkillsController
  -> ISkillCatalogService
  -> SkillCatalogService
  -> IAppDbContext.Skills
  -> EF Core / PostgreSQL
```

The controller delegates to `ISkillCatalogService.GetSkillsAsync`. The Application service uses a no-tracking query and projects database entries into `SkillResponse` objects. The implementation is registered through dependency injection. Existing tables and seed data are reused; no migration or new dependency is needed.

After restarting the backend with the updated code, the endpoint is available in [Scalar](http://localhost:5080/scalar/) under `GET /api/skills`.
