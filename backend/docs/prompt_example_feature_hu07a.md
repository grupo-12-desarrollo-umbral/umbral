# Prompt Example — HU-07A Participant Membership Validation in Session (Feature Slice)

Concrete prompt sequence for driving HU-07A through a full feature slice on `feature/hu-07a-participant-membership-validation`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

> Supersession note (2026-06-02): this prompt example captures the original X.1–X.4
> JoinToken + membership-validation baseline. Phase 0 later expanded HU-07A on the same
> branch with the participant team lobby, conditional self-select, and
> `GET /api/sessions/{id}/teams`. When driving new work, read
> `mobile/docs/plan-participant-session-team-lobby.md` and
> `backend/docs/prd/DES-69-participant-membership-validation.md` first, and treat any
> "own team only" wording below as historical unless it is explicitly describing the
> already-landed X.1–X.4 baseline.

**Key difference from HU-06:** HU-06 added the participant login path (mobile) on the shared authentication backbone, guarded so only `Participant` may use it. HU-07A goes one step further into authorization: it introduces a **new Identity-owned `JoinToken` entity** and the `ValidateParticipantMembershipAccess` use case that checks an authenticated participant belongs to the target team, returning access facts that gate (but do not perform) real-time admission. This is the first HU in the service to add a *new persisted aggregate/entity since HU-05*, so unlike HU-03 (which had no migration) HU-07A is a genuine four-phase slice: Domain → Application → Infrastructure (new `join_tokens` table) → API.

Drive each backend phase with `@backend/.agents/driver-agent.md`, selecting phases in order: **X.1 → X.2 → X.3 → X.4**. The driver delegates implementation to `@backend/.agents/backend-agent.md`; do not invoke it directly. For the frontend slice, use Step 9 directly with `@frontend/AGENTS.md`. Do not mix backend and frontend work in the same phase; the driver coordinates them as separate scoped steps tied together by the verified API contract.

---

## Required design patterns

- `Proxy`
  - Why: membership validation also gates admission to the team's real-time hub/group.
  - Phase owner: X.2 Application **and** X.4 API
  - Gate obligation: access is enforced through a guard — `AuthorizationBehaviour` / an authorization proxy / endpoint authorization policy — with **no ad-hoc role `if` checks** in handlers or endpoints. In the expanded branch scope, the conditional-self-select / locked-team rules are likewise enforced through the guard, not inline conditionals.

Transport note (NOT a pattern, NOT a gate): HU-07A is tagged SignalR (DES-11) in the matrix, but `identity-access-service` does **not** implement a SignalR hub. It produces the `JoinToken` + membership access fact that `session-operations-service` and the mobile client consume to admit a participant to the real-time group. Do not scope a hub here.

---

## Pre-resolved orient (as of 2026-06-02)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase — no need to re-run the orient prompt.

### What HU-01 through HU-06 have already landed (reuse candidates for HU-07A)

All of this lives on `develop`.

