# Prompt Example — HU-04 Team Registration and Maintenance (Feature Slice)

Concrete prompt sequence for driving HU-04 through a full feature slice on `feature/hu-04-team-registration`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-03:** HU-03 extended the existing `User` aggregate to support role assignment. HU-04 introduces an entirely new `Team` aggregate in the Identity bounded context — team reference data that is never used for runtime session state. The scope is backend-only: no frontend surface is touched. Steps 1 and 2 from the HU-01 template (read backlog, create PRD) are replaced here by a pre-resolved orient. There is no PRD creation step.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. Do not ask for backend
and frontend implementation in the same phase prompt. HU-04 has no frontend scope.

---

## Pre-resolved orient (as of 2026-05-31)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What HU-01, HU-02, and HU-03 have already landed (reuse candidates for HU-04)

All of this lives on `feature/hu-03-role-permission-assignment` (or `develop` once HU-03 merges).

**Domain layer**

- `User` aggregate root — `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; `Deactivate()` prevents protected access for inactive users; `AssignRole(Role newRole)` mutates role with invariant guards
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `ProtectedCapability` enum + permission matrix (`AuthenticatedPlatformAccess`, `AdministratorPanel`, `OperatorPanel`, `ParticipantExperience`)
- `AccessPolicy` domain service — single authority for role-to-capability evaluation and inactive-user denial
- `IdentityProvisioningPolicy` — synchronizes or creates application-side `User` records from trusted claims
- `IdentityProviderSession` entity with revocation/expiry traceability
- Domain events: `UserProvisioned`, `UserRoleAssigned`, `UserRoleRevoked`, `UserAccessDeactivated`, `AccessDecisionRecorded`, `IdentityProviderSessionStarted`, `IdentityProviderSessionEnded`

**Application layer**

- `AuthenticateUserCommand` / handler (post-login provisioning, deactivated-user rejection)
- `DeactivateUserCommand` / handler (Administrator-only)
- `AssignUserRoleCommand` / handler (Administrator-only, syncs role changes)
- `GetUsersQuery` / handler (paginated catalog, Administrator or Operator)
- `GetAuthenticatedActorProfileQuery` + handler
- `CheckProtectedCapabilityAccessQuery` + handler
- `AuthorizationBehaviour`, `ValidationBehaviour`, `PerformanceBehaviour`, `UnhandledExceptionBehaviour`
- `ICurrentUser` / `GatewayRoleParser` (trusted-header identity)

**Infrastructure / API**

- EF Core `users` + `identity_provider_sessions` tables; repository support for user catalog and role/access lifecycle
- Endpoints: `POST /api/users/authenticated`, `GET /api/users/me`, `GET /api/users`, `DELETE /api/users/{id}/access`, `PATCH /api/users/{id}/role`, `GET /api/permissions/authenticated-platform-access`
- `ProblemDetailsExceptionHandler` maps validation/auth/not-found failures

**Coverage:** 94.95% total line — below the 95% threshold. HU-04's API/integration phase must close the gap.

### What HU-04 adds on top (per PRD DES-67)

| Concern | New work |
|---|---|
| `Team` aggregate | `TeamId`, `DisplayName`, `TeamCode`, `IsActive`, `CreatedAt`, `UpdatedAt`; lifecycle operations below |
| Team lifecycle | `Register(displayName, teamCode)` (factory/constructor path), `UpdateDetails(displayName, teamCode)`, `Deactivate()` with an explicit invariant decision for already-inactive teams |
| Domain events | `TeamRegisteredEvent`, `TeamDetailsUpdatedEvent`, `TeamDeactivatedEvent` |
| Application use cases | `RegisterTeamCommand`, `UpdateTeamCommand`, `DeactivateTeamCommand`, `GetTeamsQuery`, `GetTeamByIdQuery` |
| Persistence | `teams` table, unique `TeamCode` index, `ITeamRepository`, `AddTeams` migration |
| API endpoints | `POST /api/teams`, `GET /api/teams`, `GET /api/teams/{id}`, `PATCH /api/teams/{id}`, `DELETE /api/teams/{id}/status` |
| Authorization | Mutations are Administrator-only; reads are Administrator or Operator |
| Historical traceability | Deactivation is soft — row remains queryable with `IsActive=false` |

### Branch state and prerequisite

`feature/hu-04-team-registration` should be branched from `develop` **after** HU-03 merges, or directly from `feature/hu-03-role-permission-assignment` if HU-03 has not yet been merged to `develop`.

**Before starting HU-04 implementation:** confirm that no `Team` concept exists in the domain project to avoid duplication. The identity-side `Team` is reference data only — never add runtime fields (score, join status, progress) or reference `session-operations-service` entities.

### Linear state (as of 2026-05-31)

- DES-8 (HU-04): **In Progress**, labels: `Feature`, `ready-for-agent`, `svc:identity-access-service`
- DES-7 (HU-03): **In Progress** (or Done if merged)
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`

