# Prompt Example - HU-21A Session state machine realign (Feature Slice)

Concrete prompt sequence for driving DES-76 (HU-21A) through a full slice on `feature/hu-21a-session-state-machine-realign`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for the DES-76 slice:** this is **not** a fresh rebuild. The explicit `State`-pattern state machine, the `Chain of Responsibility` transition validators, the `TransitionSessionStateFacade`, the `PATCH /api/sessions/{id}/state` endpoint, and the `SessionStateChanged` SignalR broadcast were already shipped in cycle 1 (DES-28, commit `b43a60e`) and the enum already carried the six canonical states. The two canon changes DES-76 rides on already landed: HU-15 (DES-22) creates the `LiveSession` in `Scheduled` with an immutable `MissionRuntimeSnapshot`, and HU-17 (DES-24) tore out the session-level `SessionMode`. HU-21A **locks the canonical transition model with explicit tests across all four layers** and confirms no `SessionMode` branching survived. It adds no new aggregate, no new endpoint, and no new migration. Treat every phase as verification, not re-implementation.

When working from the monorepo root, make the target workload explicit in each prompt.
For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps,
point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in
the same phase prompt; coordinate them as separate scoped steps.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- the session exposes exactly the six states `Scheduled`, `Preparing`, `Active`, `Paused`, `Finished`, `Cancelled`
- the canonical transition matrix is enforced: `Scheduled→{Preparing,Cancelled}`, `Preparing→{Active,Cancelled}`, `Active→{Paused,Finished,Cancelled}`, `Paused→{Active,Finished,Cancelled}`, `Finished`/`Cancelled` terminal, `Cancelled` reachable from any non-terminal
- a `LiveSession` is created in `Scheduled` and team association is allowed **only** while `Scheduled`
- invalid transitions are rejected with a reason
- no session-level `SessionMode` logic exists
- valid transitions broadcast live via SignalR (`SessionStateChanged`); **no** RabbitMQ audit publish (that is HU-21B)
- `Preparing → Active` starting the first substage is acknowledged as a **downstream** (HU-33A / treasure-hunt) seam, **not** built here
- the cycle-1 state machine, CoR validators, facade, endpoint, and broadcast are **verified and locked, not rebuilt**

---

## Required design patterns

- **`State` (mandated, X.1 Domain)** — `required_patterns_matrix.md:27,41,109`. An explicit per-state type owns the allowed-transition set. Already realized (`Domain/Services/SessionStates/*` behind `LiveSessionStateFactory`; `SessionStateTransitionPolicy`). Verify + lock — do not replace with enum conditionals.
- **`Chain of Responsibility` (mandated, X.2 Application)** — `required_patterns_matrix.md:28,42,109`. Ordered composable transition validators (`CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate` via `SessionTransitionChain`). Verify + lock the ordering/short-circuit — do not collapse into one handler.
- **Transport: SignalR (mandated)** — verify `SessionStateChanged` broadcasts to the `live-session:{id}` group. **RabbitMQ audit is HU-21B, not this slice.**

