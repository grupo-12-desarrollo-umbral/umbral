# HU-33A Context — Trivia orchestration as a synchronized mission substage (realign)

> Paste this section into any agent session that needs context for HU-33A (DES-78).
> Last updated: 2026-07-05 | Branch: `feature/hu-33a-trivia-substage-orchestration-realign`
> Supersedes the cycle-1 `hu33a-context.md` (DES-44, **Canceled**), which orchestrated a
> **standalone "trivia session"** over one flat question list on the whole
> `MissionRuntimeSnapshot` and ended the session when the list ran out. Canon makes trivia a
> **`SubstagePlayMode` inside a mission substage**: one snapshotted question is active per
> authoritative timer window for all teams; question advancement is timer-driven; the trivia
> substage completes when the final question timer expires and — if another substage exists —
> **all teams advance together** to the next substage in strict mission order; `Finished` is
> reached only via `SessionCompletion` after the final substage.
>
> Boundary note: `SessionOperations` is the authoritative **runtime** owner (it decides when a
> question activates, when a substage completes, and when the session ends). `ScoringMonitoring`
> (HU-37A) consumes the emitted facts to compute the `TriviaSubstageWinner`; it does not drive
> progression. Clients render the timer; they do not own the clock.

## State

- DES-78 (HU-33A): **In Progress**, labels: `canon-realign`, `needs-rebuild`, `svc:session-operations-service`, `Feature`. `ready-for-agent` **not present** at generation time — the human confirmed the ticket is ready and authorized generation (the driver's Step 2 formally applies the label). Blocked-by DES-22 (HU-15), DES-24 (HU-17) — both Done.
- **Resolved mode: realignment-rebuild** (`needs-rebuild` present) — **genuine rebuild.** DES-78 is the realignment map's phase #10 "Trivia play" row: "Synchronized trivia substage orchestration" (`canon-realignment-after-mission-runtime-rewrite.md:106`), the **rebuild successor of the Canceled DES-44** (`:69`, `:146`) — **not** in the superseded column. Unlike the verification-dominant siblings HU-16/HU-21A, HU-33A **changes behaviour**: the flat-list "trivia session" round is re-scoped to a **per-substage** synchronized round with a **substage-advancement pointer** and **timer-driven substage advancement**. This is a delete + redefine + add-pointer + migration + broadcast change across all four layers.
- **Superseded handling applied:** DES-44 (HU-33A cycle-1) is **Canceled** — its flat-list trivia-round code is the **reuse candidate** this rebuild harvests/reworks, never anchored on as a predecessor. Peer rebuilds DES-75 (HU-16), DES-76 (HU-21A), DES-77 (HU-22) are **Done/merged** and are build-on predecessors (below); DES-23/28/30 (their cycle-1 sources) are Canceled — do not anchor on them.
- Predecessor DES ids (build-on, Done/merged): **DES-22 (HU-15)**, **DES-24 (HU-17)**, **DES-76 (HU-21A)**, **DES-77 (HU-22 — the direct seam this HU builds the round on)**, **DES-75 (HU-16)**. Landed-untouched: DES-25 (HU-18), DES-26 (HU-19), DES-27 (HU-20), DES-11 (HU-07A), DES-12 (HU-07B).
- PRD DES id: **DES-70** → `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md` (authoritative; never re-fetch PRD scope from Linear). Overlaid by `backend/docs/canon-realignment-after-mission-runtime-rewrite.md`.
- Governing ADR: **`backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`** (accepted) — fixes the `ActiveSubstageId` pointer + generic timer-driven `SubstageAdvancement` + trivia-only activation + `TriviaSubstageWinner`-emitted-not-computed contract **for DES-78**. This is a **required input**, not the coverage ADR. (Disambiguation: "ADR-0005 coverage" elsewhere = `backend/docs/adr/0005-coverlet-msbuild-for-aggregate-coverage.md`, a different file.)
- Canonical source: `backend/services/session-operations-service/CONTEXT.md` §Trivia terms (`:125-159`), §SubstageAdvancement (`:121-123`), §SessionCompletion (`:177-178`), §Active (`:47-48`); `backend/docs/grilling-session-mission-restructure.md` §Trivia (`:55-70`), §Session Lifecycle (`:83-99`), §Completion (`:101-112`); `bd_umbral_entity_spec.md` §MissionRuntimeSnapshot (`:326-360`).
- Branch: `feature/hu-33a-trivia-substage-orchestration-realign`, base **`develop`** (all build-on predecessors Done/merged — HU-22/DES-77 merged as commit `6ff5b5f`; no same-service predecessor In Progress).

## Required design patterns

HU-33 is the only backlog HU mandating **three** patterns (`required_patterns_matrix.md:131,175`). HU-33A (round open/activation + close-and-advance) owns all three; HU-33B (round close/results) reuses the seams.

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | `required_patterns_matrix.md:41,131`; `CONTEXT.md:197-199`: play-mode-specific internal phases stay subordinate to the `LiveSession` lifecycle. | Question activation, timer advance, and **substage advancement** are gated by the per-`SessionState` type via `LiveSessionStateFactory.For(State)`: only `Active` advances/activates; `Paused` freezes the active question timer and resumes the **same** question; `Finished`/`Cancelled` stop — **no ad-hoc `if (State == …)` checks** and no operator-forced advance. Already realized by HU-21A/HU-22; HU-33A **extends** the same dispatch to substage advancement. |
| `Facade` (mandated) | X.2 Application | `required_patterns_matrix.md:40,131`; `CONTEXT.md:193-195`: session orchestration + outbound event/broadcast behind a narrow coordination service. | `TriviaRoundOrchestratorFacade` is the **single** orchestration entry point for activate-question, close-and-advance-question, and **close-substage-and-advance-substage**. It coordinates strategy selection, domain mutation, persistence, and SignalR broadcast — the timer worker and the session-`Active` event handler stay thin (they call the facade). No advancement logic scattered across worker/handler/endpoint. |
| `Strategy` (mandated) | X.1 Domain | `required_patterns_matrix.md:44,131`; ADR-0004 (`Strategy` = mode-specific progression policy). | `IQuestionActivationStrategy` / `SequentialQuestionActivationStrategy` is re-scoped to return the next question **within the active substage** (`ActiveSubstageId`), or `null` when the active substage's questions are exhausted (the signal to advance the substage). It stays a named interface (extension seam for HU-33B's full round engine) even with one implementation. |

