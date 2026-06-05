# Prompt Example — HU-33A Trivia Round Orchestration / Thin Slice (Feature Slice)

Concrete prompt sequence for driving HU-33A through a full feature slice on
`feature/hu-33a-trivia-round-orchestration`. Follows the pattern in
[workflow_for_prompts.md](./workflow_for_prompts.md). Context:
[hu33a-context.md](./hu33a-context.md).

**Key difference from HU-22:** HU-22 built the session-level authoritative timer
(session total duration countdown, pause/resume, SignalR tick broadcast). HU-33A
adds question-level lifecycle on top of it: a separate per-question countdown,
automatic activation of the first question when the session goes `Active`, and
automatic close-and-advance driven by the timer worker on question-timer expiry.
The operator monitors without manually controlling any question transition — the
backend drives the full round sequence.

**Thin slice boundaries (agreed 2026-06-04):**
- **IN:** question-level timer + auto-activation on session start + auto-close on
  expiry + auto-advance to next question + session auto-finish after last question.
- **OUT:** multi-round countdown engine, operator manual question controls,
  full `HU-33A` round-orchestration complexity, session-end choreography beyond
  `MoveTo(Finished)`.

Drive each backend phase with `@backend/.agents/driver-agent.md`, selecting
phases in order: **X.1 → X.2 → X.3 → X.4**. The driver delegates implementation
to `@backend/.agents/backend-agent.md`; do not invoke it directly. For the
frontend slice, use Step 9 directly with `@frontend/AGENTS.md`. Do not mix
backend and frontend work in the same phase.

---

## Required design patterns

- `State`
  - Why: question-timer behavior depends on session state — only `Active` advances
    the question timer; `Paused` freezes it; `Finished`/`Cancelled` stop it. Must
    derive from the `ILiveSessionState` abstraction introduced by HU-22, not add
    new ad-hoc `if` chains.
  - Phase owner: **X.1 Domain**
  - Gate obligation: `IsQuestionTimerAdvancing` and question-timer freeze/resume
    behavior are expressed through the `ILiveSessionState` objects (extending
    `ActiveLiveSessionState`, `PausedLiveSessionState`, etc.). No ad-hoc
    `if (State == Active)` checks for question-timer control outside the state
    abstraction.

- `Facade`
  - Why: auto-activation and close-and-advance are orchestration flows that touch
    strategy selection, domain mutation, persistence, and SignalR broadcast. A
    single facade keeps the timer worker and event handlers thin.
  - Phase owner: **X.2 Application**
  - Gate obligation: a `TriviaRoundOrchestratorFacade` (or equivalent named
    facade) is the single entry point for both (a) `ActivateNextQuestionAsync` and
    (b) `CloseAndAdvanceAsync`. The timer worker calls the facade; it does not
    orchestrate directly. No orchestration logic leaks into the worker or event
    handlers.

- `Strategy`
  - Why: the decision "which question activates next?" must be an explicit,
    interchangeable policy — not hard-coded sequential logic — to establish the
    extension seam for HU-33B's full round engine.
  - Phase owner: **X.1 Domain**
  - Gate obligation: `IQuestionActivationStrategy` interface exists with
    `SequentialQuestionActivationStrategy` as the single current implementation.
    The `Facade` (X.2) injects the strategy. HU-33B can add a new strategy
    implementation without modifying HU-33A's facade or domain.

Transport note (mandated, NOT optional): HU-33A carries **SignalR / WebSockets**
from the patterns matrix. `QuestionActivated` and `QuestionClosed` notifications
must broadcast to the `live-session:{id}` group. Pre-game countdown ticks also
broadcast. Not broadcasting is a gate defect.

---

## Pre-resolved orient (as of 2026-06-04)

> Step 1 has already been run. Paste this section into any agent session that
> needs context before picking up a phase; no need to re-run the orient prompt
> unless local docs or Linear state changed.

### What has already landed and must be reused

