# Plan: mission-design-service test coverage → ≥ 95%

> Service: `services/mission-design-service`
> Gate: ≥ 95% line coverage (cobertura `line-rate`), per the
> `aspnet-backend-testing` skill. Measured by merging cobertura reports from
> all test projects.

## Starting point (measured)

Merged unit + integration run, `check-coverage.sh`:

| Package                         | Coverage | Lines |
|---------------------------------|----------|-------|
| `umbral_backend.Web`            | 2.98%    | 16/537 |
| `umbral_backend.Infrastructure` | 8.84%    | 32/362 |
| `umbral_backend.Application`    | 14.07%   | 38/270 |
| `umbral_backend.Domain`         | 38.35%   | 51/133 |
| **Overall**                     | **10.52%** | **137/1302** |

Existing tests: 6 unit (CurrentUser ×3, ValidationException ×3), 2 integration
(create/persist happy path). `EndToEndTests` is empty.

## Strategy

Three levers, in priority order:

1. **Functional HTTP suite** (`WebApplicationFactory<Program>` + Testcontainers
   Postgres) — the workhorse. One booted app + real HTTP request exercises, in a
   single pass: every endpoint, the full MediatR pipeline (all 5 behaviours),
   the command/query handlers, `Mission.Create` + value objects, the EF
   interceptors, `MissionConfiguration` mapping, the real migration via
   `InitialiseDatabaseAsync`, DI wiring in all three `AddXServices`, and
   `Program.cs`. This single suite reclaims most of the `Web` (537) and a large
   part of `Infrastructure` (362) and `Application` (270) packages.
2. **Targeted unit tests** for branches HTTP happy-paths can't reach
   (authorization behaviour, performance >500 ms branch, exception-handler arms,
   identity service, domain invariant exceptions, value-object equality).
3. **Coverage configuration** — exclude generated EF migration code and audit
   away dead template scaffolding, so the denominator is hand-written code. This
   is what makes 95% realistic rather than a fight against generated/dead code.

Honest caveat: hitting *exactly* 95% on a Clean Architecture template is
demanding. Expect one or two measure → fill-gaps iterations after the first pass.

---

## Workstream A — Functional HTTP suite (`tests/EndToEndTests`)

**This is the highest-yield work; do it first.**

### A0. Project + bootstrap setup
- Add packages to `Application.FunctionalTests.csproj`:
  `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`.
- **Source change (required):** append `public partial class Program { }` to
  `src/Api/Program.cs` so `WebApplicationFactory<Program>` can reference the
  top-level-statement entry point.
- `CustomWebApplicationFactory : WebApplicationFactory<Program>`:
  - Start a `PostgreSqlContainer` (shared across the suite via an NUnit
    `[SetUpFixture]` / `OneTimeSetUp` to avoid per-test container cost).
  - Override `ConnectionStrings:umbral_backendDb` to the container connection
    string (`ConfigureAppConfiguration` / `UseSetting`).
  - Let the real `InitialiseDatabaseAsync` run → covers `MigrateAsync`,
    `SeedAsync`, `TrySeedAsync`, the migration `Up()`, and persistence DI.
  - Provide a typed `HttpClient` helper and a reset-between-tests mechanism
    (truncate `Missions` or recreate; Respawn optional).
- Mirror the existing flaky-Ryuk workaround: set
  `TESTCONTAINERS_RYUK_DISABLED=true` (already handled by `check-coverage.sh`).

