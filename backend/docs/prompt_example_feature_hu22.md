# Prompt Example — HU-22 Authoritative timer realign (Feature Slice)

Concrete prompt sequence for driving DES-77 (HU-22) through a full slice on `feature/hu-22-authoritative-timer-substage-realign`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for the DES-77 slice:** this is a **genuine rebuild**, not a verification lock like its sibling HU-21A. Cycle 1 (DES-30, now **Canceled**) made a **whole-session `MaximumTime` countdown** the authoritative "remaining time" and ran a separate per-question timer in parallel — the pre-canon "mission session vs trivia session" split. Canon keeps **only** the trivia `TriviaQuestionTimer` (`CONTEXT.md:125`) as an authoritative runtime clock: there is **no** session/mission-level countdown, and treasure-hunt substages advance by target resolution, not a timer. HU-22 must therefore **derive the displayed remaining time from the active substage's `SubstagePlayMode`** (AC #1), **delete** the whole-session countdown (AC #2), and **keep** the pause-freeze / trivia-resume-same-question behaviour (AC #3). That is a delete + redefine + migration + API-contract change across all four layers.

> **✅ The three former open decisions (OD-1/2/3) are RESOLVED and committed to scope (2026-07-05).** The `canon-realignment-workflow.md` open-decisions gate is satisfied; the resolutions (OD-1 = no treasure-hunt countdown; OD-2 = dispatch on `ActiveQuestionIndex`; OD-3 = redefine `RemainingSeconds` in place + drop `_sessionTimer*`) are baked into the Stop 1 guard and the Rationale. Step 5 (X.1) may start once Stop 1 confirms the slice is grabbed.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps, point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in the same phase prompt; coordinate them as separate scoped steps.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- the displayed remaining time is **derived from the active substage's `SubstagePlayMode`**: a trivia substage → the active `TriviaQuestionTimer` window; a treasure-hunt substage → **no authoritative countdown** (OD-1)
- **no** session-level `SessionMode` logic **and no** whole-session `MaximumTime` authoritative countdown survive
- pause **freezes** the active-substage timer; paused **trivia** resumes on the **same** question
- the authoritative remaining time is pushed live over SignalR (`SessionTimerUpdated`) and returned on the timer read + reconnect surface; **no** RabbitMQ
- the `State` pattern owns the advancing-vs-frozen decision (per-`SessionState` type via `LiveSessionStateFactory`), re-pointed at the substage-derived timer — not ad-hoc conditionals
- question **activation / advancement / close** (`ActivateQuestion`, `TriviaRoundOrchestratorFacade`) is acknowledged as a **downstream** DES-78 seam, **not** built or expanded here

**These three decisions are RESOLVED and committed to scope** (2026-07-05; rationale in the Rationale section) — carry them forward as fixed scope, not as a gate:
- **OD-1 — RESOLVED:** no treasure-hunt countdown (elapsed `ResolutionTime` only if the frontend needs a figure).
- **OD-2 — RESOLVED:** dispatch on `ActiveQuestionIndex` present (trivia window) vs absent (no countdown); do not build advancement (DES-78).
- **OD-3 — RESOLVED:** redefine `SessionTimerSnapshotDto.RemainingSeconds` in place as the active-substage window + drop the `_sessionTimer*` columns; keep the `MaximumTime` VO/column as authoring metadata.

---

## Required design patterns

- **`State` (mandated, X.1 Domain)** — `required_patterns_matrix.md:41,110`. The advancing-vs-frozen decision is owned by the per-`SessionState` type (`Active` advances, `Paused` freezes) via `LiveSessionStateFactory.For(State)`. Already realized; HU-22 **re-points** it at the substage-derived (trivia-question) timer and **removes** the whole-session-timer hooks. Do not replace with `if (State == …)` conditionals.
- **Transport: SignalR (mandated)** — `required_patterns_matrix.md:57,110`. The substage-derived remaining time broadcasts live as `SessionTimerUpdated` to the `live-session:{id}` group. **No RabbitMQ** in this slice.

> Applies-where note (no new gate): `GET /api/sessions/{id}/timer` and `.../participants/timer` are protected reads, but HU-22 is not matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). They inherit the standard operator / participant-membership `AuthorizationBehaviour`/gateway guard (ADR-0001/0002) — note only, no new `Proxy` gate.

---

## Pre-resolved orient (as of 2026-07-05)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

