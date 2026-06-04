# HU-22 Context — Authoritative Session Timer

> Paste this section into any agent session that needs context for HU-22.
> Last updated: 2026-06-04 | Branch: `feature/hu-22-temporizador-autoritativo-de-sesion`
>
> Boundary note: HU-22 is a `session-operations-service` slice that makes the
> backend the authority for remaining time. `SessionOperations` owns the timer
> state, freeze/resume behavior, reconnect snapshot, and real-time push. Clients
> render the timer; they do not own or derive the authoritative clock.

## State

- `DES-30` (HU-22): status **Todo**; labels `Feature`, `ready-for-agent`,
  `svc:session-operations-service`
- Predecessors already landed on the same service label:
  - `DES-11` (HU-07A): **Done** — participant membership validation baseline
  - `DES-12` (HU-07B): **Done** — reconnect/runtime admission baseline
  - `DES-23` (HU-16): **Done** — trivia session creation baseline
  - `DES-25` (HU-18): **In Progress** — team-session association
  - `DES-26` (HU-19): **Done** — session operator assignment baseline
  - `DES-28` (HU-21A): **Done** — valid session-state transitions baseline
- PRD ref: `DES-70` (PRD - Primera implementacion de
  `session-operations-service` (HU-15 a HU-36)); local file
  `backend/docs/prd/DES-70-primera-implementacion-de-session-operations-service-hu-15-a-hu-36.md`
- Blocked by: HU-21A
- Blocks (downstream): `DES-44` (HU-33A)
- Branch: `feature/hu-22-temporizador-autoritativo-de-sesion`, base =
  **`feature/hu-18-asociacion-de-equipos-a-sesiones`** (same-service predecessor
  `DES-25` is still `In Progress`; branch-base rule applies even though HU-21A is
  the direct functional prerequisite)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `State` (mandated) | X.1 Domain | "Timer behavior depends on session state (active vs paused); remaining time pushed to clients." | Timer behavior must be expressed through the `LiveSession` state model: while the session/question runtime is `Active`, remaining time decreases; while `Paused`, remaining time freezes; on resume, the countdown continues from the frozen remainder; reconnect reads the current authoritative remainder from backend-owned state. No client-owned timer and no ad-hoc state checks scattered across handlers, endpoints, or hubs. |

Transport note: HU-22 carries **SignalR / WebSockets** (from the patterns
matrix). Remaining time must be pushed live to connected clients through the
existing `SessionsHub`/real-time surface. SignalR is a hard transport gate for
this slice.

## What predecessors have already landed (reuse candidates)

All of this is on `develop`, plus the in-progress branch base
`feature/hu-18-asociacion-de-equipos-a-sesiones`.

**Domain layer**
- `LiveSession` is already the runtime aggregate root for `SessionOperations`
- HU-21A already formalized the lifecycle around `Scheduled`, `Preparing`,
  `Active`, `Paused`, `Finished`, `Cancelled`; HU-22 must derive timer behavior
  from that state model instead of reintroducing free-form status conditionals
- `SessionStateChanged` is already the lifecycle event precedent HU-22 extends
  with timer-aware behavior
- `SessionSource`, `MaximumTime`, `Team`, `SessionParticipant`, and trivia
  session baseline from HU-16 already exist

**Application layer**
- Existing MediatR + pipeline baseline is in place, including
  `AuthorizationBehaviour`, `ValidationBehaviour`, `ICurrentUser`, and
  `[Authorize]`-driven request metadata
- HU-19 already established the session-administration authorization seam
- HU-21A already established transition orchestration and validator-chain
  precedent; HU-22 should extend those flows where pause/resume affects timer
  state rather than fork a second lifecycle path
- HU-07B already established reconnect as an application concern in
  `session-operations-service`

**Infrastructure / API**
- `SessionsHub` already exists from HU-07B and was extended in HU-21A for live
  session-state broadcast; HU-22 must reuse it for timer updates rather than
  introduce a second hub
- `/api/sessions` endpoint group already exists for create/reconnect/state flows
- EF Core persistence and `ILiveSessionRepository` already round-trip
  `LiveSession`; HU-22 should verify whether timer fields can reuse existing
  timing columns or need an additive migration

**Frontend**
- No participant-facing timer UI is documented as landed in predecessor context
  files
- Reconnect and live-session runtime surfaces already exist as the integration
  seam for delivering the timer snapshot to the client

**Coverage**
- ADR-0005 gate still applies. The exact post-HU-21A aggregate percentage is
  not recorded in predecessor context files; X.4 must measure and report the
  current merged coverage instead of assuming a carried-forward number.

## What this HU adds

| Concern | New work |
|---|---|
| Authoritative timer model | Add backend-owned countdown state for the active session/question runtime so remaining time is computed from persisted server state and clock facts, not from client-side timers. |
| Pause/resume semantics | Freezing the timer on `Paused` and resuming from the same remaining value when the session returns to `Active`. |
| Reconnect snapshot | Extend the reconnect/read surface so a participant who resumes or reconnects receives the current authoritative remaining time immediately. |
| Real-time timer push | Broadcast timer updates over SignalR/WebSockets so connected clients see the remaining time change live without polling. |
| Expiry seam | Define the authoritative expiry outcome and notification seam without expanding into full trivia round orchestration; downstream HU-33A consumes the timer-backed runtime for automated progression. |
| Frontend | Participant-facing timer display that renders the backend-provided remaining time and stays in sync across pause, resume, and reconnect. |

## Touched surfaces

- `backend/services/session-operations-service/` — domain, application,
  infrastructure, and API layers
- `frontend/` participant live-session UI
- API/runtime contract boundary: timer snapshot/read shape plus SignalR
  `SessionTimerUpdated` (or equivalent) notification payload

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **HU-21A is the timer backbone.** HU-22 must build on the landed state model.
  Do not recreate lifecycle rules or bypass the existing transition flow when
  deciding whether time advances.
- **SignalR must be reused, not duplicated.** HU-07B/HU-21A already created the
  live runtime and state-broadcast path. Timer updates should extend the same
  real-time surface.
- **Authoritative means backend-owned.** The client may animate or render the
  countdown, but the source of truth for remaining time, freeze/resume behavior,
  and reconnect recovery lives in `SessionOperations`.
- **DES-30 is trivia-focused; the broader story is wider.** `umbral_user_stories.md`
  mentions mission-session countdown and trivia-question countdown, while the
  `DES-30` acceptance criteria explicitly mention the active trivia question.
  Scope this slice to the authoritative timer behavior required by DES-30 and
  record any mission-wide timer generalization as an explicit decision rather
  than silently broadening the work.
- **Do not smuggle in full round orchestration.** Sprint planning notes say
  HU-22 is the timer gate for HU-33A, but round sequencing and final-results
  flow still belong to downstream trivia orchestration slices.
- **Branch base is mechanical, not semantic.** This context bases from
  `feature/hu-18-asociacion-de-equipos-a-sesiones` because it is the same-service
  predecessor still in progress. The direct functional blocker for HU-22 is
  HU-21A, which is already done.
