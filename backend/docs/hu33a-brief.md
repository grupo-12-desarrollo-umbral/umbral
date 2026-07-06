# HU-33A — Driver brief
_Driver reads only this file. The full prompt + context are for the subagent
and human review; the driver never loads them._

> **Nature of this HU:** realignment-rebuild — **genuine rebuild, not verification.** Cycle 1
> (DES-44, Canceled) ran a standalone "trivia session" over one flat question list and moved the
> **session** to `Finished` when the list ran out (`TriviaRoundOrchestratorFacade.cs:84`). HU-33A
> re-scopes the round to a **mission trivia substage**: add `LiveSession.ActiveSubstageId` +
> timer-driven `SubstageAdvancement` (X.1 domain), re-scope question activation to the active
> substage, delete the flat-list session-finish shortcut (X.2), migrate `active_substage_id`
> (X.3), and verify the SignalR broadcasts (X.4). Never authorize a subagent to build treasure-hunt
> play (HU-29–32 — advancing there **parks**), compute the `TriviaSubstageWinner` (emitted not
> computed — HU-37A), add trivia answers/monitoring (HU-34/HU-36), a RabbitMQ publish, or a new/
> operator-advance endpoint.
>
> **✅ The four decisions D-1…D-4 are RESOLVED and committed to scope** (2026-07-05, by ADR-0005) — X.1 may start once Stop 1 confirms the slice is grabbed.

## Slice
| HU | DES | PRD | Service | Branch | Base |
|----|-----|-----|---------|--------|------|
| HU-33A — Orquestación de trivia como subetapa sincronizada | DES-78 | DES-70 | session-operations-service | feature/hu-33a-trivia-substage-orchestration-realign | develop |

Governing contract: `backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md`.

## Resolved decisions (committed scope — the phases build to these)
- **D-1 — RESOLVED: SignalR only.** `SubstageAdvancedEvent` is a domain event; no RabbitMQ publish until a consumer (HU-37A) lands.
- **D-2 — RESOLVED: winner emitted, not computed.** Session-ops raises `SubstageAdvancedEvent(fromSubstageId, fromPlayMode, toSubstageId)` (+ `SessionStateChangedEvent(→Finished)`); `ScoringMonitoring` (HU-37A) derives `TriviaSubstageWinner`. No `ScoreEntry` ledger / trivia answers exist yet.
- **D-3 — RESOLVED: no new REST/operator-advance endpoint.** The synchronized active question rides HU-22's `SessionTimerSnapshotDto.ActiveQuestion`; X.4 is broadcast-centric. Advancement is timer-driven (canon forbids operator-forced advancement).
- **D-4 — RESOLVED: generic advancement, trivia-only activation.** Advancing into a treasure-hunt substage **parks** (HU-29–32 downstream); verify end-to-end only on an **all-trivia multi-substage** mission.

## Required pattern(s) → owning phase
HU-33 is the only backlog HU mandating three patterns (`required_patterns_matrix.md:131`). HU-33A owns all three.
- **`State`** (phase X.1) — `:41,131`: activation/timer/substage advancement gated by the per-`SessionState` type via `LiveSessionStateFactory` (Active advances, Paused freezes, Finished/Cancelled stop) — obligation: no ad-hoc `if (State==…)`, no operator-forced advance.
- **`Facade`** (phase X.2) — `:40,131`: `TriviaRoundOrchestratorFacade` is the single orchestration entry point (activate-question / close-and-advance-question / close-substage-and-advance-substage); worker + handler stay thin.
- **`Strategy`** (phase X.1) — `:44,131`: `IQuestionActivationStrategy` re-scoped to return the next question **within the active substage**, `null` when exhausted (the advance signal).
- **Transport SignalR** (phase X.4) — `:57,131`: `QuestionActivated`/`QuestionClosed`/`SubstageAdvanced` to `live-session:{id}`. **No RabbitMQ.**
- No new `Proxy` gate — HU-33A adds no protected endpoint (D-3); existing reads inherit the standard operator/participant-membership `AuthorizationBehaviour`/gateway guard (ADR-0001/0002).

