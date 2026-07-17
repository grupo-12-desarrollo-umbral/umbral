# Plan: Authoritative ranking-reveal lifecycle

## TL;DR

The ranking still appears for **10 seconds between substages** during normal play.

Today, mobile independently hides a nonterminal ranking after 10 seconds, while the backend advances
on a later one-second worker tick or waits while the session is paused. This disagreement can briefly
show gameplay, dismiss the ranking during a pause, activate another substage immediately before a
mission-deadline finish, and lose the ranking after reconnecting.

Make the backend the sole authority for ending and restoring a ranking reveal:

- `SubstageRankingRevealStarted` opens the ranking.
- `SubstageAdvanced` closes a nonterminal ranking and returns mobile to gameplay.
- `Finished` keeps the ranking visible as the final screen.
- `Paused` leaves the ranking visible until backend progression resumes.
- The session snapshot carries an active reveal so reconnecting restores it.
- When the mission deadline has elapsed by reveal completion, backend finishes directly on the
  ranking instead of advancing into another substage first.

This is a lifecycle correction, not a visual redesign.

## What we are fixing

### 1. Normal nonterminal reveal can flash gameplay

Mobile starts a local timeout from `revealUntil - emittedAt` and hides the ranking at exactly 10
seconds. Backend checks the persisted reveal on its next one-second worker tick. Between those two
clocks, mobile renders the previous play surface.

### 2. Pause dismisses the ranking only on mobile

Backend excludes paused sessions from timer-worker progression, deliberately retaining the reveal.
Mobile's timeout is unaware of session state and continues running, so the ranking disappears while
the backend still considers it active.

### 3. Mission deadline at or before reveal completion can flash another substage

Backend processes the reveal branch before mission expiry. When a nonterminal reveal completes after
the mission deadline, it currently advances normally; mission expiry is handled on a later tick.
Mobile has already hidden the ranking, so it may show gameplay from the old or newly activated
substage before `Finished` arrives.

### 4. Reconnect during a reveal loses the ranking

The reveal is currently push-only. A participant reconnecting after the start event has no snapshot
field from which to restore the active ranking.

## Desired behavior

```text
SubstageRankingRevealStarted
          |
          v
   Show ranking
      |       |
   Paused   reconnect
      |       |
      +-------+----> keep/restore ranking
          |
          +-- SubstageAdvanced --> return to gameplay
          |
          +-- Finished ---------> keep ranking as final screen
```

In uninterrupted normal play, `SubstageAdvanced` still occurs after the backend's ten-second reveal
window. The only visible difference is that mobile waits through any worker-tick/event-delivery gap.

## Contract change

This is an additive backend-to-mobile contract change. Both workloads must be updated together.

Extend the session timer/snapshot response with a nullable active ranking-reveal object. Use the
existing naming and JSON casing conventions; the illustrative shape is:

```json
{
  "activeRankingReveal": {
    "substageSnapshotId": "uuid",
    "playMode": "TreasureHunt",
    "revealUntil": "2026-07-17T12:00:10Z",
    "isTerminal": false,
    "emittedAt": "2026-07-17T12:00:00Z"
  }
}
```

Requirements:

- `null` when no ranking reveal is active.
- Present while paused, even if the original wall-clock `revealUntil` has passed.
- Present until the backend commits advancement or finish.
- Sufficient for mobile to render the same ranking screen after reconnecting.
- Additive for existing consumers; flag and update any strict frontend normalizer if this snapshot is
  shared with the operator app.

The push event remains the low-latency way to open the reveal. The snapshot is the recovery source,
not a replacement for the event.

## Implementation increments

### Increment 1: Backend deadline-safe reveal completion

Change ranking-reveal completion so it decides between finishing and advancing atomically:

1. When the worker observes an elapsed ranking reveal, evaluate the authoritative mission timer at
   the same `now` instant.
2. If the mission deadline has elapsed, clear the reveal and transition directly to `Finished`.
3. Otherwise, clear the reveal and advance to the next substage as today.
4. Do not activate or broadcast an intermediate substage when finishing for the deadline.
5. Preserve terminal-reveal behavior: the last substage still finishes on its ranking.

Prefer placing this decision behind `ISubstageAdvanceCoordinator` so the worker does not duplicate
domain progression rules. The aggregate should expose the minimum explicit operation needed to make
the transition valid and atomic; do not infer expiry from mobile timestamps.

Tests:

- Deadline before a nonterminal reveal ends: session remains on the ranking until reveal completion,
  then becomes `Finished` without `SubstageAdvanced`.
- Deadline exactly equal to `SubstageRevealUntil`: same direct finish.
- Deadline after reveal completion: normal substage advancement.
- Last-substage reveal: existing terminal finish remains unchanged.

### Increment 2: Backend snapshot exposes active reveal

