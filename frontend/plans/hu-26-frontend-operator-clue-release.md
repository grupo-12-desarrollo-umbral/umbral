# HU-26 — Frontend: Operator Clue Release (web operator surface only)

**Ref:** HU-26 / DES-36 / DES-70
**Branch:** feature/hu-26-manual-clue-release (frontend slice on the same branch, or a follow-up `-frontend` branch off it)
**Date:** 2026-07-13
**Builds on:** HU-19 operator assignment (`AssignedOperatorUserId` ownership seam), HU-21A lifecycle
(`selectedOperatorState`), HU-24A operator live session panel (`OperatorSessionPanelDto.teamProgress` —
the team list this control reuses). **The HU-26 backend is landed** on
`feature/hu-26-manual-clue-release` (X.1–X.4 committed); this slice consumes its verified contract.

> ⚠️ **Read first:** `frontend/AGENTS.md` — "This is NOT the Next.js you know." Before writing code,
> read the relevant guides under `frontend/.next-docs/` (server-and-client-components, mutating-data,
> forms). Do not rely on remembered Next.js conventions.

> **Execution shape:** sequential tracer-bullets — **P1** types → **P2** client fn + server action →
> **P3** operator release control + DashboardClient wiring → **P4** tests. Each phase is an
> end-to-end runnable slice and its own commit. This is a **hu-03-shaped small surface** (one POST
> endpoint), so P1–P2 and the release **action** in P3 are **code-complete**; the one increment that
> can't be built today — how the operator *discovers* a `targetId` — is kept at contract altitude with
> a raw-UUID interim input. The backend read that unblocks a real target picker is filed as
> **[DES-94](https://linear.app/desarrollo-equipo-12/issue/DES-94)** (operator-panel `targets[]`
> enrichment); the picker is a deferred follow-up once it lands (see Open Questions).

---

## Context

HU-26 is the operator's first **write** over the runtime clue model. An operator releases a
treasure-hunt `Target`'s optional **hidden** clue (`HiddenUntilOperatorRelease`) to **one team or all
teams** during an **Active** session. Release changes **visibility only** — it does **not** advance the
substage or resolve a target. The newly-visible clue is pushed to the released team's board over the
existing `team:{teamId}` SignalR group.

**This slice is the OPERATOR (web) surface only.** The participant board reveal is the **mobile** slice
(Step 9c, `mobile/`) — it is not planned or built here. This plan adds a release **control** to the
existing operator session view, one client fn, one server action, and the DashboardClient wiring.

**The frontend already has almost every seam this needs** (verified against source):

- `app/lib/sessions.ts` — `API_GATEWAY_URL`, the private `getGatewayHeaders()` (Bearer via
  `getValidAccessToken`), `verifySession`, and `associateTeamToSession` — the **exact POST +
  409-ProblemDetails-`detail` parse** shape this slice mirrors (`sessions.ts:248-279`).
- `app/actions/sessions.ts` — `getOperatorSessionPanelAction` (the `{ data | unauthorized | error }`
  discriminated result) and the Operator-gated action shape.
- `app/dashboard/DashboardClient.tsx` — the operator live-operation branch
  (`role === 'operator' && selectedOperatorSession && selectedOperatorState`, hero
  `data-testid="operator-panel"`, `DashboardClient.tsx:1090-1190`), `selectedOperatorState`
  (`:456`), the already-loaded `operatorPanelState.panel.teamProgress` (`:1143`) that carries every
  team's `teamId` + `displayName`, the `announce(title, body)` helper (`:712`), and the
  `useState`/`useTransition` control pattern used by the lifecycle controls (`runTransition`, `:772`).

