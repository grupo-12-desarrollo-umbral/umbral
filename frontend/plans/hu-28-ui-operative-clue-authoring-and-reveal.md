# HU-28 — UI: Operative-Clue Authoring (operator web) + Reveal (participant mobile)

**Ref:** HU-28
**Branch:** feature/hu-28-operative-clues (backend X.1–X.4 already committed; **P0 backend prerequisites
— team-scoped live push + clue-id projection — owned by
`backend/plans/hu-28-operative-clue-live-push-and-id-projection.md`**)
**Date:** 2026-07-14
**Builds on:** HU-19 operator assignment (`AssignedOperatorUserId` ownership seam), HU-21A lifecycle
(`selectedOperatorState`), HU-24A operator live-session panel (`teamProgress` — the team list this
control reuses), **HU-26 operator clue release** (`OperatorClueReleasePanel`, `releaseClue` /
`releaseClueAction` — the *exact* house shape this slice mirrors), and HU-23 participant team board
(`useTeamBoard`, `TreasureHuntBoard`, `ParticipantTeamBoardDto`).

> ⚠️ **Read first:** `frontend/AGENTS.md` — "This is NOT the Next.js you know." For the participant
> phase, `mobile/AGENTS.md` — "Expo HAS CHANGED." Before writing code, read the relevant guides
> (`frontend/.next-docs/` server-and-client-components/mutating-data/forms; Expo v56 docs). Do not
> rely on remembered conventions.

> **Execution shape — two independent UI tracks that run in parallel**, gated only by the backend:
>
> - **P0 — Backend prerequisites** (separate plan: `backend/plans/hu-28-operative-clue-live-push-and-id-projection.md`).
>   Team-scoped live push + `operativeClueId` projection. **Lands first.**
> - **Track A — Operator Web (`frontend/`):** **A1** types → **A2** client fn + server action → **A3**
>   operator authoring control + DashboardClient wiring → **A4** web tests (unit + e2e).
> - **Track B — Participant Mobile (`mobile/`):** **B1** treasure-hunt reveal → **B2** trivia surfacing
>   (gated) → **B3** mobile tests.
>
> **Why the tracks parallelize:** they share **no files** and **no runtime dependency on each other** —
> Track A is the operator POST surface, Track B is the participant board reader (driven by backend
> pushes, never by the web UI). The only fan-in is the backend. Concretely:
>   - **Track A is ready now** — it needs only the landed authoring backend (X.1–X.4), *not* P0.
>   - **Track B wants P0 first** for its clean form (real `operativeClueId` key + live delivery). **B1
>     degrades gracefully without P0** (falls back to an `op:{index}` key + refetch-only reveal), so it
>     can start in parallel and tighten once P0 lands; **B2**'s design is settled
>     (`mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`), so it then needs only P0's live push.
>
> Each phase is an end-to-end runnable slice and its own commit.
>
> **Altitude:** Track A is a **hu-26-shaped operator surface** (one POST endpoint) — **A1–A3 are
> code-complete**, mirroring the landed `releaseClue` / `OperatorClueReleasePanel` almost verbatim.
> **B1 is also code-complete** (the backend surfaces operative clues through the **existing
> `visibleClues` board projection** and the exact mobile component is read below) — it carries one
> genuine wrinkle (operative clues have *null* target fields, which the current mobile type/render/keys
> mishandle). **B2** — surfacing during a **Trivia** substage, where the mobile view renders no clue
> list — is a **persistent, reviewable clue affordance with a transient toast as its arrival signal**
> (revised from toast-only — a toast alone loses the guidance if missed); both are **new components
> needing a design pass**, so B2 is kept at **contract + gate altitude**, not a verbatim skeleton. Live
> delivery is guaranteed by the **P0 push** (no longer refetch-gated).

---

## Context

HU-28 is the operator's second **write** over the runtime clue model (after HU-26 clue release). An
operator **authors a free-text operative clue** and **assigns it to one, several, or all teams** during
a **live** session (**Active or Paused**). Unlike HU-26's *release* — which reveals a treasure-hunt
`Target`'s pre-authored hidden clue tied to a target — an operative clue is **operator-authored free
text with no target**. It is **guidance, not progress**: authoring it does **not** advance the
substage, resolve a target, or change score.

**This slice spans two apps:**
- **Operator surface = web (`frontend/`).** A new authoring control on the operator session view. This
  is the near-identical sibling of HU-26's `OperatorClueReleasePanel`; the differences are: (a) a
  **free-text** input instead of a target-UUID input, (b) **multi-team** selection (one / several /
  all) instead of one-or-all, and (c) an **Active-or-Paused** gate instead of Active-only.
- **Participant surface = mobile (`mobile/`).** The operative clue arrives on the participant board
  inside the **existing** `board.visibleClues[]` (backend already merges it, team-scoped). The mobile
  board already renders `visibleClues` on its Clues tab — but as a **target** clue (`targetName` label
  over `clueText`). An operative clue has **`targetSnapshotId: null` / `targetName: null`**, which the
  current mobile `VisibleClueDto` type declares non-null and which breaks the card label and the
  `targetSnapshotId`-keyed React keys / new-clue tracking. Track B (mobile) fixes exactly that.

**Every operator seam this needs already exists** (verified against source):
- `app/lib/sessions.ts` — `API_GATEWAY_URL`, the private `getGatewayHeaders()`, `verifySession`, and
  the landed `releaseClue` (`sessions.ts:290`) whose **POST + 409-ProblemDetails-`detail` parse** this
  slice mirrors.
- `app/actions/sessions.ts` — `releaseClueAction`'s Operator-gated discriminated-union shape.
- `app/dashboard/DashboardClient.tsx` — the operator hero (`role === 'operator' && selectedOperatorSession
  && selectedOperatorState`, `data-testid="operator-panel"`, `:1094`), `selectedOperatorState`
  (`SessionLifecycleState`, `:459`), the already-loaded `operatorPanelState.panel?.teamProgress` (the
  team list, `:1156`), the `announce(title, body)` helper (`:715`), and the **live wiring of
  `OperatorClueReleasePanel`** (`:1154`) this control renders next to.