Transport note: HU-33A carries **SignalR / WebSockets** (`required_patterns_matrix.md:57,131`) — a **hard gate** verified in X.4. `QuestionActivated`, `QuestionClosed`, and a **`SubstageAdvanced`** notification broadcast to the `live-session:{id}` group over `SessionsHub`; the pre-game countdown ticks over the existing timer broadcaster. **RabbitMQ is out of scope for HU-33A** (D-1 below) — the matrix lists RabbitMQ for HU-33 because the **results/winner** publication (round close → `ScoringMonitoring`) rides the round-close/scoring slices; HU-33A **emits the domain `SubstageAdvancedEvent`** (ADR-0005) but adds no cross-service publish until a consumer (HU-37A) lands. This mirrors HU-21A deferring RabbitMQ to HU-21B.

Applies-where note (no new gate): HU-33A adds no new protected endpoint (D-3 below); the existing session reads/timer inherit the standard operator / participant-membership `AuthorizationBehaviour`/gateway guard (ADR-0001/0002). HU-33A is not in the applies-where `Proxy` set (HU-04/05/36B) — **no new `Proxy` gate**.

## Resolved decisions (committed scope)

Surfaced rather than guessed (`canon-realignment-workflow.md` open-decisions protocol; generator constraint 3), resolved 2026-07-05 by ADR-0005 + the canon authority chain. These are committed scope, not pending gates.

