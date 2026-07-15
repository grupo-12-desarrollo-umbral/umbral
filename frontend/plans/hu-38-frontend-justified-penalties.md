# HU-38 — Frontend: Justified Penalties

**Ref:** HU-38 / DES-53 / DES-85
**Branch:** feature/hu-38-justified-penalties (frontend slice rides the same branch as the backend HU)
**Date:** 2026-07-15
**Builds on:** HU-24A (`hu-24a-frontend-operator-live-session-panel.md`) — the operator hero, team
progress panel, and the `getOperatorSessionPanelAction` read seam. HU-28
(`hu-28-ui-operative-clue-authoring-and-reveal.md`) — the nested operator control shape this slice
mirrors.

> ⚠️ **Read first:** `frontend/AGENTS.md` — "This is NOT the Next.js you know." Before writing any
> code, read the relevant guides under `frontend/.next-docs/` (server-and-client-components,
> mutating-data, revalidating, forms). Do not rely on remembered Next.js conventions.

> **Execution shape:** sequential — **Phase 1** (types + client + action) → **Phase 2** (operator
> control) → **Phase 3** (score reflection, **blocked**) → **Phase 4** (e2e, **blocked**). Phases 3–4
> are held at contract + gate altitude because they depend on backend/gateway work this slice may not
> do (see Open Questions / Dependencies). Each phase is its own commit.

---

## Frontend plan concreteness rule (embedded verbatim, per `frontend/AGENTS.md`)

1. Proportion concreteness to certainty. Write code-complete detail — exact DTO/request types, real
   component skeletons, exact client-fn + server-action bodies, a data-testid contract — only for the
   fully-knowable near-term increments (typically the foundation + first authoring increment). Keep
   later, large, or blocked increments at contract + gate altitude: a contract table, scope, and gate,
   with no invented bodies. Never write code for an increment blocked on an open question.
2. Verify every code anchor against the real source before writing it. Open the files the plan names —
   exported vs. private helpers, exact signatures, the const/env it reads, the line a refactor targets —
   and write only what the source actually supports. A confident-but-wrong anchor is worse than an
   altitude note. If a detail is not verifiable, state the assumption under Open Questions rather than
   inventing it.
3. Required sections (a plan missing one is a defect): Context · Verified Backend Contract
   (endpoint/shape table) · Architecture Decisions · Environment (env vars / config consts reused) ·
   data-testid contract · phased Scope + Gate per increment · Acceptance-criteria → test mapping ·
   Open Questions / Dependencies · Out of Scope.
4. Final forms only, sequential by default. Write only the final version of each anchor — no
   "wrong → revised" trails — and keep increments sequential unless the slice genuinely parallelizes.

---

## Context

HU-38 adds an operator-facing **justified penalty**: an append-only `ScoreEntry` deduction plus its
`Penalty` child, gated to the session's assigned operator, recorded with a non-blank reason, actor,
and timestamp. The backend is implemented on `feature/hu-38-justified-penalties` (commit `141cf8b`,
`feat(scoring-monitoring): operator justified penalty application — HU-38`) — **it is not on
`develop`**, and that commit touched neither `backend/api-gateway/` nor `frontend/`.

What exists today on the frontend (do **not** rebuild): `app/lib/sessions.ts` — the gateway client
(`API_GATEWAY_URL` + `getGatewayHeaders`, Bearer token from `getValidAccessToken()`);
`app/actions/sessions.ts` — `'use server'` actions returning **discriminated result objects** rather
than throwing (`releaseClueAction`, `addOperativeClueAction`); `app/dashboard/OperativeCluePanel.tsx`
— a self-contained operator control (own `useState`/`useTransition`) nested in the operator hero;
`app/dashboard/OperatorTeamProgressPanel.tsx` — renders `team.score` per team;
`app/dashboard/DashboardClient.tsx` — hosts both, and owns `loadOperatorPanel` (line 553), the panel
refresh seam.

**Three verified blockers** stand between this slice and its stated gate. They are backend/gateway
defects found by reading source, not assumptions — each is evidenced under *Open Questions /
Dependencies*, and none may be fixed in this step ("do not modify backend code"):

1. **No gateway route** for `/api/sessions/{id}/penalties` — the POST falls through to the
   `session-ops` catch-all and 404s.
2. **The ownership Proxy denies every Operator** — the projection it reads is fed by an integration
   event nobody publishes, and its id type does not match session-operations'.
3. **No read surface reflects the deduction** — the operator panel's `score` is session-owned, and
   the ranking fold *adds* penalty magnitudes instead of subtracting them.

Phases 1–2 are fully knowable from the merged backend controller contract and land regardless.
Phase 3 (score reflection) and Phase 4 (e2e against the real API) are **blocked on those defects**
and are therefore held at contract + gate altitude, with no invented bodies.

---

## Verified Backend Contract