**Every participant seam already exists** (verified): `useTeamBoard` applies pushes wholesale via
`setBoard`, filtered by `liveSessionId` only (server-side `team:{teamId}` group scopes cross-team
isolation); `TreasureHuntBoard`'s Clues tab maps `board.visibleClues`; `team-space.tsx` feeds
`visibleClues={board.visibleClues}` in its `playMode === 'TreasureHunt'` branch.

---

## Verified Backend Contract

Anchored to the landed backend on this branch: `SessionsController.cs`
(`[HttpPost("{liveSessionId:guid}/operative-clues")]` at L339, `[Authorize(Policy = Operator)]`,
request record `AddOperativeClueRequest(string ClueText, IReadOnlyList<Guid> TeamIds)` at L384),
`AddOperativeClueResultDto.cs`, `AddOperativeClueCommandValidator.cs`, the three domain exceptions,
and `AddOperativeClueEndpointTests.cs` (asserted status codes + ProblemDetails titles). HTTP
serializes **camelCase** (ASP.NET default).

| Endpoint | Auth | Request | 200 body | Errors |
|---|---|---|---|---|
| `POST /api/sessions/{liveSessionId}/operative-clues` | `[Authorize(Policy=Operator)]` **+** ownership (non-owning operator → 403) | `{ clueText: string, teamIds: string[] /*Guid[]; ≥1*/ }` | `AddOperativeClueResultDto` | `401` no trusted headers · `403` non-Operator **or non-owning operator** (ProblemDetails `title:"Forbidden."`) · `400` empty `clueText` / `clueText` > 500 chars / empty `teamIds` (`title:"Validation failed."`) · `409` session not Active/Paused (`title:"Conflict."`) · `404` session not found (`title:"Resource not found."`) |

**`AddOperativeClueResultDto`** (backend record → camelCase JSON):

```ts
AddOperativeClueResultDto {
  operativeClueIds: string[]   // Guid[] — one id per (clue × team) row created; length === assignedTeamIds.length
  assignedTeamIds: string[]    // Guid[] — the teams the clue was assigned to (echoes the request teamIds)
  clueText: string             // echoes the authored text
}
```

**409 cause** — a **single** conflict, unlike HU-26's three. `SessionNotLiveForOperativeClueException`
(`ErrorCategory.Conflict` → 409), message *"Adding an operative clue requires an Active or Paused live
session. Current state is '{state}'."* Parse `detail` verbatim (mirroring `releaseClue`); match the
lower-cased substring **`"requires an active or paused"`** to distinguish it from any future 409.

**Contract notes that drive the render:**
- **"All teams" = send every team id.** The command validator rejects empty `teamIds` (400), so there
  is **no omit-for-all** signal here (contrast HU-26, where an omitted `teamId` meant all). The control
  builds the full team-id array explicitly when "all" is chosen.
- **`clueText` max length is 500** (validator `MaximumClueTextLength`). The control caps the input and
  disables submit past it; the backend still enforces (400).
- **403 covers both** non-Operator role *and* non-owning operator — both map to the same not-authorized
  state (integration test `AddOperativeClue_ByNonOwningOperator_ReturnsForbiddenProblemDetails`).
- **No progress side effect.** Integration test
  `AddOperativeClue_ToOneTeam_ReturnsOkAndOnlyAssignedTeamCanSeeItWithoutAdvancing` asserts the
  assigned team's board gains the clue while `activeSubstage.resolvedTargets` **stays 0** — authoring
  advances nothing.

### Participant board projection (read path — reused, one type drift to fix)

Verified `ParticipantTeamBoardDto.cs` and `ParticipantTeamBoardDtoFactory.cs`: operative clues surface
through the **existing** `visibleClues[]`, and the backend `VisibleClueDto` is now:

```
VisibleClueDto( Guid? TargetSnapshotId, string ClueText, string? TargetName )   // target fields NULLABLE
```

An operative clue is a `VisibleClueDto` with `targetSnapshotId: null` and `targetName: null` — only
`clueText` is populated. Team-scoping is **server-enforced** (integration test: `assignedBoard`
contains the clue text, `unassignedBoard` does not). The mobile type
(`mobile/src/lib/realtime/team-board-types.ts:7-11`) still declares both target fields as non-null
`string` — **stale as of this backend**; Track B (B1) widens it. The REST snapshot
(`GET /api/sessions/{liveSessionId}/participants/team-board?teamId=…`) and the `TeamBoardUpdated` push
are unchanged and reused as-is.

---

## Architecture Decisions

1. **Mirror `releaseClue` for the client fn.** House POST-with-ProblemDetails shape:
   `verifySession` → `fetch(POST, getGatewayHeaders({'Content-Type':'application/json'}))` → status
   ladder → `409` parses `detail` and throws a distinct typed `Error`. HU-28 has **one** 409 cause, so
   the ladder is simpler than HU-26's three-way parse.
2. **Server action is a discriminated union the control renders inline** — `{ data } | { notLive } |
   { unauthorized } | { error }`. Modeled on `releaseClueAction`. Genuine auth failures
   (`unauthorized`) are kept separate from transient ones (`error`) so a momentary blip never tells a
   legitimately-assigned operator they are "not authorized." Non-Operator role short-circuits to
   `{ unauthorized }` before the gateway.
3. **New component `OperativeCluePanel`, owning its own local state** (`useState` + `useTransition`),
   nested in the operator hero right after `OperatorClueReleasePanel`. No reducer, no DashboardClient
   state seam — like HU-26 this is a fire-and-forget mutation whose only durable effect (the board
   reveal) lands on the **participant** surface. Mirrors HU-26's self-contained control.
4. **The live gate is a render gate over `Active` *or* `Paused`.** The control renders its inactive
   note (no submit) unless `selectedOperatorState === 'Active' || selectedOperatorState === 'Paused'`.
   The backend also enforces it (409), so a stale-state race still surfaces non-crashingly as the
   `notLive` branch — the UI never pre-empts the backend, it just avoids offering a control that can't
   work.
