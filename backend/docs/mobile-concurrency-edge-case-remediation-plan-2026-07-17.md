# Mobile Concurrency Edge-Case Remediation Plan

**Date:** 2026-07-17  
**Status:** Proposed  
**Workload:** Mobile  
**Scope:** Close the three mobile correctness gaps found after implementing phases 1–6 of `mobile-concurrency-remediation-plan-2026-07-17.md`.

## Outcome

The mobile client must:

1. Arm its join guard before request construction can run and prevent stale attempts from mutating current hook state.
2. Collapse rapid team selections in the real lobby, including taps on different teams, and run post-join side effects exactly once for the initiating selection.
3. Reconcile ranking-reveal snapshots and SignalR notifications by server time so an older snapshot cannot overwrite a newer live transition.

The server remains authoritative for membership and session state. These changes are client-side defense-in-depth and recovery behavior. They do not change REST routes, request bodies, response bodies, or SignalR notification shapes.

## Guiding Decisions

### Separate request exclusion from UI state

React state is not a synchronous exclusion mechanism. Refs own same-render admission; state only renders progress and outcomes.

### Reset invalidates an attempt but does not cancel the network

A request may still complete after `reset()`. Every completion must prove that it still owns the active generation before it clears the guard or publishes an outcome. An old request must never clear or overwrite a newer request.

### One team selection owns navigation

Collapsing POSTs inside `useTeamJoin` is insufficient because every caller awaiting a shared promise can still run its own reconnect-context write, haptic, and navigation. The lobby needs its own synchronous selection owner.

### Reconcile real-time state with server timestamps

Use the timer snapshot's existing `observedAt` as the ordering time for snapshot state. Use `emittedAt`, `advancedAt`, and `changedAt` for live reveal, advance, and cancellation transitions. Do not use response-arrival order or a client clock.

## Phase 1 — Arm and Retire Join Attempts Safely

**Addresses:** The join guard is assigned after request construction starts.

### Implementation

Update `mobile/src/lib/membership/use-team-join.ts`:

1. Represent the active attempt with a stable identity and promise, plus a monotonically increasing generation.
2. Publish the active identity synchronously before `joinSessionTeam` can execute. One acceptable approach is to defer the API invocation to a promise microtask, assign that promise to the ref synchronously, and then start request construction.
3. When `join()` sees an active attempt, return the active promise without invoking the API again.
4. On completion, update `status` and `outcome` only if the completing attempt still owns the current generation.
5. Clear `joiningRef` only when it still points to that exact attempt. Use identity-safe cleanup so an older completion cannot clear a newer request.
6. Make `reset()` increment the generation, clear the current guard, and restore idle UI state. It invalidates late client-side completion; it does not claim to cancel the HTTP request.
7. Preserve the existing error mapping and the ability to retry after every terminal failure, including a synchronous request-construction exception.

Avoid relying on `status === 'joining'` for exclusion and avoid unconditional `joiningRef.current = null` in an old attempt's completion path.

### Tests

Extend `mobile/src/__tests__/team-join-hook.test.ts` with deterministic deferred promises:

- The guard is observable before the mocked API function runs.
- Two same-tick calls invoke the API once and share one terminal outcome.
- A synchronously thrown API mock produces a failure and permits a subsequent retry.
- Reset during request A, followed by request B, allows B to start.
- A completing after B starts does not clear B's guard.
- A completing after reset does not replace idle state or B's outcome.
- B remains the rendered terminal outcome regardless of A/B completion order.

### Acceptance

- Request construction cannot begin before the synchronous guard is armed.
- Only the owning attempt may clear the guard or publish hook state.
- Failure and reset always leave a valid retry path.

## Phase 2 — Make the Team Lobby Collapse Real Double Taps

**Addresses:** The production lobby clears the hook guard before each join, allowing duplicate POSTs and racing post-join side effects.

### Implementation

Update `mobile/src/app/(app)/team-lobby.tsx`:

1. Add a synchronous lobby-level selection ref that is claimed at the first line of `handleTeamSelect`, before reading render-lagging state or calling `resetJoin()`.
2. Ignore subsequent team selections while that ref is owned, including a tap on a different team.
3. Remove the unconditional pre-join reset that can clear an active hook attempt. Clear a previous terminal banner only through an operation that cannot invalidate an in-flight request, or let the new owning attempt clear it when it starts.
4. Bind the owning attempt to both runtime and reference team IDs.
5. Permit only that owner to write reconnect context, fire the success haptic, and call `router.replace`.
6. Release the lobby selection ref after a failed or unauthorized terminal result. A successful result may retain ownership until navigation unmounts the screen.
7. Ensure late completion after unmount or invalidation performs no reconnect-context write or navigation.

