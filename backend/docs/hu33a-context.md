# HU-33A Context — Trivia Round Orchestration (Thin Slice)

> Superseded on 2026-06-16 by
> `backend/docs/grilling-session-mission-restructure.md`,
> `backend/docs/ddd_solution_model.md`, and
> `backend/docs/bd_umbral_entity_spec.md`.
> Keep synchronized question orchestration, but rebuild it as trivia `Substage`
> behavior inside a mission `LiveSession`. A `TriviaQuiz` is not a direct
> session source, and after the final question timer expires the runtime advances
> to the next substage or completes the session.

> Paste this section into any agent session that needs context for HU-33A.
> Last updated: 2026-06-04 | Branch: `feature/hu-33a-trivia-round-orchestration`
>
> Boundary note: HU-33A is a `session-operations-service` slice. `SessionOperations`
> is the authoritative runtime owner: it decides when a question becomes active,
> when it closes, and when the session ends. `ScoringMonitoring` consumes published
> facts (`AnswerRegistered`, `QuestionClosed`) but does not control progression.
> Clients render the timer; they do not own the clock.

## ⚠️ Resolution notes

1. **Branch base is `feature/hu-22-timer-session`** — HU-22 is not yet merged to
   `develop` as of 2026-06-04. HU-33A must branch from it, not from `develop`.
   The PR for HU-33A must target `feature/hu-22-timer-session` until HU-22 merges.
2. **HU-22 built a session-level timer** — `_sessionTimerTotalDuration`,
   `_sessionTimerRemainingDuration`, `_sessionTimerAdvancingSince`,
   `_sessionTimerExpiredAt` on `LiveSession`. HU-33A adds a **question-level timer**
   (separate fields) because questions have their own `TimeLimitSeconds` from the
   `TriviaQuestionSnapshot`. The session timer (session total duration) and the
   question timer (per-question countdown) are distinct concepts and must remain
   separate fields.
3. **No operator activation endpoint** — the acceptance criteria explicitly state
   the operator monitors without manually controlling each transition. Question
   activation is automatic, driven by the `SessionStateChangedEvent` (session →
   `Active` triggers first question) and by the timer worker on question expiry
   (close current question → activate next). There is no operator-triggered
   `POST /activate` endpoint in this slice.

## State

- `DES-44` (HU-33A): status **Todo**; labels `Feature`,
  `svc:session-operations-service`. Confirm `ready-for-agent` before driving.
- Predecessors already landed on the same service label:
  - `DES-11` (HU-07A): **Done** — participant membership validation baseline
  - `DES-12` (HU-07B): **Done** — reconnect/runtime admission baseline
  - `DES-23` (HU-16): **Done** — trivia session creation baseline
  - `DES-25` (HU-18): **Done** — team-session association
  - `DES-26` (HU-19): **Done** — session operator assignment + authorization proxy
  - `DES-28` (HU-21A): **Done** — valid session-state transitions + SignalR broadcast
  - `DES-30` (HU-22): **In Progress** on `feature/hu-22-timer-session` — authoritative
    session-level timer + `ILiveSessionState` state objects + `AuthoritativeSessionTimerWorker`