**Domain layer**
- `User` aggregate (`ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; `Deactivate()`, `AssignRole()`)
- `Role` enum (`Administrator`, `Operator`, `Participant`); `ProtectedCapability` matrix incl. `ParticipantExperience`
- `AccessPolicy` domain service → `AccessDecision`; `IdentityProvisioningPolicy`; `IdentityProviderSession`
- Identity reference-data `Team` (HU-04) + `TeamMembership` record (HU-05: `TeamMembershipId`, `TeamId`, `UserId`, `AssignedAt`) — **the membership fact HU-07A consumes**
- Domain exceptions incl. `UserNotParticipantRoleException`, `TeamNotActiveException`

**Application layer**
- Auth/role/user flows; team admin + `AssignParticipantToTeamCommand`, `GetTeamParticipantsQuery`
- `UserRoleAssignmentAuthorizationProxy` (Application Proxy precedent), `AuthorizationBehaviour`, `ValidationBehaviour`, `ICurrentUser`, `GatewayRoleParser`, `[Authorize]`
- `IUserRepository`, `ITeamRepository`

**Infrastructure / API**
- Tables `users`, `identity_provider_sessions`, `teams`, `team_memberships`; migrations `Init`, `AddTeams`, `AddTeamMemberships`
- `UserRepository`, `TeamRepository`; `AuthenticatedUserLoginProxy` (Api Proxy precedent, HU-06)
- Endpoints `/api/users/*`, `/api/permissions/authenticated-platform-access`, `/api/teams/*` incl. `/api/teams/{id}/participants`
- `ProblemDetailsExceptionHandler` RFC-7807 mapping

**Coverage:** merged line coverage historically ~94.95% against the ADR-0005 gate (`backend/scripts/cover-gate.sh`).

### What HU-07A adds on top (per PRD DES-67 §73-90, §217-228)

| Concern | New work |
| --- | --- |
| `JoinToken` entity (Identity-owned) | `JoinTokenId`, `LiveSessionId`, `TeamId`, `TokenHash`, `IssuedAt`, `ExpiresAt`, `ConsumedAt`, `IssuedByUserId`, `Status`. `LiveSessionId`/`TeamId` are **loose reference ids, no FK** — `TeamId` = Identity reference-data Team id, `LiveSessionId` = opaque correlation id. |
| `JoinTokenPolicy` domain service | Issuance, expiration, consumption, replay constraints. |
| Domain events + `Consume()` | `JoinTokenIssued`, `JoinTokenConsumed`. `Consume()` defined + tested but **not endpoint-wired in HU-07A**. |
| `IssueJoinToken` command | Mint a token for `(liveSessionId, teamId)`; Admin/Operator-only; emits `JoinTokenIssued`. |
| `ValidateParticipantMembershipAccess` query | Read-only / non-consuming. For the authenticated participant only: active + `Participant`, active `TeamMembership` for `teamId`, valid `JoinToken` when supplied; returns an `AccessDecision`. |
| `IJoinTokenRepository` + persistence | New `join_tokens` table + `AddJoinTokens` migration. |
| API | `POST /api/join-tokens` (Admin/Operator → 201); `POST /api/permissions/participant-membership-access` (Participant → 200). |

### Branch state and prerequisite

`feature/hu-07a-participant-membership-validation` branches from **`develop`** (HU-06 is merged). Before X.3, confirm the branch sees the `AddTeamMemberships` migration and that `TeamMembership` + `TeamRepository` membership loading are present.

### Linear state (as of 2026-06-02)

- DES-11 (HU-07A): **verify status and labels via Linear MCP** before step 2 (no Linear MCP was available at generation time; `ready-for-agent` + `svc:identity-access-service` are assumed but unconfirmed).
- DES-12 (HU-06, predecessor): **Done** (merged via PR #13).
- DES-67 (PRD): labels `ready-for-agent`, `svc:identity-access-service`.

> Linear live state may have changed. Use the Linear MCP to verify DES-11 status and labels, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md`.

---

## 1. Orient — read service state and PRD before planning

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the README or Linear state may have changed since 2026-06-02.

```text
Read the following files and summarise what has been decided and implemented so far:
- @backend/services/identity-access-service/README.md — current implementation status
- @backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md
  — the PRD scope for all HU-01 to HU-08 slices
- @backend/docs/bd_umbral_entity_spec.md (JoinToken section) and
  @backend/docs/ddd_solution_model.md (Identity: JoinTokenPolicy, IJoinTokenRepository,
  IssueJoinToken, ValidateParticipantMembershipAccess)

Then use the Linear MCP to fetch only the current live state of:
- DES-11 (HU-07A — Validación de membresía del participante en sesión) — status and labels
- DES-12 (HU-06 — the predecessor slice) — status

Output:
- what HU-01..HU-06 already landed (User, AccessPolicy, reference-data Team, TeamMembership)
  — reuse candidates for HU-07A
- what HU-07A adds per the PRD + DDD model: JoinToken entity, JoinTokenPolicy,
  IssueJoinToken, ValidateParticipantMembershipAccess
- current Linear status and labels for DES-11

Do not start planning or implementing yet.
```

---

## 2. Label DES-11 as ready-for-agent

> DES-11 is assumed to carry `ready-for-agent` as of 2026-06-02. Use this step to confirm the label is present before execution.

```text
Use the Linear MCP to confirm (or add) the label ready-for-agent on DES-11.
Confirm the label was applied and output the updated ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-11 now carries both svc:identity-access-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md —
do not re-fetch the PRD from Linear; read the local file if you need implementation decisions.

Output the confirmed HU id, title, acceptance criteria, and labels before planning the slice.
```

In the remaining examples below, `HU-07A` and `DES-11` are the resolved values for this slice. `DES-67` is the shared PRD reference for identity-access-service; its content lives in the local file above.

---

## 4. Start the slice

```text
Prepare the participant membership validation slice on branch
feature/hu-07a-participant-membership-validation.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects frontend (mobile-facing) and identity-access-service.

The pre-resolved orient at the top of this document lists what HU-01..HU-06 have
already landed and what HU-07A adds. Do not re-read the README or PRD for scoping.

Before implementation, confirm the current service source already has the reference-data
Team aggregate and TeamMembership record from HU-04/HU-05. Extend that baseline;
do not recreate TeamMembership or the existing AccessPolicy.

Move DES-11 to In Progress and output the exact scope, branch name, and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

> Run `@backend/.agents/driver-agent.md` and select **X.1** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-07A in identity-access-service.
Use the service PRD (DES-67) and canonical docs (bd_umbral_entity_spec.md JoinToken section;
ddd_solution_model.md Identity policies).

Before writing anything, inspect the current identity-access-service domain baseline and
confirm which concepts already exist (User, Role, AccessPolicy, reference-data Team,
TeamMembership). Extend rather than recreate; do not duplicate TeamMembership.

Scope:
- new JoinToken entity in the Identity domain: JoinTokenId, LiveSessionId, TeamId, TokenHash,
  IssuedAt, ExpiresAt, ConsumedAt, IssuedByUserId, Status (Active/Consumed/Expired/Revoked).
  LiveSessionId and TeamId are loose reference ids (Guid) — NO navigation properties or
  foreign keys to runtime LiveSession/Team aggregates. Semantics to honour:
    * TeamId is the Identity reference-data Team id (the same id TeamMembership keys on) — it is
      what the membership check resolves against.
    * LiveSessionId is an opaque correlation id — Identity stores and echoes it but never
      validates it against local data.
- factory/behaviour: Issue (creates Active token, raises JoinTokenIssued), Consume (sets
  ConsumedAt + Status=Consumed, raises JoinTokenConsumed). A consumed/expired/revoked token
  cannot be consumed again — throw a domain exception. Consume() is part of the model and must
  be unit-tested, but NO HU-07A endpoint or handler triggers it — validation is read-only.
- JoinTokenPolicy domain service: validates issuance, expiration (against a supplied clock/now),
  consumption, and replay constraints. Keep this logic in the domain, not in handlers.
- confirm AccessPolicy already covers ParticipantExperience for the membership check; extend the
  matrix only if a capability is missing — do not add ad-hoc role logic.
- no changes to User/Team/TeamMembership shape are expected.

Gate:
- Domain build passes
- No existing HU-01..HU-06 domain concepts broken
- New invariants are expressed as unit tests on JoinToken and JoinTokenPolicy
  (issue, expire, consume-once, replay rejected)

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(identity-access): phase X.1 — domain layer (HU-07A)

Ref: HU-07A
Ref: DES-11
Ref: DES-67
```

---

## 6. Backend phase X.2 — Application layer

> Run `@backend/.agents/driver-agent.md` and select **X.2** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-07A in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Before writing anything, inspect the existing Application baseline and mirror its
conventions for commands, queries, validators, DTOs, handlers, and permissions.

Scope:
- IssueJoinTokenCommand + handler: mint a JoinToken for (liveSessionId, teamId), persist,
  dispatch JoinTokenIssued. Administrator or Operator only.
- ValidateParticipantMembershipAccessQuery + handler: for the AUTHENTICATED PARTICIPANT only,
  (a) confirm the actor is active and Role.Participant via AccessPolicy/ParticipantExperience,
  (b) confirm an active TeamMembership exists for the target teamId,
  (c) when a token is supplied, validate it via JoinTokenPolicy (active, not expired/consumed,
      matches the session+team) — read-only, do NOT call Consume(),
  and return an AccessDecision (capability=ParticipantExperience, isAllowed, reason, liveSessionId, teamId).
  TeamId is matched against the participant's TeamMembership; liveSessionId is echoed, not validated.
- validators: required ids present; teamId/liveSessionId well-formed.
- Proxy obligation (mandated): the authorization rule for whichever team context the
  participant is allowed to enter must be enforced through a guard — AuthorizationBehaviour, an
  authorization proxy in the style of UserRoleAssignmentAuthorizationProxy, or an endpoint policy —
  NOT an ad-hoc role/ownership `if` inside the handler.
- handler unit tests: successful issue (admin/operator), issue rejected for non-admin/operator,
  successful membership validation, validation rejected for non-participant, validation rejected
  when no membership, validation rejected for a participant targeting a team they do not belong to
  (own-context guard), validation rejected for expired/consumed token.

Gate:
- clean build passes
- handler + validator unit tests pass for all paths above
- Proxy gate: access is enforced through a guard (AuthorizationBehaviour / authorization proxy /
  endpoint policy) with no ad-hoc role or own-team `if` checks leaking into the handlers

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(identity-access): phase X.2 — application layer (HU-07A)

Ref: HU-07A
Ref: DES-11
Ref: DES-67
```

---

## 7. Backend phase X.3 — Infrastructure layer

> Run `@backend/.agents/driver-agent.md` and select **X.3** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-07A in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Before writing anything, inspect the current persistence baseline:
- ApplicationDbContext
- existing entity configurations and repositories from HU-04/HU-05
- current migration snapshot

Scope:
- IJoinTokenRepository contract + EF implementation: AddAsync, GetByIdAsync, and a lookup by
  token hash and/or session+team pair sufficient for validation and consumption.
- EF configuration for JoinToken mapping to a new join_tokens table. LiveSessionId and TeamId
  are plain columns (loose reference ids), NOT foreign keys. Map Status as an enum/string.
- review BaseAuditableEntity: if JoinToken does not need created_by/updated_by audit columns,
  Ignore(...) them in the configuration so they do not leak into the migration.
- generate the AddJoinTokens migration:
  MSBUILDDISABLENODEREUSE=1 dotnet ef migrations add AddJoinTokens
    --startup-project src/Api --project src/Infrastructure
  Inspect the generated migration for unwanted audit columns before it is committed.
- integration tests (real PostgreSQL via Testcontainers): persist + read back a JoinToken;
  issue then consume (confirm Status/ConsumedAt persisted and JoinTokenConsumed dispatched);
  replay attempt on a consumed token rejected. Validate a participant's membership end-to-end
  through the repository + TeamMembership.

Database isolation: clean shared state before each scenario (ExecuteDeleteAsync on join_tokens,
team_memberships as needed) to avoid cross-test pollution.

Test infrastructure: the test context factory must wire DispatchDomainEventsInterceptor into
DbContextOptions (optional IMediator? param) so JoinTokenIssued/JoinTokenConsumed are dispatched
during SaveChangesAsync; use a CapturingMediator for event assertions and a NoOpMediator otherwise.
Verify using statements cover Domain.Entities, Domain.Enums, Domain.Events, Domain.Exceptions,
MediatR, and Infrastructure.Persistence.Interceptors.

Gate:
- dotnet build passes on the solution
- AddJoinTokens migration created and reviewed (no leaked audit columns)
- repository integration tests pass for all paths above

Do not touch Api or frontend.
```

Commit:

```text
feat(identity-access): phase X.3 — infrastructure layer (HU-07A)

Ref: HU-07A
Ref: DES-11
Ref: DES-67
```

---

## 8. Backend phase X.4 — API layer

> Run `@backend/.agents/driver-agent.md` and select **X.4** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-07A in identity-access-service.
Use the service PRD (DES-67) and canonical docs.

Scope:
- POST /api/join-tokens (Administrator or Operator): body { "liveSessionId": "...", "teamId": "...",
  "expiresInSeconds": 0 (optional) }, dispatches IssueJoinTokenCommand, returns 201 with
  { "joinTokenId": "...", "token": "...", "liveSessionId": "...", "teamId": "...", "expiresAt": "..." }.
  The domain persists only TokenHash; the plaintext token is returned once here and never stored.
- POST /api/permissions/participant-membership-access (Participant): body { "liveSessionId": "...",
  "teamId": "...", "token": "..." (optional) }, dispatches ValidateParticipantMembershipAccessQuery,
  returns 200 with { "capability": "ParticipantExperience", "isAllowed": true, "reason": "...",
  "liveSessionId": "...", "teamId": "..." }. Reuse the existing AccessDecision DTO shape (cf.
  GET /api/permissions/authenticated-platform-access). This endpoint is read-only — it does not
  consume the token.
- proof that a non-admin/operator caller of POST /api/join-tokens receives 403.
- proof that a non-participant caller of POST /api/permissions/participant-membership-access receives 403.
- proof that a participant validating a team they do not belong to receives a denied access fact
  (or 403) — the own-team-context guard.
- map any new domain exceptions (expired/consumed token, etc.) in ProblemDetailsExceptionHandler.

Gate:
- endpoint tests pass for: issue token (201), non-admin issue rejected (403),
  membership validate (200), non-participant validate rejected (403), foreign-team validate denied
- Proxy gate: endpoint authorization is enforced via policy/[Authorize] + the guard from X.2 —
  no ad-hoc role or own-team `if` checks in the endpoints
- no regression on HU-01..HU-06 endpoints
- the ADR-0005 coverage gate (backend/scripts/cover-gate.sh, passing every test project) is green

Do not touch frontend.
```

Commit:

```text
feat(identity-access): phase X.4 — api layer (HU-07A)

Ref: HU-07A
Ref: DES-11
Ref: DES-67
```

---

## 8.5 — Docker rebuild and smoke

```text
Rebuild and restart the local backend stack for identity-access-service verification:

1. Run `docker compose build identity-access-service api-gateway` from the backend/ directory.
2. Run `docker compose up -d identity-access-service api-gateway`.
3. Wait for the services to become healthy.
4. Smoke test — issue a join token as Admin, then validate membership as a Participant
   who has a seeded TeamMembership for that team:

   Issue token (Admin):
   curl -s -X POST http://localhost:5002/api/join-tokens \
     -H "Content-Type: application/json" \
     -H "X-User-Id: admin-1" -H "X-User-Role: Administrator" -H "X-User-Email: admin@umbral.local" \
     -d '{"liveSessionId":"00000000-0000-0000-0000-000000000001","teamId":"<existing-team-id>"}'

   Validate membership (Participant with seeded TeamMembership):
   curl -s -X POST http://localhost:5002/api/permissions/participant-membership-access \
     -H "Content-Type: application/json" \
     -H "X-User-Id: participant-1" -H "X-User-Role: Participant" -H "X-User-Email: participant@umbral.local" \
     -d '{"liveSessionId":"00000000-0000-0000-0000-000000000001","teamId":"<existing-team-id>","token":"<token-from-issue>"}'

Output: build result, container status, and curl response summary.
```

**Gate:** `POST /api/join-tokens` returns 201 with trusted Admin headers; `POST /api/permissions/participant-membership-access` returns 200 with an `isAllowed` access fact for a participant who belongs to the team, and a denied fact (or 403) for a participant who does not; existing endpoints remain functional.

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in
`@frontend/plans/hu-03-frontend-role-permission-assignment.md`, save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend (mobile-facing) part of HU-07A.
Use the verified backend contract.

Scope:
- participant membership-validation flow: after participant login, call
  POST /api/permissions/participant-membership-access for the target session/team and only
  reveal the team context when the access fact isAllowed is true
- block access to another team's context: a participant who is not a member (denied access fact or 403)
  must see a clear "not authorized for this team" state — never a silent partial render of team context
- surface the join-token entry path where the product flow provides a token (validate with the token)
- do NOT implement the SignalR/real-time hub connection, reconnection, or multi-device sync here —
  those belong to session-operations + a later slice (HU-07B/HU-08). This slice consumes the access
  fact only.
- do not regress HU-06 participant login or HU-01..HU-05 web flows

Gate:
- frontend uses the verified backend contract (POST /api/permissions/participant-membership-access),
  not guessed payloads
- a participant who is allowed to enter the selected team context can validate and proceed
- a participant who does not belong is blocked even on direct URL access (redirect or clear error)
- HU-06 participant login is not regressed
```

Commit:

```text
feat(frontend): participant membership validation in session — HU-07A

Ref: HU-07A
Ref: DES-11
Ref: DES-67
```

---

## 10. Close-out

```text
Before closing HU-07A, verify every acceptance criterion against the implemented backend and frontend:
- the system validates that the authenticated participant is authorized for the requested team context
- the participant can only access a team context that the current branch rules mark as allowed

Then prepare the PR:

gh pr create --draft --base develop --title "feat: participant membership validation in session — HU-07A" \
  --body "Closes DES-11
Ref: DES-67

Touched: backend/services/identity-access-service/, frontend/"

Move HU-07A (DES-11) to Done only if all acceptance criteria pass end-to-end.
```

---

## Rationale for changes relative to HU-06

**A new persisted entity makes this a full four-phase slice.** HU-03 had no migration because `Role` was already a column. HU-07A introduces `JoinToken` — owned by Identity per `bd_umbral_entity_spec.md` and `ddd_solution_model.md` — with its own `JoinTokenPolicy`, `IJoinTokenRepository`, events (`JoinTokenIssued`/`JoinTokenConsumed`), and a `join_tokens` table. Domain → Application → Infrastructure → API are all genuinely exercised.

**JoinToken references are loose ids, not foreign keys — and the two are not symmetric.** `LiveSessionId` and `TeamId` point at `session-operations-service` targets that Identity does not own, so modeling them as FKs would collapse the bounded-context boundary. The asymmetry matters: `TeamId` is set to the **Identity reference-data Team id** (HU-04) because that is the only team identity Identity can check `TeamMembership` against — the runtime `Team` in session-ops is assumed to carry this same id as its origin (this cross-context correlation is an assumption; if session-ops mints unrelated runtime team ids, the contract needs an explicit mapping and this should be revisited). `LiveSessionId` is treated as a fully opaque correlation id that Identity stores and echoes but never validates. This decision is recorded in ADR-0007.

**Identity validates, it does not admit — and validation is non-consuming.** Per the PRD boundary rules, `ValidateParticipantMembershipAccess` returns *access facts* only; `session-operations-service` makes the final admission decision and owns the SignalR hub. Validation therefore does **not** call `Consume()` — burning a single-use token at the check would break the real join and HU-07B reconnection. `Consume()`/`JoinTokenConsumed` are kept in the domain (the model defines them) and tested, but wired to no HU-07A endpoint, mirroring how HU-01 declared `UserRoleAssigned` before HU-03 consumed it. This decision is recorded in ADR-0007.

**Endpoint shapes follow the service's existing conventions.** Issuance is resource creation, so `POST /api/join-tokens` → 201 returning the plaintext `token` once (the domain persists only `TokenHash`). Membership validation is an access-policy evaluation returning an `AccessDecision`, so it lives in the `/api/permissions/...` family as `POST /api/permissions/participant-membership-access` (POST, not GET, because the optional `token` must travel in the body, not a logged query string) — consistent with the existing `GET /api/permissions/authenticated-platform-access`, rather than an RPC-style `/membership/validate`.

**Proxy is the mandated pattern, and criterion 2 is its obligation.** The participant's allowed team-context rule must be enforced through a guard (AuthorizationBehaviour / authorization proxy / endpoint policy), consistent with `UserRoleAssignmentAuthorizationProxy` (Application) and `AuthenticatedUserLoginProxy` (Api). A green build with that ownership guard expressed as an inline `if` is a gate failure, not a pass.

**Three DES refs per commit.** HU-07A commits reference HU-07A, DES-11 (HU ticket), and DES-67 (PRD), matching the established pattern.