Read from `feature/hu-38-justified-penalties` @ `141cf8b`. Enum-ish values are JSON **strings**;
ids are **Guid** strings over the wire.

**Endpoint** (gateway base = existing `API_GATEWAY_URL`):

| Verb | Route | Request | Success | Phase |
|------|-------|---------|---------|-------|
| POST | `/api/sessions/{liveSessionId}/penalties` | `{ teamId, reason }` | **201 Created** + `AppliedPenaltyDto` | P1 |

Source anchors:
- `src/Api/Controllers/PenaltiesController.cs` — `[Route("api/sessions")]`,
  `[HttpPost("{liveSessionId:guid}/penalties")]`,
  `[Authorize(Policy = AuthorizationPolicies.AdministratorOrOperator)]`. Returns
  `CreatedAtAction(...)` ⇒ **201**, not 200. It calls `accessResolver.EnsureAccessAsync` *and* rebinds
  `command with { LiveSessionId = liveSessionId }`, so the **path id wins** over any body id.
- `src/Application/Dtos/Scores/AppliedPenaltyDto.cs` —
  `(Guid ScoreEntryId, Guid TeamId, int PenaltyAmount, string Reason, DateTimeOffset AppliedAt)`.
- `src/Application/Scores/Commands/ApplyPenalty/ApplyPenaltyCommand.cs` —
  `(Guid LiveSessionId, Guid TeamId, string Reason)`, `[Authorize(Roles = "Administrator,Operator")]`.

**Response shape** — `AppliedPenaltyDto`:

| Field | Type | Meaning |
|---|---|---|
| `scoreEntryId` | `string` (Guid) | the append-only ledger entry id |
| `teamId` | `string` (Guid) | penalized team |
| `penaltyAmount` | `number` | **positive magnitude** (see below) |
| `reason` | `string` | the justification, echoed back |
| `appliedAt` | `string` (ISO-8601) | `Penalty.AppliedAt` |

**`penaltyAmount` is a positive magnitude, not a signed delta.** `ScoreValue.Create` throws
`InvalidScoreValueException` for `value < 0` (`src/Domain/ValueObjects/ScoreValue.cs`), so a
`ScoreValue` can never be negative. The handler computes
`_scorePolicy.Award(ScoreValue.Create(100))` and `SnapshotScorePolicy.Award` returns its argument
unchanged ⇒ **`penaltyAmount` is always `100`** in the current build. The *deduction* is carried by
`ScoreEntry.EntryType == ScoreEntryType.Penalty`, which the DTO does **not** expose. The UI must
therefore render the amount as a deduction itself (`−100`) and never infer sign from the number.

**Error cases** (verified against handler / validator / proxy / policy):

| Status | Cause | Source |
|---|---|---|
| **400** | blank/whitespace/missing `reason`, or empty `teamId`/`liveSessionId` | `ApplyPenaltyCommandValidator` — `RuleFor(c => c.Reason).NotEmpty()` (FluentValidation `NotEmpty` rejects whitespace-only) |
| **400** | ineligible penalty | `DefaultPenaltyPolicy.ValidateEligibility` → `PenaltyNotEligibleException` (currently only fires on empty ids, which the validator already rejects ⇒ unreachable in practice) |
| **401** | no/invalid bearer token | gateway `AuthorizationPolicy: "default"` |
| **403** | caller is not the assigned operator (RFC 7807) | `ScoringSessionAuthorizationProxy.EnsureAccessAsync` → `ForbiddenAccessException` |

**Authorization semantics** — `ScoringSessionAuthorizationProxy`
(`src/Application/Scores/Common/Authorization/ScoringSessionAuthorizationProxy.cs`):
- `Administrator` ⇒ unrestricted (returns early, **no ownership check**).
- `Operator` ⇒ allowed only when the projection's `AssignedOperatorUserId == Guid.Parse(currentUser.Id)`.
- any other role, or a null projection row, ⇒ `ForbiddenAccessException`.

**Ledger semantics the UI must respect:** `ScoreEntry` is append-only (`ScoreEntry.Penalty(...)`
constructs a new entry and raises `ScoreEntryRegistered` + `PenaltyApplied`). There is no mutable
total anywhere in the contract — no endpoint returns "the team's new score" — so the response is
**evidence of one ledger entry**, never a running total.

---

## Architecture Decisions

1. **Group the penalty call with the other gateway session calls.** `applyPenalty` goes in the
   existing `app/lib/sessions.ts` (it is the same gateway base, the same `getGatewayHeaders`, the same
   `verifySession()` preamble as `releaseClue`/`addOperativeClue`) and `applyPenaltyAction` in
   `app/actions/sessions.ts`. No new module: unlike `mission-structure.ts` (which existed to keep base
   CRUD untouched), there is nothing here to keep untouched — and `getGatewayHeaders` is **private to
   `sessions.ts`**, so a new module would have to duplicate it for no benefit. See Environment.
