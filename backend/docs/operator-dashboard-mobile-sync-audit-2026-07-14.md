# Operator Dashboard → Mobile App UI Sync Audit

**Date:** 2026-07-14
**Status:** Audit complete, fixes pending
**Scope:** Real-time visibility of operator lifecycle actions (Start, Pause, Resume, Finish, Cancel) on participant mobile app

---

## Architecture Recap

```
Operator clicks button (DashboardClient.tsx)
  → PATCH /api/sessions/{id}/state (REST, through api-gateway)
  → TransitionSessionStateCommandHandler
  → liveSession.MoveTo(targetState) → raises SessionStateChangedEvent
  → MediatR dispatches to multiple INotificationHandlers
  → SignalR pushes to connected clients
```

SignalR push groups and their audiences:

| Group | Members |
|-------|---------|
| `live-session:{id}` | All participants + operators |
| `live-session-operators:{id}` | Operators only |
| `team:{id}` | Team members only |

---

## Verified: Per-Action UI Behavior

### Start (`Preparing` → `Active`)

| Widget | Dashboard | Mobile (Trivia) | Mobile (TreasureHunt) |
|--------|-----------|-----------------|----------------------|
| Timer bar | Running | Running | Running |
| Question | Pre-game countdown → question | Countdown → question | N/A |
| TreasureHunt board | Per-team progress visible | N/A | Board renders |
| State label | "Active" | — | — |

**Verdict:** OK. Both platforms see the transition.

### Pause (`Active` → `Paused`)

| Widget | Dashboard | Mobile (Trivia) | Mobile (TreasureHunt) |
|--------|-----------|-----------------|----------------------|
| Timer bar | "Paused" (frozen) | **BUG: "Running" (stale)** | **BUG: "Running" (stale)** |
| Question | Flash-disappear bug (see below) | Question stays (OK) | N/A |
| TreasureHunt board | Per-team progress, state "Paused" | N/A | Board stays (OK) |
| State label | "Paused" | — | — |

**Verdict:** BROKEN on mobile. Timer shows stale running state.

### Resume (`Paused` → `Active`)

| Widget | Dashboard | Mobile (Trivia) | Mobile (TreasureHunt) |
|--------|-----------|-----------------|----------------------|
| Timer bar | Running | Running | Running |

**Verdict:** OK. Timer resumes on next `SessionTimerUpdated` tick.

### Cancel (`Active` → `Cancelled`)

| Widget | Dashboard | Mobile (Trivia) | Mobile (TreasureHunt) |
|--------|-----------|-----------------|----------------------|
| Question panel | Disappears | "Session closed — cancelled by host" (red) | Board vanishes → cancellation message |
| TreasureHunt board | State label: "Cancelled" | N/A | Same cancellation message |
| Session controls | All removed ("terminal") | — | — |

**Verdict:** OK. Terminal state visible on both platforms.

### Finish (`Active` → `Finished`)

Same as Cancel but message reads "Session has ended. Thanks for playing."

**Verdict:** OK.

---

## Confirmed Issues

### Issue 1 (CRITICAL) — Mobile timer stale on Pause

**Files:**
- `mobile/src/lib/realtime/use-session-timer.ts:114-140` — `onTimerUpdated` handler
- `backend/services/session-operations-service/src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs:59` — queries only `SessionState.Active`
- `backend/services/session-operations-service/src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs:137-138` — `session.State == SessionState.Active`

**Cause:** `useSessionTimer` updates timer state from only two sources:
1. `SessionTimerUpdated` SignalR push (line 114-140)
2. REST `getParticipantTimerSnapshot` (line 72-112)

On Pause:
- The backend timer worker (`AuthoritativeSessionTimerWorker`) only processes `Active` sessions. When state becomes `Paused`, it silently stops — **no final "isPaused: true" tick is emitted**.
- `SessionStateChanged` IS received by `useActiveQuestion` (and sets `sessionState: 'Paused'` there) but `useSessionTimer` **does not listen** for `SessionStateChanged`.
- The REST re-fetch only triggers on `reconnectNonce` / `resyncNonce` changes, neither of which changes on Pause.

