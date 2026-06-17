# Prompt Example — HU-22 Authoritative Session Timer (Feature Slice)

> Superseded on 2026-06-16 by
> `backend/docs/grilling-session-mission-restructure.md`,
> `backend/docs/ddd_solution_model.md`, and
> `backend/docs/bd_umbral_entity_spec.md`.
> Do not drive this prompt as written if it assumes session-level trivia mode or
> `Scheduled`. Rebuild the timer slice around mission snapshots, canonical
> session states, and substage-specific timer behavior.

Concrete prompt sequence for driving HU-22 through a full feature slice on
`feature/hu-22-temporizador-autoritativo-de-sesion`. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md). Context:
[hu22-context.md](./hu22-context.md).

**Key difference from HU-21A:** HU-21A established the authoritative lifecycle
state machine and live state broadcast. HU-22 does not add a second state model;
it extends that baseline so remaining time becomes backend-authoritative,
state-aware (`Active` runs, `Paused` freezes), reconnect-safe, and pushed live
over SignalR. The clock belongs to `SessionOperations`, not to the client.

Drive each backend phase with `@backend/.agents/driver-agent.md`, selecting
phases in order: **X.1 -> X.2 -> X.3 -> X.4**. The driver delegates
implementation to `@backend/.agents/backend-agent.md`; do not invoke it
directly. For the frontend slice, use Step 9 directly with `@frontend/AGENTS.md`.
Do not mix backend and frontend work in the same phase.

---

## Required design patterns

- `State`
  - Why: timer behavior depends on session state (`Active` vs `Paused`), and the
    remaining time must come from the service's authoritative runtime model.
  - Phase owner: **X.1 Domain**
  - Gate obligation: timer behavior is expressed through the `LiveSession` state
    model — `Active` decrements, `Paused` freezes, resume continues from the
    frozen remainder, and reconnect reads the backend-owned remainder. No
    client-owned source of truth and no ad-hoc state `if` checks scattered
    across handlers, endpoints, or hubs.

Transport note (mandated, NOT optional): HU-22 carries **SignalR / WebSockets**
from the patterns matrix. Remaining time must be pushed live to connected
clients through the existing `SessionsHub`/real-time surface. SignalR is a hard
gate for this slice.

---

## Pre-resolved orient (as of 2026-06-04)

> Step 1 has already been run. Paste this section into any agent session that
> needs context before picking up a phase; no need to re-run the orient prompt
> unless local docs or Linear state changed.

### What has already landed and must be reused

**session-operations-service**
- `LiveSession` is already the authoritative runtime aggregate root.
- HU-21A already established the lifecycle state machine for `Scheduled`,
  `Preparing`, `Active`, `Paused`, `Finished`, `Cancelled`; HU-22 must derive
  timer behavior from that model instead of introducing a parallel state flow.
- HU-21A already established transition orchestration and live state broadcast.
- HU-19 already established the authorization seam for protected session actions.
- HU-07B already established reconnect/runtime restoration precedent in
  `session-operations-service`.
- `SessionsHub` already exists and is the reuse target for live timer updates.

**What HU-22 adds**

| Concern | New work |
| --- | --- |
| Domain | Backend-owned authoritative timer state on `LiveSession` (or its trivia runtime sub-state), including remaining-time calculation, pause freeze, resume, reconnect snapshot, and expiry semantics. |
| Application | Timer read/query and orchestration that extend reconnect and state-transition flows with authoritative timer behavior, without moving timer math into endpoints or hubs. |
| Infrastructure | Clock/tick driver plus SignalR broadcaster for timer updates; persistence of any new timer fields; integration coverage for pause/resume/reconnect timing behavior. |
| API | Participant-facing timer snapshot/read surface plus verified SignalR timer notification shape. |
| Frontend | Participant timer display driven by backend snapshots and SignalR updates. |

### Branch state and prerequisite

`feature/hu-22-temporizador-autoritativo-de-sesion` branches from
**`feature/hu-18-asociacion-de-equipos-a-sesiones`** because `DES-25` (HU-18)
is the most recent same-service predecessor still `In Progress`. The direct
functional blocker for HU-22 is HU-21A, which is already `Done`.

### Linear state

