# Prompt Example — HU-21A Valid Session State Transitions (Feature Slice)

Concrete prompt sequence for driving HU-21A through a full feature slice on
`feature/hu-21-valid-session-transactions`. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md). Context:
[hu21a-context.md](./hu21a-context.md).

**Key difference from HU-19:** HU-19 assigned session ownership (a synchronous
REST slice with `Facade` + `Proxy`). HU-21A introduces the session lifecycle
state machine: it replaces the ad-hoc `SessionState` enum with an explicit
`State` abstraction, adds guarded transitions through a `Chain of Responsibility`
validator pipeline, and broadcasts every change live over SignalR. The canonical
boundary is strict: `SessionOperations` owns the authoritative state machine;
`Identity` only supplies actor-authorization facts.

Drive each backend phase with `@backend/.agents/driver-agent.md`, selecting
phases in order: **X.1 -> X.2 -> X.3 -> X.4**. The driver delegates
implementation to `@backend/.agents/backend-agent.md`; do not invoke it
directly. For the frontend slice, use Step 9 directly with `@frontend/AGENTS.md`.
Do not mix backend and frontend work in the same phase.

---

## Required design patterns

- `State`
  - Why: lifecycle transitions need an explicit state model — not an enum with
    ad-hoc conditionals scattered across the codebase.
  - Phase owner: **X.1 Domain**
  - Gate obligation: the `LiveSession` lifecycle is expressed through an explicit
    state type where each allowed transition is a first-class domain operation
    (`TransitionTo(nextState)`) that validates preconditions and raises a
    `SessionStateChanged` domain event. No ad-hoc `if (state == X)` checks
    outside the state abstraction.

- `Chain of Responsibility`
  - Why: state transitions need an ordered, composable validator pipeline —
    not one collapsed handler with mixed concerns.
  - Phase owner: **X.2 Application**
  - Gate obligation: an ordered validator pipeline checks preconditions before
    every transition (current-state gate, operator-authorization gate, runtime
    liveness gate). Validators are registered in a stable sequence; the pipeline
    short-circuits on the first failure. New validators (HU-22 timer, HU-33A
    round-activity) can be added without modifying the pipeline infrastructure.

Transport note (mandated, NOT optional): HU-21A carries **SignalR / WebSockets**
from the patterns matrix. Valid transitions must broadcast live to all connected
clients via the existing `SessionsHub`. SignalR is a hard gate — not broadcasting
is a defect.

---

## Pre-resolved orient (as of 2026-06-04)

> Step 1 has already been run. Paste this section into any agent session that
> needs context before picking up a phase; no need to re-run the orient prompt
> unless local docs or Linear state changed.

### What has already landed and must be reused

**session-operations-service**
- `LiveSession` is already the aggregate root for live runtime state.
- `SessionState` enum exists but is NOT a state machine — HU-21A must refactor
  toward an explicit state type while keeping the DB column compatible.
- HU-07B established the `SessionsHub` SignalR hub and participant reconnect
  broadcast.
- HU-16 established the `Facade` precedent in `CreateTriviaSessionFacade`, plus
  the existing `/api/sessions` endpoint group.
- HU-19 established `AssignOperatorToSession` Facade and the assignment-aware
  authorization proxy seam.
- EF Core `live_sessions` table with `state` column, `ILiveSessionRepository`.

**identity-access-service**
- Identity owns `User`, `Role`, and coarse access facts — not the session
  lifecycle.
- The authorization surface is already plumbed: `AuthorizationBehaviour`,
  `ICurrentUser`, `[Authorize]` policies.

### What HU-21A adds