- **D-1 — RESOLVED: SignalR only; no RabbitMQ publish in HU-33A.** ADR-0005: the `TriviaSubstageWinner` is **emitted, not computed** — session-ops raises `SubstageAdvancedEvent(fromSubstageId, fromPlayMode, toSubstageId)` (+ the existing `SessionStateChangedEvent(→Finished)`); `ScoringMonitoring` (HU-37A) consumes these to derive the winner. HU-37A does not exist yet and no cross-service consumer is registered, so **no RabbitMQ publish is added here** ("no dedicated event/publish until a consumer needs one"). HU-33A broadcasts advancement live via SignalR only.
- **D-2 — RESOLVED: winner is NOT computed here.** The `ScoreEntry` ledger (HU-37A) and trivia answers (HU-34A/B) do not exist yet; computing the winner in session-ops would cross the `CONTEXT.md:187-189` Runtime-Authority boundary (scoring supplies derived views). HU-33A emits the completion signal; the winner is downstream.
- **D-3 — RESOLVED: no new REST endpoint; broadcast-centric X.4.** The synchronized active question is already exposed to participants/operator via HU-22's `SessionTimerSnapshotDto.ActiveQuestion` on the two timer GETs. Answer submission (HU-34) and answer monitoring (HU-36) own their own surfaces. HU-33A's API layer therefore **verifies the SignalR broadcasts** (`QuestionActivated`/`QuestionClosed`/`SubstageAdvanced`) + the substage-scoped active-question value on the timer read; it adds **no** REST endpoint and **no operator advance endpoint** (canon: operators cannot force substage advancement — `CONTEXT.md:122`).
- **D-4 — RESOLVED: generic advancement, trivia-only activation.** ADR-0005: `SubstageAdvancement` is timer-driven and **generic across play modes** (next substage in strict order, all teams together, no operator override). Advancing **into** a trivia substage activates its first question; advancing **into** a treasure-hunt substage **parks** (sets `ActiveSubstageId`, stops — its own runtime, HU-29–32, is downstream). A mixed mission run before the treasure-hunt runtime exists **parks** at the treasure-hunt substage rather than crashing. **DES-78 verifies end-to-end only on an all-trivia multi-substage mission** and does not claim mixed missions run.

## What predecessors have already landed

**Build-on (read for derivation):**

- **DES-22 (HU-15) — Done.** `LiveSession.Create(...)` holds the immutable `MissionRuntimeSnapshot`: the ordered `StageSnapshots → SubstageSnapshots` tree in strict order (`StageSnapshot.cs`, `SubstageSnapshot.cs` with `SubstageSnapshotId`, `SequenceOrder`, `PlayMode`), the flat `TriviaQuestionSnapshots` and `TargetSnapshots` collections **each keyed to their owning substage by `SubstageSnapshotId`** (`TriviaQuestionSnapshot.cs:49`, `TargetSnapshot.SubstageSnapshotId`). This is the seam HU-33A walks — the snapshot already models the full substage structure; **no snapshot schema change is needed.**
- **DES-77 (HU-22) — Done (the direct seam).** The authoritative timer is keyed off the active `TriviaQuestion` via `ActiveQuestionIndex`: the `_questionTimer*` window (`LiveSession.cs:15-18`), `ActivateQuestion` (`:248`), `MarkQuestionTimerExpiredIfElapsed` (`:272`), `CloseActiveQuestion` (`:277`), freeze/resume through `ActiveLiveSessionState`/`PausedLiveSessionState`, `AuthoritativeSessionTimerWorker` driving `CloseAndAdvanceAsync` on question expiry, and the whole-session countdown **deleted** (migration `20260705162936_DropSessionTimerColumns`). HU-22 explicitly **reserved question activation/advancement/close + `TriviaRoundOrchestratorFacade` + `SequentialQuestionActivationStrategy` + `QuestionActivated/ClosedEvent` + the substage pointer/advancement for DES-78** (`hu22-context.md:79,85`). HU-33A owns exactly that reserved seam.
- **DES-76 (HU-21A) — Done.** Locked the canonical `Scheduled → Preparing → Active → Paused → Finished → Cancelled` machine, the CoR transition validators, `TransitionSessionStateFacade`, and the `SessionStateChanged` SignalR broadcast. HU-21A left the **first-substage start on `Preparing → Active`** as a downstream play-layer seam (`hu21a-context.md:73`) — HU-33A implements it (set `ActiveSubstageId`, activate first trivia question). Do not touch the transition matrix.
- **DES-75 (HU-16) — Done.** Locked the whole-quiz snapshot-content-fidelity invariant (every question/option/correct-flag/score/timer copied in mission order). HU-33A executes against that frozen content; it does not re-verify fidelity.

**Landed, untouched by this HU:** DES-25 (HU-18 team association), DES-26 (HU-19 operator assignment), DES-27 (HU-20 assigned-session reads), DES-11/12 (HU-07A/07B membership + reconnect) — build around the session aggregate but do not inform the round orchestration.

**Superseded / not predecessors (do NOT anchor on):** DES-44 (HU-33A cycle-1, Canceled — the reuse candidate reworked here), DES-23/28/30 (cycle-1 HU-16/21A/22 sources, Canceled).

