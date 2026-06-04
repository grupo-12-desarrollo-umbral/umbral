# Plan: HU-22 Frontend - Operator Live Session Timer

**Ref:** HU-22
**Date:** 2026-06-04
**Scope:** Operator dashboard companion slice for the authoritative session timer.
**Builds on:** HU-16 session creation, HU-19 operator assignment, HU-21A lifecycle state
transitions and live state broadcast, HU-22 backend timer contract.

---

## Context

The current operator dashboard has two distinct session surfaces:

1. **My sessions / setup view** in `SessionsPanel.tsx`.
   This lists assigned sessions and lets the operator select one before live operation.
2. **Live operation view** in `DashboardClient.tsx`.
   This is shown when an assigned session is selected and contains the session title, lifecycle
   state, transport status, last transition, and lifecycle controls such as Start, Pause, Resume,
   Finish, and Cancel.

The timer belongs in the second surface only: the selected assigned session's live-operation view.
It should not become a primary element in the assignment list, because the operator cannot act on
time there. The timer should sit in the live-operation hero, directly under the session title /
metrics row and above the lifecycle controls, so the operator sees remaining time while deciding
whether to Start, Pause, Resume, Finish, or Cancel.

Important scope note: the backend HU-22 prompt describes the required frontend slice as
participant-facing. This plan is an operator-dashboard companion plan. It must consume the same
backend-authoritative timer snapshot and SignalR timer updates; it must not create a separate
operator-owned timer model.

---

## Design System Obligations

Follow `frontend/DESIGN.md`:

- Use the existing warm command-center vocabulary: panel surfaces, restrained ember accent,
  semantic success/warning/critical tones, full perimeter borders, and tabular numbers.
- Keep the timer dense and operational. It should read like an instrument in the command center,
  not like a large marketing hero.
- Use the ember accent sparingly. The progress bar fill may use ember for normal running state,
  warning for low time, and critical for expired/near-expired states.
- Do not use nested cards, side-stripe borders, gradient text, decorative glow stacks, or a
  separate one-note timer palette.
- Maintain readable labels and AAA-oriented contrast for the remaining-time value, pause state,
  expired state, and terminal-session state.
- Preserve the current control hierarchy: lifecycle action buttons remain the primary operator
  controls; the timer informs those controls instead of replacing them.

---

## Placement Decision

Place a new timer block in the operator live-operation branch of `DashboardClient.tsx`, inside the
existing `styles.hero` section:

```tsx
<section className={styles.hero} data-testid="operator-panel">
  <div className={styles.heroHeader}>...</div>

  <OperatorSessionTimerPanel ... />

  {liveUpdateNote && ...}
  {realtimeAuthExpired && ...}
</section>
```

The timer panel should include:

- Remaining time value, e.g. `07:18`, using tabular numbers.
- A compact label such as `Remaining`.
- Timer state chip: `Running`, `Paused`, `Not started`, `Expired`, or `Unavailable`.
- Horizontal progress bar representing `remainingSeconds / totalSeconds`.
- Short source/status metadata, e.g. `Synced from backend at 8:00:42 PM`.

The existing hero metrics row can keep `Current state`, `Transport`, and `Last transition`.
Replacing one of those metrics with the timer would make the bar too cramped. A full-width timer
block below the hero header gives the bar enough width without moving controls away from the top.

---

## Expected Backend Contract

Use the verified HU-22 backend contract once it lands through the api-gateway. If final field names
differ, keep the adaptation isolated in `app/lib/sessions.ts` and
`app/lib/realtime/session-state-client.ts`.

### Timer snapshot

Expected read surface:

```http
GET /api/sessions/{liveSessionId}/timer
Authorization: Operator assigned to session, or Administrator if backend allows admin supervision
```

Expected response:

```ts
export type SessionTimerSnapshotDto = {
  liveSessionId: string
  totalSeconds: number
  remainingSeconds: number
  timerState: 'NotStarted' | 'Running' | 'Paused' | 'Expired' | 'Unavailable'
  sessionState: SessionLifecycleState | string
  serverTime: string
  updatedAt: string
}
```

### SignalR timer update

Expected hub event on the existing `SessionsHub`:

```ts
export type SessionTimerUpdatedNotificationDto = {
  liveSessionId: string
  totalSeconds: number
  remainingSeconds: number
  timerState: 'NotStarted' | 'Running' | 'Paused' | 'Expired' | 'Unavailable'
  sessionState: SessionLifecycleState | string
  serverTime: string
  changedAt: string
}
```

Rules:

- `SessionOperations` remains the timer authority.
- `Active` / `Running` decrements from backend updates.
- `Paused` freezes the displayed value and bar.
- Reconnect refreshes from the backend snapshot before trusting any old client state.
- Missing timer context renders `Unavailable`, not stale countdown text.