> Applies-where note (no new gate): `PATCH /api/sessions/{liveSessionId}/state` is a protected mutation, but HU-21A is not matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). It inherits the standard `Operator` `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) — note only, no new `Proxy` gate.

---

## Pre-resolved orient (as of 2026-07-04)

> Step 1 has already been run. Paste this section into any agent session that needs context
> before picking up a phase - no need to re-run the orient prompt.

### What predecessors have already landed

DES-76 (HU-21A) is **Todo**, a `needs-rebuild` row in the realignment map (phase #5 "Lifecycle": "State machine `Scheduled/Preparing/Active/Paused/Finished/Cancelled`"), and the **rebuild successor of the Canceled DES-28**. Its build-on predecessors are all Done/merged:

- **DES-22 (HU-15)** — `LiveSession.Create(...)` sets `State = Scheduled` (`LiveSession.cs:64`) with an immutable `MissionRuntimeSnapshot` (creation-in-`Scheduled`, AC #2 half one).
- **DES-24 (HU-17)** — deleted the session-level `SessionMode` enum + debris (AC #5 already emergent).
- **DES-26 (HU-19)** — `LiveSession.AssignedOperatorUserId`, consumed by `OperatorAssignmentGate`.
- **DES-25 (HU-18)** — team association + `AssociatedTeamCount`; `EnsureCanAssociateTeam` enforces team-association-only-`Scheduled` (`LiveSession.cs:597-603`) — AC #2 half two.

Landed-untouched: DES-27 (HU-20), DES-11/12 (HU-07A/07B), DES-75 (HU-16 realign). Superseded and excluded from the predecessor set: DES-28 (HU-21A cycle-1), HU-22 (DES-30→DES-77), HU-33A (DES-44→DES-78) — all Canceled; DES-77/DES-78 are **downstream** of DES-76. No same-service In Progress predecessor, so the branch base is `develop`.

### What HU-21A adds on top (per DES-76, the realignment map, and PRD DES-70)

| Concern | New work |
|---|---|
| Verification posture | Lock the canonical state machine with explicit tests; confirm alignment. Do not rebuild. |
| Transition matrix (domain) | Widen the domain test to the **full** allowed+rejected matrix + terminal + cancel-from-any (the existing `SessionStateTransitionPolicyTests` covers only a sample). |
| Creation + team window (domain) | Assert `Create` ⇒ `Scheduled`; team association rejected outside `Scheduled`. |
| No `SessionMode` | Confirm (grep + test) no session-level `SessionMode` type or mode-conditioned branch remains. |
| CoR (application) | Test the ordered `CurrentStateGate → OperatorAssignmentGate → ParticipantReadinessGate` chain + short-circuit + each rejection. |
| Facade (application) | Verify `TransitionSessionStateFacade` runs chain → `MoveTo` → persist; command is `Operator`-authorized. |
| Persistence (infra) | Verify `state`/`last_state_changed_at`/`state_reason` round-trip all six states. **No new migration.** |
| API + broadcast (api) | Verify `PATCH …/state` (valid → 200, invalid → ProblemDetails) and the `SessionStateChanged` SignalR broadcast. |
| Frontend | Verification/cleanup: operator controls match the six states; no residual `SessionMode` copy/type. No contract change. |

### Out of scope for this slice (surface at Stop 1, do not build)

- **First-substage start on `Active`** (AC #3): CONTEXT.md:48 makes it the meaning of entering `Active`, but its implementation (activate the first trivia question / open the substage targets) belongs to HU-33A (DES-78) and the treasure-hunt HUs — downstream and gated on `Active`. The lifecycle already exposes the `ILiveSessionState.Enter(Active)` hook. Do not build substage-play orchestration here.
- **Timer behaviour** in `ActiveLiveSessionState.Enter` / `PausedLiveSessionState.Enter` is HU-22 (DES-77) territory — do not modify or delete.
- **RabbitMQ `SessionStateChanged` audit publish** is HU-21B — do not add.

### Branch state and prerequisite

`feature/hu-21a-session-state-machine-realign` should be branched from `develop`. All build-on dependencies are Done; no same-service predecessor is In Progress.

**Before starting:** confirm (grep) the six-state enum, the `State` classes, the CoR validators, the facade, the endpoint, and the broadcaster are present and that no session-level `SessionMode` type survives.

### Linear state (as of 2026-07-04)

- DES-76 (HU-21A): **Todo**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-22 (HU-15), DES-24 (HU-17), DES-25 (HU-18), DES-26 (HU-19): **Done** (the foundation this slice locks)
- Same-service Canceled (superseded): DES-23, DES-28, DES-30, DES-44

> Linear live state may have changed. Use the Linear MCP to verify DES-76 status and labels if needed, but do not re-fetch PRD scope - read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

---

## 1. Orient - read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only
> if the service source, README, or Linear state may have changed since 2026-07-04.

```text
Read the following and summarise what is already implemented vs. what HU-21A must lock:
- @backend/docs/hu21a-context.md - the pre-resolved HU-21A context (primary)
- @backend/docs/hu15-context.md / @backend/docs/hu17-context.md - what HU-15/HU-17 already shipped (creation-in-Scheduled + SessionMode teardown)
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md - realignment overlay (lifecycle canon delta, :33)
- @backend/services/session-operations-service/CONTEXT.md - §SessionState (:35-60) canonical per-state windows