DES-77 (HU-22) is **Todo**, a `needs-rebuild` row in the realignment map (phase #6 "Lifecycle": "Authoritative timer keyed off active `SubstagePlayMode`"), and the **rebuild successor of the Canceled DES-30**. Its build-on predecessors are all Done/merged:

- **DES-22 (HU-15)** — the immutable `MissionRuntimeSnapshot` (`LiveSession.cs:64`) carrying each `SubstageSnapshot.PlayMode` and the `TriviaQuestionSnapshot`s (`TimeLimitSeconds`) the trivia timer reads.
- **DES-24 (HU-17)** — deleted the session-level `SessionMode` enum (AC #2 at the type level; HU-22 removes its surviving *behavioural* form, the whole-session countdown).
- **DES-76 (HU-21A)** — locked the canonical state machine and the `State`-pattern `Enter` hooks, explicitly leaving the timer behaviour in `Active`/`Paused` `Enter` to HU-22.

Light build-on: DES-75 (HU-16, trivia questions in the snapshot), DES-12 (HU-07B, reconnect delivery). Landed-untouched: DES-25 (HU-18), DES-26 (HU-19), DES-11 (HU-07A). Superseded/excluded: DES-30 (HU-22 cycle-1, Canceled — the reuse candidate). **Downstream, do NOT trespass:** DES-78 (HU-33A trivia round) owns question activation/advancement/close. No same-service In Progress predecessor → branch base is `develop`.

### What HU-22 adds (per DES-77, the realignment map, and PRD DES-70)

| Concern | New work |
|---|---|
| Substage-mode-derived timer | Displayed remaining time = the active substage's timer: trivia → active `TriviaQuestionTimer` window; treasure-hunt → no countdown (OD-1). |
| Remove whole-session countdown | Delete `_sessionTimer*` (`MaximumTime` countdown), its `State` hooks, worker tick, repo predicate, and columns; retire it as the DTO's primary remaining time (OD-3). |
| Pause / resume (domain) | Verify + keep: pause freezes the active-substage timer; paused trivia resumes the **same** question. |
| Timer DTO + queries (app) | Redefine `SessionTimerSnapshotDto` remaining = active-substage window; re-point the operator + participant timer queries. |
| Worker + migration (infra) | Drop the session tick + `_sessionTimer*` columns (migration); select by advancing **question** timer. |
| Endpoints + broadcast (api) | Two timer GETs + transition `Timer` field return the substage-derived remaining; `SessionTimerUpdated` broadcast verified. |
| Frontend | Primary remaining time tracks the active trivia question; treasure-hunt shows no countdown (pending OD-1). Contract change — call it out. |

### Out of scope for this slice (surface at Stop 1, do not build)

- **Question activation / advancement / close** and **trivia round orchestration** (`ActivateQuestion`, `CloseActiveQuestion`, `TriviaRoundOrchestratorFacade`, `SequentialQuestionActivationStrategy`, `QuestionActivated/ClosedEvent`) — DES-78 (HU-33A) territory. HU-22 owns the timer **window** (freeze/resume/remaining) + the authoritative snapshot/broadcast only; leave DES-78's seam **unchanged**.
- **Treasure-hunt target resolution / substage advancement** — HU-29–32 territory.
- **A per-treasure-hunt-substage authored duration** — not in canon; would need an ADR + snapshot field (OD-1). Do not invent it.

### Branch state and prerequisite

`feature/hu-22-authoritative-timer-substage-realign` branches from `develop`. All build-on dependencies (HU-15/17/21A) are Done/merged; no same-service predecessor is In Progress.

**Before starting:** confirm (grep) that `_sessionTimer*` is the whole-session countdown, that the `_questionTimer*` window + `EnterPaused/ActiveQuestionTimerState` freeze/resume exist, that `SubstageSnapshot.PlayMode` + `TriviaQuestionSnapshot.TimeLimitSeconds` are present, and that no session-level `SessionMode` survives. **OD-1/2/3 are already resolved (committed scope) — build to them.**

### Linear state (as of 2026-07-05)

- DES-77 (HU-22): **Todo**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature`, `ready-for-agent`
- DES-22 (HU-15), DES-24 (HU-17), DES-76 (HU-21A): **Done** (the foundation this slice rebuilds on)
- DES-78 (HU-33A trivia round): **Todo, downstream** — do not trespass
- Same-service Canceled (superseded): DES-23, DES-28, DES-30, DES-44

> Linear live state may have changed. Use the Linear MCP to verify DES-77 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

---

## 1. Orient — read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-05.

```text
Read the following and summarise what is implemented today vs. what HU-22 must rebuild:
- @backend/docs/hu22-context.md — the pre-resolved HU-22 context (primary), incl. OD-1/2/3
- @backend/docs/hu21a-context.md — the sibling lifecycle realign this timer builds on (Active/Paused Enter hooks)
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md — US15/US16
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md — realignment overlay (phase #6, :102)
- @backend/services/session-operations-service/CONTEXT.md — §TriviaQuestionTimer (:125-138), §Paused (:52)

Then grep the existing session-operations source to confirm:
- LiveSession carries a whole-session _sessionTimer* countdown (from MaximumTime) AND a _questionTimer* window
- ActiveLiveSessionState.Enter / PausedLiveSessionState.Enter drive both timers; the question timer freezes on pause and resumes the same ActiveQuestionIndex
- SubstageSnapshot.PlayMode and TriviaQuestionSnapshot.TimeLimitSeconds exist on the runtime snapshot
- no session-level SessionMode type survives; question activation/advancement lives in the DES-78 trivia-round code

Then use the Linear MCP to fetch the current live state and labels of DES-77.

Output: what is canon-aligned (keep), what is the pre-canon whole-session countdown (delete), and the exact per-layer changes — plus a crisp statement of OD-1/2/3.
Do not start planning or implementing yet.
```

---

## 2. Label DES-77 as ready-for-agent

```text
Use the Linear MCP to confirm DES-77 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-77 ticket state and labels, including canon-realign and needs-rebuild.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-77 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- displayed remaining time derived from the active substage's SubstagePlayMode (trivia -> question window; treasure-hunt -> no countdown, OD-1)
- no session-level SessionMode logic and no whole-session MaximumTime countdown
- pause freezes; paused trivia resumes the same question
- authoritative remaining pushed via SignalR (SessionTimerUpdated) + returned on timer read/reconnect; no RabbitMQ
- State pattern owns advancing/frozen, re-pointed at the substage-derived timer
- question activation/advancement (DES-78) is a downstream seam, not built here

Then acknowledge OD-1/2/3 as already resolved and committed to scope (see the Rationale for the resolutions) — they are no longer a blocking gate; carry them forward as fixed scope.

Output the confirmed HU id, title, acceptance criteria, labels, the guard confirmation, and the OD resolutions before planning the slice.
```

In the remaining steps, `HU-22` and `DES-77` are the resolved values; `DES-70` is the shared session-operations PRD (local file above, overlaid by the realignment document).

---

## 4. Start the slice

```text
Prepare the authoritative-timer realignment slice on branch feature/hu-22-authoritative-timer-substage-realign.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service and frontend.

The pre-resolved orient at the top of this document lists what is canon-aligned (keep) and what is the
pre-canon whole-session countdown (delete). Do not re-read the PRD for scoping unless you need a precise detail.

This is a genuine rebuild: the displayed remaining time must derive from the active substage's SubstagePlayMode,
the whole-session MaximumTime countdown must be deleted (fields, State hooks, worker tick, repo predicate, columns),
and the pause-freeze / trivia-resume-same-question behaviour kept. Do NOT touch question activation/advancement or
the TriviaRoundOrchestratorFacade (DES-78). Do NOT build treasure-hunt target resolution or substage advancement.

OD-1/2/3 are already resolved (committed scope) — build to them. Move DES-77 to In Progress and output the exact scope, branch name,
base branch (develop), and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-22 in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu22-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open). Apply the OD-1/2/3 resolutions from Stop 1.

Gate:
- Domain build passes; a unit test locks: authoritative remaining time = the active TriviaQuestionTimer window when a trivia question is active, and no advancing countdown otherwise (OD-1)
- Paused freezes and Active resumes the SAME question at the frozen remainder
- the whole-session MaximumTime countdown (_sessionTimer*) is gone; no session-level SessionMode remains
- State pattern verified: advancing/frozen decided by per-state types via LiveSessionStateFactory, not ad-hoc conditionals

Do not touch question activation/advancement (ActivateQuestion/CloseActiveQuestion — DES-78). Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-22)

Ref: HU-22
Ref: DES-77
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-22 in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu22-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; a test proves the timer DTO/queries (operator + participant) return the active-substage (trivia-question) remaining time
- ActiveQuestion is populated only when a question is active; remaining is 0/absent otherwise
- no reference to a whole-session countdown remains; participant/operator authorization preserved

Do not touch the TriviaRoundOrchestratorFacade or question orchestration (DES-78). Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-22)