**Coverage:** session-operations-service carries the HU-07/15/16/17/18/19/20/21A/22 baseline; measure the real service percentage against the ADR-0005 (coverlet) repo gate at X.4 — do not assume a carried-forward number.

## What this HU adds

| Concern | New work |
|---|---|
| Active-substage pointer | `LiveSession.ActiveSubstageId` (nullable `Guid`) — the **single authoritative** pointer for the live substage, walking strict stage→substage order over the snapshot; null before `Active`. Persisted as `active_substage_id`. (ADR-0005.) |
| First-substage start on `Active` | On `Preparing → Active`, set `ActiveSubstageId` to the **first substage** in strict order; if it is a trivia substage, activate its first question (via the facade). Implements the `CONTEXT.md:48` "entering `Active` immediately starts the first substage" seam HU-21A reserved. |
| Substage-scoped question activation | Re-scope `ActiveQuestionIndex` semantics + `ActivateQuestion`/`EnsureCanActivateQuestion`/`CloseActiveQuestion` so activation/close operate over the **questions of the active substage** (`TriviaQuestionSnapshots.Where(q => q.SubstageSnapshotId == ActiveSubstageId)` ordered by `SequenceOrder`), not the flat global list. |
| Substage advancement | New domain behaviour (`AdvanceToNextSubstage`/`CompleteActiveSubstageAndAdvance`): when the **last question of the active substage** closes, walk to the next substage in strict order. Trivia next → activate its first question; treasure-hunt next → **park** (D-4); no next → `SessionCompletion` → `MoveTo(Finished)`. Generic across modes; timer-driven; no operator override. |
| `SubstageAdvancedEvent` | New domain event `(LiveSessionId, FromSubstageId, FromPlayMode, ToSubstageId?)` raised on each advancement (ADR-0005) — the fact `ScoringMonitoring` (HU-37A) will consume to derive `TriviaSubstageWinner`. |
| `Strategy` re-scope | `SequentialQuestionActivationStrategy.Next` counts/orders questions **of the active substage** (via `ActiveSubstageId`), returns the next in-substage position or `null` when the substage is exhausted. |
| Facade re-scope | `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync`: close question → next question **in the same substage**, else close substage → advance pointer → (trivia) activate first question / (treasure-hunt) park / (none) `Finished`. `ActivateNextQuestionAsync` and the session-`Active` handler set/read `ActiveSubstageId`. Broadcast `SubstageAdvanced`. |
| Migration | `active_substage_id` (nullable `uuid`) on `live_sessions`. Mirror `20260604143000_AddTriviaRoundState.cs`. No snapshot-schema change (questions already carry `SubstageSnapshotId`). |
| SignalR | Add a `SubstageAdvanced` notification (`fromSubstageId`, `fromPlayMode`, `toSubstageId?`) to `live-session:{id}`; keep `QuestionActivated`/`QuestionClosed`; the timer read's `ActiveQuestion` now reflects the active-substage question. |
| Behavioural change (call out) | The session **no longer `Finished`s when the flat question list ends** (cycle-1 `TriviaRoundOrchestratorFacade.cs:84`); it advances substage-by-substage and reaches `Finished` only via `SessionCompletion` after the **final** substage completes. Contract note for the frontend/operator monitor. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain (`ActiveSubstageId` + substage advancement + `SubstageAdvancedEvent` + substage-scoped activation + State/Strategy re-scope), application (facade + session-`Active` handler re-scope + `SubstageAdvanced` broadcast DTO), infrastructure (migration + persistence config + worker verify), api (SignalR broadcast verification; no new REST route).
- `frontend/` — participant question screen + operator monitoring reflect the synchronized active-substage question and substage advancement (behavioural change: no session-level "round over when questions end").
- Runtime/contract boundary: new `SubstageAdvanced` SignalR notification; the timer read's `ActiveQuestion` is now the active-substage question. **No REST contract change** (D-3). The `SubstageAdvancedEvent` domain fact is the future `ScoringMonitoring` (HU-37A) integration seam.

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **`ActiveQuestionIndex` becomes substage-scoped, not global.** Cycle-1 treats it as a position into the whole snapshot's flat `TriviaQuestionSnapshots` (`SequentialQuestionActivationStrategy.cs:15`, `TriviaRoundOrchestratorFacade.cs:84`). HU-33A re-scopes every consumer to order/count **only the active substage's** questions (`SubstageSnapshotId == ActiveSubstageId`). Grep every reader of `ActiveQuestionIndex` / `TriviaQuestionSnapshots` before editing — the strategy, the facade, `LiveSession.ActivateQuestion`, and `SessionTimerSnapshotDtoFactory` all apply the same ordering and must agree on the new substage scope.
- **Do NOT re-derive from `TriviaQuiz`.** Play executes against the immutable `MissionRuntimeSnapshot` only (HU-15/16). Never read authoring content at runtime.
- **`Finished` only via `SessionCompletion`.** Never operator-forced (`CONTEXT.md:56`, `grilling…:94`). The final substage completing is the **only** path to `Finished`; delete the cycle-1 "flat list exhausted → Finished" shortcut and route it through substage advancement.
- **Advancement is generic + operator-supervised.** No operator command/endpoint forces advancement (`CONTEXT.md:122`, D-3). It is emitted solely by the authoritative timer worker on last-question expiry — that is what satisfies AC #3 ("el operador no controla manualmente el avance de subetapa").
- **Trivia-only activation; treasure-hunt parks (D-4).** Advancing into a treasure-hunt substage sets `ActiveSubstageId` and stops. Do **not** build treasure-hunt play (HU-29–32, downstream). Verify end-to-end on an **all-trivia multi-substage** mission.
- **Extend the timer worker, don't fork it.** `AuthoritativeSessionTimerWorker` already ticks the question timer and calls `CloseAndAdvanceAsync` (HU-22 left this seam). HU-33A makes `CloseAndAdvanceAsync` advance substages; the worker stays one cohesive service.
- **`CloseAndAdvance` must stay idempotent.** The worker ticks every second; guard on `ActiveQuestionIndex`/`ActiveSubstageId` so a mid-persist tick does not double-advance (cycle-1 already null-guards the active-question — extend the guard to the substage boundary).
- **Pause freezes the active question and resumes the same one** (`CONTEXT.md:52`, `grilling…:90`) — already realized by HU-22's `Paused`/`Active` `Enter` hooks; HU-33A keeps them and must not advance while `Paused`.
- **`SubstageAdvancedEvent` is a domain event (MediatR), not a RabbitMQ publish** (D-1). Raise it; broadcast via SignalR; leave cross-service publication to HU-37A.
- **Namespace is `umbral_backend.*`** across all layers; source root `src/`. Domain tests in `tests/UnitTests/`, application tests in `tests/Application.UnitTests/`, integration in `tests/IntegrationTests/`. API layer is **Controllers** (`Api/Controllers/SessionsController.cs`), not minimal-API endpoints — mirror existing files.
- **Application-layer placement (ADR-0011/0012):** the `Facade` stays in `Application/Sessions/Common/` (shared by ≥2 slices); the `Strategy`, events, and `ActiveSubstageId` live in Domain. Do **not** introduce `Handlers/`/`DTOs/` type-buckets or a handler base class (generator constraint 11).