5. **Multi-team selection via checkboxes + a "select all" toggle** (not `<select multiple>`, which is
   poor web UX and awkward to test). Options come from the already-loaded
   `operatorPanelState.panel?.teamProgress` — **no new fetch** (same source HU-26 uses). "Select all"
   assigns every listed team id (Architecture-forced by the no-omit contract). Submit is disabled
   unless `clueText.trim()` is non-empty **and** ≥1 team is selected — pre-empting the two 400 causes
   without inventing validation copy the backend owns.
6. **No `revalidatePath` after authoring.** Authoring mutates no RSC-cached dashboard read (the
   operator panel is fetched client-side; the reveal is a board re-projection to the participant).
   Revalidating `/dashboard` would be no-op churn — omitted deliberately (mirrors HU-26 Decision 6).
7. **Participant reveal reuses the existing board path; the only mobile production change is null-target
   handling.** Widen the mobile `VisibleClueDto` target fields to nullable; render an operative clue
   (null `targetName`) with an **"OPERATIVE CLUE"** label instead of a target name; and derive a stable
   list key `targetSnapshotId ?? operativeClueId` for the React `key` and the new-clue `seenClueIds` set
   (which today key on `targetSnapshotId` and would collide/duplicate on `null`). The `operativeClueId`
   comes from **P0** (projected onto `VisibleClueDto`) — a real, stable id, not the earlier
   `op:{index}:{clueText}` fallback. No new invoke, group, REST call, or mutation on mobile —
   participants never author.
8. **No clue-as-progress, no substage advance from the UI, either surface.** The operator control
   issues only the authoring POST and reports the assigned team count; the participant board renders the
   clue as guidance under its Clues tab — target progress stays `resolvedTargets / totalActiveTargets`,
   untouched.
9. **Trivia-substage surfacing = a persistent, reviewable clue affordance + a transient toast as its
   arrival signal** (revised from toast-only). The mobile trivia view (`team-space.tsx`
   non-treasure-hunt branch) has no Clues tab, so operative clues need a home there. A toast *alone* is
   fragile: if the participant misses it (app backgrounded, glance away, auto-dismiss), the guidance is
   gone with nowhere to review it. So B2 adds **both**: (a) a persistent, dismissible clue affordance
   in the trivia branch (a small clues drawer/badge listing the team's operative clues — the durable
   store), and (b) an `OperativeClueToast` that fires as the *arrival signal* when a new operative clue
   enters `board.visibleClues`. Both are **new components** (no toast/snackbar or trivia clue list
   exists in `mobile/src/components` today, verified) whose placement/animation/dismiss are **settled in
   `mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`** — B2 stays at contract + gate altitude here
   (the component code lands in that phase). The **trigger** reuses the
   exact prop-diff pattern `TreasureHuntBoard` already uses for its new-clue dot (a `useRef` seen-set
   over the null-safe `clueKey`, filtered to operative clues, i.e. `targetSnapshotId == null`). **Live
   delivery is guaranteed by P0** — the operative-clue push means the board updates the instant the
   operator assigns, so both the toast and the persistent list populate live (no reconnect/refetch
   dependence). B1 (treasure-hunt Clues tab) also benefits: instant reveal instead of next-fetch.

---

## Environment

**No new environment variables.** Reuses (verified against source):

- **Web:** `API_GATEWAY_URL` (`app/lib/sessions.ts:22` — `process.env.API_GATEWAY_URL!`) for the
  authoring POST, via `getGatewayHeaders()` (Bearer from `getValidAccessToken()`). `getGatewayHeaders`
  is **private to `sessions.ts`** (not exported) — the new client fn lives in the **same file**, so it
  calls it directly (no import/replication), exactly as `releaseClue` does.
- **Mobile:** existing hub connection + `team:{teamId}` group membership (from HU-07B `ReconnectAsync`)
  and `getParticipantTeamBoard` REST config (HU-23) — unchanged. `constants/theme.ts` tokens only
  (`colors.emberAccent…`, already imported in `treasure-hunt-board.tsx`); no new tokens.

---

## data-testid contract (web operator control)

Single source of truth for web selectors; the A4 unit/e2e tests and the AC→test map key off these. The
control nests inside the existing operator hero (`data-testid="operator-panel"`). **Mobile adds no
testIDs** — mirroring HU-26's participant convention, the Track B tests query by rendered clue text and
the `"OPERATIVE CLUE"` label string (see testID note in B1).

| testid | Element | Phase |
|---|---|---|
| `operative-clue-panel` | `OperativeCluePanel` root section | A3 |
| `operative-clue-inactive` | inactive note when `state` ∉ {Active, Paused} (no submit rendered) | A3 |
| `operative-clue-text-input` | free-text clue `<textarea>` (maxLength 500) | A3 |
| `operative-clue-select-all` | "assign to all teams" toggle | A3 |
| `operative-clue-team-checkbox-{teamId}` | per-team assignment checkbox | A3 |
| `operative-clue-submit` | Assign button | A3 |
| `operative-clue-success` | success note (`role="status"`) — "Assigned to N team(s)" | A3 |
| `operative-clue-error` | error/conflict note (`role="alert"`) — not-live / unauthorized / transient | A3 |

---

## Phase 0 — Backend prerequisites (separate plan)

Two small backend changes land first on this branch, owned by
**`backend/plans/hu-28-operative-clue-live-push-and-id-projection.md`** — not implemented here (the
frontend only communicates via the api-gateway). This slice **consumes** their contract:

- **P0.1 — live per-team push.** `BroadcastTeamBoardNotificationHandler` gains an
  `OperativeClueAddedEvent` handler that re-projects + pushes **only the assigned team's** board over the
  existing `TeamBoardUpdated`. **Contract consumed:** authoring an operative clue delivers it to the
  assigned team's board **live** (no reconnect/refetch needed). B1 becomes instant; B2 becomes
  live-useful.