**session-operations-service — on `feature/hu-22-timer-session` (includes `develop`)**
- `LiveSession` aggregate root with:
  - `TriviaSnapshot` (`TriviaSessionSnapshot` → `TriviaQuestionSnapshot` list,
    each with `TimeLimitSeconds`, `SequenceOrder`, `ScoreValue`, `Explanation`)
  - `MoveTo()` guarded state transition
  - `SessionTransitionChain` + `SessionTransitionValidator` — extensible CoR pipeline
  - Session-level timer fields: `_sessionTimerTotalDuration`,
    `_sessionTimerRemainingDuration`, `_sessionTimerAdvancingSince`,
    `_sessionTimerExpiredAt`
  - `GetAuthoritativeSessionTimerSnapshot(now)` and `MarkSessionTimerExpiredIfElapsed(now)`
  - `IsSessionTimerAdvancing` (via `ILiveSessionState`)
- `ILiveSessionState` interface + implementations per `SessionState` enum value:
  `ActiveLiveSessionState`, `PausedLiveSessionState`, `ScheduledLiveSessionState`,
  `PreparingLiveSessionState`, `FinishedLiveSessionState`, `CancelledLiveSessionState`
- `LiveSessionStateFactory` — resolves the state object from the enum value
- `AuthoritativeSessionTimerWorker` (1-second `PeriodicTimer`) — entry point for
  HU-33A's question-timer tick extension
- `ISessionTimerBroadcaster` → `SignalRSessionTimerBroadcaster`
- `ISessionStateBroadcaster` → `SessionStateBroadcaster`
- `SessionsHub` with groups `live-session:{id}`, `team:{id}`, `participant:{id}`
- `SessionStateChangedNotificationHandler` (MediatR notification → SignalR broadcast)
- `ILiveSessionRepository` with `ListActiveTimersAsync`
- DB migration `AddAuthoritativeSessionTimerState` already applied

### What HU-33A adds

| Concern | New work |
| --- | --- |
| Domain | `ActiveQuestionIndex` (nullable int) + question-timer fields on `LiveSession`; `ActivateQuestion`, `MarkQuestionTimerExpiredIfElapsed`, `CloseActiveQuestion` methods; `QuestionActivatedEvent` + `QuestionClosedEvent`; `IQuestionActivationStrategy` + `SequentialQuestionActivationStrategy`; `ILiveSessionState` extensions for question-timer behavior. |
| Application | `TriviaRoundOrchestratorFacade` (Facade pattern) with `ActivateNextQuestionAsync` + `CloseAndAdvanceAsync`; `TriviaRoundStartedNotificationHandler` (session → Active → pre-game countdown → activate Q0); `ISessionQuestionBroadcaster` interface + notification DTOs. |
| Infrastructure | Migration `AddTriviaRoundState` (question-timer columns + `active_question_index`); extend `AuthoritativeSessionTimerWorker.TickAsync` for question-timer ticks and expiry; `SignalRSessionQuestionBroadcaster`; update `ListActiveTimersAsync` to include sessions with active question timers. |
| API | `GET /api/sessions/{liveSessionId}/trivia/active-question` — read-only, role-aware (participants see options without correct flag; operator sees all). |
| Frontend | Participant question screen (active question + live countdown + options); operator monitoring view (current question + timer). |

### Branch state and prerequisite

`feature/hu-33a-trivia-round-orchestration` branches from
**`feature/hu-22-timer-session`** (HU-22 is `In Progress` and not yet merged to
`develop` as of 2026-06-04).

The branch base includes everything on `develop` plus:
- Session-level timer fields + `ILiveSessionState` + `LiveSessionStateFactory`
- `AuthoritativeSessionTimerWorker` (1-second background service)
- `ISessionTimerBroadcaster` / `SignalRSessionTimerBroadcaster`
- Timer queries (operator + participant timer snapshot endpoints)
- DB migration `AddAuthoritativeSessionTimerState`

### Linear state

- HU ticket: `DES-44` — **Todo**, labels `Feature`, `svc:session-operations-service`;
  confirm `ready-for-agent` before driving.