## Per-phase derivation — authoritative for implementation

> Primary source for the phase subagent (per `backend-agent.md` "Read first"). Do not re-read
> the full canon or re-inspect the tree; open a cited section only to fill a gap a block leaves open.
> Mode = **realignment-rebuild**: authority chain **canon docs > tracker AC > existing code**
> (`canon-realignment-workflow.md:19-34`); keep/delete/decide per `:72-84`. Mirror-anchors point
> **only** at code classified `keep`. Governing contract: `backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`.
> Canon: trivia synchronized, timer-driven advancement, substage completes on final-question
> expiry, all teams advance together (`grilling…:57-61`, `CONTEXT.md:129-139`); winner emitted, not
> computed (ADR-0005; `CONTEXT.md:157-159`); `Finished` only via `SessionCompletion` (`CONTEXT.md:56,177-178`);
> operators cannot force advancement (`CONTEXT.md:122`); questions carry `SubstageSnapshotId`
> (`TriviaQuestionSnapshot.cs:49`). Patterns `required_patterns_matrix.md:40,41,44,131`; placement
> ADR-0011 (`structure.md`) + ADR-0012.

### Phase X.1 — Domain
**Derive** (`CONTEXT.md:47-48,121-139,157-159,177-178`; `grilling…:57-61,87,94,103`; ADR-0005; `bd_umbral_entity_spec.md:326-360`):
- `LiveSession.ActiveSubstageId` (nullable `Guid`) — the single authoritative live-substage pointer. Add `AdvanceToNextSubstage(now)` / `CompleteActiveSubstageAndAdvance(now)`: walk `MissionRuntimeSnapshot.StageSnapshots.SelectMany(s => s.SubstageSnapshots)` in strict stage-order → substage-order, find the substage after `ActiveSubstageId`. Next exists → set `ActiveSubstageId`, raise `SubstageAdvancedEvent(LiveSessionId, from, fromPlayMode, to)`, and if the new substage is `Trivia` leave it for the facade to activate its first question (treasure-hunt → park, D-4). Next is none → `SessionCompletion`: raise `SubstageAdvancedEvent(…, to: null)` then `MoveTo(Finished, now, policy)` (`Finished` only here — `CONTEXT.md:56`).
- Re-scope question activation to the active substage: `ActivateQuestion` (`:248`)/`EnsureCanActivateQuestion` (`:453`)/`CloseActiveQuestion` (`:277`) and the ordering helper `GetOrderedTriviaQuestions` (`:501-506` — currently orders **all** snapshot questions with no substage filter, the key gap) resolve against the **active substage's** questions (`SubstageSnapshotId == ActiveSubstageId`, ordered by `SequenceOrder`). `CloseActiveQuestion` on the substage's **last** question is the trigger the facade turns into substage advancement. Keep the `_questionTimer*` window + freeze/resume (HU-22) untouched.
- `SequentialQuestionActivationStrategy.Next` (`Strategy`) counts/orders **the active substage's** questions; returns the next in-substage position, `0` on entry to a trivia substage, or `null` when exhausted (advance signal).
- `State` gates it: activation/advance only in `Active`; `Paused` freezes; `Finished`/`Cancelled` stop — via `LiveSessionStateFactory.For(State)`. No operator-forced advance.