- HU ticket: `DES-30` — **Todo**, labels `Feature`,
  `svc:session-operations-service`, `ready-for-agent`
- Predecessor state worth re-checking before implementation:
  - `DES-28` (HU-21A) — Done
  - `DES-25` (HU-18) — In Progress
- PRD ref: `DES-70`, local file
  `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`

---

## 1. Orient — read current service state

> Skip this step if you have read the pre-resolved orient above and the local
> docs are unchanged.

```text
Read the following and summarise what is already decided:
- @backend/docs/hu22-context.md
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md
- @backend/docs/trivia_sprint_required_patterns_matrix.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Application/Sessions/Commands/TransitionSessionState/TransitionSessionStateFacade.cs
- @backend/services/session-operations-service/src/Api/Hubs/SessionsHub.cs

Then use the Linear MCP to fetch only the current live state of:
- DES-30 (HU-22 — Temporizador autoritativo de sesion) — status and labels
- DES-28 (HU-21A — predecessor) — status
- DES-25 (HU-18 — in-progress branch base) — status
- DES-70 (session-operations PRD) — status and labels

Output:
- what HU-07B, HU-19, and HU-21A already landed that HU-22 must reuse
- that `SessionOperations` owns the timer authority, not the client
- whether the current runtime model already contains timer-related persistence
  fields or whether HU-22 will need additive storage
- the resolved HU id, PRD id, status, and labels

Do not start planning or implementing yet.
```

---

## 2. Confirm `ready-for-agent` label

```text
Use the Linear MCP to confirm that DES-30 carries the label ready-for-agent.
If it has been removed, add it back. Output the updated ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-30 carries both
svc:session-operations-service and ready-for-agent, and output its current
status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
Do not re-fetch the PRD scope from Linear.

Output the confirmed HU id, title, acceptance criteria, labels, and PRD ref
before planning the slice.
```

In the remaining steps below, `HU-22`, `DES-30`, and `DES-70` are the resolved
references for this slice.

---

## 4. Start the slice