- PRD ref: `DES-70` (PRD — Primera implementacion de `session-operations-service`
  (HU-15 a HU-36)); local file
  `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
- Blocked by: HU-21A (Done), HU-22 (In Progress)
- Blocks (downstream): `DES-46` (HU-34A), `DES-45` (HU-33B), `DES-49` (HU-36A)
- Branch: `feature/hu-33a-trivia-round-orchestration`, base =
  **`feature/hu-22-timer-session`** (HU-22 is the most recent same-service
  predecessor, still `In Progress` and not yet merged to `develop`)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | "Timer behavior depends on session state; question activation and auto-close are lifecycle transitions." | Question-timer behavior must be expressed through the existing `ILiveSessionState` model introduced by HU-22: only the `Active` state advances the question timer; `Paused` freezes it; `Finished`/`Cancelled` stop it. No ad-hoc `if (state == Active)` checks outside the state abstraction. |
| `Facade` (mandated) | X.2 Application | "Round orchestration combines lifecycle control, coordination, and mode-specific progression." | The auto-question-activation flow (triggered on session → Active and on question-timer expiry) is coordinated by a single `TriviaRoundOrchestratorFacade`. It does not scatter activation logic across the timer worker, the event handler, and the endpoint. Endpoints and the worker stay thin. |
| `Strategy` (mandated) | X.1 Domain | "Mode-specific progression policy; countdown/activation broadcast." | `IQuestionActivationStrategy` is a named interface even with a single implementation (`SequentialQuestionActivationStrategy`). It encapsulates the decision "given the current session state, which question activates next?" The `Facade` selects the strategy at activation time. This establishes the extension seam for HU-33B's full round engine. |

Transport note: HU-33A carries **SignalR / WebSockets** (mandated from patterns
matrix). `QuestionActivated` and `QuestionClosed` notifications must broadcast
to the `live-session:{id}` group via the existing `SessionsHub`. Pre-game
countdown ticks also broadcast via SignalR. This is a hard transport gate.

## What predecessors have already landed (reuse candidates)

All of this is on `feature/hu-22-timer-session` (which includes `develop`).

**Domain layer**
- `LiveSession` aggregate root with `TriviaSnapshot` (`TriviaSessionSnapshot`
  containing ordered `TriviaQuestionSnapshot` items)
- `TriviaQuestionSnapshot` already has `TimeLimitSeconds`, `SequenceOrder`,
  `ScoreValue`, and `Explanation` — the question-level timer duration is already
  stored in the snapshot
- `SessionState` enum with `Scheduled`, `Preparing`, `Active`, `Paused`,
  `Finished`, `Cancelled`; `LiveSession.Create` sets `State = Scheduled`
- `LiveSession.MoveTo()` + `SessionStateTransitionPolicy` + `SessionTransitionChain`
  (extensible CoR pipeline — HU-33A can add a `NoActiveQuestionGate` for `HU-34A`
  without modifying the pipeline)
- HU-22's session-level timer fields: `_sessionTimerTotalDuration`,
  `_sessionTimerRemainingDuration`, `_sessionTimerAdvancingSince`,
  `_sessionTimerExpiredAt` — HU-33A adds **separate** question-level timer fields
- `ILiveSessionState` + `ActiveLiveSessionState`, `PausedLiveSessionState`, etc.
  (state objects per `SessionState`) — HU-33A extends these to control question
  timer behavior
- `LiveSessionStateFactory` — resolves the state object for the current enum value
- Domain events: `SessionStateChangedEvent`, `LiveSessionCreatedEvent`

**Application layer**
- `TransitionSessionStateCommandHandler` + `TransitionSessionStateFacade`
  (orchestration precedent)
- `SessionStateChangedNotificationHandler` (MediatR notification handler that
  triggers SignalR broadcast — HU-33A adds a handler that listens for session →
  Active to kick off the pre-game countdown)
- Existing MediatR pipeline: `AuthorizationBehaviour`, `ValidationBehaviour`,
  `ICurrentUser`
- `ISessionStateBroadcaster` → `SessionStateBroadcaster` (broadcast via
  `SessionsHub` group `live-session:{id}`)
- `ISessionTimerBroadcaster` → `SignalRSessionTimerBroadcaster` (timer tick
  broadcast — HU-33A adds `ISessionQuestionBroadcaster` for question events)

**Infrastructure layer**
- `AuthoritativeSessionTimerWorker` (1-second `PeriodicTimer` background service)
  — HU-33A **extends** `TickAsync` to also handle question-timer expiry: when
  `MarkQuestionTimerExpiredIfElapsed` returns expired, call
  `CloseActiveQuestion` + persist + broadcast `QuestionClosed`; then
  auto-activate the next question (or transition to `Finished` if last)
- `ILiveSessionRepository` with `ListActiveTimersAsync` — extend to also return
  sessions with an active question timer
- EF Core `live_sessions` table — HU-33A adds question-timer columns via migration
- `SessionsHub` + `SessionsHub` group convention `live-session:{id}`

**Frontend**
- HU-22 landed the operator timer panel (`OperatorSessionTimerPanel`) and mobile
  session-timer bar (`session-timer-bar.tsx`, `use-session-timer.ts`)
- Reconnect surface already exists for delivering the current question state on
  reconnect

**Coverage**
- ADR-0005 gate applies. X.4 must measure and report the post-HU-22 coverage
  before committing the gate number.

## What this HU adds

| Concern | New work |
|---|---|
| Active question tracking | `ActiveQuestionIndex` (nullable `int`) on `LiveSession` — points into `TriviaSnapshot.Questions`; null = no question active. Persisted as `active_question_index` column. |
| Question-level timer | Four new fields on `LiveSession` mirroring the session timer: `_questionTimerTotalDuration`, `_questionTimerRemainingDuration`, `_questionTimerAdvancingSince`, `_questionTimerExpiredAt`. Timer duration = `TriviaQuestionSnapshot.TimeLimitSeconds`. |
| `ActivateQuestion(int index, DateTimeOffset now)` | Domain method: validates session is `Active`, validates index is in bounds and not already active, sets `ActiveQuestionIndex`, initializes question timer, raises `QuestionActivatedEvent`. |
| `MarkQuestionTimerExpiredIfElapsed(DateTimeOffset now)` | Domain method mirroring `MarkSessionTimerExpiredIfElapsed`: freezes question timer at zero and returns snapshot when elapsed. Called by the timer worker each tick. |
| `CloseActiveQuestion(DateTimeOffset now)` | Domain method: clears `ActiveQuestionIndex`, freezes question timer, raises `QuestionClosedEvent`. Called by the timer worker on expiry. |
| `QuestionActivatedEvent` + `QuestionClosedEvent` | Domain events carrying `LiveSessionId`, `QuestionIndex`, `SequenceOrder`, `TimeLimitSeconds` / `ClosedAt`. |
| `IsQuestionTimerAdvancing` property | Delegates to `ILiveSessionState` — only `Active` state returns true; all others false. Mirrors `IsSessionTimerAdvancing`. |
| `IQuestionActivationStrategy` + `SequentialQuestionActivationStrategy` | Strategy pattern: `Next(LiveSession session)` returns the index of the next question to activate, or null if the sequence is complete. |
| Auto-activation on session start | `SessionStateChangedNotificationHandler` extension (or a new `TriviaRoundStartedNotificationHandler`): when session transitions to `Active`, schedule a pre-game countdown broadcast (N-second SignalR ticks), then auto-activate question 0 via `TriviaRoundOrchestratorFacade`. |
| `TriviaRoundOrchestratorFacade` | **Facade** pattern: single orchestration entry point for (a) activate-question flow and (b) close-and-advance flow. Called by event handlers and timer worker. Coordinates strategy selection, domain mutation, persistence, and broadcast. |
| Auto-close + auto-advance in timer worker | Extend `AuthoritativeSessionTimerWorker.TickAsync`: per active session, after session-timer tick, also call `MarkQuestionTimerExpiredIfElapsed`. If expired: call `facade.CloseAndAdvanceAsync(session)` → `CloseActiveQuestion`, pick next via Strategy, activate or transition session to `Finished`. Persist + broadcast. |
| Migration | `active_question_index` (nullable int), `question_timer_total_duration`, `question_timer_remaining_duration`, `question_timer_advancing_since`, `question_timer_expired_at` on `live_sessions`. |
| SignalR broadcasts | `QuestionActivated` notification (index, prompt, options, time limit) + `QuestionClosed` notification (index, closed at) broadcast to `live-session:{id}` group. Pre-game countdown ticks via existing timer broadcaster. |
| API read surface | `GET /api/sessions/{liveSessionId}/trivia/active-question` — participant/operator read of the current active question (index, prompt, options — without revealing the correct option to participants). Read-only; does not trigger activation. |
| Frontend | Participant question screen (shows active question, live countdown, options); operator live monitoring view (shows which question is active and timer). |

## Touched surfaces

- `backend/services/session-operations-service/` — domain, application,
  infrastructure, and API layers
- `frontend/` — participant question screen, operator monitoring
- `mobile/` — participant question screen + question timer bar
- API contract boundary: `QuestionActivated` SignalR notification shape,
  `QuestionClosed` SignalR notification shape, `GET active-question` response shape

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **Question timer is separate from session timer.** HU-22's session timer counts
  down total session duration (`MaximumTime`). The question timer counts down
  each question's `TimeLimitSeconds`. Both timers run concurrently when the
  session is `Active` with a question active. The worker must tick both.
- **Session timer expiry ≠ question close.** The session timer expiring means the
  total session time ran out (→ `Finished`). The question timer expiring means
  this question's time ran out (→ close question, advance). These are independent
  events that may coincide but must be handled separately.
- **Pre-game countdown is broadcast-only.** It is a SignalR-only delay before
  question 0 activates. It does not require a new DB column or state. Keep it
  simple: a configurable N-second delay (e.g. 5 seconds) broadcast as countdown
  ticks via the existing timer broadcaster before `ActivateQuestion(0)` is called.
- **No operator activation endpoint.** The acceptance criteria state the operator
  monitors without manually controlling each transition. Do not add an operator
  endpoint to activate questions. Activation is fully automatic.
- **`IQuestionActivationStrategy` exists to make the Strategy pattern explicit.**
  HU-33B's full round engine will inject a different strategy. Having the
  interface now means HU-33B does not need to refactor HU-33A's code.
- **Extend `AuthoritativeSessionTimerWorker`, do not fork it.** The worker
  already has the 1-second `PeriodicTimer` and the scope factory. HU-33A adds
  question-timer handling inside `TickAsync` after the session-timer tick.
  Keep the worker as one cohesive background service.
- **`CloseAndAdvance` must be idempotent.** The timer worker ticks every second;
  if close-and-advance is called while the previous advance is still persisting,
  it must not double-activate. Guard with `ActiveQuestionIndex` null-check before
  activating.
- **`ListActiveTimersAsync` needs to return sessions with active question timers.**
  Update or overload the repository query so the worker can find sessions where
  `active_question_index IS NOT NULL` and `question_timer_expired_at IS NULL`.
- **HU-34A depends on `ActiveQuestionIndex`.** The answer-submission slice will
  check `LiveSession.ActiveQuestionIndex` to validate the submitted answer matches
  the active question. Do not remove or rename this field.