Ref: HU-22
Ref: DES-77
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-22 in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu22-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it).

Gate:
- Infrastructure build passes
- a migration DROPS the _sessionTimer* columns (assert via model snapshot); no whole-session-timer schema remains
- ListActiveTimersAsync selects sessions by advancing question timer (not _sessionTimer*)
- a repository integration test round-trips a session with an active-question timer

Leave the worker's question close/advance call to the DES-78 facade unchanged; only remove the session-timer tick. Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-22)

Ref: HU-22
Ref: DES-77
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-22 in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu22-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- endpoint integration tests: GET /api/sessions/{id}/timer (operator) and GET /api/sessions/{id}/participants/timer (participant) return the active-substage remaining (active question -> window; none -> absent/0); authorization enforced
- hub test: a substage-derived SessionTimerUpdated broadcast reaches the live-session:{id} group
- service coverage reaches the repo gate target (ADR-0005)

Do not reshape the endpoint routes; re-point the payload meaning only. Do not add a RabbitMQ publish. Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-22)

Ref: HU-22
Ref: DES-77
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Run curl smoke checks through the gateway for:
- GET /api/sessions/{liveSessionId}/timer as an Operator -> 200; with an active trivia question, RemainingSeconds tracks the question window; with no active question, remaining is 0/absent (no whole-session countdown)
- GET /api/sessions/{liveSessionId}/participants/timer as a participant -> 200 with the same substage-derived remaining
- pause the session (PATCH .../state -> Paused), re-read the timer -> remaining is frozen; resume (-> Active) -> the same question continues from the frozen remainder
- confirm a connected SignalR client on the live-session:{id} group receives SessionTimerUpdated with the substage-derived remaining