**What is genuinely missing (drives the one blocked increment):** no operator-facing read exposes the
runtime **target ids** of the active substage. `OperatorSessionPanelDto.teamProgress[].activeSubstage`
carries only `totalActiveTargets` / `resolvedTargets` **counts** (verified — the HU-26 branch added
*only* `ReleaseClueResultDto`, nothing target-id-bearing). The `POST /clues/release` contract requires
a `targetId` (Guid), so the operator has no contract-supported way to *pick* a target from a friendly
list today. See **Open Questions** — this slice ships a raw-UUID `targetId` input (interim, mirroring
hu-10a's raw-`triviaQuizId` precedent); a target picker is deferred to
**[DES-94](https://linear.app/desarrollo-equipo-12/issue/DES-94)**, the backend enrichment that adds a
`targets[]` list to the operator panel's `activeSubstage`.

---

## Verified Backend Contract

Anchored to the landed backend on `feature/hu-26-manual-clue-release`
(`SessionsController.cs` `[HttpPost("{liveSessionId:guid}/clues/release")]` at L285,
`ReleaseClueResultDto.cs`, `ReleaseClueEndpointTests.cs` asserted status codes,
`ProblemDetailsExceptionHandler.cs` `ErrorCategory` map, the three new domain exceptions). HTTP
responses serialize **camelCase** (ASP.NET default).

| Endpoint | Auth | Request | 200 body | Errors |
|---|---|---|---|---|
| `POST /api/sessions/{liveSessionId}/clues/release` | `[Authorize(Policy=Operator)]` **+** ownership-resolver Proxy | `{ targetId: string /*Guid*/, teamId?: string /*Guid; omit ⇒ all teams*/ }` | `ReleaseClueResultDto` | `401` no trusted headers · `403` non-Operator **or non-owning operator** (RFC 7807) · `409` Conflict (ProblemDetails, `title: "Conflict."`) · `400` empty `targetId` (validator) · `404` session not found |

**`ReleaseClueResultDto`** (backend record → camelCase JSON):

```ts
ReleaseClueResultDto {
  targetId: string          // Guid — echoes the released target
  releasedTeamIds: string[] // Guid[] — one entry for a single-team release, every team for all-teams
}
```

**The three `409 Conflict` causes** — all share `title: "Conflict."`; distinguished by ProblemDetails
`detail` (parse it verbatim, exactly as `associateTeamToSession` parses its 409 `detail`):

| Domain exception | `detail` substring (verified message) | UI meaning |
|---|---|---|
| `ClueAlreadyReleasedToTeamException` | `"has already been released to team"` | duplicate — already released to that team for this target |
| `ClueNotReleasableException` | `"does not have a hidden clue"` | target has no releasable hidden clue in the active treasure-hunt substage |
| `SessionNotActiveForClueReleaseException` | `"requires an Active live session"` | session is not Active |

**Contract notes that drive the render:**
- A `403` covers **both** a non-Operator role *and* a non-owning operator (the ownership Proxy) —
  both map to the same not-authorized state (AC: "non-owned session → 403").
- All-teams release: **omit `teamId`** (do not send `null` — the request record is `Guid? TeamId`, so
  an omitted property is the canonical "all teams" signal; `releasedTeamIds` then lists every team).
- Release **does not** advance the substage or resolve a target — nothing progress-related changes; the
  operator panel's `resolvedTargets` stays put (it is `0` until HU-31 regardless).

---

## Architecture Decisions

1. **Mirror `associateTeamToSession` for the client fn.** It is the house POST-with-409-ProblemDetails
   shape: `verifySession` → `fetch(POST, getGatewayHeaders({'Content-Type':'application/json'}))` →
   status ladder → `409` parses `detail` and throws a **distinct typed `Error`** per cause. The clue
   release fn adds the three release-specific `detail` branches from the contract table.
2. **Server action is a discriminated union the control renders inline** — `{ data } | { duplicate } |
   { notReleasable } | { notActive } | { unauthorized } | { error }`. Modeled on
   `getOperatorSessionPanelAction`, but for a **mutation** so the operator sees a specific,
   non-crashing outcome per cause. Genuine auth failures (`unauthorized`) are kept separate from
   transient ones (`error`) so a momentary blip never tells a legitimately-assigned operator they are
   "not authorized." Non-Operator role short-circuits to `{ unauthorized }` before the gateway.
3. **New component `OperatorClueReleasePanel`, owning its own local state** (`useState` +
   `useTransition`), nested in the existing operator hero. No reducer, no DashboardClient state seam —
   this is a fire-and-forget mutation whose *only* durable effect (the board reveal) lands on the
   **participant** surface, not the operator dashboard. It mirrors hu-10a's `AddStageControl`
   self-contained control, not the hu-24A reducer machinery.
4. **The Active gate is a render gate.** The control renders its inactive note (and no submit) unless
   `selectedOperatorState === 'Active'`. The backend also enforces Active (409
   `SessionNotActiveForClueReleaseException`), so a stale-state race still surfaces non-crashingly as
   the `notActive` branch — the UI never pre-empts the backend, it just avoids offering a control that
   cannot work.
5. **Team list is reused from the operator panel — no new fetch.** `operatorPanelState.panel.teamProgress`
   is already loaded and live in the operator hero (`teamId` + `displayName` per team). The team
   selector's options are `All teams` (value `''`) + one option per team. When the panel has no teams
   yet, the selector shows only `All teams` and the note explains a team must be attached.
6. **No `revalidatePath` after release.** Release mutates no RSC-cached dashboard read (the operator
   panel is fetched client-side via a server action, and the reveal is a SignalR push to the
   participant board). Revalidating `/dashboard` would be a no-op churn — omitted deliberately.
7. **Raw `targetId` UUID input is the interim target selector** (Architecture-forced, not a shortcut):
   no operator read exposes runtime target ids, so a friendly target picker is not buildable against
   the current contract. This mirrors hu-10a's interim raw-`triviaQuizId` input pending a list
   endpoint. The unblocking read is **DES-94** (operator-panel `activeSubstage.targets[]`); once it
   lands, the picker is a clean drop-in — swap the input for a `<select>` over
   `panel.activeSubstage.targets`, `hasHiddenClue`-filtered. The client fn / action / result contract
   below does **not** change.
8. **No clue-as-progress, no substage advance from the UI.** The control releases and reports the
   released team ids; it renders no clue-as-progress and issues no state transition.

---

## Environment

**No new environment variables.** Reuses (verified against source):

- `API_GATEWAY_URL` (server-side, `app/lib/sessions.ts:20` — `process.env.API_GATEWAY_URL!`) for the
  release POST, via `getGatewayHeaders()` (Bearer from `getValidAccessToken()`).

`getGatewayHeaders` is **private to `sessions.ts`** (not exported) — the new client fn lives in the
**same file**, so it calls it directly (no import, no replication needed), exactly as the existing
`associateTeamToSession` / `getOperatorSessionPanel` fns do.

---

## data-testid contract

Single source of truth for selectors; the P4 unit/e2e tests and the AC→test map key off these. The new
control nests inside the existing operator hero (`data-testid="operator-panel"`).

| testid | Element | Phase |
|---|---|---|
| `clue-release-panel` | `OperatorClueReleasePanel` root section | P3 |
| `clue-release-inactive` | inactive note shown when `state !== 'Active'` (no submit rendered) | P3 |
| `clue-release-target-input` | raw `targetId` (UUID) input | P3 |
| `clue-release-team-select` | team selector (`All teams` + one option per team) | P3 |
| `clue-release-submit` | Release button | P3 |
| `clue-release-success` | success note (`role="status"`) — "Released to N team(s)" | P3 |
| `clue-release-error` | error/conflict note (`role="alert"`) — duplicate / not-releasable / not-active / unauthorized / transient | P3 |

---

## Phase 1 — Types (`app/lib/definitions.ts`)

**Scope:** add the request + result DTOs. No behaviour change; P2–P3 consume them.

```ts
// --- HU-26 operator clue release ---
// Request of POST /api/sessions/{liveSessionId}/clues/release (Operator + ownership Proxy).
// Omit teamId to release the target's hidden clue to ALL teams.
export type ReleaseClueRequest = {
  targetId: string          // Guid — the treasure-hunt target whose hidden clue becomes visible
  teamId?: string           // Guid — omit ⇒ release to all teams
}

// 200 response: the released target + the team ids the clue is now visible to.
export type ReleaseClueResultDto = {
  targetId: string
  releasedTeamIds: string[] // one id for a single-team release; every team for all-teams
}
```

**Gate:** `pnpm build` / typecheck passes. No runtime change.

---

## Phase 2 — Client fn + Server Action

**Scope:** add `releaseClue` to `app/lib/sessions.ts` and `releaseClueAction` to
`app/actions/sessions.ts`. Both mirror the existing POST-mutation shapes verbatim.

### `app/lib/sessions.ts` — add (mirrors `associateTeamToSession`'s POST + 409 `detail` parse)

Add `ReleaseClueRequest, ReleaseClueResultDto` to the `./definitions` type import, then:

```ts
// HU-26 operator clue release. Mirrors associateTeamToSession's POST + 409-ProblemDetails-detail parse.
// 403 = non-Operator or a non-owning operator (the ownership Proxy denies). The single 409 carries three
// distinct causes distinguished by the ProblemDetails `detail`, surfaced as distinct typed errors so the
// control can render a specific, non-crashing message for each. Omit teamId to release to all teams.
export async function releaseClue(
  liveSessionId: string,
  body: ReleaseClueRequest,
): Promise<ReleaseClueResultDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/clues/release`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(
        body.teamId ? { targetId: body.targetId, teamId: body.teamId } : { targetId: body.targetId },
      ),
    },
  )

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 409) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null
    const detail = problem?.detail?.toLowerCase() ?? ''
    if (detail.includes('already been released')) throw new Error('already_released')
    if (detail.includes('does not have a hidden clue')) throw new Error('not_releasable')
    if (detail.includes('requires an active live session')) throw new Error('not_active')
    throw new Error('release_conflict')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `releaseClue failed with status ${response.status}`)
  }

  return response.json() as Promise<ReleaseClueResultDto>
}
```

### `app/actions/sessions.ts` — add (Operator-gated discriminated union)

Add the aliased lib import (`releaseClue as releaseClueLib`) and the `ReleaseClueRequest,
ReleaseClueResultDto` type imports, then:

```ts
// HU-26 operator clue release. Outcomes the control renders distinctly (never throws to the client):
//   { data }          → released; result.releasedTeamIds lists the team(s) the clue is now visible to
//   { duplicate }     → 409: already released to that team for this target
//   { notReleasable } → 409: target has no releasable hidden clue in the active substage
//   { notActive }     → 409: session is not Active
//   { unauthorized }  → 403 non-owner / 401 / non-Operator → not-authorized state
//   { error }         → 400 / 404 / transient / unexpected → retryable error state
export async function releaseClueAction(
  liveSessionId: string,
  body: ReleaseClueRequest,
): Promise<
  | { data: ReleaseClueResultDto }
  | { duplicate: true }
  | { notReleasable: true }
  | { notActive: true }
  | { unauthorized: true }
  | { error: string }