> Linear live state may have changed. Use the Linear MCP to verify DES-8 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md` instead.

---

## 1. Orient — read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the README or Linear state may have changed since 2026-05-31.

```
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/identity-access-service/README.md — current implementation status
- @backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md
  — the PRD scope for all HU-01 to HU-08 slices

Then use the Linear MCP to fetch only the current live state of:
- DES-8  (HU-04 — Registro y mantenimiento de equipos) — status and labels
- DES-7  (HU-03 — Asignación de roles y permisos, the predecessor slice) — status

Output:
- what domain concepts HU-01 through HU-03 have already landed (User, Role enum,
  AccessPolicy, team-free domain) from the README — these are reuse candidates for HU-04
- what HU-04 adds on top per the PRD: Team aggregate, lifecycle operations,
  domain events, CRUD use cases, teams table, five API endpoints
- current Linear status and labels for DES-8

Do not start planning or implementing yet.
```

---

## 2. Confirm slice readiness

> DES-8 already carries the `ready-for-agent` label — skip the labelling sub-step.

```
Use the Linear MCP to confirm DES-8 carries both svc:identity-access-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-04` and `DES-8` are the resolved values for this slice. `DES-67` is the shared PRD reference for identity-access-service; its content lives in the local file above.

---

## 3. Start the slice

```
Prepare the team registration and maintenance slice on branch feature/hu-04-team-registration.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend identity-access-service only — no frontend scope.

The pre-resolved orient at the top of this document lists what HU-01 through HU-03 have
already landed and what HU-04 adds. Do not re-read the README or PRD for scoping.

Move the resolved HU ticket to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 4. Backend phase X.1 — Domain layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-04 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Before writing anything, read @backend/services/identity-access-service/README.md to
confirm that no Team concept exists yet. HU-04 introduces Team as a brand-new aggregate;
never extend or alias an existing entity.

Scope:
- Team aggregate root: TeamId (typed id or Guid), DisplayName, TeamCode, IsActive,
  CreatedAt, UpdatedAt
- Register(displayName, teamCode) — creates a new active Team, raises TeamRegisteredEvent;
  DisplayName and TeamCode must not be blank (domain validation)
- UpdateDetails(displayName, teamCode) — updates both fields, raises TeamDetailsUpdatedEvent;
  apply the same blank-value guards
- Deactivate() — sets IsActive to false, raises TeamDeactivatedEvent; decide explicitly
  whether deactivating an already-inactive team throws TeamAlreadyDeactivatedException
  or is a silent no-op — pick the throwing variant to match the User.Deactivate() pattern
  already established in HU-02
- Domain events: TeamRegisteredEvent, TeamDetailsUpdatedEvent, TeamDeactivatedEvent —
  each carries TeamId and the minimal fields needed for downstream audit
- No value objects are mandatory, but TeamCode may be wrapped if the project already has
  a value-object convention; check existing domain primitives before deciding

Gate:
- Domain build passes
- No existing HU-01 through HU-03 domain concepts broken
- Team aggregate invariants are expressed as unit tests:
  blank DisplayName rejected, blank TeamCode rejected,
  Deactivate() on active team succeeds and event is raised,
  Deactivate() on already-inactive team throws TeamAlreadyDeactivatedException,
  UpdateDetails on an inactive team is still allowed (no invariant blocks it)