- PRD ref: `DES-70`, local file
  `@backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`

---

## 1. Orient — read current service state

> Skip this step if you have read the pre-resolved orient above and the local
> docs are unchanged.

```text
Read the following and summarise what is already decided:
- @backend/docs/hu33a-context.md
- @backend/docs/hu22-context.md
- @backend/services/session-operations-service/CONTEXT.md
- @backend/services/session-operations-service/src/Domain/Entities/LiveSession.cs
- @backend/services/session-operations-service/src/Domain/Entities/TriviaSessionSnapshot.cs
- @backend/services/session-operations-service/src/Domain/ValueObjects/TriviaQuestionSnapshot.cs
- @backend/services/session-operations-service/src/Domain/Services/SessionStates/ILiveSessionState.cs
- @backend/services/session-operations-service/src/Domain/Services/SessionStates/ActiveLiveSessionState.cs
- @backend/services/session-operations-service/src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs
- @backend/services/session-operations-service/src/Application/Common/Interfaces/ISessionTimerBroadcaster.cs
- @backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md

Then use the Linear MCP to fetch only the current live state of:
- DES-44 (HU-33A — Orquestación automatizada por rondas) — status and labels
- DES-30 (HU-22 — predecessor) — status
- DES-70 (session-operations PRD) — status

Output:
- what HU-22 already landed that HU-33A must reuse (session-level timer fields,
  ILiveSessionState, AuthoritativeSessionTimerWorker)
- that TriviaQuestionSnapshot already has TimeLimitSeconds — question timer
  duration comes from there, not from a new field
- that ActiveQuestionIndex and question-level timer fields do not yet exist
- the resolved HU id, PRD id, status, and labels

Do not start planning or implementing yet.
```

---

## 2. Confirm `ready-for-agent` label

> Apply `ready-for-agent` to DES-44 once HU-22 is marked Done in Linear.

```text
Use the Linear MCP to confirm that DES-44 has the label ready-for-agent.
If it has been removed or was never applied, add it back. Output the updated
ticket state.
```

---

## 3. Confirm slice readiness

```text
Use the Linear MCP to confirm DES-44 carries both svc:session-operations-service
and ready-for-agent, and output its current status and acceptance criteria.

The acceptance criteria for this thin slice are:
1. When the session begins (→ Active), the system runs a pre-game countdown
   broadcast before activating the first question.
2. The system triggers each question automatically according to its TimeLimitSeconds.
3. The operator monitors the game without manually controlling each transition.
4. The sequence progresses smoothly from one question to the next; after the
   last question closes, the session transitions to Finished.
5. The countdown and the triggering of each question are broadcast to clients
   in real time via SignalR, without manual reloading.

Output the confirmed HU id, title, acceptance criteria, labels, and PRD ref
before planning the slice.
```

---

## 4. Start the slice

```text
Prepare the trivia round orchestration slice on branch
feature/hu-33a-trivia-round-orchestration, based on
feature/hu-22-timer-session.
Use the resolved HU id (DES-44) and PRD id (DES-70).
This slice affects session-operations-service only.

Before implementation, confirm the baseline from feature/hu-22-timer-session:
- ILiveSessionState and LiveSessionStateFactory already exist (HU-22)
- AuthoritativeSessionTimerWorker already ticks every 1 second (HU-22)
- TriviaQuestionSnapshot already has TimeLimitSeconds (from develop, HU-16)
- ActiveQuestionIndex and question-level timer fields do NOT exist yet
- No operator endpoint for question activation exists or should be added

Move DES-44 to In Progress and output the exact scope, branch name, and touched
surfaces.
```

---

## 5. Backend phase X.1 — Domain layer

> Run `@backend/.agents/driver-agent.md` and select **X.1** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.1 for HU-33A in session-operations-service.

Before writing anything, inspect the existing domain and extend rather than
recreate:
- LiveSession aggregate — especially the session-timer fields and
  MarkSessionTimerExpiredIfElapsed pattern (HU-22)