> {
  'use server'
  const session = await verifySession()
  if (session.role !== 'Operator') return { unauthorized: true }
  try {
    const data = await releaseClueLib(liveSessionId, body)
    return { data }
  } catch (error) {
    if (error instanceof IdentityError) {
      if (error.code === 'unauthorized') return { unauthorized: true }
      return { error: error.message }
    }
    if (error instanceof Error) {
      if (error.message === 'already_released') return { duplicate: true }
      if (error.message === 'not_releasable') return { notReleasable: true }
      if (error.message === 'not_active') return { notActive: true }
    }
    return { error: 'Could not release the clue. Try again.' }
  }
}
```

**Gate:** `pnpm build` passes. Calling the action from a non-Operator session returns `{ unauthorized }`
without reaching the gateway; a `403` maps to `{ unauthorized }`, each `409` cause maps to its own
branch, `400`/`404`/transient to `{ error }`.

---

## Phase 3 — Operator release control + DashboardClient wiring

**Scope:** add `OperatorClueReleasePanel.tsx` and nest it in the operator hero. Team options come from
the already-loaded operator-panel `teamProgress`; the Active gate is a render gate.

### `app/dashboard/OperatorClueReleasePanel.tsx` (new) — self-contained control (mirrors hu-10a `AddStageControl`)

```tsx
'use client'