- **P0.2 — clue id on the board.** `VisibleClueDto` gains `Guid? OperativeClueId` (camelCase
  `operativeClueId`); operative clues carry it non-null, target clues null. **Contract consumed:** the
  mobile board keys on `targetSnapshotId ?? operativeClueId` (B1) — a real, stable id, replacing the
  earlier `op:{index}:{clueText}` fallback.

**Gate seam:** both tracks assume this contract. If P0 has not landed, B1 keying falls back to
`op:{index}:{clueText}` and reveal is refetch-only — degraded but non-crashing. Do not re-derive the
backend detail here; see the backend plan.

> **P0 tests** (single-team broadcast, `operativeClueId` projection/factory, extended
> `AddOperativeClue_ToOneTeam_…` integration assertion) live in the backend plan — not repeated in the
> UI tracks below.

---

## ▶ Track A — Operator Web (`frontend/`)

*Runs in parallel with Track B; shares no files with it. **Ready now** — needs only the landed
authoring backend (X.1–X.4), **not P0**. This is a **hu-26-shaped operator surface** (one POST
endpoint), so A1–A3 are code-complete, mirroring the landed `releaseClue` / `OperatorClueReleasePanel`.*

## A1 — Types (`app/lib/definitions.ts`)

**Scope:** add the request + result DTOs. No behaviour change; A2–A3 consume them.

```ts
// --- HU-28 operator operative-clue authoring ---
// Request of POST /api/sessions/{liveSessionId}/operative-clues (Operator + ownership).
// teamIds must be non-empty; "all teams" sends every team id (no omit-for-all signal here).
export type AddOperativeClueRequest = {
  clueText: string          // free text, 1..500 chars (backend validator)
  teamIds: string[]         // Guid[] — one, several, or all; must contain ≥1
}

// 200 response: the created clue rows + the teams the clue is now assigned to + the echoed text.
export type AddOperativeClueResultDto = {
  operativeClueIds: string[] // Guid[] — one id per (clue × team); length === assignedTeamIds.length
  assignedTeamIds: string[]  // Guid[] — teams the clue was assigned to
  clueText: string
}
```

**Gate:** `pnpm build` / typecheck passes. No runtime change.

---

## A2 — Client fn + Server Action (`app/lib/sessions.ts`, `app/actions/sessions.ts`)

**Scope:** add `addOperativeClue` to `app/lib/sessions.ts` and `addOperativeClueAction` to
`app/actions/sessions.ts`. Both mirror the landed `releaseClue` / `releaseClueAction` shapes.

### `app/lib/sessions.ts` — add (mirrors `releaseClue`'s POST + 409-`detail` parse; single 409 cause)

Add `AddOperativeClueRequest, AddOperativeClueResultDto` to the `./definitions` type import, then:

```ts
// HU-28 operator operative-clue authoring. Mirrors releaseClue's POST + 409-ProblemDetails-detail parse.
// 403 = non-Operator or a non-owning operator (ownership denies). The single 409 (session not Active/
// Paused) is surfaced as a distinct typed error the control renders non-crashingly. teamIds is sent
// as-is (already non-empty; "all teams" is the full id list assembled by the caller).
export async function addOperativeClue(
  liveSessionId: string,
  body: AddOperativeClueRequest,
): Promise<AddOperativeClueResultDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/operative-clues`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ clueText: body.clueText, teamIds: body.teamIds }),
    },
  )

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Not the assigned operator.')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 409) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null
    const detail = problem?.detail?.toLowerCase() ?? ''
    if (detail.includes('requires an active or paused')) throw new Error('not_live')
    throw new Error('clue_conflict')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `addOperativeClue failed with status ${response.status}`)
  }

  return response.json() as Promise<AddOperativeClueResultDto>
}
```

### `app/actions/sessions.ts` — add (Operator-gated discriminated union)

Add the aliased lib import (`addOperativeClue as addOperativeClueLib`) and the `AddOperativeClueRequest,
AddOperativeClueResultDto` type imports, then:

```ts
// HU-28 operator operative-clue authoring. Outcomes the control renders distinctly (never throws to
// the client):
//   { data }         → assigned; result.assignedTeamIds lists the team(s) the clue is now visible to
//   { notLive }      → 409: session is not Active or Paused
//   { unauthorized } → 403 non-owner / 401 / non-Operator → not-authorized state
//   { error }        → 400 / 404 / transient / unexpected → retryable error state
export async function addOperativeClueAction(
  liveSessionId: string,
  body: AddOperativeClueRequest,
): Promise<
  | { data: AddOperativeClueResultDto }
  | { notLive: true }
  | { unauthorized: true }
  | { error: string }
> {
  'use server'
  const session = await verifySession()
  if (session.role !== 'Operator') return { unauthorized: true }
  try {
    const data = await addOperativeClueLib(liveSessionId, body)
    return { data }
  } catch (error) {
    if (error instanceof IdentityError) {
      if (error.code === 'unauthorized') return { unauthorized: true }
      return { error: error.message }
    }
    if (error instanceof Error && error.message === 'not_live') return { notLive: true }
    return { error: 'Could not assign the operative clue. Try again.' }
  }
}
```

**Gate:** `pnpm build` passes. Calling the action from a non-Operator session returns `{ unauthorized }`
without reaching the gateway; a `403` maps to `{ unauthorized }`, the `409` not-live cause to
`{ notLive }`, `400`/`404`/transient to `{ error }`.

---

## A3 — Operator authoring control + DashboardClient wiring

**Scope:** add `OperativeCluePanel.tsx` and nest it in the operator hero after
`OperatorClueReleasePanel`. Team options come from the already-loaded operator-panel `teamProgress`;
the Active-or-Paused gate is a render gate. All CSS classes below are **verified present** in
`app/dashboard/dashboard.module.css` (reused from HU-26: `cluePanel`, `panelHeader`, `panelMeta`,
`fieldLabel`, `inlineInput`, `primaryButton`, `errorBanner`, `inlineButton`).

### `app/dashboard/OperativeCluePanel.tsx` (new) — self-contained control (mirrors `OperatorClueReleasePanel`)

```tsx
'use client'