- ILiveSessionState + LiveSessionStateFactory (HU-22) — extend these for
  question-timer behavior
- TriviaSessionSnapshot + TriviaQuestionSnapshot — source of TimeLimitSeconds
  and SequenceOrder
- Domain events convention (LiveSessionCreatedEvent, SessionStateChangedEvent)

Scope:
- add `ActiveQuestionIndex` (nullable int) on `LiveSession` — null means no
  question is active
- add question-level timer fields on `LiveSession` mirroring the session-timer
  pattern from HU-22:
  - `_questionTimerTotalDuration` (TimeSpan)
  - `_questionTimerRemainingDuration` (TimeSpan)
  - `_questionTimerAdvancingSince` (DateTimeOffset?)
  - `_questionTimerExpiredAt` (DateTimeOffset?)
- add `IsQuestionTimerAdvancing` property: delegates to `ILiveSessionState`
  — return true only in `ActiveLiveSessionState`, false in all others
- extend `ILiveSessionState` implementations:
  - `ActiveLiveSessionState.IsQuestionTimerAdvancing` → true
  - all other states → false
  - `PausedLiveSessionState`: freeze question timer (parallel to session timer
    freeze on pause) — add `FreezeQuestionTimer` call to the enter-paused hook
  - `ActiveLiveSessionState`: resume question timer when session resumes from
    pause — add resume logic to enter-active hook (if question is active)
- add domain methods on `LiveSession`:
  - `ActivateQuestion(int questionIndex, DateTimeOffset now)`:
    - validate session state is Active
    - validate questionIndex is in bounds (TriviaSnapshot must exist + index
      within Questions count)
    - validate no question is already active (ActiveQuestionIndex is null)
    - set ActiveQuestionIndex = questionIndex
    - initialize question timer from TriviaSnapshot.Questions[questionIndex].TimeLimitSeconds
    - raise QuestionActivatedEvent
  - `GetActiveQuestionTimerSnapshot(DateTimeOffset observedAt)`:
    - mirrors GetAuthoritativeSessionTimerSnapshot from HU-22
    - returns AuthoritativeSessionTimerSnapshot (reuse the same value object)
      built from question-timer fields
  - `MarkQuestionTimerExpiredIfElapsed(DateTimeOffset now)`:
    - mirrors MarkSessionTimerExpiredIfElapsed from HU-22
    - if advancing and remaining <= elapsed, freeze at zero, record expiredAt
    - return the question timer snapshot
  - `CloseActiveQuestion(DateTimeOffset now)`:
    - validate ActiveQuestionIndex is not null
    - clear ActiveQuestionIndex (set to null)
    - stop/freeze question timer
    - raise QuestionClosedEvent
- add domain events:
  - `QuestionActivatedEvent`: LiveSessionId, QuestionIndex, SequenceOrder,
    TimeLimitSeconds, ActivatedAt
  - `QuestionClosedEvent`: LiveSessionId, QuestionIndex, ClosedAt,
    WasExpiredByTimer (bool)
- add `IQuestionActivationStrategy` interface:
  - `int? Next(LiveSession session)` — returns the index of the next question
    to activate, or null if the sequence is complete
- add `SequentialQuestionActivationStrategy` implementation:
  - if ActiveQuestionIndex is null: return index of the question with the
    lowest SequenceOrder (i.e., 0 for the first question)
  - if ActiveQuestionIndex is set: return the next index by SequenceOrder, or
    null if this was the last question

Gate:
- Domain build passes
- existing LiveSession invariants are not broken
- unit tests for ActivateQuestion:
  - happy path activates the question and starts the timer
  - throws when session is not Active
  - throws when index is out of bounds
  - throws when a question is already active
- unit tests for MarkQuestionTimerExpiredIfElapsed:
  - returns not-expired before time runs out
  - returns expired and freezes timer when elapsed
- unit tests for CloseActiveQuestion:
  - happy path clears ActiveQuestionIndex and raises QuestionClosedEvent
  - throws when no question is active