---

## Architecture Decisions

- **Operator timer is read-only timer state.** The operator can Start, Pause, Resume, Finish, and
  Cancel through the existing lifecycle transition actions. The timer display does not mutate time
  directly.
- **Reuse the selected operator session branch.** Only render the timer when
  `role === 'operator'`, `selectedOperatorSession` exists, and the live-operation view is open.
- **Snapshot first, SignalR second.** When the operator opens a session, fetch the authoritative
  timer snapshot. Then subscribe to `SessionTimerUpdated` events through the existing realtime
  client.
- **Reconnect reloads from REST.** On realtime reconnect, fetch the snapshot again, then resume
  applying pushed updates.
- **No client-owned countdown.** Do not calculate remaining time from `Date.now()` as the source
  of truth. If a local visual interpolation is used between backend ticks, it must reconcile to
  each backend update and stop immediately on `Paused`, `Expired`, `Finished`, or `Cancelled`.
- **Transition actions refresh timer.** After Start, Pause, Resume, Finish, or Cancel succeeds,
  refresh the timer snapshot so the displayed timer matches the newly accepted lifecycle state.
- **Terminal states are explicit.** `Finished` and `Cancelled` should dim the bar and show terminal
  timer metadata instead of a running countdown.

---

## Phases

### Phase 1 - Types

**Scope**

- Add timer DTO types to `app/lib/definitions.ts`.
- Add a narrow display model if useful for formatting and status mapping.

**Definitions**

```ts
export type SessionTimerState =
  | 'NotStarted'
  | 'Running'
  | 'Paused'
  | 'Expired'
  | 'Unavailable'

export type SessionTimerSnapshotDto = {
  liveSessionId: string
  totalSeconds: number
  remainingSeconds: number
  timerState: SessionTimerState
  sessionState: SessionLifecycleState | string
  serverTime: string
  updatedAt: string
}

export type SessionTimerUpdatedNotificationDto = {
  liveSessionId: string
  totalSeconds: number
  remainingSeconds: number
  timerState: SessionTimerState
  sessionState: SessionLifecycleState | string
  serverTime: string
  changedAt: string
}
```

**Gate**

- TypeScript accepts the new types.
- No UI behavior changes.

---

### Phase 2 - API Client And Server Action

**Scope**

- Add a timer snapshot fetcher to `app/lib/sessions.ts`.
- Add a Server Action wrapper in `app/actions/sessions.ts`.
- Reuse existing auth/session handling and gateway URL patterns.

**Client behavior**

- `getSessionTimerSnapshot(liveSessionId)` calls the backend timer endpoint.
- Map backend errors to stable frontend tokens:
  - `401` -> unauthorized/auth expired
  - `403` -> not assigned operator
  - `404` -> session not found
  - `409` or backend timer conflict -> timer unavailable
  - other -> unknown timer read failure

**Server Action behavior**

- Verify the current user session.
- Require `Operator` for this operator-dashboard slice.
- Return the backend-authored timer snapshot.
- Do not cache timer reads.

**Gate**

- Unit tests cover response mapping and error tokens.
- No timer math is introduced in the API client.

---

### Phase 3 - Realtime Timer Subscription

**Scope**

- Extend `app/lib/realtime/session-state-client.ts` to support timer updates in addition to the
  existing `SessionStateChanged` event.
- Normalize PascalCase and camelCase payloads, matching the current state-notification pattern.

**Implementation shape**

- Add `onTimerUpdated?: (notification: SessionTimerUpdatedNotificationDto) => void`.
- Subscribe to `SessionTimerUpdated`.
- Keep `JoinLiveSessionAsOperatorAsync` and `LeaveLiveSessionAsync` unchanged unless the backend
  publishes a dedicated timer group method.
- On reconnect, invoke the existing join method and let the dashboard fetch a fresh timer snapshot.

**Gate**

- Unit tests or focused integration tests prove notification normalization.
- Existing `SessionStateChanged` behavior is not regressed.
- Auth-expired/offline states still surface through `session-transport-status`.

---

### Phase 4 - Timer Display Component

**Scope**

- Add a small component, preferably `OperatorSessionTimerPanel`, colocated with dashboard
  components unless the team prefers keeping it inline in `DashboardClient.tsx`.
- Add focused CSS module classes in `dashboard.module.css`.

**Component props**

```ts
type OperatorSessionTimerPanelProps = {
  timer: SessionTimerSnapshotDto | null
  isLoading: boolean
  error: string | null
  lifecycleState: SessionLifecycleState
  lastSyncedAt: string | null
}
```