Output:
- container status
- smoke command results
- confirmation the remaining-time now tracks the active substage (NOT a whole-session countdown) — this is a contract change for the frontend
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-22 authoritative session timer.

The backend contract CHANGES in this slice: GET /api/sessions/{liveSessionId}/timer and
.../participants/timer return SessionTimerSnapshotDto whose RemainingSeconds/TotalSeconds now track the
ACTIVE SUBSTAGE's timer — the active trivia question window — not a whole-session countdown; a treasure-hunt
substage (or no active question) returns no advancing countdown (OD-1). The nested ActiveQuestion carries the
question window. Live updates arrive as SessionTimerUpdated over the SessionsHub live-session:{id} group; the
timer freezes on pause and recovers on resume/reconnect. This is a focused ~2-3 endpoint surface — choose the hu-03 exemplar.

Scope:
- render the authoritative remaining time from the timer read + SessionTimerUpdated, for the active trivia question
- freeze the displayed timer on pause and recover the correct remainder on resume/reconnect (do not run a client-owned clock as the source of truth)
- for a treasure-hunt substage (no active question), show no countdown (per the OD-1 resolution) — surface elapsed time only if that was chosen
- remove any UI copy/type that assumes a whole-session/mission countdown or a session-level SessionMode

Gate:
- frontend typecheck/build passes
- the timer UI reflects the active-question remaining from the (changed) contract, freezes on pause, and recovers on resume/reconnect against a live SessionTimerUpdated broadcast
- Gate: no UI type/copy retains a whole-session countdown or a session-level SessionMode

Do not modify backend code in this step.
```

**Frontend plan concreteness rule (embed verbatim in the generated plan's altitude choice):**

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only for the **fully-knowable near-term increments** (typically the foundation + first authoring increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): authoritative session timer — HU-22

Ref: HU-22
Ref: DES-77
Ref: DES-70
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of truth and supersedes the Step 9 seed scope — including Step 9's single seed commit: commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement the Step 9 plan at @frontend/plans/hu-22-frontend-authoritative-session-timer.md,
phase by phase per the plan's own Scope / Gate / Commit Sequence.

For each phase: implement only that phase, run its Gate (build + typecheck, plus any e2e the phase lands),
then commit with the exact subject from the plan's Commit Sequence for that phase.

STOP at any increment the plan marks blocked on an Open Question (name it) — e.g. the treasure-hunt display
if OD-1 is still open. Do not invent the blocked behaviour; surface the question and wait.

Do not re-generate the plan. Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-77 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- displayed remaining time derived from the active substage's SubstagePlayMode (trivia window; treasure-hunt no countdown, OD-1)
- no session-level SessionMode and no whole-session MaximumTime countdown
- pause freezes; paused trivia resumes the same question
- SignalR SessionTimerUpdated broadcast + timer read/reconnect surface; no RabbitMQ
- question activation/advancement (DES-78) not built here

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- the timer behaviour rebuilt (substage-derived remaining, whole-session countdown removed, pause/resume) and the migration dropping the _sessionTimer* columns
- tests and gates run (incl. ADR-0005 coverage)
- confirmation the remaining-time contract change is documented for the frontend

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-22-authoritative-timer-substage-realign \
  --title "feat(session-operations): authoritative timer keyed off the active SubstagePlayMode (HU-22)" \
  --body "Rebuilds DES-77/HU-22: the authoritative displayed remaining time is now derived from the active substage's SubstagePlayMode — a trivia substage exposes the active TriviaQuestionTimer window, a treasure-hunt substage exposes no countdown (canon has none). Removes the pre-canon whole-session MaximumTime countdown (fields, State hooks, worker tick, repository predicate, and a migration dropping the columns), keeps the pause-freeze / trivia-resume-same-question behaviour, and re-points the timer read endpoints, the transition-result Timer field, and the SessionTimerUpdated broadcast at the substage-derived value. State pattern re-pointed (advancing/frozen by per-SessionState type); SignalR transport verified; no RabbitMQ. Question activation/advancement (HU-33A/DES-78) and treasure-hunt play (HU-29-32) are out of scope. Contract change: SessionTimerSnapshotDto remaining-time now tracks the active substage."
```

