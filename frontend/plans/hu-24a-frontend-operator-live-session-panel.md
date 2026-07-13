# HU-24A — Frontend: Operator Live Session Panel

**Ref:** HU-24A / DES-32 / DES-70
**Branch:** feature/hu-24a-operator-live-session-panel (frontend slice on the same branch, or a follow-up `-frontend` branch off it)
**Date:** 2026-07-12
**Builds on:** HU-19 operator assignment, HU-20 operator assigned-session reads, HU-21A lifecycle +
`SessionStateChanged` broadcast, HU-22 authoritative timer (`SessionTimerSnapshotDto`), HU-33A
`SubstageAdvanced`, HU-36A operator-guarded answered monitor (the closest frontend analog — an
operator-group SignalR projection + `{ data | unauthorized | error }` server action). **The HU-24A
backend is landed on this branch** (X.1–X.4 committed); this slice consumes its verified contract.

> ⚠️ **Read first:** `frontend/AGENTS.md` — "This is NOT the Next.js you know." Before writing code,
> read the relevant guides under `frontend/.next-docs/` (server-and-client-components, mutating-data,
> revalidating). Do not rely on remembered Next.js conventions.

> **Execution shape:** sequential tracer-bullets — **P1** types → **P2** client + server action →
> **P3** SignalR subscription → **P4** panel component + DashboardClient wiring → **P5** tests. Each
> phase is an end-to-end runnable slice and its own commit. This is a **hu-03-shaped small surface**
> (one GET endpoint + one SignalR method, fully verified, not blocked), so every phase is
> **code-complete**; there is no contract-only increment.

---

## Context

HU-24A is the **operator, all-teams analog of HU-23's participant team board**: a guarded live read
of **session state + every team's progress rollup**, scoped to the assigned operator. The backend is
already implemented and verified on `feature/hu-24a-operator-live-session-panel`:

- `GET /api/sessions/{liveSessionId}/operator-panel` → `OperatorSessionPanelDto` (Operator policy +
  the ownership-resolver Proxy; 403 RFC 7807 for a non-owning operator).
- SignalR `OperatorSessionPanelUpdated` on the reused operator-only group `live-session-operators:{id}`
  (the operator already joins it via `JoinLiveSessionAsOperatorAsync`) — re-projected on
  `SessionStateChangedEvent` + `SubstageAdvancedEvent`.

**The frontend already has almost every seam this needs** (verified against source):

