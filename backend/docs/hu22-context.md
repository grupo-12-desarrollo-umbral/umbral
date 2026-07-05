# HU-22 Context — Authoritative timer realign (keyed off the active `SubstagePlayMode`, no session-level countdown)

> Paste this section into any agent session that needs context for HU-22 (DES-77).
> Last updated: 2026-07-05 | Branch: `feature/hu-22-authoritative-timer-substage-realign`
>
> Supersedes the cycle-1 `hu22-context.md` (DES-30, **Canceled**). The cycle-1 slice
> made a **whole-session `MaximumTime` countdown** the authoritative "remaining time"
> and ran a separate per-question timer in parallel — the pre-canon "mission session
> vs trivia session" split. Canon keeps **only** the trivia `TriviaQuestionTimer`
> (`CONTEXT.md:125`) as an authoritative runtime clock; there is **no** session-level
> or mission-level authoritative countdown, and treasure-hunt substages advance by
> target resolution, not a timer.

## State

- DES-77 (HU-22): **Todo**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature`, `ready-for-agent`. Both required labels present (`ready-for-agent` added 2026-07-05).
- **Resolved mode: realignment-rebuild** (`needs-rebuild` present) — **genuine rebuild, not verification-dominant.** DES-77 is the realignment map's phase #6 "Lifecycle" row: "Authoritative timer keyed off active `SubstagePlayMode`" (`canon-realignment-after-mission-runtime-rewrite.md:102`). It is the **rebuild successor of the Canceled DES-30** (`:68`, `:145`) — **not** in the superseded column. Unlike its sibling HU-21A (DES-76, which locked already-aligned code with tests), HU-22 must **change** behaviour: the cycle-1 whole-session `MaximumTime` countdown is the authoritative displayed remaining time today (`LiveSession.GetAuthoritativeSessionTimerSnapshot` → `_sessionTimer*`), and AC #1 requires that displayed remaining time be **derived from the active substage's `SubstagePlayMode`** instead. That is a delete + redefine + migration + API-contract change.
- **✅ The three former open decisions (OD-1/2/3) are RESOLVED and committed to scope** (see "Resolved decisions" below). The `canon-realignment-workflow.md` open-decisions gate is satisfied — the keep/delete/decide derivation below is now committed, and X.1 may start once Stop 1 confirms the slice is grabbed.
- **Superseded handling applied:** DES-30 (HU-22 cycle-1) is **Canceled** — its timer code is the **reuse candidate** this rebuild harvests/tears out, never anchored on as a predecessor. Peer rebuilds DES-76 (HU-21A) landed; DES-78 (HU-33A trivia round) is **Todo and downstream** (map phase #10) — its trivia-round orchestration coexists in the same classes; **do not touch it** (see gotchas).
- Predecessor DES ids (build-on, Done/merged): **DES-22 (HU-15)**, **DES-24 (HU-17)**, **DES-76 (HU-21A)**. Light build-on: DES-75 (HU-16 realign, trivia questions in the snapshot), DES-12 (HU-07B, reconnect delivery seam). Landed-untouched: DES-25 (HU-18), DES-26 (HU-19), DES-11 (HU-07A).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear). Overlaid by `backend/docs/canon-realignment-after-mission-runtime-rewrite.md`. US15/US16 (`DES-70:103-106`): see remaining time for the active question; timer freezes on pause and recovers on resume/reconnect.
- Canonical timer source: `backend/services/session-operations-service/CONTEXT.md` §`TriviaQuestionTimer` (`:125-138`), §Paused (`:52`), §`ResolutionTime` (`:174`); `backend/docs/grilling-session-mission-restructure.md` §Trivia (`:55-70`) + §Session Lifecycle (`:88-90`).
- Blocked by: DES-22 (HU-15), DES-24 (HU-17), DES-76 (HU-21A) — all **Done/merged**. Related/downstream: DES-78 (HU-33A trivia round).
- Branch: `feature/hu-22-authoritative-timer-substage-realign`, base **`develop`** (all build-on predecessors Done/merged; no same-service predecessor In Progress).

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | `required_patterns_matrix.md:41,110`: "Timer behavior depends on session state (active vs paused); authoritative remaining time pushed and restored on resume/reconnect." | The advancing-vs-frozen decision is owned by the per-`SessionState` type (`Active` advances the active-substage timer, `Paused` freezes it), **not** ad-hoc `if (State == …)` checks scattered across handlers/worker/hub. Already realized via `LiveSessionStateFactory.For(State)` dispatch; HU-22 **re-points** that dispatch at the substage-mode-derived (trivia-question) timer and **removes** the whole-session-timer hooks. |

Transport note: HU-22 carries **SignalR / WebSockets** (`required_patterns_matrix.md:57,110`). The authoritative remaining time must be pushed live to connected clients over the existing `SessionsHub`/`ISessionTimerBroadcaster` surface (`SessionTimerUpdated`). SignalR is a **hard transport gate** — verify in X.4. **No RabbitMQ** in this slice.

Applies-where note (no new gate): `GET /api/sessions/{id}/timer` and `GET /api/sessions/{id}/participants/timer` are protected reads, but HU-22 is **not** matrix-tagged for `Proxy` and not in the applies-where set (HU-04/05/36B). They inherit the standard `Operator` / participant-membership `AuthorizationBehaviour` guard (ADR-0001/0002) — note only, **no** new `Proxy` gate.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-22 (HU-15) — Done.** `LiveSession.Create(...)` takes the immutable `MissionRuntimeSnapshot` (`LiveSession.cs:64`) carrying the ordered Stage/Substage tree, each `SubstageSnapshot.PlayMode` (`TreasureHunt`|`Trivia`), and the `TriviaQuestionSnapshot`s (with `TimeLimitSeconds`) the trivia timer reads. This snapshot is the **only** source for "which substage / which question" — HU-22 reads it, never re-derives from `TriviaQuiz`.
- **DES-24 (HU-17) — Done.** Deleted the session-level `SessionMode` enum + debris, so AC #2 ("no timer logic conditioned on a session-level `SessionMode`") is already an emergent property at the type level; HU-22 must also remove the **whole-session `MaximumTime` countdown**, which is the surviving *behavioural* form of that retired split.
- **DES-76 (HU-21A) — Done.** Locked the canonical `Scheduled → Preparing → Active → Paused → Finished → Cancelled` machine and the `State`-pattern `Enter` hooks. HU-21A explicitly left the timer behaviour inside `ActiveLiveSessionState.Enter` / `PausedLiveSessionState.Enter` to HU-22 (`hu21a-context.md:74`). HU-22 reworks exactly those `Enter` hooks; the transition matrix, validators, facade, and `SessionStateChanged` broadcast are **not** HU-22's to touch.

**Light build-on:** DES-75 (HU-16 realign) put the trivia-question snapshots into the runtime snapshot the timer reads; DES-12 (HU-07B) is the reconnect seam the timer snapshot is delivered through on reconnect.

**Landed, untouched by this HU:** DES-25 (HU-18 team association), DES-26 (HU-19 operator assignment), DES-11 (HU-07A membership) — build around the session aggregate but do not inform the timer.

**Superseded / not predecessors (do NOT anchor on):** DES-30 (HU-22 cycle-1) — Canceled, the reuse candidate. **Downstream, do NOT trespass:** DES-78 (HU-33A trivia round) owns question **activation/advancement/close** + `TriviaRoundOrchestratorFacade`; DES-77 owns only the **timer window** (read/freeze/resume) and the authoritative remaining-time surface.

**Coverage:** session-operations-service carries the HU-07/15/17/18/19/21A baseline; measure the real service percentage against the ADR-0005 repo gate at X.4 — do not assume a carried-forward number.

## What this HU adds

| Concern | New work |
|---|---|
| Substage-mode-derived authoritative timer | The authoritative "remaining time shown" is derived from the **active substage's `SubstagePlayMode`**: a trivia substage → the active `TriviaQuestionTimer` window; a treasure-hunt substage → **no authoritative countdown** (canon has none — OD-1). Replaces the unconditional whole-session countdown as the primary remaining time. |
| Remove the whole-session countdown | Delete the `_sessionTimer*` `MaximumTime` countdown and its `State`-pattern hooks, worker tick, repo predicate, persisted columns, and its role as the DTO's primary `RemainingSeconds` — it is the pre-canon "mission session" timer with no canon term backing it (OD-3). |
| Pause / resume semantics | Pause **freezes** the active-substage timer; paused **trivia** stops the active question timer and **resumes the same question** on `Active` (`CONTEXT.md:52`, `grilling…:90`). Already realized via `EnterPaused/ActiveQuestionTimerState`; verify + keep. |
| Reconnect / read surface | The two timer read endpoints and the transition-result `Timer` field return the substage-derived authoritative remaining time so a resuming/reconnecting participant gets the trustworthy value immediately (US16). |
| Real-time push | Broadcast the substage-derived remaining time live over SignalR (`SessionTimerUpdated`); no polling. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain (timer members + `State` hooks on `LiveSession` / `SessionStates/*`), application (timer DTO/factory + the two timer queries), infrastructure (worker tick + `LiveSessionConfiguration` + a **migration dropping the `_sessionTimer*` columns**), api (two timer GET endpoints + transition-result `Timer` field).
- `frontend/` participant + operator live-session timer UI — the primary remaining time now tracks the active trivia question, and treasure-hunt substages show no countdown (pending OD-1).
- API/runtime contract boundary: `SessionTimerSnapshotDto` remaining-time semantics change (whole-session → active-substage); `SessionTimerUpdated` payload re-pointed. **This is a contract change** — call it out for the frontend.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Resolved decisions (committed scope — the keep/delete/decide derivation below builds to these)

> All three former open decisions were resolved 2026-07-05 by taking the canon-aligned recommendation as-is. The `canon-realignment-workflow.md` open-decisions gate is satisfied; these are committed scope, not pending confirmations.

- **OD-1 — RESOLVED: no treasure-hunt countdown.** Canon defines an authoritative runtime clock **only** for trivia (`CONTEXT.md:125` `TriviaQuestionTimer`); treasure-hunt substages advance by target resolution, not a timer (`CONTEXT.md:110,122`; `grilling…:31-43`). No per-treasure-hunt-substage duration exists in canon or the snapshot (`SubstageSnapshot` has no `TimeLimit`; only whole-mission `MaximumTime`). Authority chain (canon > tracker AC) settles it: **treasure-hunt substages expose no remaining-time countdown.** If the frontend needs a figure it is elapsed `ResolutionTime` (`CONTEXT.md:174`), not a countdown — a UI-contract call to confirm at Stop 2. A per-substage authored duration is **out of scope** (would need an ADR + a new authored snapshot field).
- **OD-2 — RESOLVED: dispatch on `ActiveQuestionIndex`.** The only runtime substage signal today is `LiveSession.ActiveQuestionIndex` (trivia). The authoritative-timer selector **dispatches on "is a trivia question active"** (`ActiveQuestionIndex` present → trivia window; else → no countdown per OD-1). Full multi-substage active-pointer + advancement stays **downstream** (DES-78 trivia phase #10; treasure-hunt HU-29–32 phase #8). HU-22 must **not** build question activation/advancement — that trespasses DES-78.
- **OD-3 — RESOLVED: redefine in place + drop columns.** Removing `_sessionTimer*` **redefines `SessionTimerSnapshotDto.{TotalSeconds,RemainingSeconds}` in place** as the active trivia-question window (0/absent when no active question), keeps the nested `ActiveQuestion`, re-points the worker/broadcast, drops the repo `_sessionTimer*` predicate, and adds a **migration dropping the columns** (workflow step 4: "do not preserve stale schema"). The `MaximumTime` VO/column **survives as authoring metadata**. This is a **frontend contract change** — surface at Stop 2.

## Known quirks / gotchas

- **`TriviaQuestionTimer` is the ONLY authoritative clock in canon.** `CONTEXT.md:125-138` names it explicitly and says _Avoid_: client timer, participant timer. There is **no** `SessionTimer`/`MissionTimer` term — the whole-session `MaximumTime` countdown is a pre-canon invention; delete it, do not "keep it around for treasure hunt."
- **Do NOT trespass DES-78 (trivia round).** Question **activation** (`ActivateQuestion`), **advancement/close** (`CloseActiveQuestion`, `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync`, `SequentialQuestionActivationStrategy`, `QuestionActivated/ClosedEvent`, `AddTriviaRoundState` migration) belong to DES-78. HU-22 owns the timer **window** (freeze/resume/remaining read) + the authoritative snapshot/broadcast only. Where the worker currently both ticks the session timer *and* closes-and-advances questions, HU-22 removes the session tick and **leaves** the question close/advance to DES-78 (OD-2 boundary).
- **`State` is realized, not invented.** The `LiveSessionStateFactory.For(State)` dispatch and the `Active`/`Paused` `Enter` hooks already exist. HU-22 re-points them (drop session-timer, keep question-timer freeze/resume) and locks the substage-derived behaviour with tests — it does not add a new state type.
- **Authoritative means backend-owned.** Clients render/animate; the source of truth for remaining time, freeze/resume, and reconnect recovery stays in `SessionOperations`.
- **Namespace is `umbral_backend.*`** across all layers; domain tests in `tests/UnitTests/`, application tests in `tests/Application.UnitTests/`, integration in `tests/IntegrationTests/`. Match existing files (mirror the HU-21A test layout).
- **`MaximumTime` VO may survive as authoring metadata** even after the countdown is removed (it is still snapshotted on `MissionRuntimeSnapshot`). Deciding whether to delete `MaximumTime` entirely is part of OD-3 — default: keep the VO/column on the snapshot, delete only the `_sessionTimer*` runtime countdown.

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first").
> Mode = **realignment-rebuild**: authority chain **canon docs > tracker AC > existing code**
> (`canon-realignment-workflow.md:19-34`); keep/delete/decide per `:72-84`. Mirror-anchors point **only** at code classified `keep`.
> **OD-1/2/3 are RESOLVED (2026-07-05) — the keep/delete/decide below is committed scope**, built to the resolutions: OD-1 = no treasure-hunt countdown; OD-2 = dispatch on `ActiveQuestionIndex`; OD-3 = redefine `RemainingSeconds` in place + drop `_sessionTimer*` (keep the `MaximumTime` VO/column as authoring metadata). The remaining "decide" notes below are settled — follow them as written.
> Canon: authoritative timer only for trivia (`CONTEXT.md:125` `TriviaQuestionTimer`, `:134` `ActiveTriviaQuestion`, `:137` `TriviaSubstageCompletion`; `grilling…:57-60,68`); pause freezes + trivia resumes same question (`CONTEXT.md:52`, `grilling…:90`); no session-level `SessionMode`/countdown (`canon-realignment…:24`; no canon term for a session/mission countdown). Pattern: `State` (`required_patterns_matrix.md:41,110`); transport SignalR (`:57,110`).

### Phase X.1 — Domain
**Derive** (`CONTEXT.md:52,125-138`; `grilling…:57-60,88-90`; `canon-realignment…:24`):
- The authoritative timer snapshot is **derived from the active substage's `SubstagePlayMode`**: a trivia substage active (`ActiveQuestionIndex` present) → the active `TriviaQuestionTimer` window (`_questionTimer*`); otherwise (treasure-hunt / no active question) → **no advancing countdown** (per OD-1). Expose this as the single authoritative accessor on `LiveSession` (e.g. rename/rework `GetAuthoritativeSessionTimerSnapshot` to return the active-substage timer), dispatched through `LiveSessionStateFactory.For(State)` so `Active` advances and `Paused` freezes.
- **Pause/resume:** `PausedLiveSessionState.Enter` freezes the active question timer; `ActiveLiveSessionState.Enter` resumes it on the **same** `ActiveQuestionIndex` (`_questionTimerRemainingDuration` preserved). Keep the existing `EnterPaused/ActiveQuestionTimerState` mechanics; **remove** the `EnterActive/PausedSessionState` whole-session-timer calls from the `Enter` hooks.
- **Remove** the whole-session countdown: `_sessionTimer*` fields, `EnterActiveSessionState`/`EnterPausedSessionState` (session-timer parts), `GetAdvancing/FrozenSessionTimerSnapshot`, `MarkAdvancingSessionTimerExpiredIfElapsed`, `HasAdvancingSessionTimer`, `CalculateAdvancingSessionTimerRemaining`, and the `ILiveSessionState`/`LiveSessionStateBase` session-timer hooks (`GetTimerSnapshot`/`MarkTimerExpiredIfElapsed`/`IsSessionTimerAdvancing`).
- No session-level `SessionMode` type or mode-conditioned branch exists (HU-17) — assert absence (AC #2).

**Target files** (create | edit — file to mirror):
- edit `src/Domain/Entities/LiveSession.cs` — delete `_sessionTimer*` members + their methods; keep the `_questionTimer*` window (freeze/resume/remaining); make the authoritative accessor return the active-substage (trivia-question) timer; mirror the existing `_questionTimer*` methods for shape
- edit `src/Domain/Services/SessionStates/ActiveLiveSessionState.cs`, `PausedLiveSessionState.cs`, `ILiveSessionState.cs`, `LiveSessionStateBase.cs` — drop the session-timer hooks; keep the question-timer freeze/resume in the `Enter` overrides
- keep `src/Domain/ValueObjects/AuthoritativeSessionTimerSnapshot.cs` (reused for the question window), `src/Domain/ValueObjects/SubstageSnapshot.cs` (`PlayMode`), `src/Domain/ValueObjects/TriviaQuestionSnapshot.cs` (`TimeLimitSeconds`)
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` (+ a timer-focused test) — assert: active trivia question → authoritative remaining = question window; pause freezes and resume continues the **same** question at the frozen remainder; no active question → no advancing countdown; no `_sessionTimer*`/`SessionMode` residue

**Pattern this phase owns:** `State` (mandated) — advancing-vs-frozen decided by the per-`SessionState` type via `LiveSessionStateFactory`, re-pointed at the substage-derived timer.
**Gate:** Domain build passes; a unit test locks: authoritative remaining time = the active `TriviaQuestionTimer` window when a trivia question is active and **no advancing countdown** otherwise (per OD-1); `Paused` freezes and `Active` resumes the **same** question at the frozen remainder; the whole-session `MaximumTime` countdown is **gone**; no session-level `SessionMode`. **`State` pattern verified — advancing/frozen decided by per-state types, not ad-hoc conditionals.**

**Existing code (keep / delete / decide):**
- keep `AuthoritativeSessionTimerSnapshot.cs`, `SubstageSnapshot.cs`, `TriviaQuestionSnapshot.cs`, and the `_questionTimer*` window mechanics on `LiveSession.cs` (`:266-309,335-341,359-362,370-372,429-467,486-519,543-562`) — canon-aligned `TriviaQuestionTimer`; verify + mirror
- delete the `_sessionTimer*` whole-session countdown on `LiveSession.cs` (`:15-18,32-33,70-71,102,251-259,328-333,343-357,364-368,389-426,469-484,521-540`) and the session-timer hooks on `ILiveSessionState`/`LiveSessionStateBase`/`ActiveLiveSessionState`(`:22-35`)/`PausedLiveSessionState` — pre-canon "mission session" timer, no canon term
- **settled (OD-3):** the `MaximumTime` VO/column **stays** as authoring metadata — only the runtime `_sessionTimer*` countdown is removed. **settled (OD-2):** question **activation** stays untouched — do **not** touch `ActivateQuestion`/`CloseActiveQuestion` (DES-78 owns them)

### Phase X.2 — Application
**Derive** (`CONTEXT.md:125-138`; the two timer queries + `SessionTimerSnapshotDtoFactory`):
- `SessionTimerSnapshotDto` `RemainingSeconds`/`TotalSeconds` are redefined to the **active-substage** window (trivia question, or 0/absent when no active question — OD-3), with the nested `ActiveQuestion` kept. `SessionTimerSnapshotDtoFactory.Create` reads the new authoritative accessor instead of the whole-session snapshot.
- `GetOperatorSessionTimerSnapshotQueryHandler` and `GetParticipantSessionTimerSnapshotQueryHandler` call the re-pointed accessor; authorization (operator / participant-membership) unchanged.
- `TransitionSessionStateResultDto.Timer` carries the same substage-derived snapshot.

**Target files** (create | edit — file to mirror):
- edit `src/Application/Sessions/Common/SessionTimerSnapshotDtoFactory.cs`, `SessionTimerSnapshotDto.cs` — remaining = active-substage window; keep `ActiveQuestion` nesting
- edit `src/Application/Sessions/Queries/GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQueryHandler.cs`, `GetParticipantSessionTimerSnapshot/GetParticipantSessionTimerSnapshotQueryHandler.cs` — call the re-pointed accessor
- keep `ISessionTimerBroadcaster.cs`, `SessionTimerUpdatedNotificationDto.cs` (re-point payload meaning, keep shape); **do not touch** `ITriviaRoundOrchestratorFacade`/`TriviaRoundOrchestratorFacade` (DES-78)
- edit/mirror `tests/Application.UnitTests/…` timer-query tests — assert the DTO remaining time tracks the active question window and is absent/zero with no active question

**Pattern this phase owns:** none new (`State` realized in X.1; SignalR in X.4).
**Gate:** Application build passes; a test proves the timer DTO/queries return the active-substage (trivia-question) remaining time, `ActiveQuestion` populated only when a question is active, and no reference to a whole-session countdown remains; participant/operator authorization preserved.

**Existing code (keep / delete / decide):**
- keep the two timer query handlers, `ISessionTimerBroadcaster`, `SessionTimerUpdatedNotificationDto` — re-point, do not delete
- delete none (application-layer whole-session-timer references are re-pointed, not new files)
- **settled (OD-2):** the worker/orchestrator boundary lands in X.3 — application keeps `ITriviaRoundOrchestratorFacade` untouched (DES-78)

### Phase X.3 — Infrastructure
**Derive** (`AuthoritativeSessionTimerWorker.cs`; `LiveSessionRepository.ListActiveTimersAsync`; `LiveSessionConfiguration.cs`; ADR-0008 shared Postgres testcontainer):
- The worker **stops ticking the whole-session timer** (`MarkSessionTimerExpiredIfElapsed` + its broadcast/persist). It keeps broadcasting the substage-derived remaining time and — **only where already present and owned by DES-78** — leaves question close/advance to `TriviaRoundOrchestratorFacade`. Per OD-2, HU-22 removes the session-tick; it does **not** add or expand question orchestration.
- `ListActiveTimersAsync` drops the `_sessionTimerAdvancingSince/_sessionTimerExpiredAt` predicate; it selects sessions with an advancing **question** timer.
- `LiveSessionConfiguration` drops the `_sessionTimer*` column mappings; a **new migration drops those columns** (do not preserve stale schema).

**Target files** (create | edit — file to mirror):
- edit `src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs` — remove the session-timer tick; broadcast substage-derived remaining; leave question close/advance to DES-78's facade
- edit `src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs` (`ListActiveTimersAsync`), `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — drop `_sessionTimer*`
- create `src/Infrastructure/Migrations/<timestamp>_DropAuthoritativeSessionTimerColumns.cs` — drop the `_sessionTimer*` columns; mirror `20260604120000_AddAuthoritativeSessionTimerState.cs` (inverted)
- edit/mirror `tests/IntegrationTests/Persistence/LiveSessionRepositoryIntegrationTests.cs` — round-trip a session with an active question timer; assert `_sessionTimer*` columns are gone

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; a migration **drops** the `_sessionTimer*` columns (assert via model snapshot); `ListActiveTimersAsync` returns sessions by advancing **question** timer; a repository integration test round-trips the active-question timer state; **no** whole-session-timer schema remains.

**Existing code (keep / delete / decide):**
- keep the worker's SignalR broadcast + the question-timer selection; keep `LiveSessionRepository`/`LiveSessionConfiguration` (edited, not replaced)
- delete the `_sessionTimer*` column mappings + predicate + the additive `AddAuthoritativeSessionTimerState` columns (via a new down-migration)
- **settled (OD-2):** the worker does **not** assume ownership of question advancement — that stays DES-78; if DES-78 has not yet landed, the worker keeps its existing `CloseAndAdvanceAsync` call **unchanged** (do not delete DES-78's seam)

### Phase X.4 — Api
**Derive** (`Api/Controllers/SessionsController.cs:154-176` timer GETs; `TransitionSessionStateResultDto.Timer`; `Api/Hubs/*`; ADR-0001/0002 gateway auth; ADR-0005 coverage):
- `GET /api/sessions/{liveSessionId}/timer` (operator) and `GET /api/sessions/{liveSessionId}/participants/timer` (participant) return `SessionTimerSnapshotDto` with the **active-substage** remaining time; a trivia question active → the question window; no active question → 0/absent remaining (OD-1/3). Authorization unchanged.
- A valid state transition's `Timer` field carries the same substage-derived snapshot; a live `SessionTimerUpdated` broadcast reaches the `live-session:{id}` SignalR group.

**Target files** (create | edit — file to mirror):
- keep (verify, re-point payload only) `src/Api/Controllers/SessionsController.cs` (two timer GETs + `PATCH …/state` `Timer`), `src/Api/Hubs/SessionsHub.cs`, `src/Api/Hubs/SessionStateBroadcaster.cs`
- edit/mirror `tests/IntegrationTests/Api/…` — timer endpoint tests (active-question remaining; absent with no active question; auth enforced) + a hub test (a `SessionTimerUpdated` broadcast reaches `live-session:{id}`)

**Pattern this phase owns:** none new — the timer GETs inherit the standard operator/participant `AuthorizationBehaviour` guard (ADR-0001/0002); HU-22 is not in the applies-where `Proxy` set → no new gate. **SignalR transport gate verified here.**
**Gate:** endpoint integration tests (operator + participant timer GET → active-substage remaining; auth enforced) + hub test (substage-derived `SessionTimerUpdated` broadcast to `live-session:{id}`); **ADR-0005 coverage** (service ≥ repo gate).

**Existing code (keep / decide):**
- keep `SessionsController.cs` timer GETs + `Timer` field, `SessionsHub.cs`, `SessionStateBroadcaster.cs` — verify + re-point, do not reshape the endpoint routes
- **settled (OD-3):** the frontend contract change (remaining-time meaning) **is** a contract change — surface it at Stop 2 for the frontend slice (versioned note if the frontend team needs one)