---

## Rationale

DES-77's acceptance (remaining time derived from the active substage's `SubstagePlayMode`; no session-level `SessionMode` timer logic; pause freezes and paused trivia resumes the same question) is the **canonical timer model** after the 2026-06-16 mission-runtime rewrite. Canon names an authoritative runtime clock in exactly one place — `TriviaQuestionTimer` (`CONTEXT.md:125`, "the authoritative runtime duration for one snapshotted `TriviaQuestion`"; _Avoid_: client timer) — and treasure-hunt substages advance by target resolution, not a timer (`CONTEXT.md:110,122`). Cycle 1 (DES-30) instead made a **whole-session `MaximumTime` countdown** the primary remaining time and ran a parallel question timer — the "mission session vs trivia session" split the ticket says to eliminate. So unlike its sibling HU-21A (which *verified* already-aligned lifecycle code), HU-22 must **change behaviour**: delete the whole-session countdown, derive the displayed remaining from the active substage's mode, and keep the freeze/resume machinery.

`State` (mandated) is already realized — the advancing-vs-frozen decision is owned by the `LiveSessionState` classes via `LiveSessionStateFactory`; HU-22 re-points that dispatch at the substage-derived timer rather than inventing a new pattern. SignalR is the mandated transport for the live push.

Three genuine decisions were surfaced rather than guessed (`canon-realignment-workflow.md` open-decisions protocol; generator constraint 3) and **resolved 2026-07-05 by taking the canon-aligned recommendation as-is** — they are now committed scope:

- **OD-1 — RESOLVED: no treasure-hunt countdown.** Canon defines no per-treasure-hunt-substage duration and no treasure-hunt countdown; the ticket AC's "tiempo aplicable de la subetapa de búsqueda activa" has no canon backing. Authority chain (canon > tracker AC) settles it: **treasure-hunt substages expose no remaining-time countdown; surface elapsed `ResolutionTime` only if the frontend needs a figure** (UI-contract call, confirm at Step 9). A per-substage authored duration would need an ADR + a new snapshot field — out of this slice.
- **OD-2 — RESOLVED: dispatch on `ActiveQuestionIndex`.** The only runtime substage signal is `ActiveQuestionIndex` (trivia); full active-substage tracking + advancement belongs to DES-78 (trivia) and HU-29–32 (treasure-hunt), later in the order. **The authoritative-timer selector dispatches on `ActiveQuestionIndex` present (trivia window) vs absent (no countdown); HU-22 does not build advancement** — that would trespass DES-78.
- **OD-3 — RESOLVED: redefine in place + drop columns.** **Redefine `SessionTimerSnapshotDto.RemainingSeconds`/`TotalSeconds` in place as the active-substage window (keep the nested `ActiveQuestion`) and add a migration dropping the `_sessionTimer*` columns** (workflow: do not preserve stale schema); the `MaximumTime` VO/column survives as authoring metadata. This is a frontend contract change and is called out at Step 8.5 / Step 9.

With all three resolved, the open-decisions gate is satisfied and X.1 may start once Stop 1 confirms the slice is grabbed. Question activation/advancement (HU-33A/DES-78) and treasure-hunt play (HU-29–32) remain deliberately out of scope — building them here would duplicate a peer rebuild and jump the realignment order.