**Display behavior**

- Format remaining time as `MM:SS` under one hour and `H:MM:SS` over one hour.
- Clamp percentage between `0` and `100`.
- Show empty/missing timer context as `--:--` with an `Unavailable` chip.
- Show `Paused` chip and frozen bar when backend timer state is paused.
- Show `Expired` chip, `00:00`, and critical tone when remaining time is zero or timer state is
  expired.
- Use `aria-live="polite"` for the visible time value so screen readers can detect meaningful
  changes without being spammed.
- Use `role="progressbar"` with `aria-valuemin`, `aria-valuemax`, and `aria-valuenow`.

**Visual guidance**

- Timer block: same surface language as the hero, not a nested card. Use a bordered rail or
  shallow inset area inside the hero.
- Bar track: warm neutral raised surface.
- Bar fill:
  - Running / normal: ember accent
  - Paused / not started: warning or muted tone
  - Expired / near-expired: critical tone
- Time value: tabular numeric, strong but smaller than route display type.

**Gate**

- The bar and time fit on desktop and mobile without overlapping hero text or controls.
- The timer does not push lifecycle controls below the first useful viewport on common desktop
  sizes.

---

### Phase 5 - Dashboard Wiring

**Scope**

- Wire timer state into the operator live-operation branch of `DashboardClient.tsx`.
- Fetch snapshot when `selectedRealtimeSessionId` changes.
- Apply SignalR timer updates for the selected session only.
- Refresh snapshot after successful lifecycle transitions.

**State additions**

```ts
const [sessionTimer, setSessionTimer] = useState<SessionTimerSnapshotDto | null>(null)
const [sessionTimerError, setSessionTimerError] = useState<string | null>(null)
const [sessionTimerLoading, setSessionTimerLoading] = useState(false)
```

**Lifecycle integration**

- When selected session changes:
  - clear old timer state
  - load snapshot for new session
  - start realtime subscription
- On `SessionTimerUpdated`:
  - ignore updates for other sessions
  - replace timer state with backend payload
- On `SessionStateChanged`:
  - keep current state update behavior
  - refresh snapshot if the state changed to `Active`, `Paused`, `Finished`, or `Cancelled`
- On successful Start/Pause/Resume/Finish/Cancel:
  - apply transition result
  - fetch timer snapshot

**Gate**

- Opening an assigned session shows the current backend timer snapshot.
- Start moves state to Active and the timer display reflects Running when the backend sends it.
- Pause freezes the visible value and bar.
- Resume restarts from the backend frozen remainder.
- Cancel/Finish stops the timer display and uses terminal visual treatment.

---

### Phase 6 - Tests

**Unit tests**

- Format `remainingSeconds` correctly.
- Clamp progress percentage.
- Map timer states to display tones.
- Normalize SignalR timer notification payloads.
- Verify timer snapshot error mapping.

**E2E / component-level checks**

- Operator opens `My sessions`, selects an assigned session, and opens live operation.
- Timer panel is visible in the live-operation hero.
- Mocked Running update changes the visible time and bar.
- Mocked Paused update freezes the visible time and switches the state chip.
- After clicking Pause/Resume/Cancel action buttons, timer snapshot is refreshed.
- Missing timer context renders `Unavailable` instead of stale time.

**Visual checks**

- Desktop: hero title, timer bar, and lifecycle controls do not overlap.
- Mobile/narrow viewport: timer wraps into a compact vertical layout and the bar remains readable.
- Dark and light themes preserve contrast for Running, Paused, and Expired.

---

## Acceptance Criteria

- In the operator live-session view, the assigned operator sees a remaining-time value and a
  progress bar for the selected session.
- The displayed time and bar are initialized from a backend-authoritative snapshot.
- SignalR timer updates update the visible time and bar without manual reload.
- Pause freezes the displayed time and bar; Resume continues from the backend-authored remainder.
- Start, Pause, Resume, Finish, and Cancel remain lifecycle controls, and each accepted transition
  refreshes timer state.
- Reconnect restores the timer from the backend snapshot before displaying live updates again.
- Missing or expired timer context is handled explicitly and never shows stale local countdown
  state.
- The UI follows `frontend/DESIGN.md`: restrained ember accent, warm surfaces, full borders,
  tabular live data, no nested cards, no side-stripe borders, no decorative glow stack.

---

## Out Of Scope

- Participant timer UI. The HU-22 prompt already calls for that as the primary frontend slice;
  this plan is operator-dashboard specific.
- Answer submission, round orchestration, scoring, and question navigation.
- Editing timer duration from the operator dashboard.
- Creating a client-owned countdown authority.
- Admin supervision timer unless the backend explicitly allows admin timer reads.
