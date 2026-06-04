# Plan: HU-22 Option A — Operator Timer Endpoint Fix

**Ref:** HU-22
**Date:** 2026-06-04
**Scope:** Add a dedicated operator timer REST endpoint on the backend and update the frontend
plan types + client to consume the real contract.
**Depends on:** The two existing HU-22 backend commits (domain layer + application/participant layer).
**Supersedes:** The contract section and Phase 1 types of
`frontend/plans/hu-22-frontend-operator-live-session-timer.md`.

---

## Problem Statement

The HU-22 backend currently exposes the timer snapshot only through:

```
GET /api/sessions/{id}/participants/timer?teamId={teamId}
[Authorize(Roles = "Participant")]
```

The operator dashboard plan requires a timer read surface for the **Operator** role, which has no
team membership. No such endpoint exists. Additionally, the actual DTO shapes differ from what the
frontend plan assumed:

| Contract point | Plan assumed | Backend actual |
|---|---|---|
| Snapshot `timerState` enum | `NotStarted \| Running \| Paused \| Expired \| Unavailable` | `TimerStatus` string: `Advancing \| Frozen \| Expired` + `IsAdvancing`/`IsExpired` booleans |
| Snapshot timestamp field | `serverTime`, `updatedAt` | `ObservedAt`, `AdvancingSince?`, `ExpiredAt?` |
| Notification time unit | `totalSeconds`, `remainingSeconds` (number) | `TotalMilliseconds`, `RemainingMilliseconds` (long) |
| Notification timer state | unified `timerState` enum | `IsPaused: bool` + `IsExpired: bool` |
| Notification timestamp | `serverTime`, `changedAt` | `EmittedAt` |

---

## Architecture Summary

- The existing `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` already enforces
  that the calling operator is assigned to the session. Reuse it in the new handler.
- The new endpoint returns the same `SessionTimerSnapshotDto` already defined in the application
  layer, with `teamId: null` (operators have no team context).
- No new domain logic is required. The domain method
  `LiveSession.GetAuthoritativeSessionTimerSnapshot(now)` and the factory
  `SessionTimerSnapshotDtoFactory.Create` already exist and are already tested.
- The frontend type layer is the only consumer of the contract; adapting it is isolated to
  `app/lib/definitions.ts`, the API client, and the realtime client normalization function.

---

## Phases

### Phase 1 — Backend: Operator Timer Query + Handler

**Scope:** Application layer only. No endpoint yet.

**New files:**

`src/Application/Sessions/Queries/GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQuery.cs`

```csharp
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;

[Authorize(Roles = "Operator")]
public sealed record GetOperatorSessionTimerSnapshotQuery(Guid LiveSessionId)
    : IRequest<SessionTimerSnapshotDto>;
```

`src/Application/Sessions/Handlers/GetOperatorSessionTimerSnapshotQueryHandler.cs`

```csharp
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class GetOperatorSessionTimerSnapshotQueryHandler
    : IRequestHandler<GetOperatorSessionTimerSnapshotQuery, SessionTimerSnapshotDto>
{
    private readonly ISessionAdministrationAccessResolver _accessResolver;
    private readonly TimeProvider _timeProvider;

    public GetOperatorSessionTimerSnapshotQueryHandler(
        ISessionAdministrationAccessResolver accessResolver,
        TimeProvider timeProvider)
    {
        _accessResolver = accessResolver;
        _timeProvider = timeProvider;
    }

    public async Task<SessionTimerSnapshotDto> Handle(
        GetOperatorSessionTimerSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        var liveSession = await _accessResolver.GetAuthorizedSessionAsync(
            request.LiveSessionId, cancellationToken);

        var snapshot = liveSession.GetAuthoritativeSessionTimerSnapshot(
            _timeProvider.GetUtcNow());

        return SessionTimerSnapshotDtoFactory.Create(liveSession, teamId: null, snapshot);
    }
}
```

**Unit tests:**

File: `tests/Application.UnitTests/Sessions/Queries/GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQueryHandlerTests.cs`

Mirror the structure of `GetParticipantSessionTimerSnapshotQueryHandlerTests`. Required cases:

- Active session → `TimerStatus: "Advancing"`, `IsAdvancing: true`, `TeamId: null`
- Paused session → `TimerStatus: "Frozen"`, `IsAdvancing: false`
- Resumed session → `TimerStatus: "Advancing"`, `RemainingSeconds` reflects frozen remainder
- Elapsed session → `TimerStatus: "Expired"`, `RemainingSeconds: 0`, `IsExpired: true`
- Session not found → `ISessionAdministrationAccessResolver` throws `NotFoundException`; test
  verifies `NotFoundException` propagates

**Note:** `ISessionAdministrationAccessResolver.GetAuthorizedSessionAsync` already throws
`NotFoundException` for missing sessions and `ForbiddenAccessException` when the operator is not
assigned — no need to re-test those inside the handler. A single "not found propagates" case is
sufficient.

**Gate:**

- `dotnet test` on the unit test project passes.
- `TeamId` is `null` in all handler-produced DTOs.

---

### Phase 2 — Backend: REST Endpoint + Integration Tests

**Scope:** API layer wiring and integration coverage.

**Edit:** `src/Api/Endpoints/SessionsEndpoints.cs`

Add after the existing `GetParticipantTimerSnapshotAsync` registration:

```csharp
sessions.MapGet("/{liveSessionId:guid}/timer", GetOperatorTimerSnapshotAsync)
    .RequireAuthorization(AuthorizationPolicies.Operator);
```

Add the handler method inside the class:

```csharp
private static async Task<Ok<SessionTimerSnapshotDto>> GetOperatorTimerSnapshotAsync(
    Guid liveSessionId,
    ISender sender,
    CancellationToken cancellationToken)
{
    var result = await sender.Send(
        new GetOperatorSessionTimerSnapshotQuery(liveSessionId),
        cancellationToken);

    return TypedResults.Ok(result);
}
```

**Integration tests:**

File: `tests/IntegrationTests/Api/OperatorSessionTimerSnapshotEndpointTests.cs`

Mirror `ParticipantSessionTimerSnapshotEndpointTests`. Required cases:

| Case | Role header | Expected |
|---|---|---|
| Active session | Operator (assigned) | 200, `TimerStatus: "Advancing"`, `TeamId: null` |
| Paused session | Operator (assigned) | 200, `TimerStatus: "Frozen"` |
| Resumed session | Operator (assigned) | 200, `TimerStatus: "Advancing"`, remainder from frozen |
| Session not found | Operator | 404 |
| No trusted headers | — | 401 |
| Participant role | Participant | 403 |

URL pattern: `GET /api/sessions/{liveSessionId:D}/timer` (no query params).

Response model for deserialization (inline record in the test class):

```csharp
private sealed record OperatorSessionTimerSnapshotResponse(
    Guid LiveSessionId,
    Guid? TeamId,
    string SessionState,
    int TotalSeconds,
    int RemainingSeconds,
    string TimerStatus,
    bool IsAdvancing,
    bool IsExpired,
    DateTimeOffset ObservedAt,
    DateTimeOffset? AdvancingSince,
    DateTimeOffset? ExpiredAt);
```

**Gate:**

- `dotnet test` on both unit and integration projects passes.
- A `curl` against the local dev backend with an Operator JWT returns the snapshot.

---

### Phase 3 — Frontend: Correct Type Definitions

**Scope:** `app/lib/definitions.ts` only. No behavior change.

**Replace** the timer type block in the frontend plan (which was never implemented) with the actual
backend contract.

Add to `app/lib/definitions.ts`:

```ts
// Timer status values emitted by the backend.
// "Advancing" = timer counting down (session Active and not expired).
// "Frozen"    = timer not moving (session Paused, Scheduled, or Preparing).
// "Expired"   = remaining time reached zero.
export type SessionTimerStatus = 'Advancing' | 'Frozen' | 'Expired'

// Response of GET /api/sessions/{id}/timer (Operator) and
// GET /api/sessions/{id}/participants/timer (Participant).
export type SessionTimerSnapshotDto = {
  liveSessionId: string
  teamId: string | null
  sessionState: SessionLifecycleState | string
  totalSeconds: number
  remainingSeconds: number
  timerStatus: SessionTimerStatus
  isAdvancing: boolean
  isExpired: boolean
  observedAt: string
  advancingSince: string | null
  expiredAt: string | null
}

// SignalR "SessionTimerUpdated" hub event payload.
// Note: time units are milliseconds (long on the backend), not seconds.
export type SessionTimerUpdatedNotificationDto = {
  liveSessionId: string
  remainingMilliseconds: number
  isPaused: boolean
  emittedAt: string
  totalMilliseconds: number
  isExpired: boolean
  sessionState: SessionLifecycleState | string
}
```

**Also update** the frontend plan file `frontend/plans/hu-22-frontend-operator-live-session-timer.md`:

- Replace the "Expected Backend Contract" section with the actual shapes above.
- Replace the Phase 1 type block with the same types above.
- Note that `timerState` enums (`NotStarted`, `Running`, `Paused`, `Unavailable`) do not exist in
  the backend; display chip labels are derived from `timerStatus + sessionState` in the component.

**Gate:**

- `tsc --noEmit` in the frontend directory passes with no new errors.

---

### Phase 4 — Frontend: API Client + Server Action

**Scope:** `app/lib/sessions.ts` and `app/actions/sessions.ts`.

**Add to `app/lib/sessions.ts`:**

```ts
export async function getOperatorSessionTimerSnapshot(
  liveSessionId: string,
): Promise<SessionTimerSnapshotDto> {
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/timer`,
    { cache: 'no-store' },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Timer read: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Timer read: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (!response.ok) throw new IdentityError('unknown', `Timer read failed with status ${response.status}`)

  return response.json() as Promise<SessionTimerSnapshotDto>
}
```

**Add to `app/actions/sessions.ts`:**

```ts
export async function getSessionTimerSnapshotAction(
  liveSessionId: string,
): Promise<{ data: SessionTimerSnapshotDto } | { error: string }> {
  'use server'
  // Verify session and require Operator role (same pattern as transitionSessionStateAction).
  // Call getOperatorSessionTimerSnapshot(liveSessionId).
  // Map IdentityError to { error: string } so the client can handle without uncaught throws.
}
```

**Gate:**

- `tsc --noEmit` passes.
- A manual fetch against the dev backend returns the snapshot payload with camelCase field names.

---

### Phase 5 — Frontend: Realtime Timer Subscription

**Scope:** `app/lib/realtime/session-state-client.ts`.

**Changes:**

1. Extend `SessionStateClientOptions`:

```ts
type SessionStateClientOptions = {
  liveSessionId: string
  onStatusChange: (status: SessionRealtimeStatus) => void
  onStateChanged: (notification: SessionStateChangedNotificationDto) => void
  onTimerUpdated?: (notification: SessionTimerUpdatedNotificationDto) => void
  onReconnected?: () => void  // called after reconnect so dashboard can re-fetch snapshot
}
```

2. Add `normalizeTimerNotification` to handle PascalCase payloads:

```ts
function normalizeTimerNotification(
  raw: unknown,
): SessionTimerUpdatedNotificationDto {
  const n = raw as SessionTimerUpdatedNotificationDto & {
    LiveSessionId?: string
    RemainingMilliseconds?: number
    IsPaused?: boolean
    EmittedAt?: string
    TotalMilliseconds?: number
    IsExpired?: boolean
    SessionState?: string
  }
  return {
    liveSessionId: n.liveSessionId ?? n.LiveSessionId ?? '',
    remainingMilliseconds: n.remainingMilliseconds ?? n.RemainingMilliseconds ?? 0,
    isPaused: n.isPaused ?? n.IsPaused ?? false,
    emittedAt: n.emittedAt ?? n.EmittedAt ?? '',
    totalMilliseconds: n.totalMilliseconds ?? n.TotalMilliseconds ?? 0,
    isExpired: n.isExpired ?? n.IsExpired ?? false,
    sessionState: n.sessionState ?? n.SessionState ?? '',
  }
}
```

3. Subscribe inside `createSessionStateRealtimeClient`:

```ts
if (options.onTimerUpdated) {
  connection.on('SessionTimerUpdated', (raw: unknown) => {
    options.onTimerUpdated!(normalizeTimerNotification(raw))
  })
}
```

4. Call `options.onReconnected?.()` inside the existing `connection.onreconnected` callback,
   **after** re-joining the operator group, so the dashboard can re-fetch the snapshot.

**Gate:**

- Existing `SessionStateChanged` behavior is unchanged; the prior unit tests still pass.
- A smoke test confirms `SessionTimerUpdated` events arrive and the callback fires with normalized
  camelCase fields.

---

### Phase 6 — Frontend: Timer Display Component

**Scope:** New component and CSS module. No dashboard wiring yet.

**New files:**

- `app/components/dashboard/OperatorSessionTimerPanel.tsx`
- `app/components/dashboard/operatorSessionTimerPanel.module.css`

**Component props:**

```ts
type OperatorSessionTimerPanelProps = {
  timer: SessionTimerSnapshotDto | null
  isLoading: boolean
  error: string | null
}
```

**Derived display logic (no state, pure from props):**

```ts
function deriveChipLabel(timer: SessionTimerSnapshotDto): string {
  if (timer.isExpired || timer.timerStatus === 'Expired') return 'Expired'
  if (timer.timerStatus === 'Advancing') return 'Running'
  // Frozen: distinguish pre-start from paused
  const preStart = timer.sessionState === 'Scheduled' || timer.sessionState === 'Preparing'
  return preStart ? 'Not started' : 'Paused'
}

