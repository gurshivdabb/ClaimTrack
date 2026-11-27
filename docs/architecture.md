# ClaimTrack — Architecture & Coding Standards

## 1. Tech stack

| Area | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (LTS) | Long-term support release; what employers are upgrading to |
| Web | ASP.NET Core Web API (controllers) | Controllers are what most enterprise .NET codebases use |
| ORM | Entity Framework Core | Standard .NET data access; migrations keep the schema in source control |
| Database | SQL Server (Docker container) | The default database at Winnipeg insurance, finance, and government shops |
| Validation | FluentValidation | Keeps validation rules out of controllers and easy to unit test |
| Auth | JWT bearer tokens + role-based policies | Stateless, standard for APIs |
| Password hashing | ASP.NET Core `PasswordHasher<T>` | Built in, salted, no need to roll my own |
| Logging | Serilog | Structured logs; widely used |
| Testing | xUnit, AwesomeAssertions, `FakeTimeProvider`, `WebApplicationFactory`, Testcontainers; coverlet + ReportGenerator for coverage | Unit tests for rules, integration tests against a real SQL Server |
| Docs | Swagger / OpenAPI | Lets anyone try the API in a browser |
| DevOps | Docker, docker compose | One-command setup |

Packages are added **only when the current phase needs them**.

---

## 2. Solution structure

```
ClaimTrack/
├── README.md
├── ClaimTrack.http               Runnable walkthrough requests (VS Code REST Client / Visual Studio)
├── Dockerfile                    Multi-stage build of the API image
├── docker-compose.yml            SQL Server + API with one command
├── .env.example                  Settings for docker compose (copy to the git-ignored .env)
├── docs/
│   ├── architecture.md
│   └── test-matrix.md            Which tests prove each business rule and endpoint
├── src/
│   └── ClaimTrack.Api/
│       ├── Controllers/              HTTP only: routes, status codes
│       ├── Services/                 Business logic and rules
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   ├── Configurations/       EF Core IEntityTypeConfiguration classes
│       │   ├── Migrations/
│       │   └── Seed/
│       ├── Entities/                 EF Core entity classes
│       ├── Enums/
│       ├── Dtos/                     Request/response records, grouped by feature
│       ├── Validators/               FluentValidation validators
│       ├── Common/                   Result type, exceptions, pagination
│       ├── Middleware/               Global exception handling
│       └── Program.cs                Startup and dependency injection
└── tests/
    ├── ClaimTrack.UnitTests/
    └── ClaimTrack.IntegrationTests/
```

I'm using **one API project with folders** instead of several class-library
projects (Clean Architecture). For a project this size, separate projects
add ceremony without much benefit, and folders keep the same separation of
concerns. I can explain this trade-off in interviews.

---

## 3. Layers and their responsibilities

```
HTTP request
   │
   ▼
Controller  ── maps DTOs, calls one service method, returns status code
   │
   ▼
Service     ── enforces business rules (BR-xx), permissions, workflow,
   │           writes audit log, saves changes in one transaction
   ▼
AppDbContext (EF Core) ── talks to SQL Server
```

**Controllers**
- Thin. No business logic, no direct `DbContext` access.
- Receive request DTOs, return response DTOs, never entities.
- Translate service results into HTTP status codes.

**Services**
- One per feature: `PolicyService`, `ClaimService`, `AuthService`, `AuditService`.
- Each has an interface (`IClaimService`) registered in DI, so it can be mocked in tests.
- All business rules live here. Comments reference rule IDs: `// BR-05`.
- Use `AppDbContext` directly. I am **not** adding a separate repository
  layer, because `DbContext` already works as a repository and unit of work.
  (I'll be ready to discuss this choice in interviews.)

**Entities**
- Plain classes mapped to tables. No data annotations; mapping lives in
  `Data/Configurations` using the Fluent API.

**DTOs**
- C# `record` types, for example `CreateClaimRequest` and `ClaimResponse`.
- Mapping is done by hand with small extension methods (`claim.ToResponse()`).
  No AutoMapper, so the mapping is explicit and easy to debug.

---

## 4. Error handling

| Situation | Status | How |
|---|---|---|
| Invalid input (fails validation) | 400 | FluentValidation → `ValidationProblemDetails` |
| Not logged in | 401 | JWT middleware |
| Wrong role for the endpoint | 403 | `[Authorize(Roles = ...)]` |
| Resource missing **or** not visible to this user (BR-09) | 404 | Service returns not found |
| Invalid status transition or business rule broken | 409 / 422 | Service result → Problem Details |
| Unexpected exception | 500 | Global exception middleware logs it and returns a generic Problem Details |

- Services return a small `Result<T>` type for expected failures (not found,
  conflict). Exceptions are only for truly unexpected problems.
- Never return stack traces or internal messages to the client.

---

## 5. Security

- Passwords hashed with `PasswordHasher<User>`; plain passwords are never logged or returned.
- JWT signing key comes from configuration (user-secrets locally, environment variables in Docker).
- Every controller has `[Authorize]` by default; public endpoints opt out with `[AllowAnonymous]`.
- Ownership checks (BR-09) happen in the **service query**, e.g. filtering by
  `HolderId == currentUserId`, not after loading the data.
- The current user's ID and role come from a small `ICurrentUser` service that reads the JWT claims.

---

## 6. Coding standards

### Naming (differences from Java are marked ☕)
- `PascalCase` for classes, methods, properties, and public members. ☕ Java uses camelCase for methods.
- `camelCase` for local variables and parameters.
- `_camelCase` for private fields: `private readonly AppDbContext _db;`
- Interfaces start with `I`: `IClaimService`.
- Async methods end with `Async`: `GetClaimByIdAsync`.
- One public type per file; file name matches the type.

### Language features
- Nullable reference types **enabled**; treat warnings seriously.
- File-scoped namespaces: `namespace ClaimTrack.Api.Services;`
- Constructor injection only. Primary constructors are fine for services.
- Use `var` when the type is obvious from the right-hand side.
- `async`/`await` all the way down. Never call `.Result` or `.Wait()`.
- Pass `CancellationToken` from controllers through to EF Core calls.
- Use `decimal` for money, `DateTimeOffset` (UTC) for timestamps, `DateOnly` for dates without times.

### EF Core
- Use `AsNoTracking()` for read-only queries.
- Project to DTOs with `.Select(...)` when listing, so only needed columns load.
- Each schema change gets a migration with a descriptive name: `AddAuditLog`.
- Never edit a migration that has already been applied.

### Testing
- Test names: `MethodName_Scenario_ExpectedResult`, e.g. `DecideClaim_WithoutRecommendation_ReturnsConflict`.
- Arrange / Act / Assert structure.
- Every business rule (BR-01 to BR-12, listed in the README) has at least one unit test.
- Every endpoint has at least one integration test covering success and one failure.
