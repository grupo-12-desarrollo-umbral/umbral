# HU-21A Context — Session state machine realign (Scheduled → Preparing → Active → Paused → Finished → Cancelled)

> Paste this section into any agent session that needs context for HU-21A (DES-76).
> Last updated: 2026-07-04 | Branch: `feature/hu-21a-session-state-machine-realign`

## State

- DES-76 (HU-21A): **Todo**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature`, `ready-for-agent`. Both required labels present.
- **Resolved mode: realignment-rebuild** (`needs-rebuild` present) — but **verification-dominant**. DES-76 is the realignment map's phase #5 "Lifecycle" row: "State machine `Scheduled/Preparing/Active/Paused/Finished/Cancelled`" (`canon-realignment-after-mission-runtime-rewrite.md:101`). It is **not** in the superseded column — it is the **rebuild successor** of DES-28 (`:67`, `:144`). The explicit `State`-pattern state machine, the `Chain of Responsibility` transition validators, the `TransitionSessionStateFacade`, and the SignalR broadcast were **already shipped in cycle 1 by DES-28 (commit `b43a60e`, 2026-06-04)** and the enum already carried the six canonical states. The two canon changes DES-76 depends on already landed: HU-15 (DES-22) created the `LiveSession` directly in `Scheduled` with an immutable `MissionRuntimeSnapshot`, and HU-17 (DES-24) tore out the session-level `SessionMode` concept. HU-21A does **not** rebuild the state machine — it **locks the canonical transition model with explicit tests across all four layers** and confirms no residual `SessionMode` branching survived.
- **Superseded handling applied:** DES-28 (HU-21A cycle-1, this ticket's predecessor) is **Canceled** — never anchor on it as a predecessor even though its code is what we verify. The peer rebuilds HU-22 (DES-30→DES-77) and HU-33A (DES-44→DES-78) are **also Canceled/superseded and downstream in the map** (steps #6, #10). Their cycle-1 timer + trivia-round code coexists on `develop` inside the same state classes — **do not touch it** (see gotchas): DES-76 realigns the lifecycle only; DES-77 realigns the timer, DES-78 the trivia round.
- Predecessor DES ids (build-on, Done/merged): **DES-22 (HU-15)**, **DES-24 (HU-17)**, **DES-25 (HU-18)**, **DES-26 (HU-19)**. Landed-untouched: DES-27 (HU-20), DES-11/12 (HU-07A/07B), DES-75 (HU-16 realign).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear). Overlaid by `backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.
- Canonical lifecycle source: `backend/services/session-operations-service/CONTEXT.md` §SessionState (`:35-60`, `:177`, `:198`).
- Blocked by: DES-22 (HU-15), DES-24 (HU-17) — both Done. Related: DES-25 (HU-18), DES-28 (HU-21A cycle-1).
- Branch: `feature/hu-21a-session-state-machine-realign`, base **`develop`** (all build-on predecessors Done/merged; no same-service predecessor In Progress).

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | `required_patterns_matrix.md:27,41,109`: "Lifecycle (`Scheduled`→…→`Finished`/`Cancelled`) needs an explicit state model." | An explicit per-state type owns the allowed-transition set — **not** an enum with ad-hoc conditionals. Already realized: `Domain/Services/SessionStates/*` (`ILiveSessionState` + one class per state) behind `LiveSessionStateFactory`; `SessionStateTransitionPolicy` delegates `IsTransitionAllowed` to the current state's `CanTransitionTo`. HU-21A **verifies + locks** this; it does not replace it. |
| `Chain of Responsibility` (mandated) | X.2 Application | `required_patterns_matrix.md:28,42,109`: "…plus ordered transition validators (`SessionStateTransitionPolicy`)." | An ordered, composable validator pipeline for a transition — **not** one collapsed handler. Already realized: `Application/Sessions/StateTransitions/` — `SessionTransitionChain` links `CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate` (each a `SessionTransitionValidator`), short-circuits on first failure, DI-ordered so downstream HUs register a new link without editing the pipeline. HU-21A **verifies + locks** the ordered chain. |

