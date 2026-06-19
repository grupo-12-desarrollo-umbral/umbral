# Parallel Execution Guide — HU-04 and HU-05

Merged prompt sequence for running DES-8 (HU-04 Team Registration) and DES-9
(HU-05 Participant-to-Team Assignment) in parallel on separate git worktrees.
Source documents: `backend/docs/prompt_example_feature_hu04.md` and
`backend/docs/prompt_example_feature_hu05.md`.

---

## Sync rule

| HU-04 phase done                                                                | HU-05 may start                                   |
|---------------------------------------------------------------------------------|---------------------------------------------------|
| **X.2 Application committed** (ITeamRepository interface + all command/query handlers) | Domain (Y.1) + Application (Y.2)             |
| **X.3 Infrastructure committed AND gated** (`AddTeams` migration merged/rebased, phase X.3 gate passing) | Infrastructure (Y.3) + `AddTeamMemberships` migration |
| **X.4 API committed** (all five team endpoints live + coverage gate passed)     | API (Y.4)                                         |

HU-05 **must never** run `dotnet ef migrations add AddTeamMemberships` before
HU-04's `AddTeams` migration is committed and this branch is rebased on top of it.

---

## Boundary decision (read before any implementation — both agents)

> This block is authoritative. Do not override it.

`session-operations-service` owns the **runtime** `Team` entity (internal to a
`LiveSession`; carries `currentScore`, `joinStatus`, `progressNodeId`, live seat
counts, etc.).

HU-04 and HU-05 create a separate **team registry** in
the **Identity** bounded context. It carries identity and operational-status data
only: `TeamId`, `DisplayName`, `TeamCode`, `IsActive`, audit timestamps. HU-05
extends this with `TeamMembership` — a record stating which
`Participant` is authorized for which team before a session starts.

This split is authorized by PRD DES-67:
*"implementarse como contrato explícito entre bounded contexts"*.

**Never** model session-runtime fields (`currentScore`, `joinStatus`,
`progressNodeId`, seat counts, queue position) on Identity's `Team` or
`TeamMembership`. Those fields belong exclusively in `session-operations-service`.

The Identity membership record is the authorization fact that HU-07's JoinToken
will consult. Do not conflate membership records with live access.

---

## Branch and worktree setup

```bash
# Agent A — HU-04
# Branch from develop (or from feature/hu-03-role-permission-assignment if HU-03 not yet merged)
git worktree add ../umbral-hu04 -b feature/hu-04-team-registration develop

# Agent B — HU-05
# Branch from HU-04's branch (not from develop — HU-05 needs Team aggregate + ITeamRepository)
# Run AFTER HU-04's X.2 commit is available
git worktree add ../umbral-hu05 -b feature/hu-05-participant-team-assignment feature/hu-04-team-registration
```

**Rebase protocol for Agent B before Y.3:**

```bash
# From the HU-05 worktree — run after HU-04's X.3 commit lands
git fetch origin
git rebase origin/feature/hu-04-team-registration

# Confirm AddTeams appears in migration list before proceeding
dotnet ef migrations list \
  --project backend/services/identity-access-service/src/Infrastructure \
  --startup-project backend/services/identity-access-service/src/Api
# Expected: ..., AddTeams   ← must be present and not pending
```

---

---

# HU-04 — Team Registration and Maintenance

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

> **HU-05 sync point:** After this commit is pushed, the HU-05 agent may check out
> `feature/hu-04-team-registration` and start phases Y.1 and Y.2 in parallel.

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

> **HU-05 sync point:** After this commit is pushed and the gate passes, rebase the
> HU-05 worktree on top of this commit, then proceed to phase Y.3.

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
- Aggregate line coverage must reach ≥ 95%; if it does not, add targeted tests for
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

> **HU-05 sync point:** After this commit is pushed, the HU-05 agent may proceed to Y.4.

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

## 8. Close out HU-04

