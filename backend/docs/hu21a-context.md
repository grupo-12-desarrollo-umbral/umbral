# HU-21A Context — Valid Session State Transitions

> Superseded on 2026-06-16 by
> `backend/docs/grilling-session-mission-restructure.md`,
> `backend/docs/ddd_solution_model.md`, and
> `backend/docs/bd_umbral_entity_spec.md`.
> Keep the explicit `State` pattern guidance, but rebuild lifecycle assumptions
> around canonical states `Preparing`, `Active`, `Paused`, `Finished`, and
> `Cancelled`. `Scheduled` is no longer canonical; `Finished` is reached only
> by normal final-substage completion.

> Paste this section into any agent session that needs context for HU-21A.
> Last updated: 2026-06-04 | Branch: `feature/hu-21-valid-session-transactions`
>
> Boundary note: HU-21A is a `session-operations-service` slice that formalises the
> `LiveSession` lifecycle as an explicit state machine with guarded transitions and
> live broadcast. `SessionOperations` owns the authoritative state and every
> transition decision; `Identity` supplies actor facts and coarse policy checks,
> but not the session lifecycle.

## ⚠️ Resolution gaps (must close before the driver runs — Stop 1)

1. **The session-operations PRD (`DES-70`) does not have a local file.**
   `hu19-context.md` references `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
   but that file does not exist in any worktree. Drive HU-21A scope from the
   `DES-28` acceptance criteria, patterns matrix, and the landed code on `develop`
   + `feature/hu-19-session-operator-assignment` until the PRD is authored.
2. **`DES-28` is not yet `ready-for-agent`** (currently Wave 1, unblocked when
   `HU-18` + `HU-19` land). Apply the label before the driver pre-flight.

## State

- `DES-28` (HU-21A): status **Todo**; labels `Feature`, `svc:session-operations-service`.
  **Missing `ready-for-agent`** (see gap 2).
- Predecessors already landed on the same service label:
  - `DES-11` (HU-07A): **Done** — participant membership validation baseline
  - `DES-12` (HU-07B): **Done** — reconnect/runtime admission baseline
  - `DES-23` (HU-16): **Done** — trivia session creation baseline
  - `DES-25` (HU-18): **In Progress** — team-session association
  - `DES-26` (HU-19): **In Progress** — session operator assignment
- PRD ref: `DES-70` (PRD — Primera implementacion de
  `session-operations-service` (HU-15 a HU-36)); local file
  `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
  — **file does not exist** (see gap 1).
- Blocked by: HU-16, HU-18, HU-19
- Blocks (downstream): `DES-30` (HU-22), `DES-44` (HU-33A), `DES-29` (HU-21B)
- Branch: `feature/hu-21-valid-session-transactions`, base =
  **`feature/hu-19-session-operator-assignment`** (HU-19 is the most recent
  same-service predecessor still `In Progress` and not yet merged to `develop`)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | "Lifecycle transitions need an explicit state model." | An explicit state type for `LiveSession` lifecycle transitions — not an enum with ad-hoc conditionals. Each allowed transition is a first-class domain operation (`TransitionTo(nextState)`) that validates preconditions and raises a domain event. |
| `Chain of Responsibility` (mandated) | X.2 Application | "…plus ordered transition validators." | An ordered, composable validator pipeline for state transitions — not one collapsed handler. Each validator checks one concern (e.g. current-state gate, operator-authorization, timer-liveness, participant-count); validators are registered in a stable sequence and run before the state mutation. |

Transport note: HU-21A carries **SignalR / WebSockets** (from patterns matrix).
Valid transitions must broadcast live to connected clients. SignalR wiring lands
in X.3 (hub broadcasting) and X.4 (hub endpoint / DI registration). This is a
**mandated transport**, not optional.

## What predecessors have already landed (reuse candidates)

All of this is on `develop` (with some additions on `feature/hu-19-session-operator-assignment`
not yet merged).

**Domain layer**
- `LiveSession` is already the session-runtime aggregate root for
  `SessionOperations`
- `SessionState` enum already exists: `Scheduled`, `Preparing`, `Active`,
  `Paused`, `Finished`, `Cancelled` — but it is an enum, not a state machine.
  HU-21A must replace ad-hoc enum checks with an explicit state type.
- `LiveSession.Create` already sets `State = Scheduled` and raises
  `LiveSessionCreatedEvent`
- `LiveSession.AssignedOperatorUserId` already exists from HU-19
- Value objects: `SessionSource`, `MaximumTime`, `TeamCode`
- Domain events: `LiveSessionCreatedEvent`, HU-19's assignment events
- `JoinPolicy` and participant-admission rules from HU-07A/07B

**Application layer**
- Existing MediatR + pipeline baseline: `AuthorizationBehaviour`,
  `ValidationBehaviour`, `ICurrentUser`, `[Authorize]`-driven request metadata