**Target files** (create | edit — file to mirror):
- edit `src/Domain/Entities/LiveSession.cs` — add `ActiveSubstageId` + substage-advance methods; re-scope `ActivateQuestion` (`:248`)/`EnsureCanActivateQuestion` (`:453`)/`CloseActiveQuestion` (`:277`) to the active substage; mirror the existing `_questionTimer*` method shape
- edit `src/Domain/Services/SequentialQuestionActivationStrategy.cs` — order/count the active substage's questions (replace the flat `snapshot.TriviaQuestionSnapshots.Count` at `:15`)
- create `src/Domain/Events/SubstageAdvancedEvent.cs` — mirror `src/Domain/Events/QuestionClosedEvent.cs`
- keep `src/Domain/ValueObjects/{StageSnapshot,SubstageSnapshot,TriviaQuestionSnapshot}.cs`, `src/Domain/Services/{IQuestionActivationStrategy,SessionStateTransitionPolicy}.cs`, `src/Domain/Services/SessionStates/*`, `src/Domain/Events/{QuestionActivatedEvent,QuestionClosedEvent,SessionStateChangedEvent}.cs`
- edit `tests/UnitTests/Domain/Entities/LiveSessionTests.cs` (+ a substage-advancement test) — assert: entering `Active` sets `ActiveSubstageId` to the first substage; questions activate/close within the active substage only; closing the substage's **last** question advances to the next substage (trivia → first question ready; treasure-hunt → parked); no next → `Finished` via completion; a `SubstageAdvancedEvent` is raised per advancement; advance rejected outside `Active`

**Pattern this phase owns:** `State` (activation/advance gated by per-`SessionState` type) + `Strategy` (substage-scoped `IQuestionActivationStrategy`).
**Gate:** Domain build passes; a unit test locks: first-substage start on `Active`; substage-scoped activation/close; last-question close → substage advancement (trivia activates next, treasure-hunt parks, none → `Finished` via `SessionCompletion`); `SubstageAdvancedEvent` raised with `from/fromPlayMode/to`; advancement only in `Active`, frozen in `Paused`; **no flat-list "questions exhausted → Finished" path remains.** **`State`/`Strategy` verified — decided by per-state types + the strategy interface, not ad-hoc conditionals.**