2. **The action returns a discriminated result, never throws.** Mirrors `releaseClueAction` exactly:
   `{ data } | { invalidReason } | { unauthorized } | { error }`. The control renders each branch
   distinctly; a rejection is a rendered state, not an exception.
3. **Operator-only at the action layer.** `session.role !== 'Operator'` ⇒ `{ unauthorized: true }`
   before any fetch, mirroring `releaseClueAction`. The backend policy also admits `Administrator`
   (unrestricted, no ownership check), but this slice does **not** expose penalties to admins —
   scope says operator-facing, non-operators excluded. The narrower frontend guard is deliberate and
   is the one place the UI is stricter than the backend.
4. **Reason-required is enforced twice, and the backend is authoritative.** The submit button is
   disabled while `reason.trim() === ''` (the UX gate), and the client sends `reason.trim()`. The
   **400 branch is still implemented**, because the UI gate is not a guarantee — it is a convenience.
   The UI never invents the rejection copy path away.
5. **The response is one ledger entry, not a total.** The control renders
   `−{penaltyAmount} pts` from `AppliedPenaltyDto` as *confirmation of the entry that was appended*
   and does **not** add/subtract it into any client-held score. Nothing in this slice treats a penalty
   as a mutable total (the slice gate). Reflecting the deduction in the team's *score* is Phase 3, and
   is blocked — see below.
6. **Refresh, don't patch.** When Phase 3 unblocks, the deduction reaches the operator view by
   re-reading the authoritative snapshot via the existing `loadOperatorPanel` seam
   (`DashboardClient.tsx:553`), not by locally decrementing `team.score`. Client-side arithmetic on a
   score is exactly the "mutable total outside the ledger" the gate forbids.
7. **Self-contained control, no DashboardClient state seam in P2.** `PenaltyPanel` owns its
   `useState`/`useTransition`, mirroring `OperativeCluePanel`. It takes an optional `onApplied`
   callback used only for the `announce(...)` live-region message (the pattern `OperativeCluePanel`
   already uses via `onAdded`). The panel-refresh wiring is Phase 3.

---

## Environment

**No new environment variables.** Verified against source, *not* assumed:

- `app/lib/sessions.ts` reads `const API_GATEWAY_URL = process.env.API_GATEWAY_URL!` (line 25) — the
  penalties route is a gateway route, so `applyPenalty` reuses this const **in-file**. (Contrast the
  service-direct modules: `missions.ts`/`trivias.ts`/`mission-structure.ts` hardcode
  `MISSION_DESIGN_SERVICE_URL = 'http://localhost:5001'`, and `users.ts`/`teams.ts` hardcode
  `IDENTITY_SERVICE_URL = 'http://localhost:5002'`. None of those apply here.)
- `getGatewayHeaders(headers?)` (`sessions.ts:27`) is **private to `sessions.ts`** — not exported.
  Adding `applyPenalty` to that same file is what makes it reusable by `import`; a separate module
  would have to redeclare it. This is the concrete reason for Architecture Decision 1.
- Auth is a **Keycloak bearer token** (`getValidAccessToken()` → `Authorization: Bearer …`), *not* the
  `X-User-*` trusted-header shape used by the service-direct modules. `getGatewayHeaders` already maps
  a `KeycloakAuthError` to `IdentityError('unauthorized', …)`.

⚠️ **The gateway does not route this endpoint yet.** `backend/api-gateway/src/appsettings.Development.json`
declares `scoring-ranking` (`/api/sessions/{liveSessionId}/ranking`), `scoring-hub` (`/hubs/scoring`),
and `scoring` (`/api/scoring/{**catch-all}`), then `session-ops` (`/api/sessions/{**catch-all}`).
`/api/sessions/{id}/penalties` matches only the **`session-ops` catch-all** ⇒ forwarded to
session-operations-service, which has no such endpoint ⇒ **404**. The `/api/scoring/{**catch-all}`
route is not an escape hatch: there is no path transform (`DependencyInjection.cs:132` adds only
`TrustedHeadersTransform`), so `/api/scoring/sessions/{id}/penalties` would arrive at the scoring
service verbatim and match no controller (`RankingController` and `PenaltiesController` are both
`[Route("api/sessions")]`). See Dependencies — **D1**.

---

## data-testid Contract

Single source of truth for selectors; the Phase 4 e2e and the AC→test map key off these. Phase column
= where each is introduced.

| testid | Element | Phase |
|---|---|---|
| `penalty-panel` | `PenaltyPanel` root `<section>` | P2 |
| `penalty-inactive` | not-live note (session not Active/Paused) | P2 |
| `penalty-team-select` | team `<select>` (no "all teams" option — a penalty targets exactly one team) | P2 |
| `penalty-reason-input` | reason `<textarea>` | P2 |
| `penalty-submit` | submit button (disabled while reason is blank) | P2 |
| `penalty-success` | applied-confirmation region (`role="status"`), renders `−{amount} pts` | P2 |
| `penalty-error` | rejection region (`role="alert"`) — 400 / 403 / transient | P2 |
| `team-progress-score-{teamId}` | **existing** (`OperatorTeamProgressPanel.tsx`) — the score the deduction must reach | P3 |

