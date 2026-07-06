# Prompt Example — HU-33A Trivia substage orchestration realign (Feature Slice)

Concrete prompt sequence for driving DES-78 (HU-33A) through a full slice on `feature/hu-33a-trivia-substage-orchestration-realign`. Follows the pattern in [workflow_for_prompts.md](./workflow_for_prompts.md).

**Key difference for the DES-78 slice:** this is a **genuine rebuild**, not a verification lock like HU-16/HU-21A. Cycle 1 (DES-44, now **Canceled**) orchestrated a **standalone "trivia session"** over one flat question list on the whole `MissionRuntimeSnapshot` and moved the **session** to `Finished` when the list ran out (`TriviaRoundOrchestratorFacade.cs:84`). Canon makes trivia a **`SubstagePlayMode` inside a mission substage**: one snapshotted question active per authoritative timer window for all teams; timer-driven question advancement; the trivia substage completes when the **final question timer** expires and — if another substage exists — **all teams advance together** to the next substage in strict mission order; `Finished` is reached only via `SessionCompletion` after the **final** substage. HU-33A must therefore **add an `ActiveSubstageId` pointer + timer-driven `SubstageAdvancement`**, **re-scope** question activation to the active substage, and **delete** the flat-list session-finish shortcut — a delete + redefine + add-pointer + migration + broadcast change across all four layers. Governing contract: **`backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`**.

> **✅ The four decisions (D-1…D-4) are RESOLVED and committed to scope (2026-07-05)** by ADR-0005 + the canon authority chain. The `canon-realignment-workflow.md` open-decisions gate is satisfied; carry them forward as fixed scope, not as a blocking gate. Step 5 (X.1) may start once Stop 1 confirms the slice is grabbed.

When working from the monorepo root, make the target workload explicit in each prompt. For backend steps, point to `@backend/.agents/backend-agent.md`. For frontend steps, point to `@frontend/AGENTS.md`. Do not ask for backend and frontend implementation in the same phase prompt; coordinate them as separate scoped steps.

---

## Stop 1 acceptance guard

Reject this generated prompt before implementation if it does not explicitly scope all of the following:

