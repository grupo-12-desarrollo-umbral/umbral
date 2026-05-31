# Prompt Example — HU-05 Participant-to-Team Assignment (Feature Slice)

Concrete prompt sequence for driving HU-05 through a full feature slice on `feature/hu-05-participant-team-assignment`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference from HU-04:** HU-04 introduced the `Team` aggregate as a team registry. HU-05 adds membership records that bind a `Participant`-role user to a team. This slice bridges two existing aggregates (`Team` from HU-04 and `User` from HU-01) without claiming runtime session authority — that belongs to HU-07 and `session-operations-service`. The frontend extends the HU-04 team detail view with a participants section and assign action.

**Critical branching constraint:** `feature/hu-05-participant-team-assignment` must branch from
`feature/hu-04-team-registration`, not from `develop`. HU-05 depends on HU-04's `Team`
aggregate, `ITeamRepository`, and `AddTeams` migration. Do not start the infrastructure
phase until HU-04's `AddTeams` migration is committed and gated on the source branch.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps tied together by the
verified API contract.

---

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
> Linear live state may have changed. Use the Linear MCP to verify DES-9 status if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md` instead.

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
This slice affects backend identity-access-service and frontend.

The pre-resolved orient at the top of this document lists what HU-01 through HU-04 have
already landed and what HU-05 adds. Do not re-read the README or PRD for scoping.

Confirm that the branch is rooted on feature/hu-04-team-registration (or a commit
that includes the AddTeams migration) before any implementation begins.

Move the resolved HU ticket to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-05 in identity-access-service.
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
feat(identity-access): phase X.1 — domain layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

Then run: `/debrief`

---

## 6. Backend phase X.2 — Application layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-05 in identity-access-service.
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
```

Commit:

```
feat(identity-access): phase X.2 — application layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

Then run: `/debrief`

---

## 7. Backend phase X.3 — Infrastructure layer

> **Migration gate:** Do not run `dotnet ef migrations add AddTeamMemberships` until you have
> confirmed that `AddTeams` appears in the EF migrations list and the database is up-to-date
> through that migration. Verify with:
> `dotnet ef migrations list --project backend/services/identity-access-service/Infrastructure`
> before generating the new migration.

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-05 in identity-access-service.
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
feat(identity-access): phase X.3 — infrastructure layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

Then run: `/debrief`

---

## 8. Backend phase X.4 — API layer

```
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-05 in identity-access-service.
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
feat(identity-access): phase X.4 — api layer (HU-05)

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

Then run: `/debrief`

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

## 9. Frontend slice

```
Use @frontend/AGENTS.md.
Implement the frontend part of HU-05.
Use the verified backend contract.

Scope:
- team detail view (enhanced): extend the team detail page from HU-04 to show
  a "Participants" section listing all assigned members via GET /api/teams/{id}/participants;
  display each participant's UserId and AssignedAt; if the backend later returns
  user display info, show that instead of a raw id
- assign participant action: an admin can assign a Participant-role user to the team
  via POST /api/teams/{id}/participants with a userId; provide a user selector
  (dropdown or searchable field) that fetches from GET /api/users filtered to
  Participant-role users; validate selection before submission
- error handling for assignment failures: show user-friendly messages for
  TeamNotActiveException → "This team is inactive and cannot accept new members",
  UserNotParticipantRoleException → "The selected user does not have the Participant role",
  ParticipantAlreadyAssignedToTeamException → "This user is already assigned to the team"
- optimistic UI update: after a successful assignment, add the new participant to the
  visible list without a full page reload; handle 409 and 422 responses gracefully
- participant-only route view: the team detail participants section is read-only
  for Operators (they see the list but no assign action) and hidden entirely for
  Participants (they cannot access the participants section at all)
- navigation and route guards: the participants section is accessible only to
  Administrators and Operators; direct URL access by a Participant must redirect
  or show an error — never silent partial rendering of participant data
- keep one shared app, not separate admin/operator apps
- do not regress HU-01 through HU-04 flows (login, deactivation, user list,
  role assignment, team CRUD)

Gate:
- admin can view the participant list and assign a user to a team from the UI
- operator can view the participant list but cannot assign users
- participant cannot access the participants section and is redirected or shown an error
- assignment errors (inactive team, wrong role, duplicate) are shown as user-friendly messages
- existing HU-01 through HU-04 frontend flows are not regressed
```

Commit:

```
feat(frontend): participant-to-team assignment — HU-05

Ref: HU-05
Ref: DES-9
Ref: DES-67
```

Then run: `/debrief`

---

## 10. Close out

```
Verify HU-05 end to end for DES-9 on feature/hu-05-participant-team-assignment.

Acceptance criteria:
- administrator can assign a Participant-role user to an active team
- the assignment is rejected for inactive teams (TeamNotActiveException → 409)
- the assignment is rejected for users without the Participant role (UserNotParticipantRoleException → 422)
- duplicate assignments are rejected (ParticipantAlreadyAssignedToTeamException → 409)
- administrators and operators can list participants assigned to a team
- deactivating a team preserves existing membership rows; new assignments to the deactivated team fail
- frontend: admin can view and assign participants from the team detail view
- frontend: operator sees a read-only participant list with no assign action
- frontend: participant is blocked from the participants section entirely
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

## Rationale for changes relative to HU-04

**Branch base is feature/hu-04-team-registration, not develop.** HU-05 builds on the `Team` aggregate, `ITeamRepository`, and `AddTeams` migration introduced by HU-04. Those artifacts do not exist on `develop` until HU-04 merges. Branching from `develop` early would require cherry-picking or re-implementing HU-04 concepts, which is error-prone and violates the slice ordering.

**Migration gate is a hard prerequisite, not a suggestion.** EF Core migrations reference a model snapshot. If `AddTeamMemberships` is generated before `AddTeams` is in the snapshot, the generated migration will include the `teams` table definition a second time — producing conflicts or invalid SQL at apply time. The gate (verify `AddTeams` appears in `dotnet ef migrations list`) must happen before `dotnet ef migrations add AddTeamMemberships` in the same session.

**Role check belongs in the application handler, not the domain.** `Team.AssignParticipant(userId)` receives an opaque `Guid` — it cannot load a `User` from the repository. The handler is responsible for loading the user and asserting `user.Role == Participant` before calling the domain method. Throwing `UserNotParticipantRoleException` from the handler keeps domain logic free of cross-aggregate dependencies.

**TeamMembership is an authorization fact, not a runtime seat.** HU-05 records who is assigned to which team. It does not grant live access, queue a join, or interact with `session-operations-service`. That runtime layer belongs to HU-07. Avoid overstating current authorization guarantees in error messages or documentation.

**PR targets feature/hu-04-team-registration.** Because HU-05 layers on top of HU-04, the PR must target `feature/hu-04-team-registration` (or whatever branch HU-04 landed on). After HU-04 merges to `develop`, the HU-05 PR can be retargeted to `develop`.

**Three DES refs per commit.** HU-05 commits reference DES-9 (HU ticket) and DES-67 (PRD), matching the pattern used in HU-01 through HU-04.