import { useState, useTransition } from 'react'
import type { SessionLifecycleState } from '@/app/lib/definitions'
import { addOperativeClueAction } from '@/app/actions/sessions'
import styles from './dashboard.module.css'

const MAX_CLUE_LENGTH = 500 // backend validator: AddOperativeClueCommandValidator.MaximumClueTextLength

type OperativeClueTeamOption = { teamId: string; displayName: string }

export function OperativeCluePanel({
  liveSessionId,
  state,
  teams,
  onAdded,
}: {
  liveSessionId: string
  state: SessionLifecycleState
  teams: OperativeClueTeamOption[]
  onAdded?: (count: number) => void
}) {
  const [clueText, setClueText] = useState('')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  // Live gate (Architecture Decision 4): available in Active OR Paused. The backend also enforces it,
  // so a stale race still surfaces as the notLive branch below — the UI never pre-empts, it just hides
  // a control that cannot work off a non-live session.
  const isLive = state === 'Active' || state === 'Paused'
  if (!isLive) {
    return (
      <section className={styles.cluePanel} data-testid="operative-clue-panel" aria-labelledby="operative-clue-title">
        <div className={styles.panelHeader}>
          <h2 id="operative-clue-title">Operative clue</h2>
        </div>
        <p className={styles.panelMeta} data-testid="operative-clue-inactive">
          Operative clues can be assigned once the session is Active or Paused.
        </p>
      </section>
    )
  }

  const allSelected = teams.length > 0 && selected.size === teams.length

  function toggleTeam(teamId: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(teamId)) next.delete(teamId)
      else next.add(teamId)
      return next
    })
  }

  function toggleAll() {
    setSelected(allSelected ? new Set() : new Set(teams.map((t) => t.teamId)))
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      setSuccess(null)
      const result = await addOperativeClueAction(liveSessionId, {
        clueText: clueText.trim(),
        teamIds: [...selected], // "all teams" is simply every id selected — no omit-for-all
      })
      if ('data' in result) {
        const count = result.data.assignedTeamIds.length
        setSuccess(`Assigned to ${count} team${count === 1 ? '' : 's'}.`)
        setClueText('')
        setSelected(new Set())
        onAdded?.(count)
      } else if ('notLive' in result) {
        setError('The session must be Active or Paused to assign operative clues.')
      } else if ('unauthorized' in result) {
        setError('You are not authorized to assign operative clues for this session.')
      } else {
        setError(result.error)
      }
    })
  }

  const canSubmit = clueText.trim() !== '' && selected.size > 0 && !isPending

  return (
    <section className={styles.cluePanel} data-testid="operative-clue-panel" aria-labelledby="operative-clue-title">
      <div className={styles.panelHeader}>
        <h2 id="operative-clue-title">Operative clue</h2>
        <div className={styles.panelMeta}>Write a free-text clue and assign it to one team, several, or all.</div>
      </div>

      <label className={styles.fieldLabel}>
        <span>Clue text</span>
        <textarea
          className={styles.inlineInput}
          data-testid="operative-clue-text-input"
          value={clueText}
          maxLength={MAX_CLUE_LENGTH}
          onChange={(e) => setClueText(e.target.value)}
          placeholder="e.g. Look beneath the blue banner."
          rows={3}
        />
      </label>

      <fieldset className={styles.fieldLabel}>
        <span>Assign to</span>
        {teams.length === 0 ? (
          <p className={styles.panelMeta}>No teams are attached to this session yet.</p>
        ) : (
          <>
            <label className={styles.inlineButton}>
              <input
                type="checkbox"
                data-testid="operative-clue-select-all"
                checked={allSelected}
                onChange={toggleAll}
              />
              <span>All teams</span>
            </label>
            {teams.map((t) => (
              <label key={t.teamId} className={styles.inlineButton}>
                <input
                  type="checkbox"
                  data-testid={`operative-clue-team-checkbox-${t.teamId}`}
                  checked={selected.has(t.teamId)}
                  onChange={() => toggleTeam(t.teamId)}
                />
                <span>{t.displayName}</span>
              </label>
            ))}
          </>
        )}
      </fieldset>

      <button
        className={styles.primaryButton}
        data-testid="operative-clue-submit"
        disabled={!canSubmit}
        onClick={submit}
        type="button"
      >
        {isPending ? 'Assigning…' : 'Assign clue'}
      </button>

      {success && <p className={styles.panelMeta} role="status" data-testid="operative-clue-success">{success}</p>}
      {error && <p className={styles.errorBanner} role="alert" data-testid="operative-clue-error">{error}</p>}
    </section>
  )
}
```

### `app/dashboard/DashboardClient.tsx` — wiring (anchors verified against source)

1. **Import.** Add `import { OperativeCluePanel } from './OperativeCluePanel'` alongside the sibling
   panel imports (next to `OperatorClueReleasePanel`, `DashboardClient.tsx:17`).

2. **Render.** Nest the control in the operator hero **immediately after** `OperatorClueReleasePanel`
   (`DashboardClient.tsx:1154-1164`, inside `data-testid="operator-panel"`). `selectedOperatorState` is
   in scope and non-null in this branch; teams reuse the same already-loaded source HU-26 uses
   (`operatorPanelState.panel?.teamProgress`):

```tsx
<OperativeCluePanel
  liveSessionId={selectedOperatorSession.liveSessionId}
  state={selectedOperatorState}
  teams={(operatorPanelState.panel?.teamProgress ?? []).map((t) => ({
    teamId: t.teamId,
    displayName: t.displayName,
  }))}
  onAdded={(count) =>
    announce('Operative clue assigned', `Clue assigned to ${count} team${count === 1 ? '' : 's'}.`)
  }