```text
Prepare the authoritative session timer slice on branch
feature/hu-22-temporizador-autoritativo-de-sesion, based on
feature/hu-18-asociacion-de-equipos-a-sesiones.
Use the resolved HU id (DES-30) and PRD id (DES-70).
This slice affects session-operations-service primarily and frontend as the
participant timer consumer.

Before implementation, confirm the baseline:
- HU-21A's state machine already exists and must be reused
- SessionsHub already exists for live runtime broadcast
- reconnect/runtime restoration already exists from HU-07B and is the seam for
  timer snapshot recovery
- HU-22 is the authoritative timer slice, not the full trivia round-orchestration
  slice

Move DES-30 to In Progress and output the exact scope, branch name, and touched
surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

> Run `@backend/.agents/driver-agent.md` and select **X.1** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-22 in session-operations-service.

Before writing anything, inspect and extend rather than recreate:
- LiveSession aggregate
- HU-21A's explicit session-state model
- existing domain events around state changes
- any existing timing fields or value objects already persisted on LiveSession

Scope:
- model the authoritative timer as session-owned domain state: it must be able
  to represent total duration, the current remaining time, whether the timer is
  currently advancing, and the data needed to freeze/resume correctly
- add domain behavior for timer lifecycle tied to the session state model:
  start/arm for the active trivia runtime, snapshot remaining time at any point,
  freeze on pause, resume from the frozen remainder, and mark expiry when the
  authoritative remaining time reaches zero
- make reconnect-safe timer reads a domain operation, so the application layer
  can ask the aggregate for the current authoritative remainder without
  duplicating timer math
- State obligation (mandated): express timer behavior through the existing state
  abstraction. `Active` runs the timer, `Paused` freezes it, and resumed
  `Active` continues from the frozen remainder. Do not introduce ad-hoc timer
  state checks outside the domain model.
- if the current model distinguishes between session-level timer and
  trivia-question timer, keep the abstraction explicit and do not collapse them
  into a vague generic clock

Gate:
- Domain build passes
- new invariants are unit-tested: active countdown, pause freeze, resume from
  remaining time, reconnect snapshot, expiry behavior
- State gate: timer behavior is enforced through the `LiveSession` state model
  with no ad-hoc state/time `if` chains outside the domain abstraction
- existing HU-21A lifecycle invariants are not broken

Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 — domain layer (HU-22)

Ref: HU-22
Ref: DES-30
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

> Run `@backend/.agents/driver-agent.md` and select **X.2** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-22 in session-operations-service.

Before writing anything, inspect the current application baseline and extend it:
- reconnect application flow from HU-07B
- transition/state orchestration from HU-21A
- current query/DTO patterns for session read surfaces

Scope:
- add a timer read/query surface that returns the authoritative remaining time
  and timer status for the relevant participant session/question context
- extend reconnect/runtime restoration so the participant receives the current
  authoritative timer snapshot when resuming or reconnecting
- extend pause/resume and other relevant state-transition orchestration so timer
  freeze/resume behavior is applied through the application flow that already
  owns those state changes
- keep timer math out of endpoints and hubs; application delegates calculation
  to the domain and coordinates persistence + notifications only
- handler/unit tests for:
  - active timer snapshot returned correctly
  - paused timer snapshot remains frozen
  - resumed timer snapshot continues from the frozen remainder
  - reconnect flow includes the current authoritative timer data
  - expired timer path is surfaced coherently for downstream orchestration

Gate:
- clean build passes
- handler/unit tests pass for all listed paths
- reconnect flow returns backend-authored timer data rather than trusting client
  state
- no ad-hoc timer math leaks into handlers that should live in the domain

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 — application layer (HU-22)

Ref: HU-22
Ref: DES-30
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

> Run `@backend/.agents/driver-agent.md` and select **X.3** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-22 in session-operations-service.

Scope:
- add the infrastructure needed to drive the authoritative timer over real time:
  a clock/tick mechanism or equivalent infrastructure that can evaluate active
  timers and trigger periodic updates plus expiry
- persist any new timer fields required by the domain model; if the current
  schema already contains sufficient timing columns, explicitly confirm that no
  migration is required
- extend the existing SessionsHub broadcasting path with a timer notification
  (for example `SessionTimerUpdated`) that includes at minimum:
  - LiveSessionId
  - RemainingMilliseconds or equivalent remaining-time value
  - IsPaused
  - EmittedAt
- ensure reconnect/runtime restoration can read the persisted timer state after
  process restart or client reconnect
- integration tests:
  - authoritative timer round-trips through EF
  - paused timer remains frozen across persistence boundaries
  - resumed timer continues from the persisted frozen remainder
  - SignalR timer update is broadcast to a connected client

Gate:
- dotnet build passes on the solution
- migration is confirmed no-op or created only if the model genuinely needs new
  persisted timer fields
- repository integration tests prove timer-state persistence
- SignalR integration test proves timer updates are pushed through the existing
  hub

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 — infrastructure layer (HU-22)

Ref: HU-22
Ref: DES-30
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

> Run `@backend/.agents/driver-agent.md` and select **X.4** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-22 in session-operations-service.

Scope:
- expose the participant-facing timer snapshot/read contract through the
  existing session runtime surface, for example a timer endpoint and/or an
  extension of the reconnect/session-context response
- keep the endpoint thin: bind the request, delegate to the timer query or
  reconnect flow, and return the authoritative timer snapshot from backend state
- confirm the existing SessionsHub is registered and the timer broadcaster from
  X.3 is wired so connected clients receive live timer updates
- endpoint/integration tests through the service host:
  - participant gets the correct remaining time for an active trivia session/question
  - paused session/question returns a frozen remaining time
  - resumed session/question returns the updated remaining time
  - reconnect returns the current authoritative timer snapshot
  - connected SignalR client receives live timer updates

Gate:
- endpoint/integration tests pass for active, paused, resumed, and reconnect
  timer paths
- SignalR integration test proves the timer notification reaches a connected
  client
- service coverage reaches the enforced ADR-0005 threshold

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 — api layer (HU-22)

Ref: HU-22
Ref: DES-30
Ref: DES-70
```

---

## 8.5. Docker rebuild + smoke