**Existing code (keep / delete / decide):**
- keep the `_questionTimer*` window + freeze/resume on `LiveSession.cs` (`:15-18,272-278,398-449`), `SessionState`/`SessionStates/*`/`SessionStateTransitionPolicy`, the snapshot VOs, `QuestionActivated/ClosedEvent`, `IQuestionActivationStrategy` — canon-aligned (HU-15/16/21A/22); verify + mirror
- delete/replace the **flat-list scope**: the whole-snapshot question count/order in `SequentialQuestionActivationStrategy.cs:15` and the flat activation range in `LiveSession.ActivateQuestion`/`EnsureCanActivateQuestion` — re-scope to the active substage; delete the cycle-1 assumption that the trivia round spans the whole snapshot
- decide none — the snapshot already carries `SubstageSnapshotId` on every question/target (`TriviaQuestionSnapshot.cs:49`); no schema gap. `Finished`-shortcut lives in the Application facade (X.2), removed there.

### Phase X.2 — Application — **owns `Facade`**
**Derive** (`CONTEXT.md:193-195`; ADR-0005; the cycle-1 `TriviaRoundOrchestratorFacade` + `TriviaRoundStartedNotificationHandler`):
- `TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync` (rework `:50-86`): close the active question + broadcast `QuestionClosed`; ask the strategy for the next **in-substage** question — present → activate it; **absent** → the active substage is complete: call the domain substage-advance, persist, broadcast `SubstageAdvanced`, then (new substage trivia → activate its first question; treasure-hunt → park; none → the domain already moved to `Finished`, just persist). **Delete the direct `MoveTo(Finished)` at `:84`** — completion routes through substage advancement.
- `TriviaRoundStartedNotificationHandler` (rework `:30-68`): on `SessionStateChangedEvent(→Active)`, set `ActiveSubstageId` to the first substage (domain call), run the pre-game countdown (keep), then `ActivateNextQuestionAsync` **only if** the first substage is trivia (treasure-hunt → parked).
- `ActivateNextQuestionAsync` (`:29-48`) reads the active substage via the re-scoped strategy. Add a `SubstageAdvanced` broadcast DTO + broadcaster method (mirror `QuestionClosedNotificationDto`/`ISessionQuestionBroadcaster`).
- **No RabbitMQ** (D-1); the domain `SubstageAdvancedEvent` is the downstream scoring seam.

**Target files** (create | edit — file to mirror):
- edit `src/Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs` — substage-aware close-and-advance; remove the `:84` `Finished` shortcut
- edit `src/Application/Sessions/EventHandlers/TriviaRoundStartedNotificationHandler.cs` — set first substage + trivia-only first-question activation
- create `src/Application/Sessions/Common/SubstageAdvancedNotificationDto.cs` — mirror `QuestionClosedNotificationDto.cs`; add its method to `src/Application/Common/Interfaces/ISessionQuestionBroadcaster.cs`
- edit `src/Application/Sessions/Common/TriviaQuestionSnapshotSelector.cs` (`:10-23` — currently orders **all** questions; re-scope to the active substage, matching the X.1 domain re-scope); the timer DTO factory `SessionTimerSnapshotDtoFactory.cs` (`:32-53`) reads the pointer through the selector and follows automatically — add a test asserting its `ActiveQuestion` is the active-substage question
- keep `src/Application/Sessions/Common/{ITriviaRoundOrchestratorFacade,QuestionActivatedNotificationDto,QuestionClosedNotificationDto}.cs`, `src/Application/DependencyInjection.cs` (registrations `:43-44` unchanged)
- edit/mirror `tests/Application.UnitTests/Sessions/Facades/TriviaRoundOrchestratorFacadeTests.cs` + `.../EventHandlers/TriviaRoundStartedNotificationHandlerTests.cs` — assert: last-question close advances the substage (trivia → next activates, treasure-hunt → parks, none → `Finished`); `SubstageAdvanced` broadcast; first-substage set on `Active`; idempotent double-tick does not double-advance

**Pattern this phase owns:** `Facade` — the single orchestration entry point; worker/handler stay thin.
**Gate:** Application build passes; a test proves the facade closes-and-advances within and across substages (trivia activates the next question or advances the substage; treasure-hunt parks; final substage completion → `Finished` via `SessionCompletion`), broadcasts `QuestionClosed`/`SubstageAdvanced`, and is idempotent under repeat ticks; the session-`Active` handler sets the first substage and activates the first question only for trivia; **no `MoveTo(Finished)` on flat-list exhaustion remains**; no RabbitMQ publish added. **`Facade` verified — orchestration not scattered across worker/handler/endpoint.**