function formatRemaining(seconds: number): string {
  const s = Math.max(0, seconds)
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`
  return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`
}

function progressPercent(timer: SessionTimerSnapshotDto): number {
  if (timer.totalSeconds <= 0) return 0
  return Math.min(100, Math.max(0, (timer.remainingSeconds / timer.totalSeconds) * 100))
}
```

**Render skeleton:**

- Loading state: placeholder bar and `--:--` time value.
- Error state: `Unavailable` chip, `--:--` time, short error message.
- Null timer: same as error state.
- Normal: time value with `aria-live="polite"`, chip, progress bar with `role="progressbar"` +
  `aria-valuemin={0}` + `aria-valuemax={100}` + `aria-valuenow={progressPercent(...)}`.

**CSS tone map (class suffix on the chip and bar fill):**

| Derived label | CSS modifier |
|---|---|
| Running | `--running` (ember accent) |
| Not started / Paused | `--frozen` (warning/muted) |
| Expired | `--expired` (critical) |
| Unavailable / loading | `--unavailable` (neutral muted) |

Follow `frontend/DESIGN.md`: no nested cards, no gradient text, full border rail, tabular numbers.

**Gate:**

- Component renders all four visual states (loading, unavailable, frozen, running, expired) with no
  TypeScript errors.
- Bar percentage clamps between 0 and 100.
- Time formats correctly: `600s → "10:00"`, `3661s → "1:01:01"`, `0s → "00:00"`.

---

### Phase 7 — Frontend: Dashboard Wiring

**Scope:** `app/components/dashboard/DashboardClient.tsx` (operator branch only).

**State additions:**

```ts
const [sessionTimer, setSessionTimer] = useState<SessionTimerSnapshotDto | null>(null)
const [sessionTimerError, setSessionTimerError] = useState<string | null>(null)
const [sessionTimerLoading, setSessionTimerLoading] = useState(false)
```

**Snapshot loader** (extracted helper, called by the effect and after transitions):

```ts
async function loadTimerSnapshot(liveSessionId: string) {
  setSessionTimerLoading(true)
  setSessionTimerError(null)
  const result = await getSessionTimerSnapshotAction(liveSessionId)
  if ('error' in result) {
    setSessionTimerError(result.error)
    setSessionTimer(null)
  } else {
    setSessionTimer(result.data)
  }
  setSessionTimerLoading(false)
}
```

**Session selection effect:** when `selectedRealtimeSessionId` changes:

1. Clear `sessionTimer`, `sessionTimerError`, `sessionTimerLoading`.
2. Call `loadTimerSnapshot(selectedRealtimeSessionId)`.

**Realtime client options additions:**

```ts
onTimerUpdated: (notification) => {
  if (notification.liveSessionId !== selectedRealtimeSessionId) return
  setSessionTimer((prev) => prev === null ? null : {
    ...prev,
    remainingSeconds: Math.round(notification.remainingMilliseconds / 1000),
    totalSeconds: Math.round(notification.totalMilliseconds / 1000),
    timerStatus: notification.isExpired
      ? 'Expired'
      : notification.isPaused
        ? 'Frozen'
        : 'Advancing',
    isAdvancing: !notification.isPaused && !notification.isExpired,
    isExpired: notification.isExpired,
    sessionState: notification.sessionState,
    observedAt: notification.emittedAt,
  })
},
onReconnected: () => {
  if (selectedRealtimeSessionId) loadTimerSnapshot(selectedRealtimeSessionId)
},
```

**After each lifecycle transition** (Start, Pause, Resume, Finish, Cancel), once the transition
action resolves successfully, call `loadTimerSnapshot(selectedRealtimeSessionId)`.

**Placement in JSX:**

```tsx
<section className={styles.hero} data-testid="operator-panel">
  <div className={styles.heroHeader}>...</div>

  <OperatorSessionTimerPanel
    timer={sessionTimer}
    isLoading={sessionTimerLoading}
    error={sessionTimerError}
  />

  {liveUpdateNote && ...}
  {realtimeAuthExpired && ...}