```text
From backend/, rebuild and start the stack for manual verification:

1. docker compose build session-operations-service && docker compose up -d session-operations-service
2. docker compose build api-gateway && docker compose up -d api-gateway
3. Ensure a trivia LiveSession exists and can be moved into an active runtime
   state using the HU-21A transition path.
4. Connect a client to the existing SessionsHub and observe timer updates.
5. Fetch the timer snapshot from the verified API/reconnect surface and confirm
   the remaining time matches what SignalR is broadcasting.
6. Pause the session through the HU-21A transition path; confirm the remaining
   time freezes and subsequent timer updates stop changing while paused.
7. Resume the session; confirm the timer continues from the frozen remainder,
   not from the original total duration.
8. Reconnect the participant client and confirm the resumed snapshot returns the
   current authoritative remaining time.
```

**Gate:** the running stack proves authoritative timer reads, pause freeze,
resume from the frozen remainder, reconnect snapshot recovery, and live SignalR
timer push.

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in
@frontend/plans/hu-03-frontend-role-permission-assignment.md, save it in
@frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Implement the participant-facing authoritative timer UI for HU-22 using the
verified session-operations contract and SignalR timer updates.

Scope:
- show the participant the current authoritative remaining time for the active
  trivia question/session context
- initialize the timer from the backend snapshot returned by the verified API or
  reconnect contract
- subscribe to the SignalR timer notification and keep the UI in sync with live
  updates from the backend
- render pause/freeze behavior correctly: when the session is paused, the shown
  remaining time must stop changing until resume
- on reconnect/resume, refresh from the latest backend snapshot rather than
  trusting stale local timer state
- keep this slice focused on timer display/sync; do not expand into full trivia
  round orchestration or answer-submission UX

Gate:
- the UI shows the current authoritative remaining time
- live SignalR updates visibly refresh the timer without manual reload
- pause stops the visible countdown and resume restarts it from the frozen
  remainder
- reconnect restores the correct current time from the backend snapshot
- the UI handles missing/expired timer context cleanly instead of showing stale
  client-side time
```

Commit:

```text
feat(frontend): authoritative session timer — HU-22

Ref: HU-22
Ref: DES-30
Ref: DES-70
```

---

## 10. Close-out

```text
Before opening the PR:
- confirm all four backend phase commits exist
- confirm the timer smoke path was exercised: active timer, paused freeze,
  resumed countdown, reconnect snapshot recovery
- confirm a connected SignalR client received live timer updates
- confirm the frontend shows the authoritative timer and restores it correctly
  after reconnect
- map each acceptance criterion to where it is enforced:
  AC#1 in trivia sessions, the system shows the remaining time of the active
       question -> timer read contract + SignalR timer updates
  AC#2 when the session is paused, the timer freezes -> domain timer state +
       pause/resume tests
  AC#3 on resume or reconnect, the system shows the correct current time ->
       reconnect snapshot/read flow + integration tests
  AC#4 remaining time is emitted to clients in real time via SignalR/WebSockets
       -> existing SessionsHub timer broadcaster + connected-client tests

Then open the PR:
gh pr create --draft --base feature/hu-18-asociacion-de-equipos-a-sesiones \
  --title "feat: authoritative session timer — HU-22" \
  --body "Closes DES-30
Ref: DES-70

Touched: backend/services/session-operations-service/, frontend/"
```

## Rationale

**HU-22 is an extension of HU-21A's state machine, not a replacement for it.**
The `State` pattern is mandatory because the timer only makes sense relative to
the lifecycle already modeled in HU-21A: `Active` runs, `Paused` freezes, and
reconnect reads the backend-owned remainder. A timer implemented as a client
clock or a detached background stopwatch would violate that ownership model.

**SignalR is a hard gate because this is live runtime, not a read-once value.**
The patterns matrix explicitly tags HU-22 with SignalR/WebSockets. Participants
must see the authoritative remaining time update in real time, and the existing
`SessionsHub` from HU-07B/HU-21A is the reuse target.

**There is one scope ambiguity and it should stay explicit.** The broader user
story language mentions mission-session and trivia-question countdowns, while
the `DES-30` acceptance criteria are explicitly trivia-focused. The safe reading
for this slice is: implement the authoritative timer behavior required by the
HU, keep the abstraction honest if the domain model distinguishes session-level
and question-level timers, and do not silently broaden into a full mission-timer
or round-orchestration rewrite unless the canon docs are updated together.