```
Verify HU-04 end to end for DES-8 on feature/hu-04-team-registration.

Acceptance criteria:
- administrator can register a new team with a unique TeamCode and DisplayName
- administrator can update a team's DisplayName and TeamCode
- deactivated teams remain queryable (IsActive=false) and cannot be deactivated again
- duplicate TeamCode is rejected at both application and database layers
- reads (list, get-by-id) are accessible to administrators and operators but not participants
- aggregate line coverage is ≥ 95% after the merged test suite runs

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

---

# HU-05 — Participant-to-Team Assignment

## Pre-resolved orient (as of 2026-05-31)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What HU-01 through HU-04 have already landed (reuse candidates for HU-05)

All of this lives on `feature/hu-04-team-registration` (or `develop` once HU-04 merges).

**Domain layer**

- `User` aggregate root — `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; full lifecycle with deactivation and role assignment
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `AccessPolicy` and protected capability matrix for coarse-grained access checks
- `IdentityProviderSession` and identity provisioning model from HU-01
- `Team` aggregate from HU-04: `TeamId`, `DisplayName`, `TeamCode`, `IsActive`, `CreatedAt`, `UpdatedAt`; lifecycle events `TeamRegisteredEvent`, `TeamDetailsUpdatedEvent`, `TeamDeactivatedEvent`
- `TeamAlreadyDeactivatedException` on double-deactivation (established in HU-04)

**Application layer**

- Existing user/admin flows: authenticate, deactivate access, assign role, list users, inspect actor profile, check protected capability access
- HU-04 team administration flows: `RegisterTeamCommand`, `UpdateTeamCommand`, `DeactivateTeamCommand`, `GetTeamsQuery`, `GetTeamByIdQuery`
- `IUserRepository` available for loading users by id
- `ITeamRepository` from HU-04: `GetByIdAsync`, `ListAsync`, `TeamCodeExistsAsync` — will be extended in HU-05 to include membership loading if required

**Infrastructure / API**

- Existing user/access endpoints from HU-01 through HU-03
- HU-04 team endpoints: `POST /api/teams`, `GET /api/teams`, `GET /api/teams/{id}`, `PATCH /api/teams/{id}`, `DELETE /api/teams/{id}/status`
- HU-04 persistence surface: `teams` table and `AddTeams` migration — these must exist and be committed before HU-05 infrastructure work begins

**Coverage:** Aggregate line coverage must still reach ≥95% after HU-05 membership paths are added.

### What HU-05 adds on top (per PRD DES-67)

| Concern | New work |
|---|---|
| `TeamMembership` record | Membership record: `TeamMembershipId`, `TeamId`, `UserId`, `AssignedAt` |
| Membership invariants | `Team.AssignParticipant(userId)` succeeds only for active teams; rejects duplicate assignments (same user already in same team) |
| Domain events | `ParticipantAssignedToTeamEvent` carrying `TeamId` and `UserId` |
| Domain exceptions | `TeamNotActiveException` (team is inactive), `ParticipantAlreadyAssignedToTeamException` (membership already exists) |
| Application use case | `AssignParticipantToTeamCommand` — Administrator-only; verifies team exists, user exists, `user.Role == Participant` (throws `UserNotParticipantRoleException` otherwise), then calls `Team.AssignParticipant(userId)` |
| Membership query | `GetTeamParticipantsQuery` + handler — returns participant projections (`TeamMembershipDto`) for a team; accessible to Administrator or Operator |
| Persistence | `team_memberships` table with FK to `teams` and `users`; unique constraint on `(team_id, user_id)`; `AddTeamMemberships` migration; repository loading with memberships |
| API endpoints | `POST /api/teams/{id}/participants`, `GET /api/teams/{id}/participants` |
| Authorization boundary | HU-05 records membership facts only — final runtime admission is owned by HU-07 and `session-operations-service` |

### Branch state and prerequisite

`feature/hu-05-participant-team-assignment` must be branched from `feature/hu-04-team-registration` (or from `develop` after HU-04 merges). **Never branch from `develop` before HU-04 lands**, because HU-05 depends on the `Team` aggregate, `ITeamRepository`, and the `AddTeams` migration that HU-04 introduces.

