# Plan: HU-21A Frontend — Session State-Machine Controls (cycle 2 verify/cleanup)

**Ref:** HU-21A
**Branch:** feature/hu-21a-session-state-machine-realign
**Date:** 2026-07-05
**Builds on:** HU-19 (operator assignment), HU-22 (operator live-session timer). The operator
session-control UI, the `PATCH .../state` client + server action, the SignalR
`SessionStateChanged` subscription, and the ProblemDetails error surface were all delivered in
cycle 1. This slice **verifies** them against the unchanged backend contract and **removes the
one residual non-canonical state vocabulary** still living in the admin overview.

---

## Context

The backend contract is unchanged from cycle 1:

- `PATCH /api/sessions/{liveSessionId}/state` — body `TransitionSessionStateRequest { targetState, reason? }`,
  Operator-only, `200` with `{ previousState, currentState, ... }`, `ProblemDetails` on an invalid
  target/edge. Valid transitions broadcast `SessionStateChanged` over the `SessionsHub`
  `live-session:{id}` group.

Because the frontend was already built for this contract, most of this plan is an **audit that the
existing code is correct**, plus a single concrete cleanup. There is no new endpoint, no new type
to add, and no new component. This is why the slice is modelled on the small hu-03 exemplar.

The canonical six states and their allowed transitions:

| From | Allowed targets |
|------|-----------------|
| `Scheduled` | `Preparing`, `Cancelled` |
| `Preparing` | `Active`, `Cancelled` |
| `Active` | `Paused`, `Finished`, `Cancelled` |
| `Paused` | `Active`, `Finished`, `Cancelled` |
| `Finished` | — (terminal) |
| `Cancelled` | — (terminal) |

---

## Audit — already implemented and verified against source

Every scope item except the cleanup is **already satisfied**. Verified anchors:

| Scope requirement | Where it lives | Status |
|---|---|---|
| Canonical type `SessionLifecycleState` = the six states | `app/lib/definitions.ts:260-266` | ✅ correct |
| Request/result DTOs match contract | `app/lib/definitions.ts:268-286` | ✅ correct |
| `transitionSessionState` client — `PATCH .../state`, `{targetState, reason?}` | `app/lib/sessions.ts:121-158` | ✅ correct |
| Operator-only server action | `app/actions/sessions.ts:78-86` | ✅ correct |
| **Exactly the canonical transitions per state**, terminal = `[]` | `app/dashboard/DashboardClient.tsx:164-194` (`lifecycleActions`) | ✅ matches table above exactly |
| Action buttons render only the allowed targets (`session-action-{targetState}`) | `DashboardClient.tsx:917-935` | ✅ correct |
| Terminal empty-state copy (`session-no-actions`) | `DashboardClient.tsx:937-941` | ✅ correct |
| Destructive-transition confirm + optional cancellation `reason` | `DashboardClient.tsx:943-975` | ✅ correct |
| **Subscribe `SessionStateChanged` and reflect state live** | `app/lib/realtime/session-state-client.ts:169-171` + `DashboardClient.tsx:388-409` | ✅ merges `currentState` into the session, sets a live note |
| **Surface ProblemDetails reason on rejection** — 409 `type` → cause-specific message; `invalid_transition` fallback | `sessions.ts:140-152` (map) + `DashboardClient.tsx:502-515` (`mapTransitionError`) + `911-915` (`session-transition-error` banner) | ✅ correct |
| No `SessionMode` in source | grep of `app/**` — 0 hits (only the HU-16 guard tests reference the word to forbid it) | ✅ already clean |

**Conclusion:** nothing in the transition/subscription/error surface needs to be written. It needs a
runnable check (Phase 2) and a live end-to-end verification (Phase 3).

---

## The one real gap — residual non-canonical state vocabulary

`DashboardClient.tsx` still carries a **parallel, non-canonical** session-state model used only by
the admin overview's fabricated demo cards:

- `type SessionState = 'live' | 'paused' | 'draft'` — `DashboardClient.tsx:31`
- `Session.state: SessionState` — `:46`
- hardcoded `sessions: Session[]` with `state: 'live' | 'paused' | 'draft'` — `:75-124`
- `stateLabels: Record<SessionState, string>` rendering "Live" / "Draft" / "Paused" — `:140-144`
- `adminMetrics` hint copy `"1 live, 1 paused"` — `:135`
- usages: `:568`, `:696`, `:703`, `:1101`, `:1115-1116`