Keep the hook-level guard from phase 1 as defense-in-depth; the lobby ref protects caller side effects and cross-team taps.

### Tests

Extend `mobile/src/__tests__/team-lobby-screen.test.tsx` using an unresolved join request:

- Pressing the same team twice before rerender sends one POST.
- Pressing team A and then team B before rerender sends only team A's POST.
- The duplicate caller cannot save team B as reconnect context.
- Success saves one reconnect context, fires one success path, and navigates once.
- A failed join releases the lobby guard and a later tap sends a new request.
- Late completion from an invalidated/unmounted attempt causes no navigation or persistence.

Retain the isolated hook tests, but treat the screen-level tests as the acceptance harness for the original mobile double-tap requirement.

### Acceptance

- A fast same-device double tap sends exactly one join request through the production screen.
- Rapid taps on different teams select one deterministic owner until its attempt terminates.
- Reconnect context and navigation always name the team whose join request succeeded.
- Post-join side effects execute once.

## Phase 3 — Order Ranking-Reveal Snapshots and Live Events

**Addresses:** Snapshot responses can overwrite newer SignalR transitions or reopen a reveal after an advance.

### Implementation

Update:

- `mobile/src/lib/realtime/use-session-timer.ts`
- `mobile/src/lib/realtime/use-ranking-reveal.ts`
- The associated timer/reveal types if a client-internal reconciliation type is useful

Implement a single ordered reveal transition model:

1. Surface a successful snapshot's `observedAt` alongside `activeRankingReveal`. The pair must describe the same response.
2. Do not mark reveal snapshot reconciliation as fresh when the snapshot request fails. The active-question failure fallback may continue independently, but a failed request must not bump a reveal version that replays cached reveal data.
3. Store the latest applied reveal transition as `{ state, serverTime, precedence }`, where state is either an active reveal or no reveal.
4. Convert inputs into ordered transitions:
   - Snapshot active/null: `snapshot.observedAt`.
   - `SubstageRankingRevealStarted`: `notification.emittedAt`.
   - `SubstageAdvanced`: `notification.advancedAt`.
   - `Cancelled`: `notification.changedAt`.
5. Apply a transition only when it is newer than the last applied transition. Define and test a deterministic equal-time precedence that favors closing transitions (`Cancelled` and a matching real advance) over opening transitions.
6. Continue matching `liveSessionId` and `fromSubstageId` before an advance may close a reveal.
7. Reset reconciliation state when `liveSessionId` changes so a reveal from the previous session cannot leak into the next one.
8. Preserve current behavior for pause, resume, terminal finish, and a deadline finish: these do not independently dismiss the final ranking.

Use only existing server-provided timestamps. Do not introduce a client timeout or infer authority from fetch completion order.

### Deterministic Race Tests

Extend `mobile/src/__tests__/session-timer-hook.test.ts` and `mobile/src/__tests__/team-space-ranking-reveal.test.tsx`:

- Snapshot read without a reveal, reveal-start push, then late snapshot response: reveal stays open.
- Snapshot read with a reveal, matching advance push, then late snapshot response: reveal stays closed.
- Reveal snapshot response followed by a newer advance: reveal closes.
- Newer null snapshot followed by an older reveal-start push: reveal stays closed.
- Failed resync after a prior reveal does not reopen or reseed cached reveal state.
- Equal-time start and advance resolve deterministically in favor of the advance.
- Stale advance for another substage and events for another session remain ignored.
- Changing `liveSessionId` clears the previous session's reconciliation state.
- Pause/resume and terminal/deadline-finish behavior remains unchanged.

Use manually controlled promises and direct event callbacks; do not depend on timers or probabilistic scheduling.

### Acceptance

- Network response order cannot reverse authoritative reveal state.
- A reveal cannot be dismissed by an older null snapshot.
- A reveal cannot be reopened by an older active snapshot after advancement.
- Snapshot failure does not replay cached reveal state.
- Existing REST and SignalR contracts remain unchanged.

## Verification

After each phase, run from `mobile/`:

```text
npm run lint
npx tsc --noEmit
npm test -- --runInBand
```

The phase-specific tests must fail against the pre-remediation implementation and pass after the phase. Complete the plan only when the full mobile suite remains green.

## Completion Checklist

- [ ] Phase 1: join attempts are armed before request construction and use identity-safe cleanup.
- [ ] Phase 2: production lobby double taps and cross-team rapid taps produce one owned attempt and one side-effect chain.
- [ ] Phase 3: reveal snapshots and live notifications reconcile by server time, including failure and equal-time cases.
- [ ] No public API or SignalR contract changed.
- [ ] Mobile lint, typecheck, and full tests pass.