**Existing code (keep / delete / decide):**
- keep `ITriviaRoundOrchestratorFacade`, the question notification DTOs, the DI registrations, `ISessionQuestionBroadcaster` (extended, not replaced)
- delete the cycle-1 flat-list `Finished` shortcut in `TriviaRoundOrchestratorFacade.cs:84` — replace with substage advancement; re-scope `TriviaQuestionSnapshotSelector.cs:10-23` (all-questions → active-substage) alongside the X.1 domain re-scope
- decide none

### Phase X.3 — Infrastructure
**Derive** (`AuthoritativeSessionTimerWorker.cs`; `LiveSessionConfiguration.cs`; `ApplicationDbContextModelSnapshot.cs`; ADR-0008 shared Postgres testcontainer):
- New migration adds `active_substage_id` (`uuid`, nullable) to `live_sessions`; map it in `LiveSessionConfiguration`. Mirror `20260604143000_AddTriviaRoundState.cs`. No snapshot-content schema change (questions already carry `SubstageSnapshotId`).
- `AuthoritativeSessionTimerWorker`: verify it still drives `CloseAndAdvanceAsync` on question-timer expiry (HU-22 left the call) and now advances substages through the reworked facade — **do not fork it**. `ListActiveTimersAsync` (advancing-question predicate) is unchanged.

**Target files** (create | edit — file to mirror):
- create `src/Infrastructure/Migrations/<timestamp>_AddActiveSubstagePointer.cs` — add `active_substage_id`; mirror `20260604143000_AddTriviaRoundState.cs`
- edit `src/Infrastructure/Persistence/Configurations/LiveSessionConfiguration.cs` — map `ActiveSubstageId`
- keep `src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs`, `src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs` (`ListActiveTimersAsync`), `src/Infrastructure/Realtime/SignalRSessionQuestionBroadcaster.cs` (extend for `SubstageAdvanced`)
- edit/mirror `tests/IntegrationTests/Persistence/LiveSessionRepositoryIntegrationTests.cs` — round-trip a session with an `ActiveSubstageId` set; assert the column persists and the active substage survives reload

**Pattern this phase owns:** none.
**Gate:** Infrastructure build passes; a migration **adds** `active_substage_id` (assert via model snapshot — grep, do not full-read `ApplicationDbContextModelSnapshot.cs`); a repository integration test round-trips a session with the active-substage pointer and its active-substage question timer; the worker drives substage advancement through the facade on expiry (no fork).

**Existing code (keep / delete / decide):**
- keep the worker, `LiveSessionRepository`, `LiveSessionConfiguration` (edited, not replaced), `SignalRSessionQuestionBroadcaster` (extended)
- delete none
- decide none

### Phase X.4 — Api
**Derive** (`Api/Controllers/SessionsController.cs`; `Api/Hubs/*`; ADR-0001/0002 gateway auth; ADR-0005 coverage; D-3):
- **No new REST endpoint** (D-3). Verify the two timer GETs' `SessionTimerSnapshotDto.ActiveQuestion` now reflects the **active-substage** question, and that the SignalR broadcasts (`QuestionActivated`, `QuestionClosed`, new `SubstageAdvanced`) reach the `live-session:{id}` group. **No operator advance endpoint** (canon forbids operator-forced advancement).
- Behavioural change: a multi-substage trivia session advances substage-by-substage and reaches `Finished` only after the final substage — surface for the frontend/operator monitor.

**Target files** (create | edit — file to mirror):
- keep (verify, do not reshape) `src/Api/Controllers/SessionsController.cs`, `src/Api/Hubs/SessionsHub.cs`
- edit/mirror `tests/IntegrationTests/Api/…` — a hub test that a trivia round drives `QuestionActivated` → `QuestionClosed` → (last question) `SubstageAdvanced` to `live-session:{id}`, and that the timer read exposes the active-substage question; auth enforced on the reads

**Pattern this phase owns:** none new — `State`/`Strategy` in X.1, `Facade` in X.2; the reads inherit the standard operator/participant `AuthorizationBehaviour` guard (ADR-0001/0002); HU-33A is not in the applies-where `Proxy` set → no new gate. **SignalR transport gate verified here.**
**Gate:** hub integration test (a synchronized trivia round broadcasts `QuestionActivated`/`QuestionClosed`/`SubstageAdvanced` to `live-session:{id}`; the timer read reflects the active-substage question; auth enforced) + **ADR-0005 coverage** (service ≥ repo gate); **no new REST/operator-advance endpoint added.**