`team-progress-score-{teamId}` already exists and is already asserted by
`tests/e2e/session-operator-panel.spec.ts`; Phase 3 reuses it rather than minting a new selector.

---

## Phase 1 — Foundation (types + client + action)

**Scope:** add `ApplyPenaltyRequest` + `AppliedPenaltyDto` to `definitions.ts`, `applyPenalty` to
`app/lib/sessions.ts`, and `applyPenaltyAction` to `app/actions/sessions.ts`. No UI.

### `app/lib/definitions.ts` — append after the HU-28 operative-clue block

```ts
// --- HU-38 justified penalties ---
// Request of POST /api/sessions/{liveSessionId}/penalties (Operator + ownership Proxy).
// `reason` must be non-blank — the backend validator rejects whitespace-only with 400.
export type ApplyPenaltyRequest = {
  teamId: string // Guid — the penalized team; exactly one, no all-teams form
  reason: string // the justification; sent trimmed
}

// 201 response: evidence of the one append-only ScoreEntry deduction that was recorded.
// `penaltyAmount` is a POSITIVE magnitude (ScoreValue is non-negative by construction); the
// deduction is carried by the entry's Penalty type, which this DTO does not expose. Render it as
// a deduction; never treat it as a running total.
export type AppliedPenaltyDto = {
  scoreEntryId: string // Guid — the ledger entry
  teamId: string // Guid
  penaltyAmount: number // positive magnitude (currently always 100)
  reason: string
  appliedAt: string // ISO-8601
}
```

### `app/lib/sessions.ts` — add `applyPenalty`

Add the two types to the existing `import type { … } from './definitions'` block (lines 3–20), then
append the function. Mirrors `addOperativeClue`'s POST + status mapping. **201 is the success
status** — `response.ok` covers 200–299, so no special-casing is needed; do not test for `=== 200`.

```ts
// HU-38 operator justified penalty. Mirrors addOperativeClue's gateway POST + auth/status mapping.
// 403 = a non-owning operator: the scoring ownership Proxy denies (Administrator is unrestricted
// backend-side, but the action layer keeps this Operator-only). 400 = blank reason (the backend
// validator is authoritative even though the control disables submit). Success is 201 Created.
export async function applyPenalty(
  liveSessionId: string,
  body: ApplyPenaltyRequest,
): Promise<AppliedPenaltyDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/penalties`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ teamId: body.teamId, reason: body.reason }),
    },
  )

  if (response.status === 400) throw new Error('invalid_reason')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `applyPenalty failed with status ${response.status}`)
  }

  return response.json() as Promise<AppliedPenaltyDto>
}
```

Note on the 400 branch: the backend returns a ProblemDetails validation body, but this slice does
**not** parse it. Unlike `releaseClue`'s 409 (three distinct causes behind one status, disambiguated
by the `type` slug), the 400 here has exactly one reachable cause — a blank reason
(`PenaltyNotEligibleException` fires only on empty ids, which the validator already rejects first).
One cause ⇒ one typed error ⇒ one message. If a second 400 cause ever lands, branch on the
ProblemDetails `type` as `transitionSessionState` does.

### `app/actions/sessions.ts` — add `applyPenaltyAction`

Add `applyPenalty as applyPenaltyLib` to the existing `@/app/lib/sessions` import block (lines 5–19)
— ⚠️ **aliased**, or the exported action shadows the import and recurses into itself — and
`ApplyPenaltyRequest` / `AppliedPenaltyDto` to the `import type` block (lines 22–41).

```ts
// HU-38 operator justified penalty. Outcomes the control renders distinctly (never throws to the
// client):
//   { data }          → applied; result carries the appended ScoreEntry's id, magnitude, and appliedAt
//   { invalidReason } → 400: the backend rejected a blank/whitespace reason
//   { unauthorized }  → 403 non-owner / 401 / non-Operator → not-authorized state
//   { error }         → 404 / transient / unexpected → retryable error state
export async function applyPenaltyAction(
  liveSessionId: string,
  body: ApplyPenaltyRequest,
): Promise<
  | { data: AppliedPenaltyDto }
  | { invalidReason: true }
  | { unauthorized: true }
  | { error: string }