- `app/lib/definitions.ts` — `SessionLifecycleState`, `SessionTimerSnapshotDto` (reused verbatim as
  the panel's `timer`), the flat SignalR notification DTOs.
- `app/lib/sessions.ts` — `getOperatorSessionTimerSnapshot` / `getOperatorTriviaAnsweredMonitor`
  (the exact gateway-path + auth-mapping shape this slice mirrors); `API_GATEWAY_URL`,
  `getGatewayHeaders()` (Bearer via `getValidAccessToken`), `verifySession`.
- `app/actions/sessions.ts` — `getSessionTimerSnapshotAction` (Operator-gated) and
  `getTriviaAnsweredMonitorAction` (the `{ data | unauthorized | error }` discriminated result this
  slice copies).
- `app/lib/realtime/session-state-client.ts` — the SignalR client: optional `on*` callbacks,
  per-event normalizers, `JoinLiveSessionAsOperatorAsync` on start + reconnect, `withAutomaticReconnect`.
- `app/dashboard/DashboardClient.tsx` — the operator live-operation branch
  (`role === 'operator' && selectedOperatorSession && selectedOperatorState`, hero
  `data-testid="operator-panel"`), the `timerReducer` / `answeredMonitorReducer` pattern,
  `loadTimerSnapshot` / `loadAnsweredMonitor`, the realtime effect, the session-select reset effect,
  and the render site where `OperatorSessionTimerPanel` + `AnsweredMonitorPanel` nest (~L1081–1102).

This slice adds a **new panel component**, one client fn, one server action, one SignalR subscription,
and the DashboardClient wiring to feed it — mirroring the HU-36A answered-monitor path end to end.

**Explicitly not in this slice** (HU-24B / DES-33): ranking, events, evidence, clue-as-progress, any
score ledger/ranking/winner. Progress is **target-based** (`resolvedTargets / totalActiveTargets`);
score is **`Team.CurrentScore ?? 0`** (already defaulted server-side).

---

## Verified Backend Contract

Anchored to the landed backend on `feature/hu-24a-operator-live-session-panel`
(`SessionsController.cs` `[HttpGet("{liveSessionId:guid}/operator-panel")]`,
`OperatorSessionPanelDto.cs`, `OperatorSessionPanelDtoFactory.cs`,
`SignalROperatorSessionPanelBroadcaster.cs`, `BroadcastOperatorSessionPanelNotificationHandler.cs`).
HTTP responses and the SignalR JSON hub protocol both serialize **camelCase** (ASP.NET default; no
`AddJsonProtocol` override in the service). Both the GET and the push carry the **same DTO**.

| Endpoint / event | Auth | Shape | Errors |
|---|---|---|---|
| `GET /api/sessions/{liveSessionId}/operator-panel` | `[Authorize(Policy=Operator)]` **+** ownership-resolver Proxy | `OperatorSessionPanelDto` | `401` auth · **`403` non-owning operator (RFC 7807)** · `404` session not found |
| SignalR `OperatorSessionPanelUpdated` on group `live-session-operators:{id}` | joined via `JoinLiveSessionAsOperatorAsync` (ownership-guarded at join) | `OperatorSessionPanelDto` | — (never reaches participants) |

Push triggers: `SessionStateChangedEvent` **and** `SubstageAdvancedEvent` (the handler re-loads
read-only and re-projects; it never mutates state). No new invoke, no new group.

**`OperatorSessionPanelDto`** (backend record → camelCase JSON):

```ts
OperatorSessionPanelDto {
  liveSessionId: string           // Guid
  state: string                   // SessionState.ToString(): "Scheduled"|"Preparing"|"Active"|"Paused"|"Finished"|"Cancelled"
  timer: SessionTimerSnapshotDto  // session-scoped (built with teamId: null) — the SAME type the frontend already has
  teamProgress: OperatorTeamProgressDto[]   // ordered by teamCode (backend Ordinal sort — HU-23 parity)
}

OperatorTeamProgressDto {
  teamId: string                  // Guid, runtime team id
  teamCode: string
  displayName: string
  score: number                   // int — Team.CurrentScore ?? 0 (session-owned or zero)
  activeSubstage: ActiveSubstageContextDto | null   // null when there is no active substage
}

ActiveSubstageContextDto {
  substageSnapshotId: string      // Guid
  playMode: 'TreasureHunt' | 'Trivia' | string
  title: string
  totalActiveTargets: number      // treasure-hunt: active TargetSnapshots in the active substage
  resolvedTargets: number         // 0 until HU-31 lands per-team target resolution (never inferred from clues)
  activeQuestionSequenceOrder: number | null      // trivia only (1-based); null for treasure-hunt / no question
  activeQuestionTimeLimitSeconds: number | null
}
```

**Contract notes that drive the render (do not misread these):**
- The `activeSubstage` context is **session-scoped today**, not per-team: `resolvedTargets` is a hard
  `0` and the trivia question index is session-global, so it is **identical across every team**. The
  per-team list shape is HU-31 forward-compat, not present per-team divergence. Only `score` and team
  identity genuinely differ per team now.
- `timer` is the **session** timer snapshot (`teamId: null`) — same `SessionTimerSnapshotDtoFactory`
  that feeds the existing `/timer` endpoint, so it converges with the existing operator timer view.
- `resolvedTargets / totalActiveTargets` is the **only** progress measure; clues are never progress.

---

## Architecture Decisions

1. **Mirror the HU-36A answered-monitor path exactly.** This slice is the same shape as
   `getOperatorTriviaAnsweredMonitor` → `getTriviaAnsweredMonitorAction` → `AnsweredMonitorPanel`
   nested in the operator hero, plus the operator-group SignalR subscription. Reusing that path
   (naming, error mapping, discriminated result, reducer, render site) keeps the diff small and the
   patterns house-consistent. It differs only in the payload (all-teams progress instead of an
   answered roster) and the SignalR method name.
2. **Server-action result is a discriminated union — `{ data } | { unauthorized } | { error }`.** Copy
   `getTriviaAnsweredMonitorAction` (minus its `noActiveQuestion` case). A `401/403`/non-operator maps
   to `unauthorized` (renders the not-authorized state, satisfying AC "non-owned session → 403"); a
   `404`/transient/unexpected failure maps to `error` (retryable) so a momentary blip never tells a
   legitimately-assigned operator they are "not authorized."
3. **New component `OperatorTeamProgressPanel`, nested in the existing operator hero.** It renders the
   panel's `state` (a panel-owned live state readout) + the ordered per-team rollup (score,
   target progress or active-question order). It nests inside the existing
   `data-testid="operator-panel"` hero alongside `OperatorSessionTimerPanel` and
   `AnsweredMonitorPanel`. Name chosen distinct from the pre-existing `SessionOperatorPanel.tsx`
   (admin operator-assignment) to avoid import confusion.