`'live'` and `'draft'` are **not** among the canonical six. Per the gate ("no UI type/copy
introduces or retains … a non-canonical state") this must be removed or rewritten. This is
fabricated placeholder data for an admin overview that is not yet backed by real sessions — see
Open Questions for the delete-vs-rewrite call.

---

## Architecture Decisions

- **Rewrite, don't delete.** The admin overview cards are cosmetic demo scaffolding, but deleting
  the whole "Live sessions" / metrics overview is scope creep for a session-controls slice and
  leaves an empty screen. The minimal compliant change is to make the demo speak the canonical
  vocabulary. See Open Questions if the team prefers deletion.
- **Reuse `SessionLifecycleState` instead of a second union.** Rather than rename the three demo
  strings, delete `type SessionState` and type `Session.state` as `SessionLifecycleState`. The
  canonical union then makes a non-canonical demo state a *compile error* — the gate becomes
  type-enforced, not convention-enforced.
- **Delete `stateLabels`.** Once the demo states are canonical, the label of a state is the state
  string itself; the `Record` becomes an identity map. Render `session.state` directly. Deletion
  over addition.
- **Extract the transition map to make "only canonical transitions" a unit-testable fact.**
  `lifecycleActions`, the canonical state set, and `toLifecycleState` are pure data currently
  trapped inside a `'use client'` component, so the gate can only be checked through the brittle
  RSC/DOM seam. Relocate them (no new abstraction — a straight move + `export`) to
  `app/lib/session-lifecycle.ts` so Phase 2 can assert the canonical spec directly. `lifecycleTone`
  and other UI-only maps stay in the component. See Open Questions for the export-in-place
  alternative if the team would rather not add a file.
- **No change to the ProblemDetails mapping.** The existing surface maps the backend's stable
  ProblemDetails `type` to a cause-specific, user-readable reason and falls back to
  `invalid_transition` ("That state change is not allowed from the current session state."). This
  satisfies "surface the reason". It intentionally does **not** echo the raw `detail` string — see
  Open Questions.

---

## Environment

No new environment variables. `API_GATEWAY_URL` / `NEXT_PUBLIC_API_GATEWAY_URL` already cover the
`PATCH .../state` call and the `/hubs/sessions` connection.

---

## Phases

### Phase 1 — Retire the non-canonical demo state vocabulary

**Scope**
- Remove `type SessionState`; type the demo `Session.state` as `SessionLifecycleState`.
- Rewrite the three fabricated demo session states to canonical values.
- Delete `stateLabels`; render the state string directly.
- Fix the comparison sites and the one copy string.
- (Recommended) Move `lifecycleActions`, the canonical state set, and `toLifecycleState` into
  `app/lib/session-lifecycle.ts` and import them back into `DashboardClient`.

**`DashboardClient.tsx` edits**

```ts
// :31 — delete this line entirely
- type SessionState = 'live' | 'paused' | 'draft';

// :46 — Session.state now uses the canonical union
-   state: SessionState;
+   state: SessionLifecycleState;

// :82 / :98 / :114 — demo session states → canonical
-   state: 'live',      // Downtown Trivia Night
+   state: 'Active',
-   state: 'paused',    // Science Quiz Run
+   state: 'Paused',
-   state: 'draft',     // History Knowledge Bowl (pending / not started)
+   state: 'Scheduled',

// :135 — metric hint copy
-   { label: 'Active sessions', value: '2', hint: '1 live, 1 paused', pill: 'Live overview' },
+   { label: 'Active sessions', value: '2', hint: '1 active, 1 paused', pill: 'Live overview' },

// :140-144 — delete stateLabels (identity map once states are canonical)
- const stateLabels: Record<SessionState, string> = {
-   draft: 'Draft',
-   live: 'Live',
-   paused: 'Paused',
- };
```

Usage-site fixes (states are now canonical, `stateLabels` is gone):

```ts
// :568
-     : derivedSession?.state === 'paused' ? 'warning'
+     : derivedSession?.state === 'Paused' ? 'warning'

// :696
-                        : derivedSession?.state === 'live'
+                        : derivedSession?.state === 'Active'

// :703
-                      : derivedSession ? stateLabels[derivedSession.state] : 'Live'}
+                      : derivedSession ? derivedSession.state : 'Active'}

// :1101 — "Live sessions" list excludes not-yet-started sessions
-                    {sessions.filter((s) => s.state !== 'draft').map((session) => (
+                    {sessions.filter((s) => s.state !== 'Scheduled').map((session) => (

// :1115-1116
-                          <span className={styles.chip} data-tone={session.state === 'live' ? 'success' : 'warning'}>
-                            {stateLabels[session.state]}
+                          <span className={styles.chip} data-tone={session.state === 'Active' ? 'success' : 'warning'}>
+                            {session.state}
```

**Recommended relocation — `app/lib/session-lifecycle.ts` (new)**

Move verbatim from `DashboardClient.tsx:146-198` (the `lifecycleStates` set, `lifecycleActions`
map, and `toLifecycleState`), adding `export`:

```ts
import type { SessionLifecycleState } from '@/app/lib/definitions'

export type LifecycleAction = {
  label: string
  targetState: SessionLifecycleState
  description: string
  destructive?: boolean
  allowsReason?: boolean
}

export const lifecycleStates = new Set<SessionLifecycleState>([
  'Scheduled', 'Preparing', 'Active', 'Paused', 'Finished', 'Cancelled',
])

// Allowed next actions per current state. Terminal states expose none.
export const lifecycleActions: Record<SessionLifecycleState, LifecycleAction[]> = {
  Scheduled: [
    { label: 'Prepare', targetState: 'Preparing', description: 'Open operator preparation for this session.' },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this scheduled session.', destructive: true, allowsReason: true },
  ],
  Preparing: [
    { label: 'Start', targetState: 'Active', description: 'Move teams into active answering.' },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this preparing session.', destructive: true, allowsReason: true },
  ],
  Active: [
    { label: 'Pause', targetState: 'Paused', description: 'Freeze the live session while preserving progress.' },
    { label: 'Finish', targetState: 'Finished', description: 'Terminally finish this live session.', destructive: true },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this live session.', destructive: true, allowsReason: true },
  ],
  Paused: [
    { label: 'Resume', targetState: 'Active', description: 'Return the paused session to active operation.' },
    { label: 'Finish', targetState: 'Finished', description: 'Terminally finish this paused session.', destructive: true },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this paused session.', destructive: true, allowsReason: true },
  ],
  Finished: [],
  Cancelled: [],
}

export function toLifecycleState(value: string): SessionLifecycleState | null {
  return lifecycleStates.has(value as SessionLifecycleState) ? (value as SessionLifecycleState) : null
}
```

Then in `DashboardClient.tsx`: delete the moved block and add
`import { lifecycleActions, toLifecycleState } from '@/app/lib/session-lifecycle'`.
(`lifecycleTone` and `stateLabels`-adjacent UI maps stay in the component.)

**Gate**
- `pnpm build` passes (type-check green). The canonical union now makes any non-canonical demo
  state a compile error.
- The admin overview renders unchanged visually except the chips read `Active` / `Paused` and the
  metric hint reads "1 active, 1 paused".

---

### Phase 2 — Unit test: exactly the canonical transitions, no non-canonical state

**Scope**
- Add `tests/unit/app/lib/session-lifecycle.test.ts` (vitest — matches
  `tests/unit/app/lib/sessions.test.ts`).
- Assert the map is exactly the canonical spec and terminal states are empty. This is the runnable
  proof for the gate "only canonical transitions are offered per current state".

**`tests/unit/app/lib/session-lifecycle.test.ts`**

```ts
import { describe, expect, it } from 'vitest'
import { lifecycleActions, lifecycleStates, toLifecycleState } from '@/app/lib/session-lifecycle'

// The one source of truth for which transitions the operator UI offers. If this drifts from the
// backend state machine, the operator is shown an edge the API will reject (or hidden a valid one).
const CANONICAL: Record<string, string[]> = {
  Scheduled: ['Preparing', 'Cancelled'],
  Preparing: ['Active', 'Cancelled'],
  Active: ['Paused', 'Finished', 'Cancelled'],
  Paused: ['Active', 'Finished', 'Cancelled'],
  Finished: [],
  Cancelled: [],
}

describe('session lifecycle transition map', () => {
  it('offers exactly the canonical targets for every state', () => {
    for (const [state, targets] of Object.entries(CANONICAL)) {
      expect(lifecycleActions[state as keyof typeof lifecycleActions].map((a) => a.targetState)).toEqual(targets)
    }
  })

  it('exposes no state outside the canonical six', () => {
    expect([...lifecycleStates].sort()).toEqual(
      ['Active', 'Cancelled', 'Finished', 'Paused', 'Preparing', 'Scheduled'],
    )
    // no action targets a non-canonical state either
    for (const actions of Object.values(lifecycleActions)) {
      for (const a of actions) expect(lifecycleStates.has(a.targetState)).toBe(true)
    }
  })

  it('terminal states offer no actions', () => {
    expect(lifecycleActions.Finished).toEqual([])
    expect(lifecycleActions.Cancelled).toEqual([])
  })

  it('rejects non-canonical strings via toLifecycleState', () => {
    expect(toLifecycleState('Active')).toBe('Active')
    expect(toLifecycleState('live')).toBeNull()
    expect(toLifecycleState('draft')).toBeNull()
  })
})
```

**Gate**
- `pnpm test` passes.
- The test fails if anyone reintroduces a non-canonical target or drops/adds an edge.

---

### Phase 3 — Verify the live operator flow + build gate

The transition happy-path, the rejected-edge, and the live `SessionStateChanged` reflection depend
on the running backend + SignalR hub; there is no SignalR mock harness in the Playwright suite (the
existing suite only intercepts the server-action `POST /dashboard` seam, `sessions.spec.ts:169`).
Rather than build one, verify against the seeded stack (commit `60bead0` seeds well-formed session
snapshots + an operator assignment). This is a **verification gate**, not new product code.

**Steps**
1. **Build / type-check:** `pnpm build` is green (no `SessionMode`, no non-canonical state type
   survives compilation).
2. **Unit:** `pnpm test` green (Phase 2 proves the canonical set).
3. **Canon guard (already automated):** the HU-16 guard test
   (`tests/e2e/sessions.spec.ts:260`) asserts no `SessionMode` copy in the session UI — re-run and
   keep green.
4. **Live operator flow (manual/integration against the seeded stack):**
   - Sign in as the seeded operator; open **My sessions** → select the assigned session. Confirm
     the **Session controls** tiles are exactly the canonical targets for the session's current
     state (e.g. `Scheduled` → **Prepare**, **Cancel** only).
   - Drive a valid transition (**Prepare**, then **Start**). Confirm the state chip and controls
     update and the backend accepts (`200`).
   - **Rejected edge:** in a second tab/client, transition the same session so the first tab is
     stale, then trigger a now-invalid action in the first tab. Confirm the `409` ProblemDetails
     surfaces via the `session-transition-error` banner with the mapped reason.
   - **Live broadcast:** with two operator tabs on the same session, transition in tab A and
     confirm tab B reflects the new state live (`onStateChanged`, `DashboardClient.tsx:391`) and
     shows the "State updated live…" note — no reload.

**Gate**
- `pnpm build`, `pnpm test`, and the HU-16 canon-guard e2e all pass.
- The seeded operator flow demonstrates: canonical-only actions per state, an accepted valid
  transition, a surfaced rejected-edge reason, and a live `SessionStateChanged` reflection.

---

## Commit Sequence

```
refactor(frontend): phase 1 — retire non-canonical session-state demo vocabulary; extract lifecycle map
test(frontend): phase 2 — unit-assert canonical transition map + no non-canonical state
chore(frontend): phase 3 — verify operator transition flow against seeded stack (no code)

Ref: HU-21A
```

(Phase 3 carries no code beyond re-running gates; fold it into the Phase 1/2 commits if the team
prefers no empty commit.)

---

## Out of Scope

- Any backend change (contract is frozen and correct).
- Replacing the admin overview's fabricated demo sessions/metrics/activity with real data — a
  separate ticket. This slice only makes their *state vocabulary* canonical.
- A SignalR mock harness for Playwright. The live-broadcast reflection is verified against the
  running stack; building a hub mock is disproportionate for a verification slice.
- Echoing the raw ProblemDetails `detail`/`title` string (see Open Questions).

---

## Open Questions

1. **Delete vs. rewrite the demo overview state model.** This plan rewrites it to canonical
   vocabulary (smallest compliant diff). If the team would rather the admin overview not ship
   fabricated sessions at all, Phase 1 becomes a deletion of `sessions`/`activity`/`adminMetrics`
   and their render blocks plus an empty-state — larger, and beyond the session-controls remit.
   **Recommendation: rewrite.**
2. **Extract the map, or export it in place?** Recommended: move to `app/lib/session-lifecycle.ts`.
   Alternative: add `export` to `lifecycleActions`/`lifecycleStates`/`toLifecycleState` in
   `DashboardClient.tsx` and import them into the unit test (no new file, but the test then pulls a
   `'use client'` module's React deps into the vitest run). **Recommendation: extract.**
3. **Does "surface the ProblemDetails reason" require the literal backend `detail` string?** The
   current UI maps the ProblemDetails `type` to its own cause-specific copy and discards `detail`.
   If the acceptance criterion means the exact backend text, `mapTransitionError`
   (`DashboardClient.tsx:502`) and the `sessions.ts:140-152` mapping must be widened to carry
   `detail` through. **Assumed sufficient as-is.**