</section>
```

**Gate:**

- Opening an assigned session fetches and displays the snapshot.
- A `SessionTimerUpdated` event updates the displayed time without a full re-fetch.
- After clicking Pause/Resume, the timer snapshot is refreshed and the chip reflects the new state.
- Missing/error timer renders `Unavailable` and never shows stale countdown text.
- Lifecycle buttons remain visible and usable when the timer panel is present.

---

## Execution Order

```
Phase 1  → Phase 2   (backend: query, then endpoint)
Phase 3  → Phase 4   (frontend types first, then client — TypeScript enforces the contract)
Phase 5             (realtime client, depends on types from Phase 3)
Phase 6             (component, depends on types from Phase 3)
Phase 7             (wiring, depends on Phases 4, 5, 6)
```

Backend phases (1–2) can be worked in parallel with frontend phases (3–6) once Phase 3 is done,
because the types are the shared contract.

---

## Files Touched

| File | Change |
|---|---|
| `src/Application/Sessions/Queries/GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQuery.cs` | New |
| `src/Application/Sessions/Handlers/GetOperatorSessionTimerSnapshotQueryHandler.cs` | New |
| `src/Api/Endpoints/SessionsEndpoints.cs` | Add endpoint + private handler method |
| `tests/Application.UnitTests/Sessions/Queries/GetOperatorSessionTimerSnapshot/GetOperatorSessionTimerSnapshotQueryHandlerTests.cs` | New |
| `tests/IntegrationTests/Api/OperatorSessionTimerSnapshotEndpointTests.cs` | New |
| `frontend/app/lib/definitions.ts` | Add `SessionTimerStatus`, `SessionTimerSnapshotDto`, `SessionTimerUpdatedNotificationDto` |
| `frontend/app/lib/sessions.ts` | Add `getOperatorSessionTimerSnapshot` |
| `frontend/app/actions/sessions.ts` | Add `getSessionTimerSnapshotAction` |
| `frontend/app/lib/realtime/session-state-client.ts` | Add `onTimerUpdated`, `onReconnected`, `normalizeTimerNotification` |
| `frontend/app/components/dashboard/OperatorSessionTimerPanel.tsx` | New |
| `frontend/app/components/dashboard/operatorSessionTimerPanel.module.css` | New |
| `frontend/app/components/dashboard/DashboardClient.tsx` | Wire timer state + component |
| `frontend/plans/hu-22-frontend-operator-live-session-timer.md` | Update contract section and Phase 1 types |