Then grep the existing session-operations source to confirm:
- SessionState holds exactly the six canonical states; the State classes' CanTransitionTo sets match the canonical matrix
- LiveSession.Create yields Scheduled; EnsureCanAssociateTeam rejects team association outside Scheduled
- the CoR chain CurrentStateGate -> OperatorAssignmentGate -> ParticipantReadinessGate and the PATCH .../state endpoint + SessionStateBroadcaster exist
- no session-level SessionMode type or mode-conditioned branch survives

Then use the Linear MCP to fetch the current live state and labels of DES-76.

Output: what is already correct (and must NOT be rebuilt), and the exact per-layer invariants to lock with tests.
Do not start planning or implementing yet.
```

---

## 2. Label DES-76 as ready-for-agent

```text
Use the Linear MCP to confirm DES-76 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-76 ticket state and labels, including canon-realign and needs-rebuild.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-76 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- exactly the six states Scheduled/Preparing/Active/Paused/Finished/Cancelled
- the canonical transition matrix (allowed edges + terminal + cancel-from-any-non-terminal)
- created in Scheduled; team association only in Scheduled
- invalid transitions rejected with a reason
- no session-level SessionMode logic
- SignalR SessionStateChanged broadcast; no RabbitMQ audit publish (HU-21B)
- Preparing->Active first-substage start is a downstream seam, not built here
- the cycle-1 machine/CoR/facade/endpoint/broadcast are verified and locked, NOT rebuilt

Output the confirmed HU id, title, acceptance criteria, labels, and the guard confirmation before planning the slice.
```

In the remaining steps, `HU-21A` and `DES-76` are the resolved values; `DES-70` is the shared session-operations PRD (local file above, overlaid by the realignment document).

---

## 4. Start the slice

```text
Prepare the session state-machine realignment lock slice on branch feature/hu-21a-session-state-machine-realign.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service and (verification only) frontend.

The pre-resolved orient at the top of this document lists what is already shipped and what
HU-21A locks. Do not re-read the PRD for scoping unless you need a precise implementation detail.

This is a verification slice: lock the canonical state machine, the CoR validators, the facade,
the endpoint, and the SignalR broadcast with explicit tests. Do NOT re-implement the State classes,
the SessionStateTransitionPolicy, the validator chain, the TransitionSessionStateFacade, the
PATCH .../state endpoint, or the broadcaster - they are present and canon-aligned. Do NOT touch the
timer hooks (HU-22/DES-77) or build first-substage orchestration (HU-33A/DES-78).

Move DES-76 to In Progress and output the exact scope, branch name, base branch, and touched surfaces.
```

---

## 5. Backend phase X.1 - Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-21A in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu21a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Domain build passes; a unit test locks the FULL canonical transition matrix (every allowed edge true, every other edge rejected, Finished/Cancelled terminal, Cancelled from any non-terminal)
- Create yields Scheduled; team association rejected outside Scheduled (TeamAssociationRequiresScheduledSessionException); MoveTo raises SessionStateChangedEvent only on an allowed edge
- no session-level SessionMode type remains in the service
- State pattern verified: transitions decided by per-state types (LiveSessionStateFactory), not ad-hoc conditionals

Do not modify the State classes, SessionStateTransitionPolicy, or LiveSession behaviour; do not add substage-start or timer logic. Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-21A)

Ref: HU-21A
Ref: DES-76
Ref: DES-70
```

---