- HU-16's `CreateTriviaSession` Facade — direct orchestration precedent
- HU-19's `AssignOperatorToSession` Facade + authorization proxy seam
- Cross-service client pattern: `ParticipantMembershipAccessClient`

**Infrastructure / API**
- EF Core `live_sessions` table fully mapped; `ILiveSessionRepository` exists
- `SessionsEndpoints` at `/api/sessions` with trivia-creation and reconnect
- SignalR `SessionsHub` already exists from HU-07B (participant reconnect path)
- `AuthorizationPolicies` with `AuthorizationBehaviour` pipeline

**Frontend**
- HU-16 session creation flow (operator creates a trivia session)
- HU-19 operator assignment flow (in progress)

**Coverage**
- ADR-0005 gate applies. The exact post-HU-19 aggregate percentage is not
  recorded; X.4 must measure and report current merged coverage.

## What this HU adds

| Concern | New work |
|---|---|
| State machine on `LiveSession` | Replace ad-hoc `SessionState` enum checks with an explicit state abstraction: a base `SessionState` type or closed enum-with-behaviour that encodes allowed transitions and preconditions per state. Each guardable transition (`Schedule → Preparing`, `Preparing → Active`, `Active → Paused`, `Paused → Active`, `Active → Finished`, `Scheduled → Cancelled`) is a named domain operation. |
| Domain events for transitions | Raise `SessionStateChanged` domain event on every allowed transition, carrying previous/next state, timestamp, and triggering actor. |
| Chain of Responsibility validators | An ordered, composable validator pipeline for state transitions. Validators in sequence: `CurrentStateGate` (is the transition allowed from current state?), `OperatorAuthorizationGate` (is the caller authorised to transition?), `SessionLivenessGate` (are runtime prerequisites met?), optionally extended by downstream validators. The pipeline short-circuits on first failure. |
| Application orchestration | `TransitionSessionState` command + handler: orchestrates the validator chain, calls the domain transition, persists, emits events, and triggers SignalR broadcast. The handler stays thin — validators own preconditions, the domain owns the mutation. |
| SignalR broadcast | Broadcast `SessionStateChanged` notification to connected clients via the existing `SessionsHub`. The notification includes `LiveSessionId`, `PreviousState`, `NewState`, `TransitionedAt`. |
| API surface | Operator-authorized endpoint(s) to trigger allowed state transitions on a `LiveSession`, e.g. `PATCH /api/sessions/{liveSessionId}/state` with body `{ "newState": "<valid next state>" }`. |
| Frontend | Operator session-control UI: buttons/actions for allowed transitions based on current state; live state updates broadcast via SignalR. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain, application,
  infrastructure, and API layers (the SignalR hub already exists, but hub
  broadcasting must be added or extended)
- `backend/services/identity-access-service/` — supporting actor/policy contract
  only if the existing authorization surface is insufficient
- `frontend/` operator session-control UI
- API contract boundary: state-transition command/response plus SignalR
  `SessionStateChanged` notification shape

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **This is the foundational session-runtime slice.** HU-22 (authoritative timer),
  HU-33A (round orchestration), and HU-21B (audit) all depend on HU-21A's state
  machine. Getting the state model right here unblocks or blocks the rest of the
  session-operations spine.
- **The `SessionState` enum exists but is not a state machine.** HU-07B/HU-16
  used it as a simple discriminator. HU-21A must refactor toward an explicit
  state type while keeping the existing persistence column (`live_sessions.state`
  as a string/int) compatible.
- **Do not build HU-22 timer logic here.** HU-21A defines which transitions are
  valid; HU-22 owns the authoritative countdown that triggers `Active → Finished`
  on expiry. HU-21A's transition model should be extensible so HU-22 can register
  its own gate validator later.
- **SignalR broadcast is a hard requirement.** The patterns matrix mandates
  SignalR for HU-21A. The existing `SessionsHub` should be reused/extended; do
  not create a second hub for state changes.
- **Validator extensibility matters.** HU-22, HU-33A, and HU-34A will need to
  register their own validators in the chain (e.g. "cannot transition if a round
  is active"). The Chain of Responsibility pipeline in X.2 must support ordered
  registration of additional validators without modifying the pipeline itself.
- **The existing EF column stores `SessionState` as an enum value.** The state
  machine refactor should preserve backward compatibility with the existing
  `live_sessions.state` column (store the discriminator as before, add state
  machine behaviour in the domain model only).
- **State-window ambiguity should be recorded, not guessed.** The PRD and
  acceptance criteria do not specify all guard conditions for every transition
  (e.g. can you pause before the first question starts?). If the canon docs do
  not resolve a transition rule, keep it explicit in code/tests and record the
  ambiguity rather than silently inventing a restriction.