- unit tests for ILiveSessionState extensions:
  - IsQuestionTimerAdvancing true only in Active state
  - pause freezes question timer; resume restores advancing
- unit tests for SequentialQuestionActivationStrategy:
  - returns 0 for a session with no active question
  - returns next index after current
  - returns null after last question
- State gate: question-timer control is entirely inside ILiveSessionState
  implementations — no ad-hoc if (State == Active) checks in domain methods

Do not touch Application, Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.1 — domain layer (HU-33A)

Ref: HU-33A
Ref: DES-44
Ref: DES-70
```

---

## 6. Backend phase X.2 — Application layer

> Run `@backend/.agents/driver-agent.md` and select **X.2** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.2 for HU-33A in session-operations-service.

Before writing anything, inspect the existing Application baseline and mirror
its conventions:
- TransitionSessionStateFacade (orchestration precedent)
- SessionStateChangedNotificationHandler (MediatR notification handler pattern)
- ISessionStateBroadcaster / ISessionTimerBroadcaster (broadcaster interface conventions)
- AuthorizationBehaviour / ICurrentUser (authorization patterns)

Scope:
- add `ISessionQuestionBroadcaster` interface with:
  - `BroadcastQuestionActivatedAsync(QuestionActivatedNotificationDto, CancellationToken)`
  - `BroadcastQuestionClosedAsync(QuestionClosedNotificationDto, CancellationToken)`
- add DTOs:
  - `QuestionActivatedNotificationDto`: LiveSessionId, QuestionIndex,
    SequenceOrder, Prompt, Options (without revealing IsCorrect to participants —
    the DTO includes Options as display text only), TimeLimitSeconds, ActivatedAt
  - `QuestionClosedNotificationDto`: LiveSessionId, QuestionIndex, ClosedAt,
    WasExpiredByTimer
- add `TriviaRoundOrchestratorFacade` (the mandated Facade) with:
  - `ActivateNextQuestionAsync(LiveSession session, DateTimeOffset now, CancellationToken ct)`:
    - uses injected IQuestionActivationStrategy to get next index
    - calls session.ActivateQuestion(index, now)
    - persists via repository
    - broadcasts QuestionActivated via ISessionQuestionBroadcaster
  - `CloseAndAdvanceAsync(LiveSession session, DateTimeOffset now, CancellationToken ct)`:
    - calls session.CloseActiveQuestion(now)
    - persists via repository
    - broadcasts QuestionClosed
    - gets next index via strategy
    - if next index exists: calls ActivateNextQuestionAsync
    - if no next index: calls session.MoveTo(Finished, now, transitionPolicy)
      persists + broadcasts SessionStateChanged
- add `TriviaRoundStartedNotificationHandler` (MediatR notification handler):
  - listens for `SessionStateChangedEvent` where NewState == Active AND
    session is in Trivia mode
  - broadcasts a pre-game countdown (N=5 second ticks via ISessionTimerBroadcaster
    or a dedicated pre-game broadcast method)
  - after countdown: calls `TriviaRoundOrchestratorFacade.ActivateNextQuestionAsync`
- register `SequentialQuestionActivationStrategy` as `IQuestionActivationStrategy`
  in DI
- register `TriviaRoundOrchestratorFacade` in DI
- unit tests for TriviaRoundOrchestratorFacade:
  - ActivateNextQuestionAsync persists and broadcasts QuestionActivated
  - CloseAndAdvanceAsync on non-last question persists, broadcasts QuestionClosed,
    then activates next question
  - CloseAndAdvanceAsync on last question persists, broadcasts QuestionClosed,
    then transitions session to Finished
- unit tests for TriviaRoundStartedNotificationHandler:
  - only triggers for Trivia mode sessions transitioning to Active
  - does not trigger for TreasureHunt sessions or non-Active transitions

Gate:
- clean build passes
- facade + handler unit tests pass for all listed paths
- Facade gate: orchestration for both activate and close-and-advance flows
  flows through TriviaRoundOrchestratorFacade — no orchestration logic in
  the timer worker or event handler
- Strategy gate: IQuestionActivationStrategy is injected into the facade —
  the facade does not hard-code sequential selection

Do not touch Infrastructure, Api, or frontend.
```