Do not touch other backend layers.
```

Commit:

```
feat(identity-access): phase X.1 — domain layer (HU-04)

Ref: HU-04
Ref: DES-8
Ref: DES-67
```

Then run: `/debrief`

---

## 5. Backend phase X.2 — Application layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-04 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- RegisterTeamCommand + handler: create Team via Team.Register(displayName, teamCode),
  check TeamCode uniqueness against ITeamRepository before persisting (throw a domain
  exception if duplicate), persist, dispatch domain events; Administrator-only
- UpdateTeamCommand + handler: load Team by id, call Team.UpdateDetails(displayName, teamCode),
  check TeamCode uniqueness excluding the current team's own code, persist, dispatch events;
  Administrator-only
- DeactivateTeamCommand + handler: load Team by id, call Team.Deactivate(), persist,
  dispatch events; Administrator-only
- GetTeamsQuery + handler: paginated list of all teams (active and inactive),
  accessible to Administrator or Operator; return TeamDto projections
- GetTeamByIdQuery + handler: single team by id accessible to Administrator or Operator;
  return 404-equivalent if not found
- ITeamRepository interface: declare GetByIdAsync, ListAsync(page, pageSize),
  TeamCodeExistsAsync(teamCode, excludeTeamId?) — do not implement infrastructure yet
- Validators: all commands validated via FluentValidation following existing handler patterns;
  non-empty fields, id must not be empty, role must be Administrator where required
- Handler unit tests for:
  - RegisterTeamCommand: success path, duplicate TeamCode rejected, blank name rejected,
    non-admin caller rejected
  - UpdateTeamCommand: success path, TeamCode collision with another team rejected,
    team not found, non-admin rejected
  - DeactivateTeamCommand: success path, already-inactive team throws expected exception,
    team not found, non-admin rejected
  - GetTeamsQuery: returns paginated result for admin and operator callers
  - GetTeamByIdQuery: returns team for admin and operator callers, not-found path

Gate:
- Clean build passes
- Handler unit tests pass for all paths listed above

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```
feat(identity-access): phase X.2 — application layer (HU-04)

Ref: HU-04
Ref: DES-8
Ref: DES-67
```

Then run: `/debrief`

---

## 6. Backend phase X.3 — Infrastructure layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-04 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- teams table: id (uuid pk), display_name (text not null), team_code (text not null),
  is_active (bool not null default true), created_at (timestamptz not null),
  updated_at (timestamptz not null)
- unique index on team_code (enforced at DB level in addition to application-layer check)
- EF Core entity configuration: map each column, configure the unique index,
  and add the Teams DbSet to ApplicationDbContext
- TeamRepository implementing ITeamRepository: GetByIdAsync, ListAsync(page, pageSize),
  TeamCodeExistsAsync(teamCode, excludeTeamId?)
- AddTeams migration: run dotnet ef migrations add AddTeams inside the infrastructure
  project; inspect the generated file and confirm the unique index appears; if not,
  add it manually before committing
- Integration tests (each test must ExecuteDeleteAsync teams before its scenario):
  - register a team, re-fetch by id, confirm all fields persisted and TeamRegisteredEvent raised
  - update a team's details, re-fetch, confirm changes and TeamDetailsUpdatedEvent raised
  - deactivate a team, re-fetch, confirm IsActive=false and TeamDeactivatedEvent raised
  - attempt to register two teams with the same TeamCode, confirm the second throws
    (application-level TeamCode uniqueness check in handler, DB unique constraint as backstop)
  - list teams with pagination, confirm page boundaries respected

Database isolation: each integration test must ExecuteDeleteAsync on Teams before its
scenario to avoid cross-test pollution. Follow the same CapturingMediator / NoOpMediator
pattern used in HU-02 and HU-03 infrastructure tests.

Test infrastructure: wire DispatchDomainEventsInterceptor into DbContextOptions via the
BuildContext factory (accepting an optional IMediator? parameter) so domain events are
dispatched during SaveChangesAsync. Without this wiring, domain event assertions will
silently fail.

Before writing tests, verify that using statements cover Domain.Entities, Domain.Enums,
Domain.Events, Domain.Exceptions, MediatR, and Infrastructure.Persistence.Interceptors.

Gate:
- dotnet build passes on the solution
- AddTeams migration file generated and reviewed; unique index on team_code confirmed
- Repository integration tests pass for all five paths above

Do not touch Api or frontend.
```