**Hard migration gate:** Do not run `dotnet ef migrations add AddTeamMemberships` until:
1. HU-04's `AddTeams` migration file is committed on the source branch.
2. The current branch is rebased (or merged) on top of that commit.
3. `dotnet ef database update` applies `AddTeams` cleanly before `AddTeamMemberships` is generated.

Violating this order will produce a migration chain that references a snapshot inconsistent with the actual database and will fail in CI.

### Linear state (as of 2026-05-31)

- DES-9 (HU-05): **Backlog**, labels: `Feature`, `svc:identity-access-service`
- DES-8 (HU-04): **In Progress**
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`

> DES-9 does not yet carry the `ready-for-agent` label. Add it (step 2 below) before the agent picks up the slice.

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
- DES-9  (HU-05 — Asignación de participante a equipo) — status and labels
- DES-8  (HU-04 — Registro y mantenimiento de equipos, the predecessor slice) — status

Output:
- what domain concepts HU-01 through HU-04 have already landed (User, Role enum,
  AccessPolicy, Team aggregate with lifecycle) from the README — these are reuse candidates for HU-05
- what HU-05 adds on top per the PRD: TeamMembership, AssignParticipant operation,
  membership events, two API endpoints, AddTeamMemberships migration
- current Linear status and labels for DES-9

Do not start planning or implementing yet.
```

---

## 2. Label DES-9 as ready-for-agent

> HU-05 (DES-9) is currently in Backlog without the `ready-for-agent` label.
> Add it before the agent picks up the slice.

```
Use the Linear MCP to add the label ready-for-agent to DES-9.
Confirm the label was applied and output the updated ticket state.
```

---

## 3. Confirm slice readiness

```
Use the Linear MCP to confirm DES-9 now carries both svc:identity-access-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Before continuing, confirm that HU-04's AddTeams migration exists on the current branch.
Run: dotnet ef migrations list --project backend/services/identity-access-service/Infrastructure
and verify AddTeams appears in the output. If it does not, stop and instruct the user to
rebase this branch on top of feature/hu-04-team-registration before proceeding.

Output the confirmed HU id, title, acceptance criteria, and labels.
Output the migration list result.
```

In the remaining examples below, `HU-05` and `DES-9` are the resolved values for this slice. `DES-67` is the shared PRD reference for identity-access-service; its content lives in the local file above.

---

## 4. Start the slice

```
Prepare the participant-to-team assignment slice on branch feature/hu-05-participant-team-assignment.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend identity-access-service only — no frontend scope.

The pre-resolved orient at the top of this document lists what HU-01 through HU-04 have
already landed and what HU-05 adds. Do not re-read the README or PRD for scoping.

Confirm that the branch is rooted on feature/hu-04-team-registration (or a commit
that includes the AddTeams migration) before any implementation begins.

Move the resolved HU ticket to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase Y.1 — Domain layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase Y.1 for HU-05 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Before writing anything, read @backend/services/identity-access-service/README.md to
confirm that the Team aggregate from HU-04 is present and that no TeamMembership
concept already exists. Extend Team; never recreate it.

Scope:
- TeamMembership record: TeamMembershipId (typed id or Guid), TeamId, UserId, AssignedAt (DateTimeOffset)
  — this is a child record owned by or associated with Team, not a standalone aggregate
- Team.AssignParticipant(Guid userId) — creates a TeamMembership record for (TeamId, userId),
  raises ParticipantAssignedToTeamEvent; invariants:
    * if Team.IsActive is false, throw TeamNotActiveException
    * if a TeamMembership for userId already exists on this team, throw ParticipantAlreadyAssignedToTeamException
  Memberships must be accessible as a collection on Team for invariant checking
- ParticipantAssignedToTeamEvent: carries TeamId and UserId
- New domain exceptions: TeamNotActiveException, ParticipantAlreadyAssignedToTeamException
- Note: UserNotParticipantRoleException belongs at the application layer, not here —
  the domain operation receives a userId and does not load a User; the handler is responsible
  for verifying the role before calling AssignParticipant

Gate:
- Domain build passes
- No existing HU-01 through HU-04 domain concepts broken
- Team.AssignParticipant unit tests:
    * success path — membership created, event raised
    * inactive team throws TeamNotActiveException, no event raised
    * duplicate userId throws ParticipantAlreadyAssignedToTeamException, no event raised

Do not touch other backend layers.
```

