# Plan: HU-22 Mobile — Participant Live Session Timer

**Ref:** HU-22
**Date:** 2026-06-04
**Scope:** Participant-facing live session timer for the Expo mobile app.
**Builds on:** HU-07A team lobby, HU-07B participant reconnection (`team-space.tsx`,
`useReconnect`, `SessionsHub.ReconnectAsync`), HU-22 backend authoritative timer.

> This is the **participant** slice the HU-22 prompt calls the primary frontend work. The
> operator-dashboard companion lives in `frontend/plans/hu-22-frontend-operator-live-session-timer.md`.
> Both consume the same backend-authoritative timer; neither owns timer state. Mobile must not
> compute remaining time from `Date.now()` as a source of truth.

---

## Context

The participant's live view is `LiveTeamSpace` inside `src/app/(app)/team-space.tsx`. It renders only
when `useReconnect` reports `status === 'reconnected'`, i.e. the participant has been admitted into
their team space. Today it shows a "Joined / resumed" panel, a TEAM/PARTICIPANT card, and a Leave
button. The timer belongs here, between the join panel and the team card, so the participant sees
remaining time the moment the operator starts the session.

The session "starting" is not a client event. The operator drives the lifecycle (HU-21A); the backend
`AuthoritativeSessionTimerWorker` then broadcasts `SessionTimerUpdated` to the live-session group. The
participant connection is **already a member of that group** — `SessionsHub.ReconnectAsync` adds it
(`SessionsHub.cs:50`, group `live-session:{id}`). So the mobile app is already in the right room; it
just needs to listen.

---

## Backend contract (verified against uncommitted HU-22 source — NOT the operator plan's guesses)

The operator plan predicted seconds + a `timerState` enum. The real records differ. Use these.

### REST snapshot

```
GET /api/sessions/{liveSessionId}/participants/timer?teamId={teamId}&token={token}
```

`token` is the optional participant token (same one carried in the reconnect context). Response is
`SessionTimerSnapshotDto`:

```ts
export type SessionTimerSnapshotDto = {
  liveSessionId: string
  teamId: string | null
  sessionState: string
  totalSeconds: number
  remainingSeconds: number
  timerStatus: string        // backend status token, e.g. "NotStarted" | "Running" | "Paused" | ...
  isAdvancing: boolean
  isExpired: boolean
  observedAt: string         // ISO timestamp
  advancingSince: string | null
  expiredAt: string | null
}
```

### SignalR event

Method name `"SessionTimerUpdated"`, broadcast to group `live-session:{liveSessionId}`. Payload
`SessionTimerUpdatedNotificationDto`:

```ts
export type SessionTimerUpdatedNotificationDto = {
  liveSessionId: string
  remainingMilliseconds: number   // NOTE: milliseconds, not seconds
  isPaused: boolean
  emittedAt: string
  totalMilliseconds: number       // milliseconds
  isExpired: boolean
  sessionState: string
}
```

Contract notes:

- The live event is in **milliseconds** with `isPaused` / `isExpired` booleans — there is no enum and
  no seconds field. Convert ms → s for display; derive the display state from `isPaused` / `isExpired`
  / `sessionState`.
- The event carries no `teamId`. Filter incoming events by `liveSessionId` only (the participant is in
  exactly one session group).
- `SessionOperations` is the timer authority. `Paused` freezes the value; `Expired`/terminal stop it.

---

## Architecture decisions

- **Read-only.** The participant never mutates timer state. There are no lifecycle controls on mobile.
- **Snapshot first, SignalR second.** When `status === 'reconnected'`, fetch the participant snapshot,
  then apply pushed `SessionTimerUpdated` events.
- **Reconnect reloads from REST.** On hub reconnect, re-fetch the snapshot before trusting pushed
  state. Hook into the existing `onreconnected` path in `use-reconnect.ts`.
- **Pure backend ticks (chosen).** No local per-second interpolation. The visible value changes only
  when a `SessionTimerUpdated` arrives or the snapshot is (re)fetched. Simpler and fully authoritative;
  the displayed number steps at the backend broadcast cadence. (If smoother motion is wanted later, add
  interpolation that reconciles to every backend tick and freezes on paused/expired — out of scope here.)
- **Missing context renders `Unavailable`,** never a stale countdown.
- **No new hub method.** Reuse the existing `live-session:{id}` group membership from `ReconnectAsync`.

---

## Phases

### Phase 1 — Types

**Scope**

- Add `SessionTimerSnapshotDto` and `SessionTimerUpdatedNotificationDto` (shapes above) to a new
  `src/lib/realtime/timer-types.ts`.
- Add a narrow display model + mapper, e.g.
  `type TimerDisplay = { label: string; pct: number; tone: 'running' | 'paused' | 'expired' | 'unavailable' }`.

**Gate** — TypeScript accepts the types; no behavior change.

---

### Phase 2 — REST snapshot fetcher

**Scope**

- New `src/lib/api/sessions.ts` with
  `getParticipantTimerSnapshot(liveSessionId, teamId, token?): Promise<SessionTimerSnapshotDto>`.