import { useState, useTransition } from 'react'
import type { SessionLifecycleState } from '@/app/lib/definitions'
import { releaseClueAction } from '@/app/actions/sessions'
import styles from './dashboard.module.css'

type ClueReleaseTeamOption = { teamId: string; displayName: string }

export function OperatorClueReleasePanel({
  liveSessionId,
  state,
  teams,
  onReleased,
}: {
  liveSessionId: string
  state: SessionLifecycleState
  teams: ClueReleaseTeamOption[]
  onReleased?: (target: string, count: number) => void
}) {
  const [targetId, setTargetId] = useState('')
  const [teamId, setTeamId] = useState('')            // '' ⇒ all teams
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  // Active-only render gate (Architecture Decision 4). The backend also enforces Active, so a stale
  // race still surfaces as the notActive branch below — the UI never pre-empts, it just hides a
  // control that cannot work off an inactive session.
  if (state !== 'Active') {
    return (
      <section className={styles.cluePanel} data-testid="clue-release-panel" aria-labelledby="clue-release-title">
        <div className={styles.panelHeader}>
          <h2 id="clue-release-title">Release clue</h2>
        </div>
        <p className={styles.panelMeta} data-testid="clue-release-inactive">
          Clue release is available once the session is Active.
        </p>
      </section>
    )
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      setSuccess(null)
      const result = await releaseClueAction(liveSessionId, {
        targetId: targetId.trim(),
        teamId: teamId || undefined,        // omit ⇒ all teams
      })
      if ('data' in result) {
        const count = result.data.releasedTeamIds.length
        setSuccess(`Released to ${count} team${count === 1 ? '' : 's'}.`)
        setTargetId('')
        onReleased?.(result.data.targetId, count)
      } else if ('duplicate' in result) {
        setError('That clue is already released to that team for this target.')
      } else if ('notReleasable' in result) {
        setError('That target has no releasable hidden clue in the active substage.')
      } else if ('notActive' in result) {
        setError('The session must be Active to release clues.')
      } else if ('unauthorized' in result) {
        setError('You are not authorized to release clues for this session.')
      } else {
        setError(result.error)
      }
    })
  }

  return (
    <section className={styles.cluePanel} data-testid="clue-release-panel" aria-labelledby="clue-release-title">
      <div className={styles.panelHeader}>
        <h2 id="clue-release-title">Release clue</h2>
        <div className={styles.panelMeta}>Reveal a treasure-hunt target’s hidden clue to one team or all teams.</div>
      </div>

      <label className={styles.fieldLabel}>
        <span>Target id</span>
        <input
          className={styles.inlineInput}
          data-testid="clue-release-target-input"
          value={targetId}
          onChange={(e) => setTargetId(e.target.value)}
          placeholder="Target UUID"
        />
      </label>

      <label className={styles.fieldLabel}>
        <span>Team</span>
        <select
          className={styles.inlineSelect}
          data-testid="clue-release-team-select"
          value={teamId}
          onChange={(e) => setTeamId(e.target.value)}
        >
          <option value="">All teams</option>
          {teams.map((t) => (
            <option key={t.teamId} value={t.teamId}>{t.displayName}</option>
          ))}
        </select>
      </label>

      <button
        className={styles.primaryButton}
        data-testid="clue-release-submit"
        disabled={isPending || targetId.trim() === ''}
        onClick={submit}
        type="button"
      >
        {isPending ? 'Releasing…' : 'Release'}
      </button>

      {success && <p className={styles.panelMeta} role="status" data-testid="clue-release-success">{success}</p>}
      {error && <p className={styles.errorBanner} role="alert" data-testid="clue-release-error">{error}</p>}
    </section>
  )
}
```

### `app/dashboard/DashboardClient.tsx` — wiring (all anchors verified against source)

1. **Import.** Add `import { OperatorClueReleasePanel } from './OperatorClueReleasePanel'` alongside the
   sibling panel imports (`DashboardClient.tsx:13-16`).

2. **Render.** Nest the control in the operator hero right after `OperatorTeamProgressPanel`
   (`DashboardClient.tsx:1143-1148`, inside `data-testid="operator-panel"`). `selectedOperatorState` is
   in scope (`:456`) and is a non-null `SessionLifecycleState` in this branch; teams come from the
   already-loaded operator panel (`operatorPanelState.panel?.teamProgress`, `:1143`):

```tsx
<OperatorClueReleasePanel
  liveSessionId={selectedOperatorSession.liveSessionId}
  state={selectedOperatorState}
  teams={(operatorPanelState.panel?.teamProgress ?? []).map((t) => ({
    teamId: t.teamId,
    displayName: t.displayName,
  }))}
  onReleased={(target, count) =>
    announce('Clue released', `Target ${target} revealed to ${count} team${count === 1 ? '' : 's'}.`)
  }