Commit:

```
feat(identity-access): phase Y.1 — domain layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

---

## 6. Backend phase Y.2 — Application layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase Y.2 for HU-05 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- AssignParticipantToTeamCommand + handler:
    1. Load Team by id via ITeamRepository (with memberships collection — extend
       ITeamRepository with GetByIdWithMembershipsAsync if needed)
    2. Load User by id via IUserRepository
    3. If team not found, throw NotFoundException
    4. If user not found, throw NotFoundException
    5. If user.Role != Participant, throw UserNotParticipantRoleException
    6. Call team.AssignParticipant(user.Id)
    7. Persist and dispatch domain events
    Administrator-only — enforce via AuthorizationBehaviour or explicit actor-role check
- GetTeamParticipantsQuery + handler: load team memberships for a given teamId,
  project to TeamMembershipDto (TeamMembershipId, TeamId, UserId, AssignedAt);
  accessible to Administrator or Operator; return empty list if team has no members
- ITeamRepository extension (interface only, no implementation yet):
  GetByIdWithMembershipsAsync — loads Team including its Memberships collection
- Validators: AssignParticipantToTeamCommand — teamId and userId must not be empty;
  GetTeamParticipantsQuery — teamId must not be empty
- Handler unit tests for:
    AssignParticipantToTeamCommand:
      * success path — membership created, event captured
      * team not found — NotFoundException
      * user not found — NotFoundException
      * user.Role is Operator — UserNotParticipantRoleException
      * user.Role is Administrator — UserNotParticipantRoleException
      * inactive team — TeamNotActiveException propagated
      * duplicate assignment — ParticipantAlreadyAssignedToTeamException propagated
      * non-admin caller — ForbiddenAccessException
    GetTeamParticipantsQuery:
      * team with two members — returns both projections
      * team with no members — returns empty list
      * team not found — NotFoundException (if query treats missing team as 404)

Gate:
- Clean build passes
- Handler unit tests pass for all paths listed above

Do not touch Infrastructure, Api, or frontend.
Do NOT run dotnet ef migrations add.
```

Commit:

```
feat(identity-access): phase Y.2 — application layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

> **Migration sync point — Stop here.**
>
> Confirm HU-04's `AddTeams` migration is committed **and gated** (phase X.3 gate
> passing) on `feature/hu-04-team-registration`, then rebase this branch on top of
> that commit before continuing to phase Y.3. Verify with:
>
> ```bash
> dotnet ef migrations list \
>   --project backend/services/identity-access-service/src/Infrastructure \
>   --startup-project backend/services/identity-access-service/src/Api
> ```
>
> `AddTeams` must appear in the output. If it does not, stop and rebase first.

---

## 7. Backend phase Y.3 — Infrastructure layer

> **Migration gate:** Do not run `dotnet ef migrations add AddTeamMemberships` until you have
> confirmed that `AddTeams` appears in the EF migrations list and the database is up-to-date
> through that migration (see sync point above).

```
Use @backend/.agents/backend-agent.md.
Implement backend phase Y.3 for HU-05 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

MIGRATION GATE — before running dotnet ef migrations add, verify:
1. Run: dotnet ef migrations list --project <Infrastructure project path>
2. Confirm AddTeams appears in the output and is not pending
3. Only then proceed to: dotnet ef migrations add AddTeamMemberships

Scope:
- team_memberships table: id (uuid pk), team_id (uuid fk → teams.id), user_id (uuid fk → users.id),
  assigned_at (timestamptz not null)
- Unique constraint on (team_id, user_id) — enforced at DB level in addition to domain invariant
- EF Core entity configuration: map each column, configure the unique constraint,
  add the TeamMemberships DbSet to ApplicationDbContext or configure as owned collection on Team