### A1. Endpoint scenarios (black-box HTTP)
| # | Request | Asserts | Covers (beyond the endpoint) |
|---|---------|---------|------------------------------|
| 1 | `POST /api/missions` valid | 201, `Location`, body `Id`/`Status="Draft"` | CreateMission handler, `Mission.Create`, both value objects, `MissionDto`, ValidationBehaviour pass-path, DispatchDomainEvents interceptor, `SaveChanges`, `MissionConfiguration`, migration |
| 2 | `POST` with `X-User-Id` header | persisted `CreatedBy` == header id | `AuditableEntityInterceptor` Added branch, `CurrentUser` via real header, LoggingBehaviour userId-present path |
| 3 | `POST` empty `Name` | 400 ProblemDetails, validation detail | ValidationBehaviour failure-path, `ProblemDetailsExceptionHandler` ValidationException arm |
| 4 | `GET /api/missions` | 200, includes created mission | GetMissions handler, ValidationBehaviour no-validators path |
| 5 | `GET /api/missions/{id}` existing | 200 detail | GetMissionById found-path |
| 6 | `GET /api/missions/{id}` unknown | 404 ProblemDetails | GetMissionById `NotFoundException`, exception-handler NotFound arm |
| 7 | `GET /health` (DB up) | 200 `Healthy` | HealthEndpoints CanConnect true-branch |
| 8 | `GET /alive` | 200 `Alive` | alive lambda |

### A2. Health unhealthy (Phase-2 AC: 503 when Postgres unreachable)
The one tricky AC. Approach: a factory variant that boots + migrates against a
live container, then **stop the container** before calling `GET /health` →
`CanConnectAsync` false → `Results.Problem(503)`. Covers the false branch + the
503 line. (Fallback: a focused integration test asserting `CanConnectAsync()`
== false against a dead connection string, accepting the endpoint's
`Results.Problem` line stays uncovered.)

### A3. Environment branch (optional, cheap)
One test with the factory forced to `Production` to cover `app.UseHsts()` in
`Program.cs`.

---

## Workstream B — Domain unit tests (`tests/UnitTests`)

Fast, no Docker. Drives `Domain` (133) toward ~100%.

- `MissionTests`: Create success (fields set, `Draft`, raises
  `MissionCreatedEvent`); trims name/description; throws
  `MissionNameRequiredException` for null/empty/whitespace (`[TestCase]`);
  throws `MissionDescriptionRequiredException`; propagates
  `DifficultyValueRequiredException` and `MaximumTimeMustBePositiveException`.
- `DifficultyTests`: Create success + trim; throws on empty/whitespace;
  equality via `GetEqualityComponents` (equal vs different).
- `MaximumTimeTests`: Create success; throws on `0` and negative; equality.
- `MissionCreatedEventTests`: ctor exposes the mission.

(The four domain exception classes get covered as they are thrown above.)

---

## Workstream C — Application unit tests (`tests/UnitTests`)

Drives `Application` (270). Behaviours are the bulk here.

- `CreateMissionCommandValidatorTests` (FluentValidation `TestValidate`): valid
  passes; one failing case per rule — empty `Name`, `Name` > 200, empty
  `Description`, `Description` > 2000, empty `Difficulty`, `Difficulty` > 100,
  `MaximumTimeMinutes` ≤ 0.
- `AuthorizationBehaviourTests` (mock `ICurrentUser`/`IIdentityService`, fake
  request types decorated with `[Authorize]`): no-attribute pass-through;
  `[Authorize]` + null user → `UnauthorizedAccessException`; role attribute with
  matching role → pass; non-matching → `ForbiddenAccessException`; policy
  attribute, `AuthorizeAsync` false → `ForbiddenAccessException`, true → pass.
  (Also covers `AuthorizeAttribute` / `Permissions`.)
- `ValidationBehaviourTests`: no validators → `next` called; validators pass →
  `next`; validators fail → `ValidationException`.
- `UnhandledExceptionBehaviourTests`: `next` throws → logs + rethrows; `next` ok
  → returns.
- `PerformanceBehaviourTests`: fast request → no warning; `next` delayed > 500 ms
  → warning branch + `GetUserNameAsync` when userId present.
- `LoggingBehaviourTests`: userId present → `GetUserNameAsync` called; empty →
  skipped.
- `ResultTests`: `Success` → succeeded/no errors; `Failure` → not succeeded +
  errors.

---

## Workstream D — Infrastructure tests (`tests/IntegrationTests`)