Commit:

```text
feat(session-operations): phase X.2 — application layer (HU-33A)

Ref: HU-33A
Ref: DES-44
Ref: DES-70
```

---

## 7. Backend phase X.3 — Infrastructure layer

> Run `@backend/.agents/driver-agent.md` and select **X.3** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.3 for HU-33A in session-operations-service.

Before writing anything, inspect:
- AuthoritativeSessionTimerWorker (HU-22) — the tick loop to extend
- AddAuthoritativeSessionTimerState migration (HU-22) — the naming convention
  for timer columns
- LiveSessionConfiguration EF mapping — to mirror for new columns
- ILiveSessionRepository.ListActiveTimersAsync — to extend for question timers
- SignalRSessionTimerBroadcaster — the broadcaster implementation pattern to
  mirror for ISessionQuestionBroadcaster

Scope:
- add EF migration `AddTriviaRoundState` with columns on `live_sessions`:
  - `active_question_index` (int, nullable)
  - `question_timer_total_duration` (interval)
  - `question_timer_remaining_duration` (interval)
  - `question_timer_advancing_since` (timestamp with time zone, nullable)
  - `question_timer_expired_at` (timestamp with time zone, nullable)
- update `LiveSessionConfiguration` EF mapping for all five new fields
- implement `SignalRSessionQuestionBroadcaster : ISessionQuestionBroadcaster`:
  - `BroadcastQuestionActivatedAsync` → sends `QuestionActivated` to
    `live-session:{liveSessionId}` group on SessionsHub
  - `BroadcastQuestionClosedAsync` → sends `QuestionClosed` to
    `live-session:{liveSessionId}` group on SessionsHub
- extend `AuthoritativeSessionTimerWorker.TickAsync`:
  - after the existing session-timer tick, also handle question-timer tick:
    - for each live session returned by the repository:
      - if `ActiveQuestionIndex` is not null and question timer is advancing:
        - call `session.MarkQuestionTimerExpiredIfElapsed(now)`
        - if expired: call `facade.CloseAndAdvanceAsync(session, now, ct)`
          (the facade handles persist + broadcast + next-question activation)
      - if question timer is not advancing (no active question), skip
  - keep session-timer tick logic intact
- update `ILiveSessionRepository` and `ListActiveTimersAsync` to also return
  sessions with an active question timer (`active_question_index IS NOT NULL`
  AND `question_timer_expired_at IS NULL`) even if the session timer itself
  is not advancing (this can occur if session timer already expired but the
  question timer is still running — handle edge case)
- register `SignalRSessionQuestionBroadcaster` in DI
- integration tests:
  - question-timer fields round-trip through EF (activate question, persist,
    reload, verify active_question_index and timer fields)
  - timer worker tick triggers CloseAndAdvanceAsync when question timer elapses
  - after last question closes, session transitions to Finished (persisted)
  - QuestionActivated and QuestionClosed are broadcast to the hub group

Gate:
- dotnet ef migrations add confirms AddTriviaRoundState is generated (or
  confirmed no-op against snapshot)
- review migration for audit-column leaks before commit
- repository integration tests prove question-timer fields persist and reload
- timer worker integration test proves question expiry triggers close-and-advance
- SignalR integration tests prove QuestionActivated and QuestionClosed reach
  connected clients

Do not touch Api or frontend.
```

Commit:

```text
feat(session-operations): phase X.3 — infrastructure layer (HU-33A)

Ref: HU-33A
Ref: DES-44
Ref: DES-70
```

---

## 8. Backend phase X.4 — API layer

> Run `@backend/.agents/driver-agent.md` and select **X.4** from the phase menu.

```text
Use @backend/.agents/backend-agent.md.
Implement backend phase X.4 for HU-33A in session-operations-service.