## 6. Backend phase X.2 - Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-21A in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu21a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; a test proves the validator chain runs CurrentStateGate -> OperatorAssignmentGate -> ParticipantReadinessGate in order and short-circuits on the first failure
- each gate's rejection is asserted (InvalidSessionStateTransitionException, SessionOperatorNotAssignedException, LiveSessionRequiresAtLeastOneTeamException)
- the TransitionSessionStateFacade orchestrates chain -> MoveTo -> persist and the command is Operator-authorized
- Chain of Responsibility verified: ordered composable validators, not one collapsed handler

Do not rewrite the chain or the facade. Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-21A)

Ref: HU-21A
Ref: DES-76
Ref: DES-70
```

---

## 7. Backend phase X.3 - Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-21A in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu21a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it, as the block instructs).

Gate:
- Infrastructure build passes
- NO new migration - assert (ef migrations add dry-check or model-snapshot grep) the model already carries the state / last_state_changed_at / state_reason columns and needs no schema change
- a repository integration test round-trips a transitioned session's State, LastStateChangedAt, and StateReason

Do not touch Api or frontend. Do not add an empty migration.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-21A)

Ref: HU-21A
Ref: DES-76
Ref: DES-70
```

---

## 8. Backend phase X.4 - API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-21A in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu21a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration test: a valid transition on PATCH /api/sessions/{liveSessionId}/state -> 200 with previous/next state; an invalid target/edge -> ProblemDetails; Operator-guarded
- hub test: a transition broadcasts SessionStateChanged to the live-session:{id} group with previous/next state
- service coverage reaches the repo gate target (ADR-0005)

Do not touch frontend. Do not reshape the PATCH .../state contract or add a RabbitMQ publish.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-21A)

Ref: HU-21A
Ref: DES-76
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Run curl smoke checks through the gateway for:
- PATCH /api/sessions/{liveSessionId}/state with a valid next state (e.g. a Scheduled session -> Preparing) as an Operator -> expect 200 with previous/next state
- PATCH /api/sessions/{liveSessionId}/state with an invalid edge (e.g. Scheduled -> Finished) -> expect a ProblemDetails rejection
- confirm a connected SignalR client on the live-session:{id} group receives SessionStateChanged on a valid transition

Output:
- container status
- smoke command results
- confirmation the state-transition contract and broadcast are unchanged from cycle 1 (no contract drift for the frontend)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-21A session state-machine controls.

The backend contract is UNCHANGED from cycle 1: PATCH /api/sessions/{liveSessionId}/state takes
a TransitionSessionStateRequest(TargetState, Reason), Operator-only, 200 with previous/next state,
ProblemDetails on an invalid target/edge; valid transitions broadcast SessionStateChanged over the
SessionsHub live-session:{id} group. This is a small verification/cleanup surface — choose the hu-03 exemplar.

Scope:
- confirm the operator session-control UI offers exactly the transitions valid from the current state (Scheduled: Preparing/Cancelled; Preparing: Active/Cancelled; Active: Paused/Finished/Cancelled; Paused: Active/Finished/Cancelled; terminal: none)
- subscribe to the SessionStateChanged SignalR notification and reflect the new state live
- surface the ProblemDetails reason on a rejected transition
- remove or rewrite any residual UI copy/types that mention a session-level SessionMode or a state not in the canonical six

Gate:
- frontend typecheck/build passes
- the operator control flow exercises a valid transition against the (unchanged) API contract, incl. the rejected-edge path, and reflects a live SessionStateChanged broadcast
- Gate: only canonical transitions are offered per current state; no UI type/copy introduces or retains SessionMode or a non-canonical state