Transport note: HU-21A carries **SignalR / WebSockets** (`required_patterns_matrix.md:57,109`). Valid transitions must broadcast live — already realized: `SessionStateChangedEvent` → `SessionStateChangedNotificationHandler` → `ISessionStateBroadcaster` → `SessionStateBroadcaster` (`SessionsHub`, group `live-session:{id}`). This is a **mandated transport**, verified in X.4. **RabbitMQ / `SessionStateChanged` async audit is HU-21B, not HU-21A** (`required_patterns_matrix.md:156` — "HU-21A (transitions + broadcast), HU-21B (audit event)"): do **not** add an outbound RabbitMQ publish in this slice.

Applies-where note (no new gate): `PATCH /api/sessions/{liveSessionId}/state` is a protected mutation, but HU-21A is **not** matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). It inherits the standard `Operator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) — note only, **no** new `Proxy` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-22 (HU-15) — Done.** Rebuilt session creation around `Mission` as the only `SessionSource`: `LiveSession.Create(...)` sets `State = Scheduled` (`LiveSession.cs:64`), takes the immutable `MissionRuntimeSnapshot` (the ordered Stage/Substage tree the "first substage" would start from), and validates the source. This is the creation-in-`Scheduled` half of AC #2. See `hu15-context.md`.
- **DES-24 (HU-17) — Done.** Locked the single-source invariant and **deleted the session-level `SessionMode`** enum + its debris — so AC #5 ("no `SessionMode` logic") is already an emergent property; HU-21A confirms no branching remains. See `hu17-context.md`.
- **DES-26 (HU-19) — Done.** Added `LiveSession.AssignedOperatorUserId`, consumed by the `OperatorAssignmentGate` (operator required for `Preparing`/`Active`).
- **DES-25 (HU-18) — Done.** Team association + `AssociatedTeamCount`, consumed by `ParticipantReadinessGate` (≥1 team to go `Active`); `EnsureCanAssociateTeam` already enforces team-association-only-`Scheduled` (`LiveSession.cs:597-603`) — the team-window half of AC #2. (DES-25's realign is a 📝 wording-only row, already satisfied by this code.)

**Landed, untouched by this HU:** DES-27 (HU-20, assigned-session reads), DES-11/12 (HU-07A/07B, membership + reconnect), DES-75 (HU-16 realign, trivia-substage selection into the snapshot) build on/around the session aggregate but do not inform the lifecycle transition model — do not anchor on them.

**Superseded / not predecessors (do NOT anchor on):** DES-28 (HU-21A cycle-1, this ticket's source) — Canceled; HU-22 (DES-30→DES-77) and HU-33A (DES-44→DES-78) — Canceled, and their timer/round rebuilds are **downstream** of DES-76 in the map.

**Coverage:** session-operations-service carries the HU-07/15/17/18/19 baseline; verify the real service percentage against the ADR-0005 repo gate at phase X.4.

## What this HU adds

| Concern | New work |
|---|---|
| Verification posture | The canonical state machine, CoR validators, facade, endpoint, and SignalR broadcast already exist (cycle-1 DES-28 + HU-15/17 canon changes). HU-21A **locks the canonical model with explicit tests** and confirms alignment; it does not rebuild. |
| Canonical transition matrix (domain) | Extend the domain test so **every** allowed edge and **every** rejected edge is asserted against `SessionStateTransitionPolicy` / the State classes: `Scheduled→{Preparing,Cancelled}`, `Preparing→{Active,Cancelled}`, `Active→{Paused,Finished,Cancelled}`, `Paused→{Active,Finished,Cancelled}`, `Finished`/`Cancelled` terminal, `Cancelled` reachable from any non-terminal. The existing `SessionStateTransitionPolicyTests` covers only a representative sample. |
| Creation + team window (domain) | Assert `LiveSession.Create` yields `Scheduled` and `EnsureCanAssociateTeam` rejects team association in any state ≠ `Scheduled` (`TeamAssociationRequiresScheduledSessionException`). |
| No `SessionMode` branching | Confirm (grep + test) no session-level `SessionMode` type or mode-conditioned transition survives; the state machine is source/mode-agnostic. |
| Chain of Responsibility (application) | Test the ordered `CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate` chain: registration order preserved, first failure short-circuits, each gate's rejection reason (`InvalidSessionStateTransitionException`, `SessionOperatorNotAssignedException`, `LiveSessionRequiresAtLeastOneTeamException`). |
| Facade orchestration (application) | Verify `TransitionSessionStateFacade` runs the chain → `LiveSession.MoveTo` (domain re-asserts + raises `SessionStateChangedEvent`) → persist; the `TransitionSessionStateCommand` is `Operator`-authorized. |
| Persistence (infrastructure) | Verify the `live_sessions` state / `LastStateChangedAt` / `StateReason` columns round-trip all six states. **No new migration** — the state column has existed since `InitSessionOperations`. |
| API + broadcast | Verify `PATCH /api/sessions/{liveSessionId}/state`: valid target → `200` (previous/next state), invalid target → ProblemDetails, and the `SessionStateChanged` SignalR notification reaches the `live-session:{id}` group. |
| Frontend | Verification/cleanup only: operator session-control affordances match the six canonical states; no residual `SessionMode` copy/type. No contract change. |

## Touched surfaces

- `backend/services/session-operations-service` (owner — domain/application/infra/api verification tests; targeted grep confirmations)
- `frontend/` operator session-control UI (verification + stale `SessionMode` cleanup; no contract change)
- API contract boundary: **unchanged** — `PATCH /api/sessions/{liveSessionId}/state` + the `SessionStateChanged` SignalR notification already exist (cycle-1); HU-21A proves and locks them

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| | | |

## Known quirks / gotchas

- **Do NOT rebuild the state machine.** The six-state enum, the `State`-pattern classes, `SessionStateTransitionPolicy`, the CoR validators, `TransitionSessionStateFacade`, the `PATCH …/state` endpoint, and the SignalR broadcast are all present and canon-aligned (verified 2026-07-04). Reject any plan that re-adds a state type, a validator pipeline, or a broadcast path — HU-21A verifies and locks, it does not duplicate.
- **The transition graph already matches canon exactly.** `Scheduled→{Preparing,Cancelled}`, `Preparing→{Active,Cancelled}`, `Active→{Paused,Finished,Cancelled}`, `Paused→{Active,Finished,Cancelled}`, `Finished`/`Cancelled` terminal. Do not change edges — only widen the tests to cover the full matrix.
- **AC #3 "`Preparing → Active` starts the first substage immediately" is a cross-HU seam, not HU-21A code.** CONTEXT.md:48 makes first-substage start the *meaning* of entering `Active`; the *implementation* (activate the first trivia question / open the substage's targets) is owned by the play/orchestration HUs — HU-33A trivia (DES-78) and treasure-hunt HUs (HU-29–32) — which are **downstream** in the realignment map and require `Active` state (`QuestionActivationRequiresActiveSessionException`). The lifecycle already provides the `ILiveSessionState.Enter(Active)` hook. **Do not build substage-play orchestration here** — that would duplicate/trespass DES-78. Record it as a `decide` and surface at Stop 1 (see X.1 block).
- **The timer behaviour inside `ActiveLiveSessionState.Enter` / `PausedLiveSessionState.Enter` is HU-22 (DES-77) territory.** Those `Enter` overrides start/freeze the authoritative session + question timers (cycle-1 HU-22 code). DES-77 realigns the timer; **do not modify or delete** the timer hooks in this slice — HU-21A verifies only the lifecycle transition/broadcast.
- **`Finished` is reached only via normal final-substage completion (`SessionCompletion`), never operator-forced.** CONTEXT.md:56 + DES-76's transition list. The `PATCH …/state` endpoint permits `Active/Paused → Finished` at the policy level; the "only via `SessionCompletion`" constraint is a play-layer obligation (DES-78) — note it, do not add a domain guard that would conflict with the downstream completion path.
- **Namespace is `umbral_backend.*`** across all session-ops layers; the domain test project namespace is `umbral_backend.SessionOperations.UnitTests.*`. Match existing files.
- **Domain tests live in `tests/UnitTests/`** (not `Application.UnitTests`): `tests/UnitTests/Domain/Services/SessionStateTransitionPolicyTests.cs` and `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` already exist — extend these, don't create a parallel project.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **realignment-rebuild (verification-dominant)**: authority chain **canon docs > tracker AC > existing code**
> (`canon-realignment-workflow.md:19-34`); keep/delete/decide per `:72-84`. Mirror-anchors point **only** at code classified `keep`.
> Canon-delta (`canon-realignment-after-mission-runtime-rewrite.md:33`): canonical `SessionState`s are `Scheduled → Preparing → Active → Paused → Finished → Cancelled`; `LiveSession` is created in `Scheduled`; team association only while `Scheduled`; operator-driven readiness moves `Scheduled → Preparing` before `Active`. No session-level `SessionMode` (`:24`, torn out by HU-17).
> Lifecycle canon: `CONTEXT.md` §SessionState (`:35-60` per-state transitions + windows, `:56` `Finished` only via `SessionCompletion`, `:177` `SessionCompletion`, `:198` lifecycle model); PRD DES-70:96-99 (US12 move through the six states / US13 reject invalid with a reason), `:193` (`State` pattern where mapped), `:203` (substage active immediately on `Active`); patterns `required_patterns_matrix.md:41,42,109,156`.

### Phase X.1 — Domain
**Derive** (`CONTEXT.md:35-60`; `canon-realignment-after-mission-runtime-rewrite.md:33`; PRD DES-70:96-99,193):
- The `State`-pattern machine exposes exactly the six canonical states and the exact transition matrix. Lock it as a test over the as-built domain: for each `SessionState`, `LiveSessionStateFactory.For(state).CanTransitionTo(next)` (via `SessionStateTransitionPolicy.IsTransitionAllowed`) returns true for **only** the canonical edges and false for every other edge; `Finished`/`Cancelled` are terminal; `Cancelled` is reachable from `Scheduled`/`Preparing`/`Active`/`Paused`.
- `LiveSession.Create(...)` yields `State == Scheduled` (`LiveSession.cs:64`); `EnsureCanAssociateTeam` throws `TeamAssociationRequiresScheduledSessionException` for any state ≠ `Scheduled` (`:597-603`) — AC #2.
- `LiveSession.MoveTo` re-asserts via `SessionStateTransitionPolicy.EnsureCanTransition` and raises `SessionStateChangedEvent` on success (`:235-249`) — AC #4 (reject invalid) and the broadcast source.
- No session-level `SessionMode` type or mode-conditioned branch exists (HU-17 removed it) — AC #5; assert absence.

**Target files** (create | edit — file to mirror):
- edit `tests/UnitTests/Domain/Services/SessionStateTransitionPolicyTests.cs` — widen to the **full** allowed+rejected matrix + terminal + cancel-from-any-non-terminal (currently a representative sample)
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` — assert `Create` ⇒ `Scheduled`, team-association-only-`Scheduled`, and that `MoveTo` over a rejected edge throws `InvalidSessionStateTransitionException` and raises no event; mirror the existing tests in that file
- keep (verify, do not rewrite) `Domain/Enums/SessionState.cs`, `Domain/Services/SessionStates/*`, `Domain/Services/SessionStateTransitionPolicy.cs`, `Domain/Entities/LiveSession.cs`, `Domain/Events/SessionStateChangedEvent.cs`, `Domain/Exceptions/{InvalidSessionStateTransitionException,TeamAssociationRequiresScheduledSessionException,LiveSessionRequiresAtLeastOneTeamException}.cs`