4. **The countdown stays on the existing timer path; the panel does not render a second timer.** The
   panel DTO's `timer` field is typed but the visible countdown continues to be driven by the existing
   `getSessionTimerSnapshotAction` + `SessionTimerUpdated` wiring feeding `OperatorSessionTimerPanel`
   (HU-22 semantics). They converge (same backend factory), so surfacing a second countdown would be
   redundant. The per-team rollup shows target progress / active-question order, not a per-team clock.
5. **Reuse the operator group + existing join — no new invoke, no new group.** The subscription is a
   new `connection.on('OperatorSessionPanelUpdated', …)` handler on the client that already joins
   `live-session-operators:{id}` via `JoinLiveSessionAsOperatorAsync` (on start and on reconnect). The
   hub-join ownership guard is the subscription's authorization boundary — participants and non-owning
   operators never receive the push.
6. **Snapshot-first, SignalR-second, reconnect-reload.** On session select, fetch the panel snapshot
   and start the subscription; `OperatorSessionPanelUpdated` replaces panel state wholesale (it is a
   full re-projection, not a delta); `onReconnected` re-fetches the snapshot before trusting pushes —
   identical to the timer / answered-monitor lifecycle already in `DashboardClient`.
7. **No progress model invented on the client.** `resolvedTargets / totalActiveTargets` and
   `score` are rendered verbatim from the DTO. No ranking, no ledger, no clue-as-progress, no winner.

---

## Environment

**No new environment variables.** Reuses (verified against source):
- `API_GATEWAY_URL` (server-side, `app/lib/sessions.ts:19` — `process.env.API_GATEWAY_URL!`) for the
  operator-panel REST read, via `getGatewayHeaders()` (Bearer token from `getValidAccessToken()`).
- `NEXT_PUBLIC_API_GATEWAY_URL` (client-side, `session-state-client.ts:41`) for the SignalR hub URL
  (`/hubs/sessions`) + `/api/realtime/hub-token` for the access token.

`getGatewayHeaders` is **private to `sessions.ts`** (not exported) — the new client fn lives in the
**same file**, so it calls it directly (no import, no replication needed).

---

## data-testid contract