> {
  'use server'
  const session = await verifySession()
  if (session.role !== 'Operator') return { unauthorized: true }
  try {
    const data = await applyPenaltyLib(liveSessionId, body)
    return { data }
  } catch (error) {
    if (error instanceof IdentityError) {
      if (error.code === 'unauthorized') return { unauthorized: true }
      return { error: error.message }
    }
    if (error instanceof Error && error.message === 'invalid_reason') return { invalidReason: true }
    return { error: 'Could not apply the penalty. Try again.' }
  }
}
```

No `revalidatePath('/dashboard')`: the operator panel is read through `getOperatorSessionPanelAction`
on a client-side refresh loop, not through an RSC cache — `addOperativeClueAction` omits it for the
same reason. The refresh seam is Phase 3.

**Gate:** `npm run build` passes (typecheck included — there is no separate `typecheck` script;
`package.json` declares `build`/`lint`/`test`). `applyPenaltyAction` invoked from a non-Operator
session returns `{ unauthorized: true }` without a fetch. No UI change; no `penaltyAmount` arithmetic
anywhere.

---

## Phase 2 — Operator penalty control

**Scope:** new `app/dashboard/PenaltyPanel.tsx`, wired into the operator hero in `DashboardClient.tsx`
alongside `OperativeCluePanel`.

### `app/dashboard/PenaltyPanel.tsx` (new)

Client component; mirrors `OperativeCluePanel`'s shape (self-contained state, live gate, discriminated
result rendering). **Confirm against `frontend/.next-docs/server-and-client-components` before
writing.** Differences from `OperativeCluePanel`, each deliberate: **no all-teams option** (a penalty
targets exactly one team — the backend takes a single `teamId`), and **submit is disabled while the
reason is blank** (the reason-required gate).

```tsx
'use client'

import { useState, useTransition } from 'react'
import type { SessionLifecycleState } from '@/app/lib/definitions'
import { applyPenaltyAction } from '@/app/actions/sessions'
import styles from './dashboard.module.css'

type PenaltyTeamOption = { teamId: string; displayName: string }

// HU-38 operator justified-penalty control. Self-contained (own useState/useTransition), nested in
// the operator hero beside OperativeCluePanel. The applied penalty is an append-only ScoreEntry
// deduction: the success note confirms THE ENTRY (−amount), it is not a team total, and this
// component never does arithmetic on a score.
export function PenaltyPanel({
  liveSessionId,
  state,
  teams,
  onApplied,
}: {
  liveSessionId: string
  state: SessionLifecycleState
  teams: PenaltyTeamOption[]
  onApplied?: (teamId: string, amount: number) => void
}) {
  const [teamId, setTeamId] = useState('')
  const [reason, setReason] = useState('')
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  // Live gate: a penalty only makes sense against a running session. Mirrors OperativeCluePanel —
  // the UI hides a control that cannot work, but never pre-empts a backend rejection.
  const isLive = state === 'Active' || state === 'Paused'
  if (!isLive) {
    return (
      <section className={styles.cluePanel} data-testid="penalty-panel" aria-labelledby="penalty-title">
        <div className={styles.panelHeader}>
          <h2 id="penalty-title">Penalty</h2>
        </div>
        <p className={styles.panelMeta} data-testid="penalty-inactive">
          Penalties can be applied once the session is Active or Paused.
        </p>
      </section>
    )
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      setSuccess(null)
      const result = await applyPenaltyAction(liveSessionId, { teamId, reason: reason.trim() })
      if ('data' in result) {
        // penaltyAmount is a positive magnitude; the deduction is the entry's type. Render the sign.
        setSuccess(`Penalty applied: −${result.data.penaltyAmount} pts.`)
        setReason('')
        setTeamId('')
        onApplied?.(result.data.teamId, result.data.penaltyAmount)
      } else if ('invalidReason' in result) {
        setError('A penalty requires an explicit reason.')
      } else if ('unauthorized' in result) {
        setError('You are not authorized to penalize teams in this session.')
      } else {
        setError(result.error)
      }
    })
  }

  // Reason-required: the backend validator is authoritative, this is the UX gate in front of it.
  const canSubmit = reason.trim() !== '' && teamId !== '' && !isPending

  return (
    <section className={styles.cluePanel} data-testid="penalty-panel" aria-labelledby="penalty-title">
      <div className={styles.panelHeader}>
        <h2 id="penalty-title">Penalty</h2>
        <div className={styles.panelMeta}>Deduct points from a team, with a justification.</div>
      </div>

      <label className={styles.clueField}>
        <span className={styles.clueFieldLabel}>Team</span>
        {teams.length === 0 ? (
          <p className={styles.panelMeta}>No teams are attached to this session yet.</p>
        ) : (
          <select
            className={styles.clueControl}
            data-testid="penalty-team-select"
            value={teamId}
            onChange={(e) => setTeamId(e.target.value)}
          >
            <option value="">Select a team…</option>
            {teams.map((t) => (
              <option key={t.teamId} value={t.teamId}>
                {t.displayName}
              </option>
            ))}
          </select>
        )}
      </label>

      <label className={`${styles.clueField} ${styles.clueFieldFull}`}>
        <span className={styles.clueFieldLabel}>Reason</span>
        <textarea
          className={`${styles.clueControl} ${styles.clueTextarea}`}
          data-testid="penalty-reason-input"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="e.g. Team used a phone during a no-device substage."
          rows={3}
        />
      </label>

      <div className={styles.clueActions}>
        <button
          className={styles.primaryButton}
          data-testid="penalty-submit"
          disabled={!canSubmit}
          onClick={submit}
          type="button"
        >
          {isPending ? 'Applying…' : 'Apply penalty'}
        </button>
      </div>

      {success && (
        <p className={styles.panelMeta} role="status" data-testid="penalty-success">
          {success}
        </p>
      )}
      {error && (
        <p className={styles.errorBanner} role="alert" data-testid="penalty-error">
          {error}
        </p>
      )}
    </section>
  )
}
```

No new CSS: every class used (`cluePanel`, `panelHeader`, `panelMeta`, `clueField`, `clueFieldFull`,
`clueFieldLabel`, `clueControl`, `clueTextarea`, `clueActions`, `primaryButton`, `errorBanner`) is
already consumed by `OperativeCluePanel.tsx` from `dashboard.module.css`.

### `app/dashboard/DashboardClient.tsx` — wire it in

Import `PenaltyPanel` beside the existing `OperativeCluePanel` import (line 18), then add it inside
the `<div className={styles.cluePanelsRow}>` block, after `<OperativeCluePanel … />` (which closes at
~line 1221). `teams` is derived the same way both sibling panels derive it:

```tsx
<PenaltyPanel
  liveSessionId={selectedOperatorSession.liveSessionId}
  state={selectedOperatorState}
  teams={(operatorPanelState.panel?.teamProgress ?? []).map((t) => ({
    teamId: t.teamId,
    displayName: t.displayName,
  }))}
  onApplied={(teamId, amount) =>
    announce('Penalty applied', `−${amount} pts recorded for the selected team.`)
  }
