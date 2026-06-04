# HU-19 Context — Session Operator Assignment

> Paste this section into any agent session that needs context for HU-19.
> Last updated: 2026-06-03 | Branch: `feature/hu-19-session-operator-assignment`
>
> Boundary note: HU-19 is a `session-operations-service` slice with an
> `identity-access-service` dependency. `SessionOperations` owns the source of truth
> for assignment as `LiveSession.AssignedOperatorUserId`; `Identity` owns actor facts
> and coarse policy checks, but not runtime session state or the final admission /
> authorization decision for live-session actions.

## State

- `DES-26` (HU-19): status **Todo**; labels `Feature`, `ready-for-agent`,
  `svc:session-operations-service`
- Predecessors already landed on the same service label:
  - `DES-11` (HU-07A): **Done** - participant membership validation baseline
  - `DES-12` (HU-07B): **Done** - reconnect/runtime admission baseline
  - `DES-23` (HU-16): **Done** - trivia session creation baseline
- PRD ref: `DES-70` (PRD - Primera implementacion de
  `session-operations-service` (HU-15 a HU-36)); local file
  `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
- Blocks (downstream): `DES-27` (HU-20), `DES-28` (HU-21A), `DES-53` (HU-38)
- Branch: `feature/hu-19-session-operator-assignment`, base = **`develop`**
  (no same-service predecessor is currently `In Progress`)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Facade` (mandated) | X.2 Application | "Operator assignment is orchestration..." | A single application orchestration entry point (`AssignOperatorToSession`) coordinates session lookup, target-actor validation through an Identity-facing port, assignment mutation on `LiveSession`, persistence, and audit/event publication. The endpoint stays thin and does not scatter these steps across transport code. |
| `Proxy` (mandated) | X.2 Application + X.4 API | "...plus guarded access to protected session actions." | Assignment-aware authorization must be structural: an authorization proxy / policy seam guards protected operator/admin session actions with no ad-hoc role or ownership `if` checks in handlers or endpoints. The slice must leave a reusable guard for downstream session-administration actions. |

Transport note: HU-19 has **no** SignalR / RabbitMQ obligation. This is a
synchronous REST slice that sets runtime session ownership and the authorization
seam later session actions build on.

## What predecessors have already landed (reuse candidates)

All of this is on `develop`.

**Domain layer**
- `LiveSession` is already the session-runtime aggregate root for
  `SessionOperations`
- `LiveSession.AssignedOperatorUserId` already exists as nullable session-owned
  state and is explicitly part of the canonical model
- `LiveSession` already governs runtime concerns such as `SessionState`,
  `SessionSource`, team registration, participant admission, and trivia snapshot
- `JoinPolicy` already expresses session-owned runtime admission rules; this is
  the existing precedent for "Identity provides facts, SessionOperations decides"

**Application layer**
- Existing MediatR + pipeline baseline is in place, including
  `AuthorizationBehaviour`, `ValidationBehaviour`, `ICurrentUser`, and
  `[Authorize]`-driven request metadata
- HU-07B already introduced a structural authorization seam in
  `ReconnectAuthenticatedParticipantAuthorizationProxy`
- HU-16 already introduced a mandated `Facade` in
  `CreateTriviaSessionFacade`, which is the direct orchestration precedent for
  HU-19

**Infrastructure / API**
- `AssignedOperatorUserId` is already mapped in EF Core
  (`live_sessions.assigned_operator_user_id`) and present in the current model
  snapshot, so the assignment column itself is already persisted
- `SessionsEndpoints` and `/api/sessions` already exist for trivia-session
  creation and reconnect paths
- Cross-service client precedent to `identity-access-service` already exists in
  `ParticipantMembershipAccessClient`; HU-19 can mirror that pattern for narrow
  operator-actor fact validation if needed

**Frontend**
- No confirmed operator-assignment UI is documented as landed in predecessor
  context files; treat HU-19 as the first admin-facing session-operator
  assignment surface

**Coverage**
- ADR-0005 gate still applies. The exact post-HU-16 aggregate percentage is not
  recorded in the predecessor context files, so X.4 must measure and report the
  current merged coverage instead of assuming a carried-forward number.

## What this HU adds

| Concern | New work |
|---|---|
| Session-owned assignment behavior | Formalize assignment on `LiveSession` as domain behavior instead of a bare persisted property: assign/change the responsible operator while preserving session ownership of the source of truth. |
| Auditability | Record operator-assignment changes for audit via session-owned domain events and/or session history records carrying previous/new operator identifiers and actor context. |
| Application orchestration | `AssignOperatorToSession` use case as the mandated `Facade`: load session, validate the target actor through an Identity-facing access-facts port, mutate assignment, persist, and emit audit/session events. |
| Authorization seam | Realize the mandated `Proxy` by introducing an assignment-aware authorization guard for protected session administration, rather than inline role/ownership checks. |
| API surface | Admin-facing session endpoint to assign or change the responsible operator for a `LiveSession`, and an assignment state response that exposes the session's assigned operator as session state. |
| Frontend | Admin session-management flow to assign/change the responsible operator and display the current responsible operator using the verified backend contract. |

## Touched surfaces

- `backend/services/session-operations-service/` - domain, application,
  infrastructure, and API layers
- `backend/services/identity-access-service/` - supporting actor-facts / policy
  contract only if the existing surface is insufficient; never as the source of
  truth for session assignment
- `frontend/` admin session-management UI
- API contract boundary: session-operator assignment command/response plus any
  supporting Identity-facing internal validation contract

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **Do not duplicate ownership in Identity.** `AssignedOperatorUserId` already
  exists on `LiveSession` and is the canonical source of truth. Any
  Identity-side representation must be explicitly derived/indexed data, not a
  second owner.
- **The persistence column is already there.** HU-19 should start by verifying
  whether the existing `AssignedOperatorUserId` mapping is sufficient before
  adding any migration. The likely work is domain behavior + orchestration +
  API/tests, not a new schema column.
- **Identity validates actors; SessionOperations owns runtime decisions.**
  Identity can answer "is this target user a valid operator/admin actor?" or
  provide policy facts. It should not own "who is assigned to this session?" and
  should not become the final authority on session administration.
- **`Proxy` must be structural.** Do not satisfy the pattern by adding role or
  operator-ownership `if` checks inside handlers/endpoints. Follow the existing
  authorization-proxy precedent from HU-07B and make the guard reusable by later
  session-administration slices (`HU-20`, `HU-21A`).
- **HU-20 depends on this slice's session state.** Session list/read models for
  "my assigned sessions" are HU-20 scope. HU-19 should expose assignment as part
  of session state, but it should not expand into the full assigned-sessions read
  projection.
- **State-window ambiguity should be recorded, not guessed.** The PRD and
  acceptance criteria do not explicitly say whether reassignment is allowed only
  before start or also during `Preparing` / `Active` / `Paused`. If the canon
  docs do not resolve that, note the ambiguity in the implementation prompt
  rationale rather than silently inventing a restriction.