Single source of truth for selectors; the P5 e2e/unit tests and the AC→test map key off these. The
existing operator hero keeps `data-testid="operator-panel"`; the new panel nests inside it. (Named
`operator-session-panel` — distinct from the hero's `operator-panel`.)

| testid | Element | Phase |
|---|---|---|
| `operator-session-panel` | `OperatorTeamProgressPanel` root section | P4 |
| `panel-session-state` | panel-owned live `SessionState` readout | P4 |
| `panel-unauthorized` | not-authorized (403/401/non-operator) state | P4 |
| `panel-error` | transient/unexpected read-failure state | P4 |
| `panel-no-teams` | panel mounted, no teams associated yet | P4 |
| `team-progress-{teamId}` | one team rollup row | P4 |
| `team-progress-score-{teamId}` | that team's score cell | P4 |
| `team-progress-targets-{teamId}` | `resolvedTargets/totalActiveTargets` cell (treasure-hunt / no question) | P4 |
| `team-progress-question-{teamId}` | active-question order cell (trivia) | P4 |

---

## Phase 1 — Types (`app/lib/definitions.ts`)

**Scope:** add the three response DTOs. `SessionLifecycleState` and `SessionTimerSnapshotDto` already
exist (reused). No behaviour change; downstream phases consume these.

```ts
// --- HU-24A operator live session panel (all-teams progress rollup) ---
// Response of GET /api/sessions/{id}/operator-panel (Operator) AND the SignalR
// "OperatorSessionPanelUpdated" push on live-session-operators:{id} — same DTO for both.
// Progress is target-based (resolvedTargets/totalActiveTargets), score is session-owned-or-zero.
// No ranking / events / evidence / clue-as-progress (those are HU-24B).
export type OperatorActiveSubstageContextDto = {
  substageSnapshotId: string
  playMode: 'TreasureHunt' | 'Trivia' | string
  title: string
  totalActiveTargets: number
  resolvedTargets: number                          // 0 until HU-31 lands per-team target resolution
  activeQuestionSequenceOrder: number | null       // trivia only (1-based); null otherwise
  activeQuestionTimeLimitSeconds: number | null
}

export type OperatorTeamProgressDto = {
  teamId: string                                   // runtime team id
  teamCode: string
  displayName: string
  score: number                                    // Team.CurrentScore ?? 0
  activeSubstage: OperatorActiveSubstageContextDto | null
}

export type OperatorSessionPanelDto = {
  liveSessionId: string
  state: SessionLifecycleState | string            // current lifecycle state
  timer: SessionTimerSnapshotDto                   // session-scoped (teamId null); HU-22 semantics
  teamProgress: OperatorTeamProgressDto[]          // ordered by teamCode (backend Ordinal sort)
}
```

**Gate:** `pnpm build` / typecheck passes. No runtime change.

---

## Phase 2 — API client + Server Action

**Scope:** add `getOperatorSessionPanel` to `app/lib/sessions.ts` and `getOperatorSessionPanelAction`
to `app/actions/sessions.ts`. Both mirror the existing operator-panel-style reads verbatim.

### `app/lib/sessions.ts` — add (mirrors `getOperatorTriviaAnsweredMonitor`, `getOperatorSessionTimerSnapshot`)

Add `OperatorSessionPanelDto` to the `./definitions` type import, then:

```ts
// HU-24A operator live session panel snapshot. Mirrors getOperatorSessionTimerSnapshot's gateway
// path + auth/status mapping. 403 = a non-owning operator: the ownership Proxy denies the read.
export async function getOperatorSessionPanel(
  liveSessionId: string,
): Promise<OperatorSessionPanelDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/operator-panel`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Operator panel: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator panel: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (!response.ok) throw new IdentityError('unknown', `Operator panel read failed with status ${response.status}`)

  return response.json() as Promise<OperatorSessionPanelDto>
}
```

### `app/actions/sessions.ts` — add (mirrors `getTriviaAnsweredMonitorAction`, without `noActiveQuestion`)

Add the aliased lib import (`getOperatorSessionPanel as getOperatorSessionPanelLib`) and the
`OperatorSessionPanelDto` type import, then:

```ts
// HU-24A operator live session panel. Three outcomes the panel renders distinctly:
//   { data }          → session state + ordered per-team progress rollup
//   { unauthorized }  → 403 non-owner / 401 auth expired / non-operator → not-authorized state
//   { error }         → 404 / unexpected / transient backend failure → error state; never throws
// Genuine auth failures are kept separate from transient ones so a momentary 5xx/network blip does
// not tell a legitimately-assigned operator they are "not authorized".
export async function getOperatorSessionPanelAction(
  liveSessionId: string,
): Promise<
  | { data: OperatorSessionPanelDto }
  | { unauthorized: true }
  | { error: string }
