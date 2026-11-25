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

---

## 7. Decisions log

Add an entry whenever an important choice is made, so I can explain it later.

| Date | Decision | Reason |
|---|---|---|
| 2026-10-30 | Single API project with folders, not multi-project Clean Architecture | Right size for the project; easier to learn and navigate |
| 2026-10-30 | No repository layer over EF Core | DbContext already provides repository and unit-of-work behavior |
| 2026-10-30 | Return 404 instead of 403 for other users' data | Avoids revealing that a resource exists (BR-09) |
| 2026-10-30 | Manual DTO mapping, no AutoMapper | Explicit and easier to debug |
| 2026-11-06 | Store enums as text (e.g. `UnderReview`), not numbers | Readable in SQL queries, and reordering enum values can't corrupt existing data. Renaming a value needs a migration. |
| 2026-11-18 | Input-only validation in FluentValidation (400); rules that need policy data in the service (422); one custom `ValidationFilter` instead of the deprecated auto-validation package | Keeps validators free of database access, puts business rules in services as §3 requires, and means controllers never contain validation code |
| 2026-11-18 | Policy and claim numbers come from SQL Server sequences (format `POL-yyyy-nnnnn`) | Unique even under concurrent requests (max + 1 is not); the counter doesn't reset yearly and gaps are accepted, which avoids a locked counter table |
| 2026-11-18 | Write tests alongside features from the end of Phase 1 (moved from Phase 3): unit tests for pure rules, integration tests against a real SQL Server via Testcontainers; no EF in-memory provider or `DbContext` mocks | Catches mistakes when they're made, especially in Phase 2's security and workflow rules; in-memory fakes don't enforce SQL Server constraints, sequences, or query behaviour, so tests could pass while the real API fails |
| 2026-11-18 | AwesomeAssertions instead of FluentAssertions | FluentAssertions 8+ moved to a paid commercial licence (Xceed); AwesomeAssertions continues the same code and syntax under the free Apache-2.0 licence |
| 2026-11-18 | JWTs signed with HS256 (one shared secret key), checked with zero clock skew | The same API both issues and checks tokens, so one secret is enough; RS256 (private/public key pair) is worth it only when other services must verify tokens without being able to create them. Zero skew makes the 60-minute expiry exact |
| 2026-11-18 | Secure by default with `MapControllers().RequireAuthorization()`, not a global fallback policy; roles via `[Authorize(Roles = nameof(Role.X))]` | Every controller endpoint needs a token unless marked `[AllowAnonymous]`, matching §5. A fallback policy would also apply to unknown URLs and answer 401 instead of 404. `nameof` turns a misspelt role into a compile error. A unit test fails if `[AllowAnonymous]` appears outside `AuthController` |
| 2026-11-19 | Audit entries are added to the same `DbContext` and saved by the service's single `SaveChangesAsync`; no explicit transaction. `AppDbContext` refuses to save modified or deleted audit entries | One `SaveChanges` is one database transaction, so a change and its audit entry are saved together or not at all (BR-07). The guard is a safety net for BR-08; it can't see bulk `ExecuteUpdate`/`ExecuteDelete`, which a production system would block with database permissions |
| 2026-11-19 | Claims list returns a page envelope (`items`, `page`, `pageSize`, `totalCount`, `totalPages`); bad paging/sort values get 400, not silently corrected; max page size 100; every sort ends with `Id` as a tie-breaker | Self-describing and visible in Swagger. 400 tells the client exactly what's wrong. The cap stops huge requests. Without a unique tie-breaker, equal values can come back in a different order per query, so a claim could appear on two pages or none. The count runs after the BR-09 filter, so totals never reveal other users' claims |
| 2026-11-25 | ClaimTrack stays a backend-only project: the Angular front end (built as Phase 4 extras) was removed, and Azure deployment was dropped | The project's goal is to show backend skills for .NET roles; a front end and cloud deployment I couldn't fully explain would dilute that. The API is explored through Swagger and the `ClaimTrack.http` walkthrough, and runs with `docker compose up`. The supervisor-only users lookup stays: it's a useful API feature on its own |
| 2026-11-20 | Supervisor-only `GET /api/users?role=` returning only ID, name, and role; EF SQL command logs lowered to Warning | Supervisors need to pick an adjuster to assign and a policyholder for a new policy, so they need to see who's who; returning only what's needed keeps emails private. SQL logs were kept visible for learning until then |
| 2026-11-20 | Documents: anyone who can see a claim may list/download/upload (uploads only while not final); PDF/PNG/JPEG identified by magic bytes; 10 MB and 20 per claim; stored on disk under random GUID keys behind `IDocumentStorage`, outside `/app` (a volume in Docker); names cleaned for display only; downloads as attachments with `nosniff`; never deleted; uploads audited; a download endpoint added | Uploads are a classic attack surface: names and Content-Type are attacker-controlled, so neither is trusted. Disk + interface keeps the database small and makes Azure Blob a drop-in. The file is written first and deleted if the database save fails, so there are no orphans. Not handled, documented: malware scanning |
| 2026-11-20 | Overdue claims: a `BackgroundService` (startup, then hourly on a `TimeProvider`-driven `PeriodicTimer`) runs a scoped `OverdueClaimsService` that sets `Claim.FlaggedOverdueAt` with one idempotent SQL `UPDATE`; the clock starts at the `ReviewStarted` audit entry; the flag is kept after a decision; `?overdue=` filter; not audited; disabled in tests except where switched on | A stored flag gives supervisors a triage list and records when a claim went overdue. Deriving the start from the audit log avoids a second copy of the same fact. Idempotent update means several API copies can't double-flag. Errors are caught per run, because an unhandled exception in a background service stops the whole app. No audit entry, because audit actors must be real users |
| 2026-11-20 | Built-in ASP.NET Core rate limiting: login/register 5 a minute per IP; everything else 100 a minute per user (IP if anonymous); health checks exempt; sliding windows; rejected immediately with 429 + `Retry-After`; limits configurable and validated at startup | Slows password guessing, the one known security gap. Sliding windows stop a client sending a double burst across a minute boundary. No queue, so attackers can't tie up the server. Known limits, documented: per-IP only (account lockout would be the next layer), needs trusted forwarded headers behind a proxy, and counters are per instance (a shared store for several instances) |
| 2026-11-20 | Two health endpoints: `/health` (readiness, includes SQL Server, 3-second limit) and `/health/live` (liveness, nothing external); anonymous; body is only `Healthy`/`Unhealthy`; successful probes logged at Verbose | A database outage should stop traffic, not restart a healthy API. Load balancers can't log in, so the response reveals nothing. SqlClient ignored the timeout (it can block before returning a Task), so the check runs on `Task.Run` with `WaitAsync`; an unreachable database now reports in ~3 s instead of ~15 s |
| 2026-11-19 | `docker compose up` runs SQL Server + API in Development mode; the API applies migrations on startup only when `Database:MigrateOnStartup` is true (compose sets it); secrets from a git-ignored `.env` (`.env.example` committed); SQL Server not published outside Docker; no HTTPS redirection inside containers | One command gives a working, explorable system. EF Core locks the database while migrating, so parallel starts are safe. Production would instead run migrations as a separate step (an EF migration bundle) so the app never needs schema-changing permissions, and use a least-privilege login instead of `sa`. TLS belongs to the proxy in front of a container |
| 2026-11-19 | Multi-stage Dockerfile; runtime image `aspnet:10.0-noble-chiseled-extra`; non-root user; secrets only as runtime environment variables; Production by default | The SDK and source never ship. Chiseled images have no shell or package manager, which leaves an attacker little to work with; `-extra` adds the culture data SQL Server's client needs. Trade-off: no `docker exec` shell for debugging |
| 2026-11-19 | Serilog: readable console lines in Development, one JSON object per line elsewhere (chosen in code by environment); levels in `appsettings.json`; one request summary line with `UserId` and `UserRole`; path only, never query strings, headers, tokens, or emails. Each host owns its logger (`preserveStaticLogger: true`, request logging given the host's logger explicitly) | JSON lines are what log tools (Seq, Elastic, Azure) search. Logs are a common place for secrets and personal data to leak, so what's logged is deliberately minimal and tested. The global static logger was shut down by other hosts in the same process (the tests start several), silently losing logs |
| 2026-11-19 | Every 400 is built by one `ValidationProblems` helper, used by `ValidationFilter` and ASP.NET's automatic 400 (`InvalidModelStateResponseFactory`) | Field names always follow the JSON naming policy (camelCase), including query-string and unreadable-input errors; ASP.NET's error dictionary ignores case, so reusing it kept PascalCase keys |
| 2026-11-19 | Claim edits are audited (`Updated` action, old → new values in Details) | Requirements change: a policyholder raising a claimed amount after submitting is exactly what auditors and fraud teams look for |
| 2026-11-18 | BR-09 filters live in one `VisibilityRules` class (`query.VisibleTo(currentUser)`), applied inside SQL; adjusters see only the policies behind claims assigned to them | One place for the whole rule, so no service method can forget it; other users' rows are never loaded. Requirements §2 is silent on adjusters and policies; need-to-know access lets them check coverage while reviewing, without seeing anything outside their caseload |
| 2026-11-18 | One role per account; a staff member who is also a customer uses a separate Policyholder account | Separation of duties: nobody can approve their own claim, and the audit trail never mixes someone's customer and staff actions |
| 2026-11-18 | Seed data via a startup seeder that runs in Development only, not EF `HasData` | `HasData` would put password hashes in migration files and fix them forever; `UseSeeding` runs under `dotnet ef`, where user-secrets aren't loaded. The seeder only inserts missing rows, so it's safe on every startup, and the demo password (`Seed:DemoPassword`) comes from user-secrets |
| 2026-11-19 | Demo claims are built directly by a `SeedClaimBuilder` that checks every step against the real rules (`ClaimLifecycle`, `ClaimPolicyRules`), not by calling the services | The services need a logged-in user and use the current time, so every seeded step would get the same timestamp. The builder gives realistic past dates and different actors, yet can't produce a history the API would reject; an integration test replays every seeded history |