- the automated round runs **inside a trivia substage** (`SubstagePlayMode = Trivia`), driven by a single authoritative **`ActiveSubstageId`** pointer walking strict stage→substage order — **not** a standalone trivia session over a flat question list
- one snapshotted question is active per timer window for all teams (`SynchronizedTriviaQuestion`); question advancement is **timer-driven**
- when the **final** question of the active trivia substage closes, the substage completes and — if another substage exists — **all teams advance together** to the next substage; `Finished` is reached **only** via `SessionCompletion` after the final substage
- advancement is **generic + timer-driven + operator-supervised** — no operator command/endpoint forces substage advancement (`CONTEXT.md:122`)
- the three mandated patterns are realized: `State` (activation/advance gated by per-`SessionState` type via `LiveSessionStateFactory`), `Facade` (`TriviaRoundOrchestratorFacade` is the single orchestration entry point), `Strategy` (`IQuestionActivationStrategy` re-scoped to the active substage)
- SignalR broadcasts `QuestionActivated`/`QuestionClosed`/**`SubstageAdvanced`** to `live-session:{id}`; **no RabbitMQ publish** in this slice (D-1)
- the `TriviaSubstageWinner` is **emitted, not computed** here — `SubstageAdvancedEvent` is raised for downstream scoring (HU-37A); the winner is **not** calculated in session-ops (D-2)

**These four decisions are RESOLVED and committed to scope** (2026-07-05; rationale below) — carry them forward as fixed scope:
- **D-1 — RESOLVED:** SignalR only; the `SubstageAdvancedEvent` is a domain event, no RabbitMQ publish until a consumer (HU-37A) lands.
- **D-2 — RESOLVED:** the winner is emitted, not computed (ADR-0005; no `ScoreEntry` ledger / trivia answers exist yet).
- **D-3 — RESOLVED:** no new REST endpoint and no operator-advance endpoint; the active question rides HU-22's timer DTO `ActiveQuestion`; X.4 is broadcast-centric.
- **D-4 — RESOLVED:** advancement generic, activation trivia-only; advancing into a treasure-hunt substage **parks** (HU-29–32 downstream). Verify end-to-end only on an **all-trivia multi-substage** mission.

---

## Required design patterns

HU-33 is the only backlog HU mandating three patterns (`required_patterns_matrix.md:131`). HU-33A owns all three.

- **`State` (mandated, X.1 Domain)** — `required_patterns_matrix.md:41,131`; `CONTEXT.md:197-199`. Question activation, timer advance, and **substage advancement** are gated by the per-`SessionState` type via `LiveSessionStateFactory.For(State)` (only `Active` advances; `Paused` freezes and resumes the same question; `Finished`/`Cancelled` stop). Already realized by HU-21A/HU-22; HU-33A extends the same dispatch to substage advancement — not `if (State == …)` conditionals, and no operator-forced advance.
- **`Facade` (mandated, X.2 Application)** — `required_patterns_matrix.md:40,131`; `CONTEXT.md:193-195`. `TriviaRoundOrchestratorFacade` is the single orchestration entry point for activate-question, close-and-advance-question, and close-substage-and-advance-substage; the timer worker + session-`Active` handler stay thin.
- **`Strategy` (mandated, X.1 Domain)** — `required_patterns_matrix.md:44,131`; ADR-0004. `IQuestionActivationStrategy` / `SequentialQuestionActivationStrategy` re-scoped to return the next question **within the active substage**, or `null` when the substage is exhausted (the advance signal).
- **Transport: SignalR (mandated)** — `required_patterns_matrix.md:57,131`. `QuestionActivated`/`QuestionClosed`/`SubstageAdvanced` broadcast to the `live-session:{id}` group. **No RabbitMQ** in this slice (D-1).

> Applies-where note (no new gate): HU-33A adds no new protected endpoint (D-3); the existing reads/timer inherit the standard operator / participant-membership `AuthorizationBehaviour`/gateway guard (ADR-0001/0002). HU-33A is not in the applies-where `Proxy` set — note only, no new `Proxy` gate.

---

## Pre-resolved orient (as of 2026-07-05)

> Step 1 has already been run. Paste this section into any agent session that needs context before picking up a phase — no need to re-run the orient prompt.

### What predecessors have already landed

DES-78 (HU-33A) is **In Progress**, a `needs-rebuild` row in the realignment map (phase #10 "Trivia play": "Synchronized trivia substage orchestration"), and the **rebuild successor of the Canceled DES-44**. Its build-on predecessors are all Done/merged:

- **DES-22 (HU-15)** — the immutable `MissionRuntimeSnapshot` carrying the strict-ordered `StageSnapshots → SubstageSnapshots` tree and the `TriviaQuestionSnapshot`s **each keyed to their substage by `SubstageSnapshotId`** (`TriviaQuestionSnapshot.cs:49`). The snapshot already models the substage structure — no snapshot schema change is needed.
- **DES-77 (HU-22)** — the direct seam: the authoritative timer keyed off the active `TriviaQuestion` (`ActiveQuestionIndex`, `_questionTimer*` window, `ActivateQuestion`/`CloseActiveQuestion`, `AuthoritativeSessionTimerWorker` driving `CloseAndAdvanceAsync` on expiry). HU-22 **reserved** question activation/advancement/close + the facade + strategy + the substage pointer/advancement for DES-78.
- **DES-76 (HU-21A)** — the canonical state machine + `SessionStateChanged` broadcast; it left the `Preparing → Active` first-substage start as a play-layer seam HU-33A implements.
- **DES-75 (HU-16)** — the whole-quiz snapshot-content-fidelity lock. HU-33A executes against that frozen content.

Landed-untouched: DES-25 (HU-18), DES-26 (HU-19), DES-27 (HU-20), DES-11/12 (HU-07A/07B). Superseded/excluded: DES-44 (HU-33A cycle-1, Canceled — the reuse candidate reworked here), DES-23/28/30 (cycle-1 HU-16/21A/22 sources, Canceled). No same-service In Progress predecessor → branch base is `develop`.

### What HU-33A adds (per DES-78, the realignment map, ADR-0005, and PRD DES-70)

| Concern | New work |
|---|---|
| Active-substage pointer | `LiveSession.ActiveSubstageId` (nullable `Guid`) — the single authoritative live-substage pointer, walked in strict stage→substage order; persisted as `active_substage_id`. |
| First-substage start | On `Preparing → Active`, set `ActiveSubstageId` to the first substage; if trivia, activate its first question. |
| Substage-scoped activation | Re-scope `ActiveQuestionIndex` + `ActivateQuestion`/`CloseActiveQuestion`/`GetOrderedTriviaQuestions` + the strategy to the **active substage's** questions (`SubstageSnapshotId == ActiveSubstageId`). |
| Substage advancement | Last-question close → advance to the next substage (trivia → activate first question; treasure-hunt → park; none → `SessionCompletion` → `Finished`). Generic, timer-driven, no operator override. |
| `SubstageAdvancedEvent` | New domain event `(LiveSessionId, FromSubstageId, FromPlayMode, ToSubstageId?)` — the downstream scoring (HU-37A) seam. |
| Facade / handler / worker re-scope | `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` advances substages (delete the `:84` `Finished` shortcut); `TriviaRoundStartedNotificationHandler` sets the first substage; `AuthoritativeSessionTimerWorker` drives it (keep, no fork). |
| Migration | `active_substage_id` (nullable `uuid`) on `live_sessions`; mirror `AddTriviaRoundState`. |
| SignalR | Add `SubstageAdvanced` to `live-session:{id}`; keep `QuestionActivated`/`QuestionClosed`. |
| Behavioural change | The session no longer `Finished`s when the flat question list ends; it advances substage-by-substage. Contract note for the frontend/operator monitor. |

### Out of scope for this slice (surface at Stop 1, do not build)

- **Treasure-hunt play** — target resolution + treasure-hunt substage completion (HU-29–32). Advancing into a treasure-hunt substage **parks** (D-4).
- **`TriviaSubstageWinner` computation** — emitted, not computed here (D-2). No `ScoreEntry` ledger / trivia answers exist yet (HU-34A/B, HU-37A). Do not build per-substage scoring in session-ops.
- **RabbitMQ publication** of the substage-advanced/winner fact (D-1) — deferred until HU-37A consumes it.
- **Trivia answer submission / monitoring** (HU-34, HU-36) — HU-33A is orchestration only.
- **A new REST or operator-advance endpoint** (D-3) — advancement is timer-driven; the active question rides HU-22's timer DTO.

### Branch state and prerequisite

`feature/hu-33a-trivia-substage-orchestration-realign` branches from `develop`. All build-on dependencies (HU-15/16/17/21A/22) are Done/merged; no same-service predecessor is In Progress.

**Before starting:** confirm (grep) that `ActiveQuestionIndex` is today a **flat** pointer over the whole snapshot (`SequentialQuestionActivationStrategy.cs:15`, `TriviaRoundOrchestratorFacade.cs:84`), that `TriviaQuestionSnapshot.SubstageSnapshotId` + the strict-ordered `StageSnapshots → SubstageSnapshots` tree exist, that the `_questionTimer*` window + `AuthoritativeSessionTimerWorker.CloseAndAdvanceAsync` seam exist (HU-22), and that no `ActiveSubstageId`/`SubstageAdvanced*`/`SessionCompletion` symbol exists yet. **D-1…D-4 are already resolved (committed scope) — build to them.**

### Linear state (as of 2026-07-05)

- DES-78 (HU-33A): **In Progress**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature` (apply `ready-for-agent` at Step 2)
- DES-22 (HU-15), DES-24 (HU-17), DES-75 (HU-16), DES-76 (HU-21A), DES-77 (HU-22): **Done** (the foundation this slice builds on)
- Same-service Canceled (superseded): DES-23, DES-28, DES-30, DES-44

> Linear live state may have changed. Use the Linear MCP to verify DES-78 status and labels if needed, but do not re-fetch PRD scope — read the local file at `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` and overlay `@backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.

---

## 1. Orient — read service state, PRD, and realignment overlay

> **Skip this step if you have read the pre-resolved orient section above.** Run it only if the service source, README, or Linear state may have changed since 2026-07-05.

```text
Read the following and summarise what is implemented today vs. what HU-33A must rebuild:
- @backend/docs/hu33a-context.md — the pre-resolved HU-33A context (primary), incl. D-1…D-4
- @backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md — the governing contract
- @backend/docs/hu22-context.md — the timer seam this round builds on (ActiveQuestionIndex, worker CloseAndAdvanceAsync)
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md — US35
- @backend/docs/canon-realignment-after-mission-runtime-rewrite.md — realignment overlay (phase #10, :106)
- @backend/services/session-operations-service/CONTEXT.md — §Trivia (:125-159), §SubstageAdvancement (:121-123), §SessionCompletion (:177-178)

Then grep the existing session-operations source to confirm:
- ActiveQuestionIndex is a flat pointer over the whole snapshot's TriviaQuestionSnapshots (SequentialQuestionActivationStrategy.cs:15; TriviaRoundOrchestratorFacade.cs:84 moves the SESSION to Finished when the flat list ends)
- TriviaQuestionSnapshot carries SubstageSnapshotId, and StageSnapshots -> SubstageSnapshots is strict-ordered
- the _questionTimer* window + AuthoritativeSessionTimerWorker.CloseAndAdvanceAsync seam exist (HU-22)
- no ActiveSubstageId / SubstageAdvanced* / SessionCompletion symbol exists yet

Then use the Linear MCP to fetch the current live state and labels of DES-78.

Output: what is canon-aligned (keep — the substage snapshot + HU-22 timer window + HU-21A state machine), what is the standalone flat-list "trivia session" (delete/rework), and the exact per-layer changes — plus a crisp statement of D-1…D-4.
Do not start planning or implementing yet.
```

---

## 2. Label DES-78 as ready-for-agent

```text
Use the Linear MCP to confirm DES-78 still carries the label ready-for-agent.
If it is missing, add it.
Output the updated DES-78 ticket state and labels, including canon-realign and needs-rebuild.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-78 carries both svc:session-operations-service
and ready-for-agent labels and output its current status and acceptance criteria.

The PRD scope is already in the local file at
@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md.
The realignment overlay is in
@backend/docs/canon-realignment-after-mission-runtime-rewrite.md, and the governing contract is
@backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md.
Do not re-fetch PRD scope from Linear.

Before planning, explicitly confirm the Stop 1 acceptance guard:
- the round runs inside a trivia substage, driven by an ActiveSubstageId pointer in strict stage->substage order
- one synchronized question per timer window; timer-driven question advancement
- last question of the substage closes -> substage completes -> all teams advance to the next substage (if any); Finished only via SessionCompletion
- advancement is generic + timer-driven + operator-supervised (no operator-forced advance)
- State + Facade + Strategy realized; SignalR broadcasts QuestionActivated/QuestionClosed/SubstageAdvanced; no RabbitMQ
- TriviaSubstageWinner emitted (SubstageAdvancedEvent), not computed here

Then acknowledge D-1…D-4 as already resolved and committed to scope (see the Rationale) — carry them forward as fixed scope, not a blocking gate.

Output the confirmed HU id, title, acceptance criteria, labels, the guard confirmation, and the D-1…D-4 resolutions before planning the slice.
```

In the remaining steps, `HU-33A` and `DES-78` are the resolved values; `DES-70` is the shared session-operations PRD (local file above, overlaid by the realignment document).

---

## 4. Start the slice

```text
Prepare the trivia substage orchestration realignment slice on branch feature/hu-33a-trivia-substage-orchestration-realign.
Use the HU id and DES id resolved from Linear in the previous step.
This slice affects backend session-operations-service and frontend.

The pre-resolved orient at the top of this document lists what is canon-aligned (keep) and what is the
standalone flat-list "trivia session" (delete/rework). Do not re-read the PRD for scoping unless you need a precise detail.

This is a genuine rebuild: add an ActiveSubstageId pointer + timer-driven SubstageAdvancement, re-scope question
activation to the active substage, and delete the flat-list session-finish shortcut (TriviaRoundOrchestratorFacade.cs:84).
Advancement is generic, timer-driven, and operator-supervised — no operator-forced advance. Advancing into a treasure-hunt
substage PARKS (do NOT build treasure-hunt play, HU-29-32). The TriviaSubstageWinner is emitted (SubstageAdvancedEvent),
not computed here (no ScoreEntry ledger yet — HU-37A). SignalR only; no RabbitMQ.

D-1…D-4 are already resolved (committed scope) — build to them. Move DES-78 to In Progress and output the exact scope,
branch name, base branch (develop), and touched surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-33A in session-operations-service, per the
**X.1 derivation block in @backend/docs/hu33a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open). Apply the D-1…D-4 resolutions from Stop 1.

Gate:
- Domain build passes; a unit test locks: entering Active sets ActiveSubstageId to the first substage; question activation/close operate within the active substage only; closing the substage's LAST question advances to the next substage (trivia -> first question ready; treasure-hunt -> parked; none -> Finished via SessionCompletion)
- a SubstageAdvancedEvent(from, fromPlayMode, to) is raised per advancement; advancement is rejected outside Active and frozen in Paused
- no flat-list "questions exhausted -> Finished" path remains
- State + Strategy verified: activation/advance decided by per-state types via LiveSessionStateFactory and the substage-scoped IQuestionActivationStrategy, not ad-hoc conditionals

Do not build treasure-hunt play (HU-29-32) or compute the TriviaSubstageWinner (HU-37A). Do not touch other backend layers or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 - domain layer (HU-33A)

Ref: HU-33A
Ref: DES-78
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-33A in session-operations-service, per the
**X.2 derivation block in @backend/docs/hu33a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- Application build passes; a test proves the Facade closes-and-advances within and across substages (trivia activates the next question or advances the substage; treasure-hunt parks; final-substage completion -> Finished via SessionCompletion), broadcasts QuestionClosed/SubstageAdvanced, and is idempotent under repeat ticks
- the session-Active handler sets the first substage and activates the first question only for trivia
- no MoveTo(Finished) on flat-list exhaustion remains; no RabbitMQ publish added
- Facade verified: orchestration is the single entry point, not scattered across worker/handler/endpoint

Do not compute the winner (HU-37A) or add a RabbitMQ publish. Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 - application layer (HU-33A)

Ref: HU-33A
Ref: DES-78
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-33A in session-operations-service, per the
**X.3 derivation block in @backend/docs/hu33a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; grep the model snapshot rather than
full-reading it).

Gate:
- Infrastructure build passes
- a migration ADDS active_substage_id (nullable uuid) to live_sessions (assert via model snapshot); no snapshot-content schema change (questions already carry SubstageSnapshotId)
- a repository integration test round-trips a session with the active-substage pointer and its active-substage question timer
- the AuthoritativeSessionTimerWorker drives substage advancement through the reworked facade on question-timer expiry (keep the worker skeleton; do not fork it)

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 - infrastructure layer (HU-33A)

Ref: HU-33A
Ref: DES-78
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-33A in session-operations-service, per the
**X.4 derivation block in @backend/docs/hu33a-context.md** (your spec — do not
re-read the canon or re-inspect the tree; open a cited canon section only to
fill a gap the block leaves open).

Gate:
- a hub integration test drives a synchronized trivia round and asserts QuestionActivated -> QuestionClosed -> (last question) SubstageAdvanced broadcasts reach the live-session:{id} group
- the timer read (GET /api/sessions/{id}/timer and .../participants/timer) exposes the ACTIVE-SUBSTAGE question via SessionTimerSnapshotDto.ActiveQuestion; authorization enforced
- service coverage reaches the repo gate target (ADR-0005 coverlet gate)

Add NO new REST endpoint and NO operator-advance endpoint (advancement is timer-driven; the active question rides the timer DTO). Do not add a RabbitMQ publish. Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 - api layer (HU-33A)

Ref: HU-33A
Ref: DES-78
Ref: DES-70
```

---

## 8.5. Docker rebuild and smoke

```text
From the monorepo root, rebuild and restart the backend stack after the API phase:

docker compose build session-operations-service api-gateway
docker compose up -d session-operations-service api-gateway

Smoke the synchronized trivia orchestration through the gateway on an ALL-TRIVIA MULTI-SUBSTAGE mission
(create it via the seeders / mission-design if needed). No advancement endpoint exists — drive it by the timer:
- POST /api/sessions with a mission carrying >=2 trivia substages -> 201; then PATCH .../state to Preparing, then Active
- GET /api/sessions/{liveSessionId}/timer as an Operator -> 200; SessionTimerSnapshotDto.ActiveQuestion is the first question of the FIRST substage; RemainingSeconds tracks its window
- let the question timers expire (or observe the worker): questions advance within the substage; after the LAST question of substage 1 closes, the ActiveQuestion moves to the FIRST question of substage 2 (all teams together)
- after the final substage's last question closes, the session reaches Finished (SessionCompletion) — NOT when the flat question list "ends"
- confirm a SignalR client on live-session:{id} receives QuestionActivated / QuestionClosed / SubstageAdvanced

Output:
- container status
- smoke command results (state transitions + timer reads + observed advancement)
- confirmation the session advances substage-by-substage and Finishes only after the final substage (behavioural change for the frontend/operator monitor)
```

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file — following the **frontend plan concreteness rule** (below), modelled on the exemplar closest to this slice's shape (`@frontend/plans/hu-03-frontend-role-permission-assignment.md` for a small 1–few-endpoint surface; `@frontend/plans/hu-10a-frontend-mission-hierarchy-authoring.md` for a large/multi-endpoint or partially-blocked surface) — save it in `@frontend/plans/` for the following:
Use @frontend/AGENTS.md.
Implement the frontend slice for HU-33A synchronized trivia substage orchestration.

There is NO new REST contract in this slice (D-3): the synchronized active question is read from the existing
GET /api/sessions/{liveSessionId}/timer and .../participants/timer via SessionTimerSnapshotDto.ActiveQuestion, whose
value now tracks the ACTIVE SUBSTAGE's question. New real-time signals arrive over the SessionsHub live-session:{id} group:
QuestionActivated, QuestionClosed, and the new SubstageAdvanced (fromSubstageId, fromPlayMode, toSubstageId?). Behavioural
change: the round advances substage-by-substage and the session Finishes only after the final substage — there is no
session-level "round over when questions end". This is a focused real-time read surface (no new endpoint) — choose the hu-03 exemplar.

Scope:
- render the synchronized active question + live countdown from the timer read + QuestionActivated/QuestionClosed, for all teams in lockstep
- reflect substage advancement on SubstageAdvanced (new substage banner / transition), and session completion on the SessionStateChanged(-> Finished) signal
- remove any UI copy/type that assumes a standalone "trivia session" or a session that ends when the question list runs out
- operator monitoring view shows which substage + question is active (read-only; the operator does not advance)

Gate:
- frontend typecheck/build passes
- the question UI reflects the active-substage question, advances on the live QuestionActivated/QuestionClosed/SubstageAdvanced signals, and shows completion only after the final substage
- Gate: no UI type/copy retains a standalone trivia-session or a flat-list "questions exhausted -> finished" model

Do not modify backend code in this step.
```

**Frontend plan concreteness rule (embed verbatim in the generated plan's altitude choice):**

1. **Proportion concreteness to certainty.** Write code-complete detail — exact DTO/request types, real component skeletons, exact client-fn + server-action bodies, a `data-testid` contract — only for the **fully-knowable near-term increments** (typically the foundation + first authoring increment). Keep later, large, or blocked increments at **contract + gate altitude**: a contract table, scope, and gate, with no invented bodies. Never write code for an increment blocked on an open question.
2. **Verify every code anchor against the real source before writing it.** Open the files the plan names — exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets — and write only what the source actually supports. A confident-but-wrong anchor (e.g. "reuse `getIdentityHeaders`" when it is not exported) is worse than an altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than inventing it.
3. **Required sections** (both exemplars carry these; a plan missing one is a defect): Context · Verified Backend Contract (endpoint/shape table) · Architecture Decisions · **Environment** (env vars / config consts reused) · **data-testid contract** · phased Scope + Gate per increment · **Acceptance-criteria → test mapping** · Open Questions / Dependencies · Out of Scope.
4. **Final forms only, sequential by default.** Write only the final version of each anchor — no "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

Commit:

```text
feat(frontend): synchronized trivia substage orchestration — HU-33A

Ref: HU-33A
Ref: DES-78
Ref: DES-70
```

---

## 9b. Implement the frontend plan

> Run only after the Step 9 plan is written and reviewed. The plan is the source of truth and supersedes the Step 9 seed scope — including Step 9's single seed commit: commit per the plan's own per-phase Commit Sequence, not the one above.

```text
Use @frontend/AGENTS.md. Implement the Step 9 plan at @frontend/plans/hu-33a-frontend-synchronized-trivia-substage-orchestration.md,
phase by phase per the plan's own Scope / Gate / Commit Sequence.

For each phase: implement only that phase, run its Gate (build + typecheck, plus any e2e the phase lands),
then commit with the exact subject from the plan's Commit Sequence for that phase.

STOP at any increment the plan marks blocked on an Open Question (name it). Do not invent the blocked behaviour;
surface the question and wait. Do not re-generate the plan. Do not modify backend code in this step.
```

---

## 10. Close-out

```text
Use the Linear MCP to re-check DES-78 acceptance criteria and labels.
Verify the final implementation against the Stop 1 acceptance guard:
- the automated round runs inside a trivia substage, driven by ActiveSubstageId in strict stage->substage order
- last question of the substage -> substage completes -> all teams advance to the next substage (if any); Finished only via SessionCompletion
- advancement is timer-driven + operator-supervised (no operator-forced advance)
- State + Facade + Strategy realized; SignalR QuestionActivated/QuestionClosed/SubstageAdvanced; no RabbitMQ
- TriviaSubstageWinner emitted (SubstageAdvancedEvent), not computed here

Run final backend and frontend verification required by the repo instructions.
Summarise:
- commits created
- the orchestration rebuilt (ActiveSubstageId pointer, substage-scoped activation, timer-driven substage advancement, flat-list session-finish removed) and the migration adding active_substage_id
- tests and gates run (incl. ADR-0005 coverage)
- confirmation the behavioural change (advances substage-by-substage; Finishes only after the final substage) is documented for the frontend

Create the PR:

gh pr create \
  --base develop \
  --head feature/hu-33a-trivia-substage-orchestration-realign \
  --title "feat(session-operations): synchronized trivia substage orchestration (HU-33A)" \
  --body "Rebuilds DES-78/HU-33A: trivia orchestration now runs inside a mission trivia substage instead of a standalone trivia session. Adds LiveSession.ActiveSubstageId (the single authoritative live-substage pointer, walked in strict stage->substage order) and timer-driven, operator-supervised SubstageAdvancement: one synchronized question is active per authoritative timer window for all teams, question advancement is timer-driven, and when the final question of a trivia substage closes the substage completes and all teams advance together to the next substage; Finished is reached only via SessionCompletion after the final substage. Re-scopes question activation to the active substage (ActiveQuestionIndex, ActivateQuestion/CloseActiveQuestion, the strategy, and the timer DTO), removes the cycle-1 flat-list session-finish shortcut, and adds a migration for active_substage_id. Patterns: State (advance gated by per-SessionState type), Facade (TriviaRoundOrchestratorFacade single entry point), Strategy (substage-scoped IQuestionActivationStrategy). Emits SubstageAdvancedEvent for downstream scoring (HU-37A) — the TriviaSubstageWinner is emitted, not computed here. SignalR QuestionActivated/QuestionClosed/SubstageAdvanced verified; no RabbitMQ. Treasure-hunt play (HU-29-32) parks; verified end-to-end on an all-trivia multi-substage mission. Behavioural change: the session advances substage-by-substage and Finishes only after the final substage."
```

---

## Rationale

DES-78's acceptance (the automated round runs inside a trivia substage; the final question timer expiring closes the substage and advances all teams to the next substage; the operator does not manually advance) is the **canonical trivia model** after the 2026-06-16 mission-runtime rewrite. Canon makes trivia a `SubstagePlayMode` inside a mission substage (`grilling…:55-61`, `CONTEXT.md:129-139`): one `SynchronizedTriviaQuestion` per authoritative `TriviaQuestionTimer` window, `TriviaQuestionAdvancement` is timer-driven, and `TriviaSubstageCompletion` fires when the final question timer expires — then `SubstageAdvancement` moves all teams to the next substage in strict order, with no operator override (`CONTEXT.md:121-122`). Cycle 1 (DES-44) instead ran a **standalone trivia session** over one flat question list on the whole snapshot and moved the session to `Finished` when the list ran out — the "trivia session vs mission session" split the rewrite eliminates. So unlike its verification-dominant siblings HU-16/HU-21A, HU-33A must **change behaviour**: add the `ActiveSubstageId` pointer + timer-driven substage advancement, re-scope activation to the active substage, and route completion through `SessionCompletion`.

The three mandated patterns are realized, not invented: `State` (already the advancing-vs-frozen decision owner via `LiveSessionStateFactory`, extended to gate substage advancement), `Facade` (`TriviaRoundOrchestratorFacade` already the orchestration entry point, reworked to advance substages), and `Strategy` (`IQuestionActivationStrategy` re-scoped to the active substage). SignalR is the mandated transport for the live push.

Four decisions were surfaced rather than guessed (`canon-realignment-workflow.md` open-decisions protocol; generator constraint 3) and **resolved 2026-07-05** by ADR-0005 (`backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`) + the canon authority chain — now committed scope:

- **D-1 — RESOLVED: SignalR only, no RabbitMQ.** The matrix lists RabbitMQ for HU-33 because the results/winner publication rides the round-close/scoring slices. ADR-0005: the winner is emitted, not computed; session-ops raises the domain `SubstageAdvancedEvent`. No cross-service consumer (HU-37A) exists yet, so no RabbitMQ publish is added here (mirrors HU-21A deferring RabbitMQ to HU-21B).
- **D-2 — RESOLVED: winner emitted, not computed.** ADR-0005 rejects computing `TriviaSubstageWinner` in session-ops: it crosses the `CONTEXT.md:187-189` Runtime-Authority boundary and is not computable in this slice (no `ScoreEntry` ledger / trivia answers yet — HU-34A/B, HU-37A). Session-ops emits `SubstageAdvancedEvent(fromSubstageId, fromPlayMode, toSubstageId)` + the existing `SessionStateChangedEvent(→Finished)`; scoring derives the winner downstream.
- **D-3 — RESOLVED: no new endpoint; broadcast-centric X.4.** The synchronized active question already rides HU-22's `SessionTimerSnapshotDto.ActiveQuestion`; answer/monitor surfaces are HU-34/HU-36. Advancement is timer-driven, so there is no operator-advance endpoint (canon forbids operator-forced advancement — `CONTEXT.md:122`).
- **D-4 — RESOLVED: generic advancement, trivia-only activation.** ADR-0005: advancement is uniform (next substage, strict order, all teams, no override); only entry activation differs by play mode. Advancing into a treasure-hunt substage **parks** (its runtime, HU-29–32, is downstream); a mixed mission parks rather than crashes. DES-78 verifies end-to-end only on an all-trivia multi-substage mission.

With all four resolved, the open-decisions gate is satisfied and X.1 may start once Stop 1 confirms the slice is grabbed. Treasure-hunt play (HU-29–32), winner computation (HU-37A), and trivia answers (HU-34) remain deliberately out of scope — building them here would jump the realignment order.