Do not modify backend code in this step.
```

**Frontend plan concreteness rule (embed verbatim in the generated plan's altitude choice):**

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only for the **fully-knowable near-term increments** (typically the foundation + first authoring increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): session state-machine controls - HU-21A

Ref: HU-21A
Ref: DES-76
Ref: DES-70
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of
> truth and supersedes the Step 9 seed scope — including Step 9's single seed commit:
> commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement the Step 9 plan at @frontend/plans/hu-21a-frontend-session-state-machine-controls.md,
phase by phase per the plan's own Scope / Gate / Commit Sequence.

For each phase: implement only that phase, run its Gate (build + typecheck, plus any e2e the
phase lands), then commit with the exact subject from the plan's Commit Sequence for that phase.

STOP at any increment the plan marks blocked on an Open Question (name it). Do not invent the
blocked behaviour; surface the question and wait.

Do not re-generate the plan. Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-76 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- exactly the six states Scheduled/Preparing/Active/Paused/Finished/Cancelled
- the canonical transition matrix (allowed edges + terminal + cancel-from-any-non-terminal)
- created in Scheduled; team association only in Scheduled
- invalid transitions rejected with a reason
- no session-level SessionMode logic
- SignalR SessionStateChanged broadcast; no RabbitMQ audit publish
- Preparing->Active first-substage start acknowledged as a downstream seam, not built here

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- the per-layer invariant tests added (full transition matrix, creation-in-Scheduled, team-window, CoR ordering, endpoint + broadcast)
- tests and gates run (incl. ADR-0005 coverage)
- confirmation the canonical state machine is locked and DES-77 (timer) / DES-78 (trivia round) can rebuild on it

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-21a-session-state-machine-realign \
  --title "feat(session-operations): lock canonical session state machine (HU-21A)" \
  --body "Locks DES-76/HU-21A: the LiveSession lifecycle exposes exactly Scheduled -> Preparing -> Active -> Paused -> Finished -> Cancelled, created in Scheduled with team association allowed only while Scheduled, invalid transitions rejected, and valid transitions broadcast live via SignalR. Adds per-layer verification tests over the cycle-1 State machine (full transition matrix), the Chain of Responsibility validators, the TransitionSessionStateFacade, the PATCH /api/sessions/{id}/state endpoint, and the SessionStateChanged broadcast, and confirms no session-level SessionMode branching survives (HU-17 teardown). No new aggregate, endpoint, or migration; first-substage start (HU-33A/DES-78), the timer (HU-22/DES-77), and the RabbitMQ audit event (HU-21B) are out of scope."
```

---

## Rationale

DES-76's acceptance criteria (exactly six states; created in `Scheduled`; team association only in `Scheduled`; `Preparing → Active` starts the first substage; reject invalid transitions; no `SessionMode` logic) are the **canonical lifecycle**. After the 2026-06-16 mission-runtime rewrite the pieces landed across three tickets: the cycle-1 DES-28 built the `State`-pattern machine and the CoR validators with the six states already present; HU-15 (DES-22) made creation happen in `Scheduled` with an immutable snapshot; HU-17 (DES-24) removed the session-level `SessionMode`. So most of DES-76's acceptance is already an emergent property of code on `develop`. HU-21A exists to make that property **explicit, tested, and structurally permanent** — it converts an emergent guarantee into a locked transition matrix and confirms no mode branching survived, so DES-77 (timer) and DES-78 (trivia round) can safely rebuild on the lifecycle.

Both mandated patterns (`State`, `Chain of Responsibility`) are already realized and are carried as explicit X.1/X.2 gates — verifying them, not inventing them. Two acceptance items are deliberately **not** built here and are surfaced at Stop 1: AC #3's "`Preparing → Active` starts the first substage immediately" is the *meaning* of entering `Active` (CONTEXT.md:48), but its *implementation* (activate the first trivia question / open the substage targets) is owned by the downstream play HUs (HU-33A/DES-78, treasure-hunt HU-29–32), which are gated on `Active` and later in the realignment order — the lifecycle already exposes the `Enter(Active)` hook they extend. Likewise the timer behaviour in the `Active`/`Paused` state entries belongs to HU-22 (DES-77), and the RabbitMQ audit publish to HU-21B. Building any of those here would trespass the realignment order and duplicate a peer rebuild.