Scope:
- add a read-only endpoint to `SessionsEndpoints`:
  GET /api/sessions/{liveSessionId}/trivia/active-question
  - role-aware response:
    - Participant: returns prompt, options (display text only, IsCorrect hidden),
      TimeLimitSeconds, QuestionIndex, question timer snapshot (remaining ms)
    - Operator: returns same as participant (operator sees the live game view,
      not the answer — answer reveal is HU-35 scope)
  - returns 404 if session does not exist
  - returns 204 No Content if no question is currently active
  - returns 403 if the caller is not a participant or operator of this session
- keep the endpoint thin: call a query handler, return the DTO
- note: there is NO POST/PUT endpoint for activating questions — activation
  is fully automatic and operator-triggered endpoints must NOT be added
- endpoint integration tests:
  - returns 204 when no question is active
  - returns 200 with question data when a question is active (options without
    IsCorrect for participants)
  - returns 404 for unknown session
  - returns 403 for unauthorized caller
- confirm the SignalR hub is fully wired end-to-end:
  - connect a test client to the live-session group
  - transition session to Active
  - verify QuestionActivated is received within the pre-game countdown window
  - let the question timer elapse
  - verify QuestionClosed and then QuestionActivated (for Q2, if applicable)
    are received without manual intervention

Gate:
- endpoint integration tests pass for success, no-active-question, unknown
  session, and unauthorized caller paths
- SignalR end-to-end test confirms automatic question activation and
  close-and-advance without any operator action
- service coverage reaches the enforced ADR-0005 threshold (≥93%)

Do not touch frontend.
```

Commit:

```text
feat(session-operations): phase X.4 — api layer (HU-33A)

Ref: HU-33A
Ref: DES-44
Ref: DES-70
```

---

## 8.5. Docker rebuild + smoke

```text
From backend/, rebuild and start the stack for manual verification:

1. docker compose build session-operations-service && docker compose up -d session-operations-service
2. docker compose build api-gateway && docker compose up -d api-gateway
3. Ensure a LiveSession exists in Active state with teams assigned and
   TriviaSnapshot loaded (use seed-dev-data.sh or existing fixture).
4. Connect to the SessionsHub as a participant — confirm the QuestionActivated
   notification is received automatically (no operator action needed).
5. Wait for TimeLimitSeconds to elapse — confirm QuestionClosed is received,
   then QuestionActivated for the next question fires automatically.
6. After the last question closes, confirm the session transitions to Finished
   automatically (SessionStateChanged broadcast received).
7. Verify the read endpoint:
   curl -i http://localhost:<gateway-port>/api/sessions/<liveSessionId>/trivia/active-question \
     -H "X-User-Id: <participantId>" -H "X-User-Role: Participant" \
     -H "X-User-Email: p@umbral.test"
   Expect 200 with active question data (or 204 if between questions).
8. Verify 204 when no question is active (before first activation or between questions).
```

**Gate:** the full automatic sequence plays out without any operator HTTP call;
all five acceptance criteria are observable in the running stack.

---

## 9. Frontend slice

```text
Generate a multi phase plan in a markdown file, like the one in
@frontend/plans/hu-03-frontend-role-permission-assignment.md, save it in
@frontend/plans/ for the following:
Use @frontend/AGENTS.md.

Implement the trivia round UI for HU-33A using the verified
session-operations contract (GET /api/sessions/{liveSessionId}/trivia/active-question)
and the SignalR QuestionActivated / QuestionClosed notifications.

Scope:
- participant question screen:
  - subscribe to SignalR QuestionActivated notification and display the active
    question (prompt + answer options) when received
  - show a live countdown timer driven by the backend-provided TimeLimitSeconds
    and remaining-ms snapshot (reuse the use-session-timer hook pattern from HU-22)
  - clear the question display on QuestionClosed notification
  - handle pre-game countdown (broadcast before first question)
  - handle session Finished state (all questions done)
