
## [001] Phase X.1 — Team domain entity, events, and exceptions (HU-04)
**Date:** 2026-05-31
**Phase:** X.1 — Domain layer
**Commits:** 6628347
**HU tickets advanced:** HU-04

**What was built**
`Team` aggregate root entity in `src/Domain/Entities/Team.cs` with value-based equality (via `BaseAuditableEntity`), invariant enforcement via static factory (`Register`) and guarded setters (`UpdateDetails`, `Deactivate`). Three domain events (`TeamRegisteredEvent`, `TeamDetailsUpdatedEvent`, `TeamDeactivatedEvent`) raised at each lifecycle transition. Three domain exceptions (`TeamCodeRequiredException`, `TeamDisplayNameRequiredException`, `TeamAlreadyDeactivatedException`) for invariant violations. Unit tests in `tests/UnitTests/Domain/Entities/TeamTests.cs` covering registration, detail updates, deactivation, idempotent deactivation guard, and null/whitespace rejection.

**Why this approach**
Follows the existing HU-03 domain pattern (User, IdentityProviderSession) for consistency — sealed entity, private constructors, static factory, `Require*` guard methods. Domain events are raised inline so the application layer can react transactionally. The `TeamCode` field acts as a stable business key distinct from the `TeamId` GUID, anticipating future cross-context references.

**Deliberately skipped**
- Soft-delete or archival — only active/deactivated is modelled; no `DeletedAt` or tombstone state
- `TeamCode` uniqueness enforcement — deferred to the infrastructure layer (DB unique constraint + repository check)
- Team membership or user-to-team assignment — not in HU-04 domain scope; belongs to a later phase
- Integration tests — only unit tests were added; integration tests will come in the infrastructure phase

**Next session needs to know**
Build passes clean (0 errors, 2 NuGet warnings). The next phase is X.2 — Application layer: `TeamService` use cases orchestrating `RegisterTeam`, `UpdateTeamDetails`, `DeactivateTeam` with repository abstractions.

---

## [002] Phase X.2 — Application CQRS handlers, validators, and repository interface (HU-04)
**Date:** 2026-05-31
**Phase:** X.2 — Application layer
**Commits:** 83cd124
**HU tickets advanced:** HU-04

**What was built**
CQRS command/query artifacts for the Team lifecycle: `RegisterTeamCommand`, `UpdateTeamCommand`, `DeactivateTeamCommand`, `GetTeamByIdQuery`, `GetTeamsQuery` — each with FluentValidation validators. `ITeamRepository` interface defining the application boundary. `TeamDto` output model. Five MediatR handlers orchestrating repository calls. Unit tests for all handlers and validators in `tests/UnitTests/`.

**Why this approach**
Follows the existing HU-03 CQRS-with-MediatR pattern for consistency. Commands and queries are separated to allow independent validation and future auth decoration. The `ITeamRepository` interface keeps the application layer decoupled from EF Core.

**Deliberately skipped**
- `TeamCode` uniqueness validation in the application layer — deferred to the infrastructure repository
- Integration tests — deferred to X.3

**Next session needs to know**
All unit tests pass. The `ITeamRepository` interface needs an EF Core implementation in X.3. Handlers currently assume `TeamCode` uniqueness is checked in the repository.

---

## [003] Phase X.3 — EF Core persistence, repository, and integration tests (HU-04)
**Date:** 2026-05-31
**Phase:** X.3 — Infrastructure layer
**Commits:** 55033fe, 6c5d20e, b9340cd
**HU tickets advanced:** HU-04

**What was built**
EF Core `TeamConfiguration` (entity mapping), `TeamRepository` implementing `ITeamRepository` with `TeamCode` uniqueness check, EF Core migration `20260531161852_AddTeams`, `DependencyInjection` registration. `TeamCodeAlreadyExistsException` domain exception added. Register/Update handlers retrofitted to throw it on duplicate code. Integration tests in `TeamRepositoryIntegrationTests.cs` covering CRUD and the uniqueness constraint.

**Why this approach**
EF Core with code-first migrations matches the existing infrastructure patterns. The `TeamCode` uniqueness check uses LINQ `.Any()` query rather than catching a DB constraint exception, keeping exception handling domain-idiomatic. Testcontainers with PostgreSQL used for integration tests, consistent with HU-03.

**Deliberately skipped**
- Soft-delete — not in scope for HU-04
- Team membership mapping table — deferred to HU-05

**Next session needs to know**
Build and integration tests pass. The `TeamCodeAlreadyExistsException` was added to the Domain layer and handlers were retrofitted. The next phase is X.4 — API layer: minimal API endpoints for Team CRUD.

---

## [004] Phase X.4 — Minimal API endpoints and integration tests (HU-04)
**Date:** 2026-05-31
**Phase:** X.4 — API layer
**Commits:** ecf4278
**HU tickets advanced:** HU-04

**What was built**
`TeamsEndpoints` minimal API group with `POST /teams`, `GET /teams`, `GET /teams/{id}`, `PUT /teams/{id}`, `DELETE /teams/{id}` endpoints. `ProblemDetailsExceptionHandler` for structured error responses. Full integration tests in `IdentityAccessApiEndpointsTests.cs` covering request/response lifecycle for all endpoints. `ProblemDetailsExceptionHandlerTests` for exception-to-response mapping.

**Why this approach**
Minimal API pattern consistent with existing HU-03 API structure. All endpoints use MediatR to dispatch to application layer handlers, preserving the layered architecture. `ProblemDetails` follows RFC 9457 for standardized error responses. Integration tests use `WebApplicationFactory` with Testcontainers-backed PostgreSQL.

**Deliberately skipped**
- Swagger/OpenAPI documentation — not required for this sprint
- Authorization/role-based access control on team endpoints — deferred to a later HU

**Next session needs to know**
Build passes. All endpoint integration tests cover success and error paths. Ready for HU-05 participant-team assignment.