| Concern | New work |
| --- | --- |
| Domain | Explicit `SessionState` type/machine on `LiveSession` with named `TransitionTo(nextState)` operations; `SessionStateChanged` domain event carrying previous/next/timestamp/actor. |
| Application | `TransitionSessionState` command + handler orchestrating the Chain-of-Responsibility validator pipeline, domain transition, persistence, event dispatch, and SignalR broadcast trigger. Ordered validators: `CurrentStateGate`, `OperatorAuthorizationGate`, `SessionLivenessGate`. |
| Infrastructure | SignalR hub broadcasting from `SessionsHub` (extend existing), EF round-trip for the state column (backward-compatible), repository support for transition persistence. |
| API | Operator-authorized `PATCH /api/sessions/{liveSessionId}/state` endpoint; SignalR `SessionStateChanged` notification shape. |
| Frontend | Operator session-control UI (allowed transition buttons based on current state); live state updates pushed via SignalR. |

### Branch state and prerequisite

`feature/hu-21-valid-session-transactions` branches from
**`feature/hu-19-session-operator-assignment`** (HU-19 is the most recent
same-service predecessor still `In Progress`; HU-16 is already `Done` on
`develop`, and HU-18 is `In Progress` but in `identity-access-service`).

HU-19 (`feature/hu-19-session-operator-assignment`) has 1 commit not yet on
`develop`. The branch base includes:
- `LiveSession.AssignedOperatorUserId` + assignment domain behavior
- HU-19's authorization proxy seam (reusable by HU-21A)
- All HU-07B/HU-16 content already on `develop`

### Linear state

- HU ticket: `DES-28` — **Todo**, labels `Feature`, `svc:session-operations-service`;
  **`ready-for-agent` missing** (resolve before driving).
- PRD ref: `DES-70`, local file
  `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
  — **file does not exist** (drive from acceptance criteria + patterns matrix +
  landed code until the PRD is authored).

---

## 1. Orient — read current service state

> Skip this step if you have read the pre-resolved orient above and the local
> docs are unchanged.

```text
Read the following and summarise what is already decided:
- @backend/docs/hu21a-context.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Domain/Enums/SessionState.cs
- @backend/services/session-operations-service/src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs
- @backend/services/session-operations-service/src/Api/Hubs/SessionsHub.cs
- @backend/docs/ddd_solution_model.md

Then use the Linear MCP to fetch only the current live state of:
- DES-28 (HU-21A — Transiciones válidas de estado de sesión) — status and labels
- DES-23 (HU-16 — predecessor) — status
- DES-25 (HU-18 — predecessor) — status
- DES-26 (HU-19 — predecessor) — status
- DES-70 (session-operations PRD) — status and labels

Output:
- what HU-07B, HU-16, and HU-19 already landed that HU-21A must reuse
- that SessionState is currently an enum and must be refactored toward an
  explicit state type
- that SessionsHub already exists for SignalR broadcast
- the resolved HU id, PRD id (or confirmed absence), status, and labels

Do not start planning or implementing yet.
```

---

## 2. Confirm `ready-for-agent` label

> `DES-28` is currently in Wave 1 and does not carry `ready-for-agent`. Apply
> it once the ticket is unblocked (HU-18 + HU-19 landed).

```text
Use the Linear MCP to confirm that DES-28 has the label ready-for-agent.
If it has been removed or was never applied, add it back. Output the updated
ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-28 carries both svc:session-operations-service
and ready-for-agent, and output its current status and acceptance criteria.

The PRD scope (DES-70) is referenced but has no local file in
@backend/docs/prd/ — do not re-fetch PRD scope from Linear. Drive scope from
the DES-28 acceptance criteria, the patterns matrix, and the landed code.

Output the confirmed HU id, title, acceptance criteria, labels, and PRD ref
before planning the slice.
```

In the remaining steps below, `HU-21A`, `DES-28`, and `DES-70` are the resolved
references for this slice.

---

## 4. Start the slice

```text
Prepare the valid session state transitions slice on branch
feature/hu-21-valid-session-transactions, based on
feature/hu-19-session-operator-assignment.
Use the resolved HU id (DES-28) and PRD id (DES-70).
This slice affects session-operations-service primarily, with
identity-access-service only as a supporting actor-authorization dependency and
frontend as an operator session-control UI consumer.

Before implementation, confirm the baseline:
- SessionState is currently an enum on LiveSession; HU-21A must introduce an
  explicit state type