- TeamRepository: implement GetByIdWithMembershipsAsync — loads Team including its
  Memberships collection via Include(); extend existing GetByIdAsync if it already exists
- AddTeamMemberships migration: run only after gate above; inspect generated file and
  confirm foreign keys and unique constraint appear; if not, add manually before committing
- Integration tests (each test must ExecuteDeleteAsync on TeamMemberships and Teams before
  its scenario to avoid cross-test pollution):
    * assign a participant to an active team, re-fetch with memberships, confirm membership
      persisted with correct TeamId and UserId, and ParticipantAssignedToTeamEvent raised
    * assign the same participant twice, confirm ParticipantAlreadyAssignedToTeamException thrown
      and only one membership row exists
    * assign a participant to an inactive team, confirm TeamNotActiveException thrown and no
      membership row created
    * load team participants query, confirm projections match persisted memberships

Database isolation: each integration test must ExecuteDeleteAsync on TeamMemberships first,
then Teams (FK order), then Users if needed. Follow the CapturingMediator / NoOpMediator
pattern from prior infrastructure tests.

Test infrastructure: wire DispatchDomainEventsInterceptor into DbContextOptions via the
BuildContext factory (optional IMediator? parameter) so domain events are dispatched during
SaveChangesAsync. Verify using statements cover Domain.Entities, Domain.Enums, Domain.Events,
Domain.Exceptions, MediatR, and Infrastructure.Persistence.Interceptors.

Gate:
- Migration gate (AddTeams listed) confirmed before AddTeamMemberships is generated
- dotnet build passes on the solution
- AddTeamMemberships migration file reviewed; FK and unique constraint confirmed
- Integration tests pass for all four paths above

Do not touch Api or frontend.
```

Commit:

```
feat(identity-access): phase Y.3 — infrastructure layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

---

## 8. Backend phase Y.4 — API layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase Y.4 for HU-05 in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- POST /api/teams/{id}/participants (Administrator-only):
  accepts { "userId": "<guid>" }, dispatches AssignParticipantToTeamCommand,
  returns 201 Created with the new membership id or 204 if no body is returned
- GET /api/teams/{id}/participants (Administrator or Operator):
  returns list of TeamMembershipDto for the given team; 404 if team not found

ProblemDetails error mapping (add to ProblemDetailsExceptionHandler if not already present):
- TeamNotActiveException → 409 Conflict (team is inactive, cannot accept assignments)
- ParticipantAlreadyAssignedToTeamException → 409 Conflict
- UserNotParticipantRoleException → 422 Unprocessable Entity
  (user exists but does not have the Participant role)

Endpoint tests:
- POST /api/teams/{id}/participants:
    * success (201 or 204)
    * team not found (404)
    * user not found (404)
    * user is not a Participant (422)
    * team is inactive (409)
    * duplicate assignment (409)
    * non-admin caller (403)
- GET /api/teams/{id}/participants:
    * success for admin — returns membership list
    * success for operator — returns membership list
    * participant caller — 403
    * team not found — 404
    * team with no participants — 200 with empty list

Coverage gate:
- Run the merged unit + integration + endpoint test suite
- Aggregate line coverage must reach ≥ 95%; if it does not, add targeted tests for
  any uncovered branch before closing the phase

Do not touch frontend.
```

Commit:

```
feat(identity-access): phase Y.4 — api layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

---

## 8.5 — Rebuild backend container images

The backend Docker images were built before the new endpoints existed. Rebuild
and restart so the live service reflects the membership endpoints:

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
# Assign a participant to a team
curl -s -X POST http://localhost:5002/api/teams/<team-id>/participants \
  -H "Content-Type: application/json" \
  -H "X-User-Id: admin-1" \
  -H "X-User-Role: Administrator" \
  -H "X-User-Email: admin@umbral.local" \
  -d '{"userId":"<participant-user-id>"}'