Commit:

```
feat(identity-access): phase X.3 — infrastructure layer (HU-04)

Ref: HU-04
Ref: DES-8
Ref: DES-67
```

Then run: `/debrief`

---

## 7. Backend phase X.4 — API layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-04 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- POST /api/teams (Administrator-only): accepts { "displayName": "…", "teamCode": "…" },
  dispatches RegisterTeamCommand, returns 201 Created with the new team id
- GET /api/teams (Administrator or Operator): returns paginated list of TeamDto,
  supports page/pageSize query params; includes active and inactive teams
- GET /api/teams/{id} (Administrator or Operator): returns TeamDto for the given id;
  returns 404 if not found
- PATCH /api/teams/{id} (Administrator-only): accepts { "displayName": "…", "teamCode": "…" },
  dispatches UpdateTeamCommand, returns 204 No Content
- DELETE /api/teams/{id}/status (Administrator-only): dispatches DeactivateTeamCommand,
  returns 200 OK with updated team state (IsActive=false); confirm the response body
  or re-fetch to show the caller the deactivated row
- ProblemDetails error mapping: ensure TeamAlreadyDeactivatedException → 409 or 422,
  duplicate TeamCode → 409 Conflict, not-found → 404, validation errors → 400

Endpoint tests:
- POST /api/teams: success (201), duplicate TeamCode (409), blank fields (400),
  non-admin caller (403)
- GET /api/teams: success for admin, success for operator, non-participant caller
  (confirm operator has access and participant does not)
- GET /api/teams/{id}: success, not found (404)
- PATCH /api/teams/{id}: success (204), TeamCode collision (409), unknown team (404),
  non-admin caller (403)
- DELETE /api/teams/{id}/status: success (200, IsActive=false in response),
  already-inactive team (409 or 422), non-admin caller (403)

Coverage gate:
- Run the merged unit + integration + endpoint test suite
- Aggregate branch coverage must reach ≥ 95%; if it does not, add targeted tests for
  any uncovered branch before closing the phase

Do not touch frontend.
```

Commit:

```
feat(identity-access): phase X.4 — api layer (HU-04)

Ref: HU-04
Ref: DES-8
Ref: DES-67
```

Then run: `/debrief`

---

## 7.5 — Rebuild backend container images

The backend Docker images were built before the new endpoints existed. Rebuild
and restart so integration tests and downstream consumers can reach the live service:

```bash
cd backend && docker compose build identity-access-service
docker compose up -d identity-access-service
```

Also rebuild the gateway if any proxy configuration changed:

```bash
docker compose build api-gateway
docker compose up -d api-gateway
```

Verify the new endpoints respond:

```bash
# Create a team
curl -s -X POST http://localhost:5002/api/teams \
  -H "Content-Type: application/json" \
  -H "X-User-Id: admin-1" \
  -H "X-User-Role: Administrator" \
  -H "X-User-Email: admin@umbral.local" \
  -d '{"displayName":"Equipo Alpha","teamCode":"ALPHA"}'

# List teams
curl -s http://localhost:5002/api/teams \
  -H "X-User-Id: admin-1" \
  -H "X-User-Role: Administrator" \
  -H "X-User-Email: admin@umbral.local"
```

**Gate:** all five team endpoints return the expected status codes when called with trusted headers; existing HU-01 through HU-03 endpoints remain functional.

---

## 8. Frontend slice

```
Use @frontend/AGENTS.md.
Implement the frontend part of HU-04.
Use the verified backend contract.

Scope:
- team list view: an admin or operator can browse all teams (active and inactive)
  via GET /api/teams with pagination; show DisplayName, TeamCode, IsActive status,
  and CreatedAt for each team
- team detail view: clicking a team row navigates to a detail page that fetches
  GET /api/teams/{id} and displays all fields; an admin sees action buttons
  (edit, deactivate) while an operator sees a read-only view