- SessionsHub already exists for SignalR broadcast
- ILiveSessionRepository already supports CRUD for LiveSession
- HU-19's authorization proxy seam is available for operator-authorization gates
- HU-22, HU-33A, and HU-21B are downstream and will extend the validator chain

Move DES-28 to In Progress and output the exact scope, branch name, and touched
surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

> Run `@backend/.agents/driver-agent.md` and select **X.1** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-21A in session-operations-service.

Before writing anything, inspect the existing domain and extend rather than
recreate:
- LiveSession aggregate (especially State, Create, assignment behaviour)
- SessionState enum
- existing domain events (LiveSessionCreatedEvent, assignment events)
- any session-history / audit event conventions already present

Scope:
- introduce an explicit state type/abstraction for LiveSession lifecycle:
  replace ad-hoc SessionState enum checks with a closed set of state objects
  or a discriminated state type that encodes allowed transitions and
  preconditions per state. The state machine must support:
  - Scheduled → Preparing (session is being set up)
  - Preparing → Active (session goes live)
  - Active → Paused (operator pauses)
  - Paused → Active (operator resumes)
  - Active → Finished (session ends naturally)
  - Scheduled → Cancelled (session cancelled before start)
  - Preparing → Cancelled (session cancelled during setup)
- each allowed transition is a named domain operation on LiveSession, e.g.
  TransitionToPreparing(), TransitionToActive(), TransitionToPaused(),
  TransitionToFinished(), TransitionToCancelled(). Each validates that the
  transition is legal from the current state.
- raise SessionStateChanged domain event on every allowed transition, carrying
  PreviousState, NewState, Timestamp, and TriggeredByActorId.
- preserve backward compatibility with the existing live_sessions.state
  persistence column (store a discriminator string/int that maps to the
  state abstraction).

Gate:
- Domain build passes
- existing LiveSession invariants are not broken
- each allowed transition is unit-tested (happy path + rejected illegal
  transition)
- SessionStateChanged event is raised on every legal transition and not
  raised on illegal ones
- no ad-hoc state `if` checks remain outside the state abstraction

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 — domain layer (HU-21A)

Ref: HU-21A
Ref: DES-28
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

> Run `@backend/.agents/driver-agent.md` and select **X.2** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-21A in session-operations-service.

Before writing anything, inspect the existing Application baseline and mirror
its conventions:
- CreateTriviaSessionFacade (orchestration precedent)
- AssignOperatorToSessionFacade
- AuthorizationBehaviour / ICurrentUser / [Authorize] usage
- existing pipeline behaviours

Scope:
- add TransitionSessionState command + handler: loads the LiveSession, runs
  the Chain-of-Responsibility validator pipeline, calls the domain transition,
  persists, dispatches domain events, and triggers SignalR broadcast (the
  broadcast is signaled via a notification/event that X.3's infrastructure
  picks up).