# List team participants
curl -s http://localhost:5002/api/teams/<team-id>/participants \
  -H "X-User-Id: admin-1" \
  -H "X-User-Role: Administrator" \
  -H "X-User-Email: admin@umbral.local"
```

**Gate:** the two membership endpoints return expected status codes with trusted headers; all existing endpoints from HU-01 through HU-04 remain functional.

---

## 9. Close out HU-05

```
Verify HU-05 end to end for DES-9 on feature/hu-05-participant-team-assignment.

Acceptance criteria:
- administrator can assign a Participant-role user to an active team
- the assignment is rejected for inactive teams (TeamNotActiveException → 409)
- the assignment is rejected for users without the Participant role (UserNotParticipantRoleException → 422)
- duplicate assignments are rejected (ParticipantAlreadyAssignedToTeamException → 409)
- administrators and operators can list participants assigned to a team
- deactivating a team preserves existing membership rows; new assignments to the deactivated team fail
- aggregate line coverage is ≥ 95% after the merged test suite runs

Then:
- open or update the draft PR targeting feature/hu-04-team-registration (not develop,
  because this branch is layered on top of HU-04)
- include DES-9 and DES-67 in the PR description
- note in the PR that this branch must be merged after HU-04 merges
- move HU-05 to Done only if all acceptance criteria pass
```

PR command:

```
gh pr create --draft --base feature/hu-04-team-registration --title "feat: participant-to-team assignment — HU-05"
```

---

---

## Design notes

| Guard | Why it matters |
|---|---|
| Boundary decision block repeated in every phase prompt | Prevents the agent from accidentally modelling session-runtime fields (`currentScore`, `joinStatus`) on the Identity `Team`; the boundary is subtle enough to be re-stated at each phase start rather than assumed to persist from orientation |
| Sync gate: HU-05 Y.1+Y.2 only after HU-04 X.2 | Y.2 extends `ITeamRepository` and invokes `Team.AssignParticipant`, both of which are defined in HU-04's X.1/X.2; starting Y.1+Y.2 earlier produces compilation errors in a shared-codebase parallel run |
| Migration sync stop at Y.2 | EF Core migration snapshots are cumulative; generating `AddTeamMemberships` before `AddTeams` exists in the snapshot causes the migration to re-emit the `teams` table definition — producing conflicts or duplicate-table errors at apply time |
| `TeamCodeExistsAsync` in `ITeamRepository` | The unique DB index is a backstop against concurrent races; the application-layer check provides a structured domain exception (`TeamCodeAlreadyExistsException`) with a machine-readable body instead of a raw 500 from a DB constraint violation |
| `UserNotParticipantRoleException` lives in the application handler, not the domain | `Team.AssignParticipant(userId)` receives an opaque `Guid` and cannot load `User` from a repository; the handler owns cross-aggregate validation to keep the domain layer free of repository dependencies |
| `DeleteBehavior.Restrict` on both FKs in `team_memberships` | Prevents cascading deletes from silently removing audit-critical membership history when a `User` or `Team` row is removed; the acceptance criterion requires that deactivating a team preserves membership rows |
| HU-05 branch roots on `feature/hu-04-team-registration`, not `develop` | `develop` does not contain the `Team` aggregate until HU-04 merges; branching from `develop` before that forces the HU-05 agent to re-implement or stub HU-04 artifacts, breaking the single-responsibility boundary between slices |
| Rebase protocol before Y.3 (not merge) | A rebase keeps the HU-05 commit history linear on top of HU-04, making the eventual PR to `develop` a clean fast-forward once HU-04 merges; a merge commit would introduce a divergent history that is harder to review and retarget |
| Coverage gate at X.4 and Y.4 (not just Y.4) | Starting below 95% (94.95% after HU-03), each phase risks landing below the threshold; checking at both API phases catches regressions introduced by HU-04 endpoints before HU-05 adds its own surface |
| `DELETE /api/teams/{id}/status` returns the updated record | The acceptance criterion requires the caller to observe `IsActive=false` after the call; returning the updated body avoids a mandatory follow-up GET in end-to-end tests and keeps the acceptance test deterministic |