/>
```

**Gate:** `pnpm build` + typecheck pass. Opening an **Active** assigned session shows `clue-release-panel`
with a `targetId` input, a team selector (`All teams` + one option per attached team), and a Release
button; a non-Active session shows `clue-release-inactive` and no submit; releasing a valid target’s
hidden clue shows `clue-release-success` ("Released to N team(s)"); a second identical release shows the
duplicate message in `clue-release-error`; a bad/target-without-hidden-clue shows the not-releasable
message; no clue-as-progress renders and no state transition is issued from the control.

---

## Phase 4 — Tests

**Scope:** unit coverage for the client fn, the action-shaped result, and the control’s render states;
one live e2e mirroring the manual seed / `session-operator-panel.spec.ts`.

### Unit

- **`tests/unit/app/lib/sessions.test.ts`** — `releaseClue`: `200` → parsed `ReleaseClueResultDto`;
  `403`/`401` → `IdentityError('unauthorized', …)`; `404` → `Error('session_not_found')`; `400` →
  `Error('invalid_input')`; `409` with each `detail` → `already_released` / `not_releasable` /
  `not_active` (and an unrecognised `detail` → `release_conflict`); assert the **all-teams** body omits
  `teamId` while a single-team body includes it.
- **`tests/unit/app/dashboard/operator-clue-release-panel.test.ts`** (new, mirror
  `operator-team-progress-panel.test.ts`) — render states: `state !== 'Active'` → `clue-release-inactive`,
  no `clue-release-submit`; `state === 'Active'` → input + selector (options = `All teams` + team names)
  + submit disabled while `targetId` empty; a mocked `releaseClueAction` returning `{ data }` →
  `clue-release-success`; `{ duplicate }` / `{ notReleasable }` / `{ notActive }` / `{ unauthorized }` /
  `{ error }` → the matching `clue-release-error` copy.

### E2E — `tests/e2e/session-clue-release.spec.ts` (mirror `session-treasure-hunt-manual-seed.spec.ts` seed + `session-operator-panel.spec.ts` header)

Drive create → assign → attach two teams → `Active` as op-1 against the real gateway, reusing that
spec’s `token` / `api` / `sql` helpers. Because no operator read exposes the runtime target id (the
blocked dependency), read it from the runtime snapshot the same way the manual seed does — a `sql()`
query against `live_session_mission_runtime_snapshot_*` for the active substage’s first
`TargetSnapshotId` — then:

```ts
// P3/P4 — happy path + no-duplicate (names only; bodies land in P4)
test('operator releases a target hidden clue to one team: success note + released count', …)  // AC1, AC2
test('releasing the same target to the same team again shows the duplicate message', …)        // AC3 (no-duplicate)
test('the clue-release control is inactive until the session is Active', …)                     // AC1 (Active gate)
```

Non-owner `403` is asserted at the unit/action layer (`releaseClue` `403` → `IdentityError` → action
`{ unauthorized }` → `clue-release-error`), matching how the timer/answered/panel specs keep non-owner
coverage off the live UI (a second sub-keyed operator against the live stack is flaky). The **no-leak**
and **live board reveal** properties are participant-board (mobile) assertions — **out of scope here**,
owned by Step 9c.

**Gate:** `pnpm test` (vitest) + `pnpm exec playwright test` pass; `pnpm build` / typecheck clean.

---

## Acceptance-criteria → test mapping

DES-36 acceptance criteria (`hu26-brief.md` §Acceptance criteria), scoped to the operator web surface.

| # | Acceptance criterion (operator surface) | Covered by | Phase |
|---|---|---|---|
| AC1 | An operator can release a clue to a team during an Active session | `releaseClue` `200` → `ReleaseClueResultDto`; `clue-release-success`; Active render gate; e2e happy path + "inactive until Active" | P2 / P3 / P4 |
| AC2 | Release to one team vs all teams (omit `teamId`) | body omits `teamId` for all-teams; team selector `All teams` option; unit body assertion | P2 / P3 / P4 unit |
| AC3 | The same clue cannot be released twice to the same team for the same target | `409 already_released` → `{ duplicate }` → duplicate message; e2e no-duplicate | P2 / P3 / P4 |
| AC (guard) | Non-owning / non-operator release is refused (403) | `403` → `IdentityError('unauthorized')` → `{ unauthorized }` → `clue-release-error`; `session.role !== 'Operator'` short-circuit | P2 / P3 / P4 unit |
| AC (guard) | Release does not advance the substage / resolve a target from the UI | control issues only the release POST; renders no clue-as-progress, no transition (visual/scope assertion) | P3 |

---

## Open Questions / Dependencies

- **Target discovery — interim input now, picker after DES-94 (drives Architecture Decision 7).** No
  operator-facing read exposes the runtime `TargetSnapshotId`s of the active substage —
  `OperatorSessionPanelDto` carries only target **counts** (verified: the HU-26 branch added *only*
  `ReleaseClueResultDto`). This slice ships a raw `targetId` UUID input as the interim selector (hu-10a
  raw-`triviaQuizId` precedent). The enabling read is tracked as
  **[DES-94](https://linear.app/desarrollo-equipo-12/issue/DES-94)** — a backend, session-operations
  enrichment adding `activeSubstage.targets: [{ targetSnapshotId, name, sequenceOrder, hasHiddenClue }]`
  to the operator panel. **Follow-up once DES-94 lands:** add `OperatorActiveSubstageContextDto.targets`
  to `definitions.ts`, then swap `clue-release-target-input` for a `<select>` over
  `panel.activeSubstage.targets` filtered to `hasHiddenClue`. The `releaseClue` client fn /
  `releaseClueAction` / result contract are unchanged — only the target-selection widget changes.
  **DES-94 is not a blocker of this slice** (the raw input ships now); it is the follow-up's precondition.
- **Dependency — backend running through the gateway.** The HU-26 backend is committed on
  `feature/hu-26-manual-clue-release` (X.1–X.4). The P4 e2e needs the service + api-gateway rebuilt and
  up (`docker compose build session-operations-service api-gateway`) so the real `/clues/release`
  endpoint is live, plus the hidden-clue seed (`session-clue-release-manual-seed.spec.ts`, on the
  branch) to author a releasable target. Unit phases do not.
- **409 cause disambiguation depends on the ProblemDetails `detail` text.** The three conflict causes
  share `title: "Conflict."`; `releaseClue` distinguishes them by substring-matching `detail` (exactly
  as `associateTeamToSession` does). If the backend later stabilises a ProblemDetails `type` per cause
  (as the lifecycle transition endpoint does), switch the branch to match on `type` — lower-risk than
  message matching. Tracked, not blocking.

## Out of Scope

- **The participant board clue reveal (mobile, Step 9c).** The `team:{teamId}` `TeamBoardUpdated` push
  and the mobile board’s reveal are a separate app (`mobile/`) — not planned, typed, or built here.
- **No-leak / live-reveal e2e assertions** — those are participant-board (mobile) properties.
- Any backend change — the contract is landed on the feature branch; this slice is frontend-only.
- Clue-as-progress, substage advance, or target resolution from the UI (HU-31), conditional/auto
  release (HU-27), operator-authored runtime clues (HU-28), evidence/QR (HU-29/30).
- A friendly target picker and the operator target-list read that backs it — the read is DES-94
  (backend), the picker is this plan's follow-up after DES-94 lands (see Open Questions). Neither is
  built in this slice; the raw-UUID input stands in.
- `revalidatePath('/dashboard')` after release (Architecture Decision 6 — release mutates no RSC read).

---

## Commit Sequence

```
feat(frontend): phase 1 — operator clue release types — HU-26
feat(frontend): phase 2 — clue release client fn + server action — HU-26
feat(frontend): phase 3 — operator clue release control + dashboard wiring — HU-26
test(frontend): phase 4 — operator clue release unit + e2e — HU-26

Ref: HU-26
Ref: DES-36
Ref: DES-70
```