**Pattern this phase owns:** `State` (mandated) — the per-state `CanTransitionTo` set behind `LiveSessionStateFactory`.
**Gate:** Domain build passes; a unit test locks the **full** canonical transition matrix (every allowed edge true, every other edge rejected, `Finished`/`Cancelled` terminal, `Cancelled` from any non-terminal); `Create` ⇒ `Scheduled`; team association rejected outside `Scheduled`; `MoveTo` raises `SessionStateChangedEvent` only on an allowed edge; no session-level `SessionMode` type remains in the service. **`State` pattern verified — transitions decided by per-state types, not ad-hoc conditionals.**

**Existing code (keep / delete / decide):**
- keep `Domain/Enums/SessionState.cs`, `Domain/Services/SessionStates/*`, `SessionStateTransitionPolicy.cs`, `LiveSession.cs` (`:64`, `:235-249`, `:597-603`, `:620-626`), `SessionStateChangedEvent.cs`, the three exceptions above — canon-aligned; verify + mirror, do not rewrite
- delete none — HU-17 already removed `SessionMode` and its debris; **verify absent**, do not re-create
- **decide** `Domain/Services/SessionStates/ActiveLiveSessionState.cs` (`Enter`): CONTEXT.md:48 says entering `Active` immediately starts the first `Substage`, but the current `Enter` starts only the session/question **timers** (HU-22 code); no substage activation exists. First-substage activation is owned by HU-33A (DES-78, trivia) / treasure-hunt HUs (HU-29–32), **downstream** in the map and gated on `Active`. **Do not add substage-play orchestration in HU-21A** — record the seam and surface at Stop 1; the lifecycle already exposes the `Enter(Active)` hook those HUs extend.