- team create form: an admin can register a new team via POST /api/teams with
  DisplayName and TeamCode fields; show inline validation for blank fields and
  a user-friendly error for duplicate TeamCode (409); redirect to the new team's
  detail page on success (201)
- team edit form: an admin can update DisplayName and TeamCode via PATCH /api/teams/{id};
  show the same validation rules; handle 404 (team deleted between navigation and
  submission) and 409 (duplicate TeamCode) errors gracefully
- team deactivation action: an admin can deactivate a team via DELETE /api/teams/{id}/status
  with a confirmation dialog (similar to the user deactivation pattern from HU-02);
  update the UI to reflect IsActive=false without a full page reload; handle
  409/422 for already-inactive teams
- navigation: add a "Teams" link in the main navigation, visible only to
  Administrators and Operators (derive visibility from GET /api/permissions/
  authenticated-platform-access or the role from GET /api/users/me);
  hide the link from Participants
- route and component guards: team management routes must be accessible only to
  Administrators and Operators; a Participant accessing a team URL directly must
  be redirected or shown an error — never silent partial rendering
- keep one shared app, not separate admin/operator apps
- do not regress HU-01, HU-02, or HU-03 flows (login, deactivation, user list,
  role assignment)

Gate:
- admin can create, view, edit, and deactivate teams from the UI
- operator can view teams (list and detail) but not create, edit, or deactivate
- participant cannot access team routes and is redirected or shown an error
- direct URL access to a protected team route by an unauthorized role results
  in a redirect or error
- existing HU-01 through HU-03 frontend flows are not regressed
```

Commit:

```
feat(frontend): team registration and maintenance — HU-04

Ref: HU-04
Ref: DES-8
Ref: DES-67
```

Then run: `/debrief`

---

## 9. Close out

```
Verify HU-04 end to end for DES-8 on feature/hu-04-team-registration.

Acceptance criteria:
- administrator can register a new team with a unique TeamCode and DisplayName
- administrator can update a team's DisplayName and TeamCode
- deactivated teams remain queryable (IsActive=false) and cannot be deactivated again
- duplicate TeamCode is rejected at both application and database layers
- reads (list, get-by-id) are accessible to administrators and operators but not participants
- aggregate branch coverage is ≥ 95% after the merged test suite runs

Then:
- open or update the draft PR to develop
- include DES-8 and DES-67 in the PR description
- move HU-04 to Done only if all acceptance criteria pass
```

PR command:

```
gh pr create --draft --base develop --title "feat: team registration and maintenance — HU-04"
```

---

## Rationale for changes relative to HU-03

**Team is a new aggregate root, not an extension of User.** HU-03 mutated an existing entity (`User.AssignRole`). HU-04 introduces `Team` from scratch with its own identity, lifecycle, and event set. The domain phase must confirm no prior `Team` concept exists before writing a single line.

**TeamCode uniqueness is a dual-layer contract.** Application handlers must validate uniqueness before persisting (to give a structured domain exception), and the database must enforce it again via a unique index (as a backstop against concurrent races). Both layers are required; neither alone is sufficient.

**Deactivation throws on double-deactivation.** The `User.Deactivate()` pattern established in HU-02 throws `UserAccessAlreadyDeactivatedException` rather than silently no-oping. `Team.Deactivate()` must follow the same idiom (`TeamAlreadyDeactivatedException`) so the codebase is internally consistent.

**DELETE /api/teams/{id}/status returns the updated record.** The acceptance criterion requires a client to observe `IsActive=false` after calling the endpoint. Returning the updated team in the response body avoids a mandatory follow-up GET, making the acceptance test simpler and the API easier to use.

**Frontend covers full team CRUD with role-scoped visibility.** Administrators create, edit, and deactivate teams from the UI; Operators browse teams read-only; Participants cannot access team routes at all. Role-based route guards and navigation visibility follow the same pattern established in HU-03.

**Three DES refs per commit.** HU-04 commits reference DES-8 (HU ticket) and DES-67 (PRD), matching the pattern used in HU-01 through HU-03.