/>
```

**Gate:** `pnpm build` + typecheck pass. Opening an **Active or Paused** assigned session shows
`operative-clue-panel` with a clue `<textarea>`, an "All teams" toggle + one checkbox per attached team,
and an Assign button disabled until text is non-empty **and** ≥1 team is chosen; a non-live session
shows `operative-clue-inactive` and no submit; assigning a clue shows `operative-clue-success`
("Assigned to N team(s)") and clears the form; no clue-as-progress renders and no state transition is
issued from the control.

---

## A4 — Web tests (unit + e2e)

### Web unit

- **`tests/unit/app/lib/sessions.test.ts`** — `addOperativeClue`: `200` → parsed
  `AddOperativeClueResultDto`; `403`/`401` → `IdentityError('unauthorized', …)`; `404` →
  `Error('session_not_found')`; `400` → `Error('invalid_input')`; `409` with the not-live `detail` →
  `Error('not_live')` (and an unrecognised `detail` → `Error('clue_conflict')`); assert the POST body is
  `{ clueText, teamIds }` with the exact selected ids (single-team array and multi-team array).
- **`tests/unit/app/dashboard/operative-clue-panel.test.ts`** (new, mirror
  `operator-clue-release-panel.test.ts`) — render states: `state ∉ {Active, Paused}` →
  `operative-clue-inactive`, no `operative-clue-submit`; `state === 'Active'` **and** `state ===
  'Paused'` → textarea + per-team checkboxes + "All teams"; submit disabled while text empty or no team
  selected; "All teams" selects every checkbox; a mocked `addOperativeClueAction` returning `{ data }` →
  `operative-clue-success` with the assigned count; `{ notLive }` / `{ unauthorized }` / `{ error }` →
  the matching `operative-clue-error` copy.

### Web E2E — `tests/e2e/session-operative-clue.spec.ts` (mirror `session-clue-release.spec.ts` create→assign→attach→Active header)

Drive create → assign → attach two teams → `Active` as op-1 against the real gateway, reusing that
spec's `token` / `api` / `sql` helpers (no target seed needed — operative clues carry no target).

```ts
// A3/A4 (names only; bodies land in A4)
test('operator assigns an operative clue to one team: success note + assigned count', …)   // AC1, AC2
test('operator assigns an operative clue to all teams via the select-all toggle', …)        // AC2
test('the operative-clue control is inactive until the session is Active or Paused', …)     // AC1 (live gate)
test('submit is disabled with empty text or no team selected', …)                           // AC4 (validation pre-empt)
```

Non-owner `403` is asserted at the unit/action layer (`addOperativeClue` `403` → `IdentityError` →
action `{ unauthorized }` → `operative-clue-error`), matching how the HU-26 / timer / answered specs
keep non-owner coverage off the live UI (a second sub-keyed operator against the live stack is flaky).

**Gate (Track A):** `pnpm test` (web vitest) + `pnpm exec playwright test` (web e2e) pass; `pnpm build`
/ typecheck clean on `frontend/`. Track A needs only the landed authoring backend (X.1–X.4) up through
the gateway for the e2e — **not P0**.

---

## ▶ Track B — Participant Mobile (`mobile/`)

*Runs in parallel with Track A; shares no files with it. **Best after P0** (real `operativeClueId` key +
live delivery), but **B1 degrades gracefully without P0** — falls back to an `op:{index}` key and
refetch-only reveal — so it can start alongside Track A and tighten once P0 lands. **B2** is gated on
P0's live push (its design is settled in `mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`). Both
phases are read-only off the existing board path —
**no new invoke, group, REST call, or mutation** on mobile.*

## B1 — Treasure-hunt reveal (code-complete)

**Scope:** make the treasure-hunt board render an operative clue (a `visibleClues` entry with **null**
target fields), team-scoped, off the existing snapshot/push path. Three edits in `mobile/`:

### 1. `mobile/src/lib/realtime/team-board-types.ts` — widen `VisibleClueDto` target fields to nullable

```ts
// HU-23 target clues carry a target; HU-28 operative clues are operator-authored free text with NO
// target (both target fields null) but a stable operativeClueId. Backend VisibleClueDto (post-P0):
// `Guid? TargetSnapshotId, string ClueText, string? TargetName, Guid? OperativeClueId`.
export type VisibleClueDto = {
  targetSnapshotId: string | null;
  clueText: string;
  targetName: string | null;
  operativeClueId: string | null; // P0: set only for operative clues; null for target clues
};
```

### 2. `mobile/src/components/treasure-hunt-board.tsx` — render operative clues + null-safe keys

- **`ClueCard`** — when `targetName` is null (operative clue), render an **"OPERATIVE CLUE"** label
  instead of a (missing) target name; otherwise unchanged:

```tsx
function ClueCard({ clue }: { clue: VisibleClueDto }) {
  return (
    <Card parchment>
      <View accessibilityRole="text" style={{ gap: spacing.xs }}>
        <Text variant="label" muted>{clue.targetName ?? 'OPERATIVE CLUE'}</Text>
        <Text variant="mono">{clue.clueText}</Text>
      </View>
    </Card>
  );
}
```

- **Stable list key** — the current React `key`, the `seenClueIds` baseline, the acknowledge loop, and
  the `hasNewClues` check all key on `c.targetSnapshotId`, which is **null** for operative clues
  (collisions + duplicate React keys). The board projection carries no operative-clue id, so derive a
  stable key from the target id or the clue's list position + text:

```tsx
// Target clues key on targetSnapshotId; operative clues on their P0 operativeClueId. Exactly one is
// non-null per clue, so the key is always stable and never collides on null.
const clueKey = (c: VisibleClueDto) => c.targetSnapshotId ?? c.operativeClueId!;
```

  Then thread `clueKey(c)` through the four sites (`new Set(visibleClues.map(clueKey))` seed; the `for`
  acknowledge loop; the `hasNewClues` `some((c) => !seenClueIds.current.has(clueKey(c)))`; and
  `visibleClues.map((c) => <ClueCard key={clueKey(c)} clue={c} />)`). No index needed — the id is
  stable across projections, unlike the earlier position-based fallback.

### 3. No treasure-hunt screen change

`team-space.tsx` already feeds `visibleClues={board.visibleClues}` in its `playMode === 'TreasureHunt'`
branch (`:341-355`); operative clues arrive in that same array, so no `team-space.tsx` edit is needed
for the treasure-hunt path.

> **testID note:** mirroring HU-26's participant convention, `TreasureHuntBoard` uses no `testID`s on
> clue cards — the B3 mobile tests query by rendered `clueText` and the `"OPERATIVE CLUE"` label string.
> The existing `treasure-hunt-clue-indicator` (new-clue dot) is unchanged and now also fires for a
> newly-arrived operative clue via the null-safe `clueKey`.

**Gate (B1):** mobile typecheck passes with the nullable `VisibleClueDto`; the Clues tab renders an
operative clue (`targetSnapshotId: null`, `targetName: null`) with the `"OPERATIVE CLUE"` label + its
`clueText`, alongside any target clues; multiple operative clues render without duplicate-key warnings;
the new-clue indicator fires when an operative clue arrives; team isolation is unchanged (server
per-team group).

## B2 — Trivia-substage surfacing: persistent clue affordance + arrival toast (contract + gate; design settled)

**Scope:** give operative clues a home on the **non-treasure-hunt** view (which has no Clues tab) via
**two** new components, both wired once at `team-space.tsx` / `LiveTeamSpace` level; no new realtime
seam (P0 already delivers the clue live). **Kept at altitude — no verbatim skeleton — because these are
new components** (neither a clue list nor a toast/snackbar exists in `mobile/src/components` today); the
concrete look/placement/animation/dismiss are **settled in
`mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`**, which the implementer follows. What *is* fixed:

- **(a) Persistent clue affordance (the durable store):** a dismissible list/drawer/badge in the trivia
  branch showing the team's operative clues (`board.visibleClues` filtered to `targetSnapshotId == null`),
  so a participant can re-open guidance they've already received. This is the fix for toast-only
  fragility — a missed toast no longer loses the clue.
- **(b) `OperativeClueToast` (the arrival signal):** fires when `board.visibleClues` gains an operative
  clue whose key is not yet seen. **Trigger** reuses the B1 `clueKey` + `TreasureHuntBoard`'s
  `hasNewClues` prop-diff pattern: a `useRef` seen-set over `clueKey`, filtered to operative clues.
  Prop-diff bookkeeping only — never owns or recomputes board state (Architecture Decisions 3, 7).
- **Placement:** rendered once at `LiveTeamSpace` level over the trivia branch (covering treasure-hunt
  too is a design call — its Clues tab + dot may already suffice, so default trivia-only to avoid
  double-signalling).
- **testIDs:** `operative-clue-toast` and `operative-clue-list` — transient/new elements need stable
  selectors. These are the only mobile `testID`s added by the slice.
- **Team isolation & guidance-not-progress** carry over unchanged (server per-team group; both surfaces
  show `clueText` only, issue nothing).

**Design resolved** (`mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`): collapsed
`operative-clue-list` chip (ember-dot cue, taps to expand the same parchment `ClueCard`s) + a
viewport-top-pinned, trivia-only `operative-clue-toast` (non-parchment, one truncated line, slide/fade
~220ms in · ~4s hold · ~180ms out, `Animated` + a11y announce); tapping the toast opens the list and
dismisses it; chip stays collapsed on arrival. **No remaining blocker** — the backend push is delivered
by P0, so B2 is ready to build once P0 lands (see prompt #4).

**Gate (B2, if built):** on a trivia substage, a P0 push adding an operative clue shows the toast
(auto-dismiss) **and** the clue in the persistent list; dismissing the toast leaves the list entry; a
board update adding **no** operative clue (e.g. a score-only re-projection) shows nothing new; both
surfaces derive from prop diffs / `board.visibleClues` only (no board state ownership, no new
subscription); team isolation unchanged.

---

## B3 — Mobile tests

- **`mobile/src/__tests__/team-board-hook.test.ts`** — add a case: a `TeamBoardUpdated` push carrying a
  new `VisibleClueDto` with `targetSnapshotId: null` / `targetName: null` / `operativeClueId: '<guid>'`
  lands in `board.visibleClues`; a different-`liveSessionId` push does not (cross-session isolation,
  mirroring the existing case).
- **`mobile/src/__tests__/treasure-hunt-board.test.tsx`** — add cases (reuse `renderBoard` /
  `switchTab`): an operative clue (`{ targetSnapshotId: null, clueText: 'Look beneath the blue banner.',
  targetName: null, operativeClueId: '<guid>' }`) renders on the CLUES tab with the `"OPERATIVE CLUE"`
  label + its `clueText`; a target clue and an operative clue render together without a duplicate-key
  warning; two operative clues with identical `clueText` but distinct `operativeClueId` render without
  collision; growing `visibleClues` with an operative clue while off the CLUES tab shows
  `treasure-hunt-clue-indicator`.

**Gate (Track B):** mobile test suite passes; `pnpm build` / typecheck clean on `mobile/`.

---

## Acceptance-criteria → test mapping

HU-28 acceptance criteria, split across the operator (web) and participant (mobile) surfaces.

| # | Acceptance criterion | Covered by | Phase |
|---|---|---|---|
| AC1 | An operator can author a free-text operative clue during a live (Active/Paused) session | `addOperativeClue` `200`; `operative-clue-success`; Active-or-Paused render gate; e2e happy path + "inactive until live" | A2 / A3 / A4 |
| AC2 | Assign to one team, several, or all (teamIds ≥1; all = full id list) | checkbox multi-select + "All teams"; body assertion (single + multi ids); e2e one-team + all-teams | A3 / A4 |
| AC3 | The 200 result echoes the created clue ids, assigned team ids, and clue text | `AddOperativeClueResultDto` parsed; success count derived from `assignedTeamIds.length` | A1 / A2 / A4 unit |
| AC4 | Empty text / no teams is a non-crashing validation state | submit disabled until text non-empty **and** ≥1 team; backend 400 → `{ error }` → `operative-clue-error`; e2e disabled-submit | A3 / A4 |
| AC (guard) | Non-owning / non-operator authoring is refused (403), non-crashingly | `403` → `IdentityError('unauthorized')` → `{ unauthorized }` → `operative-clue-error`; `session.role !== 'Operator'` short-circuit | A2 / A4 unit |
| AC5 | A team sees only its own operative clue on the participant board | server per-team group + `visibleClues` scoping; hook test (matching vs different-session push); no client team filter | B1 / B3 mobile |
| AC6 | The operative clue shows as guidance (its text), not as progress; no substage advance | operative clue renders under Clues tab with `"OPERATIVE CLUE"` label; `resolvedTargets/totalActiveTargets` untouched; control issues only the POST | A3 / B1 |
| AC7 | On a Trivia substage (no Clues tab), the clue surfaces (arrival toast) and stays reviewable (persistent list) | `OperativeClueToast` (arrival) + persistent clue affordance on new operative clue in `visibleClues`; P0 delivers it live; `operative-clue-toast` / `operative-clue-list` — **design resolved in `mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`; ready once P0 lands** | B2 |

---

## Open Questions / Dependencies

- **Operative clue during a Trivia substage — persistent affordance + arrival toast (B2), design
  RESOLVED.** Operative clues surface in `board.visibleClues` regardless of play mode, but
  `team-space.tsx` renders `visibleClues` **only** in its `playMode === 'TreasureHunt'` branch — the
  trivia/fallback branch renders `SubstageProgress` + the question stage but no clue list. **Decision
  (revised from toast-only):** during trivia the clue surfaces via a **persistent, reviewable clue
  affordance** (durable store — a missed toast no longer loses guidance) **plus** an `OperativeClueToast`
  arrival signal. The four sub-questions (list layout; toast placement; animation + dismiss; tap
  behavior) are now **answered in `mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`**:
  collapsed chip + viewport-pinned trivia-only toast, ~4s hold, tap-opens-list. **No remaining blocker**
  — trigger logic is fixed (B2) and P0 delivers the clue live; B2 is ready to build once P0 lands.
- **Stable operative-clue identity on the board — resolved in P0.** The board `VisibleClueDto` now
  carries `operativeClueId` (P0.2), so the mobile list key is `targetSnapshotId ?? operativeClueId`
  (Architecture Decision 7) — a real, stable id. The earlier `op:{index}:{clueText}` fallback is
  dropped. Resolved, not tracked.
- **Live push — wired in P0 (targeted per-team), not deferred.** **Contract consumed:** authoring
  delivers the clue to the assigned team's board live (no reconnect/refetch) — making B1 instant and
  B2 live-useful. The handler mechanics (team-scoped broadcast, per-team fan-out, why it is not
  collapsed to a batched event) are owned by
  `backend/plans/hu-28-operative-clue-live-push-and-id-projection.md`; not re-derived here.
- **Dependency — backend running through the gateway (A4 e2e only).** The HU-28 backend is committed on
  this branch (X.1–X.4). The web e2e needs the service + api-gateway rebuilt and up
  (`docker compose build session-operations-service api-gateway`) so `/operative-clues` is live. Unit
  and mobile phases do not.
- **409 disambiguation depends on the ProblemDetails `detail` text.** `addOperativeClue` matches the
  substring `"requires an active or paused"` to distinguish the single not-live 409. If the backend
  later stabilises a ProblemDetails `type` per cause, switch the branch to match on `type` — lower-risk
  than message matching. Tracked, not blocking.

## Out of Scope

- **All backend work.** The authoring contract is landed (X.1–X.4) and the two P0 changes (team-scoped
  push + clue-id projection) are owned by
  `backend/plans/hu-28-operative-clue-live-push-and-id-projection.md`. This plan is frontend + mobile
  only and consumes P0's contract.
- **Clue-as-progress, substage advance, or target resolution from either UI** (HU-31); conditional/auto
  release (HU-27); HU-26 target hidden-clue release (separate, already shipped); evidence/QR
  (HU-29/30).
- **Editing, retracting, or listing already-authored operative clues** — the backend exposes only the
  authoring POST; no read/update/delete endpoint exists.
- **The trivia B2 component build itself** (persistent clue affordance + arrival toast) — design is
  settled (`mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md`), but the component code lands in the
  B2 phase, not this contract+gate note. P0 provides live delivery, so B2's only remaining dependency is
  P0 landing.
- **Backend event/broadcast shape** (e.g. collapsing the per-team `OperativeClueAddedEvent` into a
  batched event) — owned by the backend plan; out of scope for this frontend/mobile slice.
- **A participant authoring/mutation surface** — participants never author; no invoke, button, or hub
  method is added on mobile.
- **`revalidatePath('/dashboard')` after authoring** (Architecture Decision 6 — authoring mutates no RSC
  read).
- **Map, coordinates, standings/ranking, scoring** — untouched HU-23 placeholders.

---

## Commit Sequence

P0 backend commits live in `backend/plans/hu-28-operative-clue-live-push-and-id-projection.md`. The two
UI tracks below are independent and can land in either order (or interleaved) once P0 is in.

**Track A — Operator Web (`frontend/`):**

```
feat(frontend): A1 — operative-clue authoring types — HU-28
feat(frontend): A2 — operative-clue client fn + server action — HU-28
feat(frontend): A3 — operator operative-clue authoring control + dashboard wiring — HU-28
test(frontend): A4 — operative-clue unit + e2e (web) — HU-28

Ref: HU-28
```

**Track B — Participant Mobile (`mobile/`):**

```
feat(mobile): B1 — participant operative-clue reveal on the treasure-hunt board — HU-28
test(mobile): B3 — operative-clue hook + component tests — HU-28
# B2 (trivia persistent affordance + arrival toast): design settled in mobile/docs/hu-28-mobile-operative-clue-trivia-ux.md; build once P0 lands

Ref: HU-28
```