- implement the mandated Chain-of-Responsibility as an ordered validator
  pipeline. Validators in registration order:
  1. CurrentStateGate — is the requested transition legal from the current
     LiveSession state?
  2. OperatorAuthorizationGate — is the caller authorised to perform this
     transition on this session? (Reuses HU-19's authorization seam.)
  3. SessionLivenessGate — are runtime preconditions met? (Extensible:
     downstream HUs register additional gates here.)
- the pipeline must short-circuit on the first validation failure and throw
  a domain-appropriate exception (mapped to 4xx by ProblemDetails).
- validators are resolved from DI and registered in a stable sequence; the
  pipeline itself is not modified to add new validators — only the registration
  changes.
- handler unit tests: each allowed transition succeeds; each illegal transition
  is rejected by the CurrentStateGate; unauthorized caller rejected by the
  OperatorAuthorizationGate; unknown session returns NotFound.

Gate:
- clean build passes
- handler + validator unit tests pass for all listed paths
- State gate: transitions are enforced through the state abstraction — no
  ad-hoc `if` chains in the handler
- Chain-of-Responsibility gate: validators are ordered and composable; the
  pipeline short-circuits on first failure
- SignalR broadcast is triggered as a domain event notification (actual
  broadcasting belongs to X.3)

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 — application layer (HU-21A)

Ref: HU-21A
Ref: DES-28
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

> Run `@backend/.agents/driver-agent.md` and select **X.3** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-21A in session-operations-service.

Scope:
- verify the existing EF mapping for LiveSession.State is backward-compatible
  with the new state abstraction. The DB column still stores a discriminator
  (string or int); no migration is expected if the column type is unchanged.
- extend the LiveSession repository if needed to support transition persistence
  (it likely already round-trips LiveSession; confirm with integration tests).
- implement a SignalR hub broadcaster that listens for SessionStateChanged
  domain events (via MediatR notification handler) and pushes the state change
  to all connected clients through the existing SessionsHub. The notification
  payload must include at minimum:
  - LiveSessionId
  - PreviousState
  - NewState
  - TransitionedAt (timestamp)
- the hub broadcaster must be registered in DI and wired to the existing
  SignalR infrastructure.
- integration tests:
  - transition round-trips through EF (state change persisted and reloadable)
  - SessionStateChanged is broadcast through the hub for valid transitions
  - illegal transitions are not persisted and no broadcast occurs

Gate:
- dotnet build passes on the solution
- migration is confirmed no-op (or justified migration if the model genuinely
  changed)
- repository integration tests prove state transition persistence
- SignalR broadcast integration test proves SessionStateChanged is pushed to
  the hub

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 — infrastructure layer (HU-21A)

Ref: HU-21A
Ref: DES-28
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

> Run `@backend/.agents/driver-agent.md` and select **X.4** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-21A in session-operations-service.

Scope:
- add an Operator-authorized state-transition endpoint to the existing
  SessionsEndpoints group, for example:
  PATCH /api/sessions/{liveSessionId}/state
  body: { "newState": "<valid next state value>" }
- keep the endpoint thin: bind the request, send TransitionSessionState,
  return 200/204 with the current state (liveSessionId, currentState,
  lastTransitionedAt).
- confirm the existing SessionsHub is registered and the notification handler
  from X.3 is wired so that state changes broadcast to connected clients.
- endpoint integration tests through the service host:
  - successful transition from any allowed state
  - illegal transition rejected with 4xx (e.g. 422 Unprocessable)
  - unauthorized caller receives 401/403
  - unknown session receives 404
  - after a successful transition, a connected SignalR client receives
    SessionStateChanged

Gate:
- endpoint integration tests pass for success, illegal transition,
  unauthorized, and unknown session paths
- SignalR integration test proves the notification reaches a connected client
- service coverage reaches the enforced ADR-0005 threshold

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 — api layer (HU-21A)

Ref: HU-21A
Ref: DES-28
Ref: DES-70
```

---

## 8.5. Docker rebuild + smoke

```text
From backend/, rebuild and start the stack for manual verification:

1. docker compose build session-operations-service && docker compose up -d session-operations-service
2. docker compose build api-gateway && docker compose up -d api-gateway
3. Ensure a LiveSession exists (HU-16 create path or fixture data) in Scheduled
   state.
4. Transition the session:
   curl -i -X PATCH http://localhost:<gateway-port>/api/sessions/<liveSessionId>/state \
     -H "Content-Type: application/json" \
     -H "X-User-Id: <operatorId>" -H "X-User-Role: Operator" -H "X-User-Email: op@umbral.test" \
     -d '{"newState": "Preparing"}'
   Expect success with the updated state returned.
5. Repeat with an illegal transition (e.g. Scheduled → Active skipping
   Preparing) -> expect 4xx rejection.
6. Repeat as a non-operator caller -> expect 401/403.
7. Connect to the SignalR hub and verify the SessionStateChanged notification
   is received after a valid transition.
```

**Gate:** the transition path is reachable in the running stack; legal
transitions succeed and broadcast via SignalR; illegal transitions and
unauthorized callers are rejected.

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in
@frontend/plans/hu-03-frontend-role-permission-assignment.md, save it in
@frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Implement the operator session-control flow for HU-21A using the verified
session-operations contract (PATCH /api/sessions/{liveSessionId}/state) and
the SignalR SessionStateChanged notification.

Scope:
- present the current live session state to the operator
- show only the ALLOWED transitions as actionable buttons/controls based on
  the current state (e.g. if Scheduled, show "Prepare" and "Cancel"; if
  Active, show "Pause" and "Finish"; etc.)
- submit the transition and update the UI immediately on success; revert on
  failure with a clear error
- subscribe to the SignalR SessionStateChanged notification to receive live
  state updates pushed from the server
- handle backend rejection cleanly for illegal transitions, unauthorized
  callers, or unknown sessions
- keep this slice focused on state transitions; do not expand into timer
  controls (HU-22) or round orchestration (HU-33A)

Gate:
- the UI shows the current session state and only allowed transitions
- each allowed transition button triggers the correct PATCH request
- illegal transition rejection is rendered cleanly (not a broken screen)
- SignalR-connected clients receive and render live state updates
- unauthorized callers are blocked from the UI controls
```

Commit:

```text
feat(frontend): valid session state transitions — HU-21A

Ref: HU-21A
Ref: DES-28
Ref: DES-70
```

---

## 10. Close-out

```text
Before opening the PR:
- confirm all four backend phase commits exist
- confirm the transition smoke path was exercised (legal transition, illegal
  transition rejected, unauthorized rejected)
- confirm the SignalR broadcast is received by a connected client
- confirm the frontend shows current state, only allowed transitions, and
  receives live SignalR updates
- map each acceptance criterion to where it is enforced:
  AC#1 the session exposes at least Scheduled, Preparing, Active, Paused,
       Finished, Cancelled -> state machine domain model
  AC#2 operator can only execute allowed state transitions -> Chain-of-
       Responsibility CurrentStateGate
  AC#3 system rejects invalid transitions and informs the reason ->
       validator pipeline + ProblemDetails
  AC#4 each state change is recorded with date, responsible user, and
       reason when applicable -> SessionStateChanged domain event +
       persistence

Then open the PR:
gh pr create --draft --base feature/hu-19-session-operator-assignment \
  --title "feat: valid session state transitions — HU-21A" \
  --body "Closes DES-28
Ref: DES-70

Touched: backend/services/session-operations-service/, frontend/"
```

## Rationale

**HU-21A is the foundational session-runtime slice.** HU-22 (timer), HU-33A
(round orchestration), and HU-21B (audit trail) all depend on an authoritative
state machine. Every downstream slice in `session-operations-service` reads
`LiveSession.State` to decide what's allowed — getting the state model right
here unblocks or blocks the entire spine.

**Two patterns, not one.** `State` is the obvious fit: the session lifecycle is
the textbook example of a finite state machine, and the existing ad-hoc
`SessionState` enum must be replaced with a type that encodes transitions and
preconditions. `Chain of Responsibility` is equally mandatory: the PRD and
patterns matrix explicitly require composable validators because downstream
slices (timer, round-activity guard) must add their own gates without modifying
the pipeline infrastructure. A single collapsed handler with `if` chains would
fail both pattern gates.

**SignalR is a hard transport gate, not optional.** The patterns matrix tags
HU-21A with SignalR. Every valid transition must broadcast live. The existing
`SessionsHub` from HU-07B is the reuse target — do not create a second hub.

**Branch chaining is explicit.** HU-21A branches from
`feature/hu-19-session-operator-assignment` because HU-19 is still `In Progress`
when this slice starts. The PR base must target `feature/hu-19-session-operator-assignment`,
not `develop`, until HU-19 merges. If HU-19 merges before HU-21A starts, rebase
to `develop`.

**One ambiguity is intentionally noted.** The allowed-transition matrix
(Scheduled ↔ Preparing, Preparing ↔ Active, Active ↔ Paused, Active → Finished,
Scheduled/Cancelled → Cancelled) is derived from the existing `SessionState`
enum and the domain model docs. If the acceptance criteria or a team decision
adds or removes a transition, adjust the state machine accordingly — do not
silently invent transition rules.