- Use `apiClient.get` with `cache: 'no-store'` and no-cache headers, mirroring `teams.ts:23`.
- Build the query string with `teamId` and optional `token`, URL-encoded.
- Map `ApiError` statuses to stable tokens, reusing the `use-team-lobby.ts:18` pattern:
  `0 → network-error`, `401 → unauthorized`, `403 → forbidden`, `404 → not-found`,
  `409 → timer-unavailable`, other → `error`.

**Gate** — unit test covers the URL/query building and each error token. No timer math here.

---

### Phase 3 — Realtime subscription

**Scope**

- Extend `createSessionsHubConnection` in `src/lib/realtime/sessions-hub.ts` to expose
  `onTimerUpdated(cb: (n: SessionTimerUpdatedNotificationDto) => void): () => void`, implemented as
  `connection.on('SessionTimerUpdated', cb)` returning an unsubscribe that calls `connection.off`.
- Do **not** add a new invoke or group method — `ReconnectAsync` already joins `live-session:{id}`.
- Keep `start`/`stop`/`reconnect` unchanged.

**Gate** — focused test proves the handler receives a payload and unsubscribe removes it; existing
reconnect behavior is not regressed.

---

### Phase 4 — Timer state hook

**Scope**

- New `src/lib/realtime/use-session-timer.ts`:
  `useSessionTimer({ client, liveSessionId, teamId, token, isReconnected, reconnectNonce })`.
- Behavior:
  - When `isReconnected` becomes true, fetch the snapshot (snapshot-first).
  - Subscribe via `client.onTimerUpdated`; ignore events whose `liveSessionId` differs.
  - On each event, replace state: convert ms → s, set paused/expired/state from the booleans.
  - Re-fetch the snapshot whenever the hub reconnects. Expose a `reconnectNonce` from
    `use-reconnect.ts` (increment in the existing `onreconnected` callback) and depend on it.
  - Track `lastSyncedAt` from `observedAt` / `emittedAt`.
  - Expose `{ timer, isLoading, error, display }`.

**Gate** — unit tests: ms→s conversion, paused freeze, `isExpired` → expired tone, missing →
unavailable, event filtering by `liveSessionId`, re-fetch on reconnect nonce change.

---

### Phase 5 — Timer bar component + wiring

**Scope**

- New `src/components/session-timer-bar.tsx` (React Native, no SVG):
  - A track `View` (warm raised surface) containing a fill `View` with `width: \`${pct}%\``.
  - Tabular `MM:SS` (or `H:MM:SS` over an hour) via the `Text` component; `--:--` when unavailable.
  - A small state chip: `Running` / `Paused` / `Not started` / `Expired` / `Unavailable`.
  - Fill colour from `constants/theme.ts`: `emberAccent` (running), `signalWarning`
    (paused / not started), `signalCritical` (expired/near-expired); dim on terminal states.
  - Accessibility: `accessibilityRole="progressbar"` with
    `accessibilityValue={{ min: 0, max: 100, now: pct }}`, and an `accessibilityLabel` for the value.
  - Clamp `pct` to `[0, 100]`.
- Wire into `LiveTeamSpace` in `team-space.tsx`: call `useSessionTimer` (passing the hub `client`,
  `result.liveSessionId`, `result.teamId`, and the context token), and render `<SessionTimerBar />`
  between the join panel and the TEAM/PARTICIPANT card. The hub `client` and a reconnect nonce need to
  be surfaced from `useReconnect` (currently it owns the client internally) — extend its return value.

**Gate**

- Opening a live team space shows the current backend snapshot.
- A mocked `SessionTimerUpdated` (running) changes the visible value and bar width.
- A mocked paused update freezes the value and switches the chip.
- Missing timer context shows `Unavailable`, not stale time.
- Bar and text fit narrow phone widths without overlapping the existing panels.

---

### Phase 6 — Tests

- Unit: ms→s formatting, percentage clamp, status→tone mapping, snapshot error mapping, event
  normalization + `liveSessionId` filtering, reconnect re-fetch.
- Component: render `LiveTeamSpace` with a fake hub client; assert running → paused → expired
  transitions and the unavailable fallback. Follow the patterns in `src/__tests__/`.

---

## Acceptance criteria

- In the live team space, the participant sees a remaining-time value and progress bar for their
  session, initialized from the backend participant snapshot.
- `SessionTimerUpdated` events update the visible time and bar with no manual reload.
- Pause freezes the value and bar; expiry shows `00:00` with the critical tone.
- Hub reconnect re-fetches the snapshot before showing live updates again.
- Missing/expired context renders `Unavailable`, never a stale local countdown.
- No new hub method; participant uses the existing `live-session:{id}` group membership.
- Visuals follow `mobile/DESIGN.md`: warm surfaces, restrained ember accent, full borders,
  tabular live data, no nested cards.

---

## Out of scope

- Operator dashboard timer (separate plan).
- Answer submission, rounds, scoring, question navigation.
- Editing timer duration from mobile.
- Local client-owned countdown / interpolation (deferred; pure backend ticks chosen).