## Per phase — gate + owned pattern
| Phase | Gate | Pattern |
|-------|------|---------|
| X.1 Domain | Domain build; unit test locks: entering `Active` sets `ActiveSubstageId` to the first substage; activation/close within the active substage only; last-question close advances the substage (trivia → next activates, treasure-hunt → parks, none → `Finished` via `SessionCompletion`); `SubstageAdvancedEvent(from,fromPlayMode,to)` raised; advance only in `Active`, frozen in `Paused`; **no flat-list "questions exhausted → Finished" path remains** | `State` + `Strategy` |
| X.2 Application | App build; test proves the Facade closes-and-advances within/across substages (trivia activates next / treasure-hunt parks / final → `Finished` via completion), broadcasts `QuestionClosed`/`SubstageAdvanced`, idempotent under repeat ticks; the session-`Active` handler sets the first substage + activates the first question only for trivia; no `MoveTo(Finished)` on flat-list exhaustion; no RabbitMQ | `Facade` |
| X.3 Infrastructure | Infra build; a migration **ADDS** `active_substage_id` (nullable uuid) to `live_sessions` (assert via model snapshot); repo integration test round-trips a session with the active-substage pointer + its question timer; `AuthoritativeSessionTimerWorker` drives advancement through the reworked facade on expiry (no fork) | — |
| X.4 Api | Hub integration test: a trivia round broadcasts `QuestionActivated`→`QuestionClosed`→(last question)`SubstageAdvanced` to `live-session:{id}`; the timer read exposes the active-substage question; auth enforced + ADR-0005 coverage. **No new REST/operator-advance endpoint** | — (standard `AuthorizationBehaviour`, no new Proxy) |

Commit subjects — copy each phase's exact subject from the prompt's Steps 5–8 verbatim:
- X.1 `feat(session-operations): phase X.1 - domain layer (HU-33A)`
- X.2 `feat(session-operations): phase X.2 - application layer (HU-33A)`
- X.3 `feat(session-operations): phase X.3 - infrastructure layer (HU-33A)`
- X.4 `feat(session-operations): phase X.4 - api layer (HU-33A)`

Trailer (every phase): `Ref: HU-33A` / `Ref: DES-78` / `Ref: DES-70`

## Acceptance criteria
- the automated round runs inside a trivia substage of a mission (`ActiveSubstageId`, strict stage→substage order)
- when the final question timer expires, the substage closes and — if another substage exists — all teams advance together to the next substage; `Finished` only via `SessionCompletion` after the final substage
- the operator does not manually control substage advancement (timer-driven, operator-supervised)
- **out of scope (surface, do not build):** treasure-hunt play (HU-29–32 — parks); `TriviaSubstageWinner` computation (emitted, not computed — HU-37A); trivia answer submission/monitoring (HU-34/HU-36); RabbitMQ publish (D-1); any new REST/operator-advance endpoint (D-3)

## Endpoints + smoke (driver verifies at Stop 2)
_No new endpoints (D-3). The synchronized active question rides HU-22's timer read; advancement is timer-driven — smoke it by driving the timer, not an endpoint. Behavioural change: the session advances substage-by-substage and Finishes only after the final substage (not when the flat question list ends)._
After the X.4 docker rebuild (`docker compose build session-operations-service api-gateway && docker compose up -d session-operations-service api-gateway`), smoke on an **all-trivia, ≥2-substage** mission through the gateway:
- `POST /api/sessions` (mission with ≥2 trivia substages) → **201**; then `PATCH …/state` → `Preparing` → `Active`
- `GET /api/sessions/{liveSessionId}/timer` — Operator → **200**; `SessionTimerSnapshotDto.ActiveQuestion` = first question of the **first** substage; `RemainingSeconds` tracks its window
- let the question timers expire (or observe the worker): questions advance within substage 1; after its **last** question closes, `ActiveQuestion` becomes the first question of substage 2 (all teams together)
- after the **final** substage's last question closes → session `Finished` (`SessionCompletion`)
- a SignalR client on `live-session:{id}` receives `QuestionActivated` / `QuestionClosed` / `SubstageAdvanced`
- response/runtime shapes for the frontend contract: `SessionTimerSnapshotDto { …, ActiveQuestion? }` (now the active-substage question); new `SubstageAdvanced { fromSubstageId, fromPlayMode, toSubstageId? }` SignalR notification

## Frontend slice
Human-driven — see Steps 9 (generate plan) + 9b (implement it) of
`prompt_example_feature_hu33a.md`. **Behavioural change (no new REST contract):** the timer read's `ActiveQuestion` now tracks the active substage; a new `SubstageAdvanced` SignalR signal marks substage transitions; the round advances substage-by-substage and Finishes only after the final substage (no session-level "round over when questions end").