### Phase X.2 — Application
**Derive** (`required_patterns_matrix.md:42,109` ordered transition validators; `CONTEXT.md:44,48` operator/team readiness for `Preparing`/`Active`; PRD DES-70:99 reject invalid with a reason):
- The transition runs an ordered `Chain of Responsibility`: `CurrentStateGate` (structural reachability via `SessionStateTransitionPolicy`) → `OperatorAssignmentGate` (operator required for `Preparing`/`Active`) → `ParticipantReadinessGate` (≥1 team for `Active`). The chain is built from DI registration order (`SessionTransitionChain`), each link short-circuits on failure, and downstream HUs (timer/round) add a link without editing the pipeline.
- `TransitionSessionStateFacade` resolves the authorized session, runs the chain, calls `LiveSession.MoveTo` (domain re-asserts + raises `SessionStateChangedEvent`), and persists; `TransitionSessionStateCommand` is `[Authorize(Roles = "Operator")]`.
- HU-21A adds the **tests** that lock this ordering/short-circuit; it does not change the chain or the facade.

**Target files** (create | edit — file to mirror):
- edit `tests/Application.UnitTests/Sessions/StateTransitions/SessionTransitionChainTests.cs` — assert order preserved **and** first-failure short-circuit skips later gates
- create/extend gate tests under `tests/Application.UnitTests/Sessions/StateTransitions/Validators/` — one per gate, asserting each rejection (`InvalidSessionStateTransitionException`, `SessionOperatorNotAssignedException`, `LiveSessionRequiresAtLeastOneTeamException`); mirror `SessionTransitionChainTests`
- edit `tests/Application.UnitTests/Sessions/Commands/TransitionSessionState/TransitionSessionStateCommandHandlerTests.cs` — chain-then-`MoveTo` orchestration + `Operator` authorization
- keep (verify, do not rewrite) `Application/Sessions/StateTransitions/*`, `Application/Sessions/Commands/TransitionSessionState/*`, `Application/Sessions/EventHandlers/SessionStateChangedNotificationHandler.cs`