/>
```

`onApplied` is announce-only in P2 — the panel refresh it should also trigger is Phase 3, and is
blocked. (`teamId` is threaded now so the Phase 3 wiring is an edit, not a signature change.)

**Gate:** `npm run build` + `npm run lint` pass. As an Operator on a live session, the control renders
with a team picker and a reason field; submit is **disabled** while the reason is blank or no team is
selected. On a non-live session the inactive note renders instead. Non-operators never see the panel
(it is inside the operator hero branch, and the action rejects them regardless). No client-side score
arithmetic. **The apply path itself cannot be exercised end-to-end yet — D1/D2 below.**

---

## Phase 3 — Score reflection in the operator view ⛔ BLOCKED

**Blocked on D2 + D3.** Per the concreteness rule, no code is written for a blocked increment. Held at
contract + gate altitude.

**Scope (when unblocked):** after a successful apply, re-read the authoritative snapshot through the
existing `loadOperatorPanel` seam (`DashboardClient.tsx:553`) so `team-progress-score-{teamId}`
reflects the deduction. Thread a `refreshPanel`-style callback into `PenaltyPanel.onApplied` instead
of the announce-only call. **No client-side decrement of `team.score`** — the refresh reads the
ledger-derived value, which is the whole point of the slice gate.

**Why it is blocked — no read surface currently reflects a penalty:**

| Surface | Reflects the deduction? | Evidence |
|---|---|---|
| `OperatorSessionPanelDto.teamProgress[].score` | **No** | It is session-operations' `Team.CurrentScore` (`definitions.ts:479`). No session-operations code references `ScoreEntryRegistered`/`PenaltyApplied`/`ScoreEntryRecorded` — the ledger never reaches it. |
| `GET /api/sessions/{id}/ranking` → `RankingRowDto.TotalScore` | **No — it moves the wrong way** | `Ranking.Refresh` folds `TotalScore: group.Sum(entry => entry.ScoreValue.Value)`, ignoring `EntryType`. `ScoreValue` is non-negative ⇒ a Penalty of 100 **adds 100** to the total instead of deducting it. |

So the slice requirement "reflect the resulting score change (deduction) in the operator view" is not
satisfiable by any endpoint that exists today. Which surface it should read is a **product + backend
decision**, not a frontend one (Q1 below) — writing a client against either surface now would ship a
UI that displays a wrong number.

**Gate (when unblocked):** applying a penalty to a team on a live session updates that team's
`team-progress-score-{teamId}` to the ledger-derived value on the next snapshot read; the value comes
from a re-read, never from client arithmetic; two penalties in a row deduct twice (append-only, no
clobber); `npm run build` passes.

---

## Phase 4 — E2E ⛔ BLOCKED

**Blocked on D1 + D2** (and, for the score assertion, D3).

**Scope (when unblocked):** Playwright spec `tests/e2e/session-penalty.spec.ts`, using the existing
`operatorPage` fixture (`tests/fixtures/auth.ts` — it extends `operatorPage` and `adminPage` only; a
**non-owning** operator fixture must be added, since no current fixture models one) and the
`data-testid` contract above. It follows the manual-seed pattern of the sibling operator specs
(`session-clue-release-manual-seed.spec.ts`, `session-operative-clue.spec.ts`) — a live session with
attached teams is a precondition the spec cannot create from the dashboard alone.

**Test names (bodies land with the phase, not invented here):**

```ts
// happy path
test('assigned operator applies a justified penalty to a team', ...)              // AC1, AC2, AC3
test('the applied penalty is confirmed as a deduction, not a total', ...)         // AC5
// reason-required
test('submit is disabled until a non-blank reason is entered', ...)               // AC2
test('a whitespace-only reason is rejected by the backend as 400', ...)           // AC2
// authorization
test('a non-owning operator is rejected with the not-authorized state', ...)      // AC4
test('a non-live session shows the inactive note instead of the control', ...)    // AC6
// score reflection (Phase 3)
test('the team score reflects the deduction after the next snapshot read', ...)   // AC5
```

**Gate (when unblocked):** `npx playwright test session-penalty` green; the flow exercises a real
apply against the real API contract — **including** the reason-required (400) and non-owner (403)
rejection paths; `npm run build` + `npm run lint` + `npm test` pass; no UI treats the penalty as a
mutable total outside the ledger.

---

## Acceptance Criteria → Test mapping

HU-38 criteria from `backend/docs/hu38-brief.md` §Acceptance criteria, narrowed to the frontend slice
("an operator action to apply a justified penalty (reason required) to a team in a supervised session
and see the score reflect the deduction; no ranking/audit/history").

| # | Acceptance criterion | Covered by | Phase | Status |
|---|---|---|---|---|
| AC1 | The operator can register a penalty against a team of an assigned session | `assigned operator applies a justified penalty` | P4 | ⛔ blocked (D1, D2) |
| AC2 | Every penalty requires an explicit reason | `submit is disabled until a non-blank reason`, `whitespace-only reason … 400` | P2 / P4 | ✅ UI gate (P2) · ⛔ 400 path (D1) |
| AC3 | The system records `appliedAt` + `appliedByUserId` | `assigned operator applies …` (asserts `appliedAt` echoed in the confirmation) | P4 | ⛔ blocked (D1, D2). `appliedByUserId` is backend-derived from the token and is **not** in `AppliedPenaltyDto` — not frontend-assertable |
| AC4 | Penalties are not exposed to non-operators / non-owned sessions | `a non-owning operator is rejected …` + the action's Operator guard | P1 / P4 | ✅ action guard (P1) · ⛔ 403 path (D1, D2) |
| AC5 | The penalty impacts the score through an append-only `ScoreEntry` deduction, never a mutable total | `confirmed as a deduction, not a total`, `team score reflects the deduction …` | P2 / P3 / P4 | ✅ no-mutable-total (P2) · ⛔ reflection (D3) |
| AC6 | The control is unavailable off a live session | `a non-live session shows the inactive note` | P2 / P4 | ✅ rendered (P2) · ⛔ asserted (D1) |

**The slice gate is not met by Phases 1–2 alone.** "The flow exercises applying a justified penalty
against the real API contract, including the reason-required and non-owner rejection paths" requires
D1 and D2 resolved; "reflect the resulting score change (deduction) in the operator view" requires D3.
Phases 1–2 satisfy the build/typecheck gate and the no-mutable-total gate, and are correct against the
merged contract — but shipping them alone means shipping a control whose submit always fails. Resolve
D1/D2 before Phase 2 merges to a demo branch, or land Phases 1–2 behind the existing live-session gate
knowing the apply path 404s.

---

## Commit Sequence

- `feat(frontend): penalty types, gateway client + operator-gated action - HU-38` (P1)
- `feat(frontend): operator justified-penalty control - HU-38` (P2)
- `feat(frontend): reflect penalty deduction in the operator panel - HU-38` (P3, blocked)
- `test(frontend): e2e operator justified penalty - HU-38` (P4, blocked)

Each carries: `Ref: HU-38` / `Ref: DES-53` / `Ref: DES-85`.

---

## Open Questions / Dependencies

Blockers first. Each is a **backend/gateway change this step must not make** (`do not modify backend
code`), evidenced against source.

- **D1 — the gateway does not route `/api/sessions/{id}/penalties` (blocks every runtime path).**
  `appsettings.Development.json` + `appsettings.json` route `/api/sessions/{liveSessionId}/ranking` →
  `scoring` and `/api/sessions/{**catch-all}` → `session-ops`; penalties matches only the catch-all ⇒
  session-operations ⇒ 404. Commit `141cf8b` did not touch `backend/api-gateway/`. **Fix shape:** a
  `scoring-penalties` route (`Path: /api/sessions/{liveSessionId}/penalties`, `ClusterId: scoring`,
  `AuthorizationPolicy: default`) in **both** appsettings files — YARP prefers the more specific match
  over the catch-all, exactly as `scoring-ranking` already does. Until this lands, Phase 1's client
  cannot be exercised and Phases 3–4 cannot start.
- **D2 — the ownership Proxy denies *every* Operator (blocks the AC1 happy path *and* makes the AC4
  403 path pass for the wrong reason).** Two independent faults:
  1. **Nobody publishes the event that feeds the projection.** Scoring's
     `LiveSessionOperatorAssignedConsumer` populates the `SessionOperatorAssignmentProjection` that
     `ScoringSessionAuthorizationProxy` reads, keyed on
     `[EntityName("session-operator-assigned")]`. Session-operations publishes eight integration
     events (`session-answer-registered`, `session-state-changed`, `session-target-resolved`, …) —
     **none is `session-operator-assigned`**. `LiveSessionOperatorAssignedEvent` is referenced only by
     `LiveSession.cs` and its own file: it is a domain event with no publisher. ⇒
     `GetAssignedOperatorUserIdAsync` returns `null` ⇒ `ForbiddenAccessException` for **every**
     Operator. Only an `Administrator` (unrestricted, early return) can currently apply a penalty —
     and this slice deliberately does not expose that path (AD 3).
  2. **The operator id types do not match.** Session-operations'
     `LiveSessionOperatorAssignedEvent.AssignedOperatorUserId` is `int?` (the identity numeric user id
     — `assignSessionOperator(liveSessionId, operatorUserId: number)` in `app/lib/sessions.ts:103`
     confirms the frontend speaks the same `int`). Scoring's
     `LiveSessionOperatorAssignedIntegrationEvent.AssignedOperatorUserId` is `Guid`, and the proxy
     compares it against `Guid.Parse(_currentUser.Id)` — the Keycloak `sub`. Even once published,
     an `int` identity id and a Keycloak `sub` Guid never match. **Which id is canonical for session
     ownership across these two contexts is a backend/context-map decision** — flag it, do not paper
     over it frontend-side.
- **D3 — no read surface reflects the deduction (blocks Phase 3 and AC5's reflection half).** See the
  Phase 3 table: the operator panel's `score` is session-owned and no scoring event reaches it; the
  ranking fold sums `ScoreValue.Value` ignoring `EntryType`, so a penalty **raises** the ranking total
  by its magnitude. Both are backend defects.
- **Q1 — which surface should the operator's post-penalty score read?** Options: (a) session-operations
  consumes `ScoreEntryRegistered`/`PenaltyApplied` and folds it into `Team.CurrentScore`, keeping the
  operator panel the single read (smallest frontend change — Phase 3 becomes a `loadOperatorPanel`
  call); (b) the frontend adds a ranking client and reads scoring's ledger-derived total directly
  (crosses a bounded context in the UI, and the brief's frontend slice explicitly excludes ranking);
  (c) scoring exposes a per-team ledger total endpoint (none exists). **Resolve before Phase 3.**
  Whichever is chosen, D3's ranking-fold sign bug must be fixed for (b)/(c) to be correct.
- **Q2 — is `penaltyAmount` meant to be operator-chosen?** It is hardcoded
  (`BasePenaltyMagnitude = ScoreValue.Create(100)` in `ApplyPenaltyCommandHandler`), and
  `ApplyPenaltyCommand` has no amount field, so the control offers no amount input. If a future HU
  makes the magnitude a policy or an input, the request type and the control both change.
- **Q3 — a non-owning-operator e2e fixture does not exist.** `tests/fixtures/auth.ts` extends only
  `operatorPage` and `adminPage`. Phase 4's AC4 test needs an operator who is *not* the session's
  assigned operator; the fixture shape follows `hu-03`'s `participantPage` addition. Blocked behind D2
  regardless (today *every* operator is non-owning as far as the proxy is concerned).

---

## Out of Scope

- **Any backend or gateway change** — including D1's route, D2's publisher/id alignment, and D3's
  ranking-fold sign bug. This step is frontend-only ("do not modify backend code in this step"); each
  is filed above with its fix shape so the backend HU can pick it up.
- **Administrator penalty application.** The backend policy admits `Administrator` (unrestricted, no
  ownership check); this slice is operator-facing only (AD 3).
- **Ranking, audit, and penalty history surfaces** — later HUs, per the brief's frontend slice ("no
  ranking/audit/history").
- **Penalty reversal / editing.** The ledger is append-only by construction; there is no reversal
  endpoint in the contract.
- **Mobile participant surface.** The penalized team seeing its own deduction is not this slice; the
  brief scopes HU-38's frontend to the operator **web** surface.
- **Realtime push of the deduction.** Scoring has a `/hubs/scoring` broadcaster
  (`RankingBroadcaster`), but this slice reads through the existing snapshot seam; wiring the scoring
  hub into the dashboard is separate work.