Real Postgres (existing Testcontainers pattern). Drives `Infrastructure` (362).

- Move/add **handler integration tests**: `GetMissionsQueryHandler` (ordering,
  empty + populated) and `GetMissionByIdQueryHandler` (found + `NotFound`).
- `AuditableEntityInterceptorTests`: add entity → `Created`/`CreatedBy` set from
  mocked `ICurrentUser` + `TimeProvider`; modify entity → `LastModified` set
  (Modified branch). (`HasChangedOwnedEntities` owned-modified branch is hard —
  no mutators on `Mission`; acceptable residual.)
- `DispatchDomainEventsInterceptorTests`: SaveChanges with a `Mission` →
  `IMediator.Publish` called for `MissionCreatedEvent` and events cleared.
  (Largely also covered by functional POST.)
- `IdentityServiceTests` (pure unit): each method returns its canned value
  (`GetUserNameAsync` → userId, `IsInRoleAsync`/`AuthorizeAsync` → false,
  `CreateUserAsync`/`DeleteUserAsync` → `Failure`).
- `ApplicationDbContextInitialiserTests`: initialiser against a dead connection
  string → `InitialiseAsync` throws (covers the catch/log branch).

---

## Workstream E — Web unit tests (`tests/UnitTests`)

Branches the functional suite can't trigger.

- `ProblemDetailsExceptionHandlerTests`: call `TryHandleAsync` with each
  exception → `NotFoundException` (404), `ValidationException` (400),
  `UnauthorizedAccessException` (401), `ForbiddenAccessException` (403), generic
  (500); assert status + JSON body. Use `DefaultHttpContext`.
- `CurrentUser`: already covered (×3) + functional.

---

## Workstream F — Coverage configuration & dead-code audit

**Required to make 95% achievable and meaningful.**

- Add `coverage.runsettings` (coverlet) excluding generated code:
  - `[*]*.Migrations.*` — EF `Init`, `Init.Designer`, `ApplicationDbContextModelSnapshot`
    (generated; `Designer`/`Snapshot` are not executed at runtime and would
    otherwise sink the denominator).
  - Honor `[ExcludeFromCodeCoverage]` (coverlet default).
  - Point `check-coverage.sh` at the runsettings.
- **Dead template scaffolding — decision needed (delete vs. exclude).** Audit
  and, if unused in Phase 2, remove (cleaner) or exclude:
  `Hubs/WebhookHub.cs` (not mapped in `Program.cs` — recommend delete),
  `IWebhookDispatcher`, `INotifier`, `IClock`, `Common/Models/LookupDto`,
  `Common/Models/PagedResult`, `Constants/Roles`, `AddKeyVaultIfConfigured`
  branch. (`AuthorizeAttribute`/`Permissions` are kept — exercised by
  Workstream C.)

---

## Measurement & exit

- Extend `check-coverage.sh` to run all three test projects
  (`UnitTests`, `IntegrationTests`, `EndToEndTests`) with
  `--collect:"XPlat Code Coverage" --settings coverage.runsettings`, merge, and
  assert ≥ 95%.
- Run with VPN off + `TESTCONTAINERS_RYUK_DISABLED=true` (host networking quirk
  documented in the script).
- Exit criteria: overall `line-rate` ≥ 95%; all Phase-2 acceptance criteria in
  `des-66-local-backend-platform-baseline.md` (incl. health-unhealthy) covered
  by an automated test.

## Required source changes (non-test)
1. `src/Api/Program.cs`: add `public partial class Program { }` (enables
   `WebApplicationFactory<Program>`).
2. `tests/IntegrationTests/GlobalUsings.cs`: `global using Microsoft.EntityFrameworkCore;`
   — **already applied; the integration project does not compile without it. Commit this.**
3. (Decision) delete unused template scaffolding per Workstream F.

## Suggested order
A0 → A1 (biggest jump) → B → C → E → D → A2/A3 → F → measure → fill gaps.