1. Add the nullable active-reveal DTO to the authoritative session snapshot.
2. Build it from persisted aggregate state, not transient domain events.
3. Populate it for both trivia and treasure-hunt substages.
4. Keep it populated while paused.
5. Clear it only when reveal completion and advancement/finish are committed.

Tests:

- Active reveal appears in the snapshot with the correct substage, play mode, deadline, and terminal
  marker.
- Paused reveal remains present after its original wall-clock deadline.
- Completed reveal returns `null`.
- Snapshot serialization uses the expected camelCase wire shape.

### Increment 3: Mobile uses authoritative lifecycle events

Refactor `useRankingReveal` from a local-duration timer into a small authoritative state machine:

- On matching `SubstageRankingRevealStarted`, set the active reveal.
- On matching `SubstageAdvanced`, clear a nonterminal reveal.
- On `SessionStateChanged -> Finished`, retain/show the ranking as final.
- On `Cancelled`, clear the reveal and preserve the existing cancellation UI.
- On `Paused`, make no reveal transition.
- Remove the nonterminal `setTimeout` as a presentation authority.
- Seed the active reveal from the session snapshot on initial load and reconnect.
- Ignore stale events for another session or a previously completed substage.

The ranking projection and visual component remain unchanged. The hook only decides whether that
component owns the screen.

Tests:

- Ranking remains visible after ten local seconds until `SubstageAdvanced` arrives.
- `SubstageAdvanced` returns to the correct next play surface.
- Pausing beyond ten seconds keeps the ranking visible.
- Resuming alone does not dismiss it; the subsequent backend advancement does.
- `Finished` retains the ranking as the final screen.
- `Cancelled` shows the cancellation surface.
- Snapshot seed restores a reveal after reconnect without receiving a new start event.
- Events for another session and stale substage events are ignored.

### Increment 4: Cross-layer integration verification

Add a focused contract/integration scenario for each timing boundary:

1. **Normal:** reveal starts, remains for ten seconds, backend advances on its next tick, mobile then
   returns to gameplay with no intermediate flash.
2. **Pause:** pause one second into the reveal, wait beyond ten seconds, confirm ranking remains;
   resume and confirm it closes only after backend advancement.
3. **Deadline:** deadline lands exactly at reveal completion, confirm no `SubstageAdvanced` event and
   ranking becomes the final screen.
4. **Reconnect:** disconnect during the reveal, reconnect from the snapshot, confirm ranking is
   restored and later closes through the authoritative event.

Use fake time for mobile tests and the backend `TimeProvider`/worker harness for backend tests so the
boundaries are deterministic.

## Files likely involved

Backend:

- `services/session-operations-service/src/Infrastructure/Realtime/AuthoritativeSessionTimerWorker.cs`
- `services/session-operations-service/src/Application/Sessions/Common/SubstageAdvanceCoordinator.cs`
- `services/session-operations-service/src/Application/Sessions/Common/SessionTimerSnapshotDtoFactory.cs`
- `services/session-operations-service/src/Application/Dtos/Sessions/SessionTimerSnapshotDto.cs`
- `services/session-operations-service/src/Domain/Entities/LiveSession.cs`
- Focused worker, coordinator, snapshot-factory, endpoint, and serialization tests.

Mobile:

- `src/lib/realtime/use-ranking-reveal.ts`
- `src/lib/realtime/ranking-reveal-types.ts`
- `src/lib/realtime/sessions-hub.ts`
- `src/app/(app)/team-space.tsx`
- `src/__tests__/team-space-ranking-reveal.test.tsx`
- Snapshot/API and reconnect tests that seed `LiveTeamSpace`.

## Non-goals

- Changing the ten-second ranking duration.
- Redesigning the ranking screen or ranking projection.
- Suppressing live ranking convergence during the reveal.
- Adding client-side grace periods or worker-tick-sized timeout padding.
- Changing the existing five-second per-question answer reveal.

## Verification gates

Backend:

```sh
make -C backend build SVC=session-operations-service
make -C backend test SVC=session-operations-service
make -C backend gate SVC=session-operations-service
```

Mobile:

```sh
cd mobile
npm test -- --runInBand src/__tests__/team-space-ranking-reveal.test.tsx
npm test -- --runInBand
npm run lint
npx tsc --noEmit
```

## Done when

- Normal reveals still last ten backend-authoritative seconds.
- Mobile never exposes gameplay between local timeout and backend advancement.
- Paused reveals remain visible for the entire pause.
- Deadline completion finishes directly on the ranking without activating another substage.
- Reconnecting during a reveal restores the ranking.
- Terminal, finished, and cancelled presentation behavior remains correct.
- Backend and mobile focused suites, full suites, lint/type checks, and backend coverage gate pass.