> {
  'use server'
  const session = await verifySession()
  if (session.role !== 'Operator') return { unauthorized: true }
  try {
    const data = await getOperatorSessionPanelLib(liveSessionId)
    return { data }
  } catch (error) {
    if (error instanceof IdentityError) {
      if (error.code === 'unauthorized') return { unauthorized: true }
      return { error: error.message }
    }
    return { error: 'Unexpected error fetching operator panel' }
  }
}
```

**Gate:** `pnpm build` passes. Calling the action from a non-Operator session returns `{ unauthorized }`
without reaching the gateway; a 403 from the gateway maps to `{ unauthorized }`, a 404 to `{ error }`.

---

## Phase 3 — SignalR subscription (`app/lib/realtime/session-state-client.ts`)

**Scope:** add an optional `onOperatorPanel` callback + an `OperatorSessionPanelUpdated` subscription +
a `normalizeOperatorPanel` normalizer. No change to join/leave/reconnect (the operator already joins
`live-session-operators:{id}` via `JoinLiveSessionAsOperatorAsync` on start and reconnect).

Add `OperatorSessionPanelDto` to the `@/app/lib/definitions` type import. Extend the options type:

```ts
type SessionStateClientOptions = {
  // …existing callbacks…
  onOperatorPanel?: (panel: OperatorSessionPanelDto) => void
}
```

Normalizer — SignalR's default JSON hub protocol is camelCase (no `AddJsonProtocol` override in the
service), but keep the same defensive `camel ?? Pascal ?? default` idiom the sibling normalizers use,
extended to the nested `teamProgress` / `activeSubstage`:

```ts
function normalizeOperatorPanel(raw: unknown): OperatorSessionPanelDto {
  const p = raw as Record<string, any>
  const teams: any[] = p.teamProgress ?? p.TeamProgress ?? []
  return {
    liveSessionId: p.liveSessionId ?? p.LiveSessionId ?? '',
    state: p.state ?? p.State ?? '',
    // The panel's countdown is driven by the dedicated SessionTimerUpdated path (HU-22); this timer
    // field is passed through typed but not re-rendered as a second clock (Architecture Decision 4).
    timer: (p.timer ?? p.Timer) as OperatorSessionPanelDto['timer'],
    teamProgress: teams.map((t) => {
      const sub = t.activeSubstage ?? t.ActiveSubstage ?? null
      return {
        teamId: t.teamId ?? t.TeamId ?? '',
        teamCode: t.teamCode ?? t.TeamCode ?? '',
        displayName: t.displayName ?? t.DisplayName ?? '',
        score: t.score ?? t.Score ?? 0,
        activeSubstage:
          sub === null
            ? null
            : {
                substageSnapshotId: sub.substageSnapshotId ?? sub.SubstageSnapshotId ?? '',
                playMode: sub.playMode ?? sub.PlayMode ?? '',
                title: sub.title ?? sub.Title ?? '',
                totalActiveTargets: sub.totalActiveTargets ?? sub.TotalActiveTargets ?? 0,
                resolvedTargets: sub.resolvedTargets ?? sub.ResolvedTargets ?? 0,
                activeQuestionSequenceOrder:
                  sub.activeQuestionSequenceOrder ?? sub.ActiveQuestionSequenceOrder ?? null,
                activeQuestionTimeLimitSeconds:
                  sub.activeQuestionTimeLimitSeconds ?? sub.ActiveQuestionTimeLimitSeconds ?? null,
              },
      }
    }),
  }
}
```

Subscribe inside `createSessionStateRealtimeClient`, alongside the existing optional subscriptions
(mirror the `onTeamAnswered` block — also an operator-group event) and destructure `onOperatorPanel`
in the options:

```ts
if (onOperatorPanel) {
  connection.on('OperatorSessionPanelUpdated', (raw: unknown) => {
    onOperatorPanel(normalizeOperatorPanel(raw))
  })
}
```

**Gate:** `pnpm build` passes. Unit test proves `OperatorSessionPanelUpdated` invokes `onOperatorPanel`
with a normalized payload (camelCase and PascalCase inputs both yield the same object; missing
`teamProgress` → `[]`). Existing `SessionStateChanged` / timer / answered subscriptions unregressed.

---

## Phase 4 — Panel component + DashboardClient wiring

**Scope:** add `OperatorTeamProgressPanel.tsx` (+ a CSS module) and wire panel state into the operator
live-operation branch of `DashboardClient.tsx` (reducer + loader + realtime subscription + reset/reconnect + render).

### `app/dashboard/OperatorTeamProgressPanel.tsx` (new) — mirrors `AnsweredMonitorPanel` states

```tsx
import type { OperatorSessionPanelDto, OperatorTeamProgressDto } from '@/app/lib/definitions'
import styles from './operatorTeamProgressPanel.module.css'

type OperatorTeamProgressPanelProps = {
  panel: OperatorSessionPanelDto | null
  unauthorized: boolean            // true ⇒ not-authorized state, no team data
  error: string | null             // non-null ⇒ transient/unexpected read failure (not an auth problem)
  loading: boolean
}

// Target-based progress for treasure-hunt / no-question; active-question order for trivia. Never
// clue-based. Score is rendered verbatim (session-owned or zero) — no ranking, no ledger (HU-24B).
function ProgressCell({ team }: { team: OperatorTeamProgressDto }) {
  const sub = team.activeSubstage
  if (sub && sub.playMode === 'Trivia' && sub.activeQuestionSequenceOrder !== null) {
    return (
      <span className={styles.progress} data-testid={`team-progress-question-${team.teamId}`}>
        Question {sub.activeQuestionSequenceOrder}
      </span>
    )
  }
  const resolved = sub?.resolvedTargets ?? 0
  const total = sub?.totalActiveTargets ?? 0
  return (
    <span className={styles.progress} data-testid={`team-progress-targets-${team.teamId}`}>
      {resolved}/{total} targets
    </span>
  )
}