**Manifestation:** Participant sees timer bar with "Running" label and last active tick value while the session is actually paused. No visual indication the session was paused at all (in-trivia question stays on screen but timer never freezes).

**Affects:** Both trivia and treasure hunt substages.

**Recommended fix:** `useSessionTimer` must subscribe to `SessionStateChanged` and react:
- On `Paused`: set `isPaused: true` (frozen timer) without waiting for a timer tick
- On `Active` (from Paused): set `isPaused: false` (resume)

Alternatively, trigger a REST re-fetch when `SessionStateChanged` fires.

This is a **mobile-only** fix — no backend change needed (the state reached SignalR groups correctly).

### Issue 2 (MINOR) — Dashboard question flash on Pause

**File:** `frontend/app/dashboard/DashboardClient.tsx:593-596, 833-839`

**Cause:** Race between two signals after Pause:
1. REST transition response (line 837): `dispatchTimer` + `hydrateActiveQuestion(timer.activeQuestion, false)` — shows frozen question with paused-state timer
2. SignalR `SessionStateChanged` (line 596): `resetTriviaRound()` — clears question to idle (`null`)

The REST response arrives first and renders the frozen question, then the SignalR push immediately erases it. Result: question flashes then disappears.

**Recommended fix:** The `onStateChanged` handler (line 593-599) should NOT call `resetTriviaRound()` on Pause. The REST response already handles the correct state. Only reset on Cancel/Finish (terminal states).

### Issue 3 (COSMETIC) — Stale comment in `useTeamBoard`

**File:** `mobile/src/lib/realtime/use-team-board.ts:28-29`

> "There is no `TeamBoardUpdated` push yet"

This comment is stale. `BroadcastTeamBoardNotificationHandler` in the backend does push `TeamBoardUpdated` on both `SessionStateChangedEvent` and `SubstageAdvancedEvent`. The handler at line 87-91 is wired and receiving these pushes.

**Recommended fix:** Remove or update the comment.

---

## Mixed Mission (Trivia ↔ TreasureHunt) Verification

Substage transitions work correctly on mobile because `SubstageAdvancedEvent` triggers `BroadcastTeamBoardNotificationHandler` which pushes `TeamBoardUpdated` to `team:{id}`. The mobile `useTeamBoard` handler (line 87-91) replaces the board wholesale, and `team-space.tsx:378` switches between treasure hunt board and trivia surface based on `board.activeSubstage.playMode`.

**Verdict:** OK for mixed missions. Substage advance → board push → UI switches correctly.

---

## Suggested Skills

For the implementing agent:
- `aspnet-backend-testing` — if backend timer changes are needed
- `cqrs-mediatr-aspnetcore` — if domain event handling changes
- `systematic-debugging` — to reproduce and verify fixes

For mobile fixes:
- `tdd` — recommended for `useSessionTimer` changes (existing test at `mobile/src/__tests__/session-timer-hook.test.ts`)

---

## Key Source References

| Component | Path |
|-----------|------|
| Mobile timer hook | `mobile/src/lib/realtime/use-session-timer.ts` |
| Mobile active question hook | `mobile/src/lib/realtime/use-active-question.ts` |
| Mobile team board hook | `mobile/src/lib/realtime/use-team-board.ts` |
| Mobile team space screen | `mobile/src/app/(app)/team-space.tsx` |
| Dashboard main client | `frontend/app/dashboard/DashboardClient.tsx` |
| Backend timer worker | `backend/services/session-operations-service/src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs` |
| Backend team board broadcaster handler | `backend/services/session-operations-service/src/Application/Sessions/EventHandlers/BroadcastTeamBoardNotificationHandler.cs` |
| Backend session state broadcaster | `backend/services/session-operations-service/src/Api/Hubs/SessionStateBroadcaster.cs` |
| Backend session operations controller | `backend/services/session-operations-service/src/Api/Controllers/SessionsController.cs` |
| Backend transition handler | `backend/services/session-operations-service/src/Application/Sessions/Commands/TransitionSessionState/TransitionSessionStateCommandHandler.cs` |
| Dashboard lifecycle actions | `frontend/app/lib/session-lifecycle.ts` |