- operator monitoring view:
  - show which question is currently active (index + prompt preview)
  - show the question countdown timer
  - show that the operator has no manual activation controls (monitoring only)
- reconnect: on reconnect, call GET active-question to restore current question
  state without waiting for the next SignalR push

Gate:
- participant sees the question automatically when QuestionActivated fires
- countdown decrements in real time and clears on QuestionClosed
- operator monitoring view reflects current question and timer without controls
- reconnect restores question state without reload
```

Commit:

```text
feat(frontend): trivia round orchestration — HU-33A

Ref: HU-33A
Ref: DES-44
Ref: DES-70
```

---

## 10. Close-out

```text
Before opening the PR:
- confirm all four backend phase commits exist
- confirm the smoke path was exercised: session → Active triggers pre-game
  countdown → Q0 activates automatically → timer expires → Q0 closes →
  Q1 activates automatically → ... → last question closes → session Finished
  — all without any operator HTTP call to activate questions
- confirm QuestionActivated and QuestionClosed SignalR notifications reach
  connected clients automatically
- confirm the read endpoint returns 200 with question data or 204 with no active
  question, and 403 for unauthorized callers
- map each acceptance criterion to where it is enforced:
  AC#1 pre-game countdown fires when session → Active →
       TriviaRoundStartedNotificationHandler + SignalR broadcast
  AC#2 system triggers each question automatically from its TimeLimitSeconds →
       AuthoritativeSessionTimerWorker + MarkQuestionTimerExpiredIfElapsed +
       TriviaRoundOrchestratorFacade.CloseAndAdvanceAsync
  AC#3 operator has no manual controls → no POST/activate endpoint exists;
       GET active-question is read-only
  AC#4 sequence progresses question-to-question, ends in Finished →
       SequentialQuestionActivationStrategy + CloseAndAdvanceAsync
  AC#5 countdown and question triggering broadcast via SignalR in real time →
       ISessionQuestionBroadcaster + SignalRSessionQuestionBroadcaster

Then open the PR:
gh pr create --draft --base feature/hu-22-timer-session \
  --title "feat: trivia round orchestration (thin slice) — HU-33A" \
  --body "Closes DES-44
Ref: DES-70

Touched: backend/services/session-operations-service/, frontend/, mobile/"
```

---

## Rationale

**Why automatic activation, not operator-triggered.** The acceptance criteria
are unambiguous: "the operator monitors the game without manually controlling
each transition" and "the system triggers each question according to its timer."
An operator activation endpoint would violate both criteria. The backend owns
the entire round sequence from session → Active to session → Finished.

**Why a separate question-level timer.** HU-22 built a session-level timer
tracking total session duration (`MaximumTime`). Questions have their own
`TimeLimitSeconds` in `TriviaQuestionSnapshot`. These are independent durations
and must remain independent fields. The question timer reuses HU-22's timer
patterns (`MarkExpiredIfElapsed`, `AuthoritativeSessionTimerSnapshot`) but runs
on separate columns and a separate lifecycle.

**Three patterns, one slice.** `State` extends HU-22's `ILiveSessionState` to
control question-timer behavior — without it, the timer would re-introduce the
ad-hoc `if (State == Active)` checks the state objects were designed to eliminate.
`Facade` keeps the timer worker thin — the worker calls one method; the facade
owns the orchestration. `Strategy` makes `SequentialQuestionActivationStrategy`
a named, injectable policy so HU-33B can add a different strategy without
touching HU-33A's code.

**Branch chain.** HU-33A branches from `feature/hu-22-timer-session` because
HU-22 is not yet merged. The PR targets the same base. Once HU-22 merges to
`develop`, this branch's diff will naturally rebase onto `develop` through the
normal PR flow.

**`CloseAndAdvance` idempotency.** The timer worker ticks every second. If two
ticks race on expiry, the second tick must not double-close or double-activate.
Guard inside `CloseActiveQuestion` with a null-check on `ActiveQuestionIndex` —
if already null, the method is a no-op.