export function OperatorTeamProgressPanel({
  panel,
  unauthorized,
  error,
  loading,
}: OperatorTeamProgressPanelProps) {
  if (unauthorized) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Session panel</div>
        <p className={styles.stateNote} role="status" data-testid="panel-unauthorized">
          You are not authorized to monitor this session.
        </p>
      </section>
    )
  }

  // A transient read failure is NOT an authorization problem — surface it as retryable, never as
  // "not authorized" (which would misinform a legitimately-assigned operator).
  if (error !== null) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Session panel</div>
        <p className={styles.stateNote} role="status" data-testid="panel-error">
          Couldn’t load session progress. It will refresh automatically.
        </p>
      </section>
    )
  }

  if (panel === null) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Session panel</div>
        <p className={styles.stateNote} data-testid="panel-no-teams">
          {loading ? 'Loading session progress…' : 'No progress data yet.'}
        </p>
      </section>
    )
  }

  return (
    <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
      <div className={styles.header}>
        <span className={styles.eyebrow} id="operator-session-panel-title">Session panel</span>
        <span className={styles.state} data-testid="panel-session-state" aria-live="polite">
          {panel.state}
        </span>
      </div>
      {panel.teamProgress.length === 0 ? (
        <p className={styles.stateNote} data-testid="panel-no-teams">No teams associated yet.</p>
      ) : (
        <ul className={styles.list}>
          {panel.teamProgress.map((team) => (
            <li key={team.teamId} className={styles.row} data-testid={`team-progress-${team.teamId}`}>
              <span className={styles.teamName}>{team.displayName}</span>
              <span className={styles.teamCode}>{team.teamCode}</span>
              <span className={styles.score} data-testid={`team-progress-score-${team.teamId}`}>
                {team.score} pts
              </span>
              <ProgressCell team={team} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
```

**CSS:** add `app/dashboard/operatorTeamProgressPanel.module.css` mirroring
`answeredMonitorPanel.module.css` (`.panel`, `.eyebrow`, `.header`, `.state`, `.stateNote`, `.list`,
`.row`, `.teamName`, `.teamCode`, `.score`, `.progress`) — same warm-surface panel language per
`frontend/DESIGN.md`; no new palette, no nested cards.

### `app/dashboard/DashboardClient.tsx` — wiring (all anchors verified against source)

Mirror the `answeredMonitor` machinery already in the file.

1. **Imports.** Add `getOperatorSessionPanelAction` to the `@/app/actions/sessions` import (L7); add
   `import { OperatorTeamProgressPanel } from './OperatorTeamProgressPanel'`; add
   `OperatorSessionPanelDto` to the `@/app/lib/definitions` type import block (L18-28).

2. **Reducer + state** (mirror `timerReducer`/`answeredMonitorReducer`, ~L229–327). Add:

```tsx
interface OperatorPanelState {
  loading: boolean
  unauthorized: boolean
  error: string | null
  panel: OperatorSessionPanelDto | null
}
const emptyOperatorPanel: OperatorPanelState = { loading: false, unauthorized: false, error: null, panel: null }

type OperatorPanelAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'loaded'; data: OperatorSessionPanelDto }   // from snapshot fetch OR SignalR push (full re-projection)
  | { type: 'unauthorized' }
  | { type: 'failed'; error: string }

function operatorPanelReducer(state: OperatorPanelState, action: OperatorPanelAction): OperatorPanelState {
  switch (action.type) {
    case 'reset': return emptyOperatorPanel
    case 'load': return { ...state, loading: true, unauthorized: false, error: null }
    case 'loaded': return { loading: false, unauthorized: false, error: null, panel: action.data }
    case 'unauthorized': return { ...emptyOperatorPanel, unauthorized: true }
    case 'failed': return { ...state, loading: false, error: action.error }
  }
}
```

   Then, beside the existing reducers (~L371-372):
   `const [operatorPanelState, dispatchOperatorPanel] = useReducer(operatorPanelReducer, emptyOperatorPanel)`.

3. **Loader** (mirror `loadAnsweredMonitor`, L473-487). Add after it:

```tsx
const loadOperatorPanel = useCallback(async (liveSessionId: string) => {
  dispatchOperatorPanel({ type: 'load' })
  const result = await getOperatorSessionPanelAction(liveSessionId)
  // Drop a late response for a session the operator has since switched away from.
  if (selectedRealtimeSessionIdRef.current !== liveSessionId) return
  if ('data' in result) dispatchOperatorPanel({ type: 'loaded', data: result.data })
  else if ('unauthorized' in result) dispatchOperatorPanel({ type: 'unauthorized' })
  else dispatchOperatorPanel({ type: 'failed', error: result.error })
}, [])
```

4. **Realtime subscription** — inside the `createSessionStateRealtimeClient({ … })` options (the effect
   at L489-643), add the callback (mirrors `onTeamAnswered`; the push is a full re-projection so it
   replaces panel state wholesale):

```tsx
onOperatorPanel: (panel) => {
  if (panel.liveSessionId !== selectedRealtimeSessionId) return
  dispatchOperatorPanel({ type: 'loaded', data: panel })
},
```

   In `onReconnected` (L620-625) add `void loadOperatorPanel(selectedRealtimeSessionId)`; add
   `loadOperatorPanel` to that effect's dependency array (L633-643).

5. **Session-select reset effect** (L645-654): add `dispatchOperatorPanel({ type: 'reset' })` beside the
   timer/monitor resets, and `void loadOperatorPanel(selectedRealtimeSessionId)` beside the other
   initial loads; add `loadOperatorPanel` to its deps.

6. **Render.** Nest the panel in the operator hero right after `OperatorSessionTimerPanel` (L1081-1085,
   inside `data-testid="operator-panel"`):

```tsx
<OperatorTeamProgressPanel
  panel={operatorPanelState.panel}
  unauthorized={operatorPanelState.unauthorized}
  error={operatorPanelState.error}
  loading={operatorPanelState.loading}
/>
```

**Gate:** `pnpm build` + typecheck pass. Opening an assigned session mounts `operator-session-panel`
with the ordered team rows (score `0 pts`, `0/N targets`) and a `panel-session-state` readout; a
`SessionStateChanged` / `SubstageAdvanced` transition updates the panel via `OperatorSessionPanelUpdated`
**without manual reload**; a non-owning operator sees `panel-unauthorized`; no ranking/events/evidence
copy renders.

---

## Phase 5 — Tests

**Scope:** unit coverage for the client, action-shaped result, SignalR normalizer, and panel render;
one live e2e mirroring `session-operator-timer.spec.ts` / `session-answered-monitor.spec.ts`.

### Unit

- **`tests/unit/app/lib/sessions.test.ts`** — `getOperatorSessionPanel`: `200` → parsed DTO;
  `403`/`401` → `IdentityError('unauthorized', …)`; `404` → `IdentityError('unknown','Session not found')`;
  other `!ok` → `IdentityError('unknown', …)`.
- **`tests/unit/app/lib/realtime/session-state-client.test.ts`** — an `OperatorSessionPanelUpdated`
  emission invokes `onOperatorPanel` with the normalized object; assert camelCase **and** PascalCase
  inputs normalize identically and a missing `teamProgress` coalesces to `[]`.
- **`tests/unit/app/dashboard/operator-team-progress-panel.test.ts`** (new, mirror
  `operator-session-timer-panel.test.ts`) — render states: `unauthorized` → `panel-unauthorized`;
  `error` → `panel-error`; `panel === null` → `panel-no-teams`; a treasure-hunt team →
  `team-progress-targets-{id}` shows `resolvedTargets/totalActiveTargets`; a trivia team with an
  active question → `team-progress-question-{id}`; `score: 0` → `0 pts`; `state` → `panel-session-state`.

### E2E — `tests/e2e/session-operator-panel.spec.ts` (mirror `session-operator-timer.spec.ts` header + seed)

Drive a real create → assign → team → `Active` session through the gateway (reuse that spec's
`token`/`api`/`sql` helpers and the sub-keyed admin-identity `beforeAll`), then:

```ts
// P4/P5 — happy path + live push (names only; bodies land in P5)
test('operator opens the session panel: state readout + per-team progress rollup render', …)  // AC2, AC3
test('a session-state transition updates the panel state without manual reload', …)            // AC2, AC5 (OperatorSessionPanelUpdated push)
```

Non-owner 403 is asserted at the unit/action layer (`getOperatorSessionPanel` `403` → `IdentityError`
→ action `{ unauthorized }` → `panel-unauthorized`), matching how the timer/answered specs keep
non-owner coverage off the live UI (a second sub-keyed operator against the live stack is flaky).

**Gate:** `pnpm test` (vitest) + `pnpm exec playwright test` pass; `pnpm build` / typecheck clean.

---

## Acceptance-criteria → test mapping

DES-32 acceptance criteria (`hu24a-brief.md` §Acceptance criteria).

| # | Acceptance criterion | Covered by | Phase |
|---|---|---|---|
| AC1 | Operator sees only sessions assigned/authorized to them (non-owned → 403) | `getOperatorSessionPanel` `403` → `IdentityError('unauthorized')`; action `{ unauthorized }`; `panel-unauthorized` render | P2 / P4 / P5 unit |
| AC2 | Panel reflects session-state changes without manual reload | `OperatorSessionPanelUpdated` → `onOperatorPanel` → `panel-session-state`; e2e "transition updates the panel"; client unit | P3 / P4 / P5 |
| AC3 | Operator monitors team progress in real time | per-team rollup rows (score / target progress); push on `SubstageAdvanced`; panel unit + e2e | P4 / P5 |
| AC4 | System blocks access/subscription to unauthorized sessions | Proxy denies read (403 → `unauthorized`); hub-join ownership guard (`JoinLiveSessionAsOperatorAsync`, reused) keeps the push off non-owners | P2 / P3 |
| AC5 | Panel reflects state + progress in real time via SignalR, no manual reload | snapshot-first + `OperatorSessionPanelUpdated` wholesale replace + reconnect reload | P3 / P4 / P5 |

---

## Open Questions / Dependencies

- **Dependency — backend running through the gateway.** The HU-24A backend is committed on
  `feature/hu-24a-operator-live-session-panel` (X.1–X.4). The P5 e2e needs the service + api-gateway
  rebuilt and up (`docker compose build session-operations-service api-gateway`) so the real
  `/operator-panel` endpoint and `OperatorSessionPanelUpdated` push are live. Unit phases do not.
- **SignalR wire casing (resolved, kept defensive).** The service registers no `AddJsonProtocol`
  override, so the JSON hub protocol serializes **camelCase** (ASP.NET default) — matching the DTO
  types. `normalizeOperatorPanel` keeps the sibling normalizers' `camel ?? Pascal` fallbacks anyway,
  so a future protocol change cannot break it.
- **Timer source (resolved by Architecture Decision 4).** The panel DTO carries a session-scoped
  `timer`, but the visible countdown stays on the existing `/timer` + `SessionTimerUpdated` path
  (HU-22), which converges (same `SessionTimerSnapshotDtoFactory`). If product later wants the panel to
  own the countdown, swap the timer source into `OperatorTeamProgressPanel` — low-risk, isolated.
- **`resolvedTargets` is `0` until HU-31.** The backend hardcodes it; the panel shows
  `0/totalActiveTargets`. This is expected, not a defect — do not infer progress from clues.
- **Non-owner 403 is unit/action-tested, not live-e2e-tested** (see P5 rationale) — a second sub-keyed
  operator against the live stack is flaky, mirroring the timer/answered specs' choice.

## Out of Scope

- **HU-24B (DES-33): ranking, events, evidence, clue-as-progress, `SessionTeamWinner`.** Not rendered,
  not typed here.
- Any score ledger / ranking / penalties / winner computation (`ScoringMonitoring`, HU-37/HU-39).
- Any backend change — the contract is landed on the feature branch; this slice is frontend-only.
- A new SignalR group or invoke — the operator group `live-session-operators:{id}` and
  `JoinLiveSessionAsOperatorAsync` are reused; only a new `connection.on` handler is added.
- A second/duplicate countdown in the new panel (Architecture Decision 4).
- Participant-facing surfaces — this is the operator web surface only.
- Re-guarding existing mutation endpoints (`PATCH …/state`, `POST …/teams`) — their own HUs.

---

## Commit Sequence

```
feat(frontend): phase 1 — operator session panel types — HU-24A
feat(frontend): phase 2 — operator panel client + server action — HU-24A
feat(frontend): phase 3 — OperatorSessionPanelUpdated SignalR subscription — HU-24A
feat(frontend): phase 4 — operator team-progress panel + dashboard wiring — HU-24A
test(frontend): phase 5 — operator live session panel unit + e2e — HU-24A

Ref: HU-24A
Ref: DES-32
Ref: DES-70
```