**Pattern this phase owns:** `Chain of Responsibility` (mandated) — the ordered `SessionTransitionValidator` links.
**Gate:** Application build passes; a test proves the validator chain runs `CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate` in order and short-circuits on the first failure; each gate's rejection is asserted; the facade orchestrates chain → `MoveTo` → persist and the command is `Operator`-authorized. **`Chain of Responsibility` verified — ordered composable validators, not one collapsed handler.**

**Existing code (keep / delete / decide):**
- keep `Application/Sessions/StateTransitions/*`, `Application/Sessions/Commands/TransitionSessionState/*`, `SessionStateChangedNotificationHandler.cs` — canon-aligned; verify + mirror
- delete none
- decide none

### Phase X.3 — Infrastructure
**Derive** (`Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`; `Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`; ADR-0008 shared Postgres testcontainer):
- The persisted `LiveSession` round-trips its `State` (all six values), `LastStateChangedAt`, and `StateReason`. The state column has existed since `20260603210909_InitSessionOperations`; the reason/timestamp columns since the runtime-recovery/timer migrations. **No new migration** — a transition mutates existing columns.
- HU-21A adds the **test** that locks the state/reason round-trip after a `MoveTo`.

**Target files** (create | edit — file to mirror):
- create/extend a repository integration test — mirror `tests/IntegrationTests/Persistence/LiveSessionRepositoryIntegrationTests.cs`: persist a session, `MoveTo(Preparing)`, `UpdateAsync`, reload, assert `State`/`LastStateChangedAt`/`StateReason` round-trip
- keep `Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs`, `Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; **no new migration** — assert (`ef migrations add` dry-check or model-snapshot grep) the model already carries the `state`/`last_state_changed_at`/`state_reason` columns and needs no schema change; a repository integration test round-trips a transitioned session's state, timestamp, and reason.

**Existing code (keep / delete / decide):**
- keep `LiveSessionConfiguration.cs`, `LiveSessionRepository.cs`
- delete none
- decide none

### Phase X.4 — Api
**Derive** (`Api/Controllers/SessionsController.cs:62-83` transition endpoint; `Api/Hubs/{SessionsHub,SessionStateBroadcaster}.cs`; ADR-0001/0002 gateway auth; ADR-0005 coverage):
- `PATCH /api/sessions/{liveSessionId}/state` is `Operator`-guarded, body `TransitionSessionStateRequest(TargetState, Reason)`: a valid target → `200` with `{ liveSessionId, previousState, newState, lastStateChangedAt, timer }`; an unparseable/undefined target → ProblemDetails; a rejected transition surfaces the domain rejection as ProblemDetails (AC #4). On success, `SessionStateChanged` is broadcast to the `live-session:{id}` SignalR group.
- HU-21A verifies + covers; it reshapes nothing.

**Target files** (create | edit — file to mirror):
- edit `tests/IntegrationTests/Api/TransitionSessionStateEndpointTests.cs` — a valid transition → `200` + response shape; an invalid target/edge → ProblemDetails; `Operator` authorization enforced
- edit `tests/IntegrationTests/Api/SessionStateBroadcastHubTests.cs` — a transition broadcasts `SessionStateChanged` to the session group with previous/next state
- keep (verify, do not reshape) `Api/Controllers/SessionsController.cs`, `Api/Hubs/*`

**Pattern this phase owns:** none new — `State`/`Chain of Responsibility` are realized in X.1/X.2; the endpoint inherits the standard `Operator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002). HU-21A is not in the applies-where `Proxy` set → no new gate.
**Gate:** endpoint integration test (valid transition → `200` with previous/next state; invalid target/edge → ProblemDetails; `Operator`-guarded) + hub test (transition broadcasts `SessionStateChanged` to `live-session:{id}`); **ADR-0005 coverage** (service ≥ repo gate).

**Existing code (keep / decide):**
- keep `Api/Controllers/SessionsController.cs` (`PATCH …/state`, `Operator`), `Api/Hubs/{SessionsHub,SessionStateBroadcaster}.cs` — verify, do not reshape
- decide none
