# HU-10A — Frontend: Mission Hierarchy Authoring

**Ref:** HU-10A / DES-15 / DES-62
**Branch:** feature/hu-10a-mission-hierarchy-structure
**Date:** 2026-06-20
**Builds on:** HU-09 (`hu-09-frontend-mission-management.md`) — base mission CRUD. HU-09's frontend plan
explicitly deferred hierarchy authoring to HU-10A.

> ⚠️ **Read first:** `frontend/AGENTS.md` — "This is NOT the Next.js you know." Before writing any
> code, read the relevant guides under `frontend/.next-docs/` (server-and-client-components,
> mutating-data, revalidating, forms). Do not rely on remembered Next.js conventions.

> **Execution shape:** sequential tracer-bullets — **Phase 1** (foundation) → **Phase 2**
> (authoring, built up in increments 2.1 → 2.2 → 2.3) → **Phase 3** (integration + e2e). Each
> increment is an end-to-end runnable slice and its own commit.

---

## Context

The backend is **fully implemented** by HU-09's rebuild — the entire `MissionNode` Composite
(`Stage` → `Substage` → optional `Clue`), `SubstagePlayMode`, `Target`-based treasure hunt,
optional `Clue` guidance, `MissionActivationPolicy` readiness, and all 19 `/api/missions`
endpoints. **No backend work in this slice.** Greenfield frontend surfacing the existing structure
for administrator authoring.

What exists today (do **not** rebuild): `app/lib/definitions.ts` (`MissionSummaryDto`, `MissionDto`
— flat, no hierarchy types); `app/lib/missions.ts` (`listMissions`, `getMissionById`,
`createMission`, `updateMission`, `activateMission`, `deactivateMission`); `app/actions/missions.ts`
(matching Administrator-gated actions, mutations `revalidatePath('/dashboard')`);
`app/dashboard/MissionsPanel.tsx` — a **511-line single client component**, all state + handlers in
one `MissionsPanel` function, detail view inline as `if (view === 'detail') return (…)`, plus a
`MissionForm` subcomponent.

---

## Verified Backend Contract

Every structure mutation returns the **full `MissionResponse`** (entire hierarchy), not a delta —
the UI replaces mission state wholesale after each call (the `onMutated` seam below). Enum values
are JSON **strings**.

**Response shapes** (Phase 1 adds all as DTOs in `definitions.ts`):
- `MissionResponse`: `id, name, description, difficulty, maximumTimeMinutes, isActive, activationState, isSourceReady, stages: MissionStageDto[]`
- `MissionStageDto`: `id, title, sequenceOrder, substages: MissionSubstageDto[]`
- `MissionSubstageDto`: `id, title, sequenceOrder, playMode: 'TreasureHunt'|'Trivia', winnerScore: number|null, triviaQuizSelection: TriviaQuizSelectionDto|null, targets: MissionTargetDto[], clues: MissionClueDto[]`
- `MissionTargetDto`: `id, name, qrCode, sequenceOrder, isActive, clueId: number|null`
- `MissionClueDto`: `id, title, sequenceOrder, text, visibilityPolicy`
- `TriviaQuizSelectionDto`: `triviaQuizId`
- `MissionReadinessDto`: `missionId, activationState, isReady, failures: string[]`

**Endpoints** (all Administrator-only; gateway base = existing `MISSION_DESIGN_SERVICE_URL`):

| Verb | Route | Request | Step |
|------|-------|---------|------|
| GET | `/api/missions/{id}` | — | P1 (read tree) |
| POST | `/api/missions/{mid}/nodes` | `{nodeType, title, sequenceOrder, stageId?, substageId?, playMode?, clueText?, clueVisibilityPolicy?}` | 2.1 |
| PUT | `/api/missions/{mid}/nodes/{nid}` | `{title, sequenceOrder, clueText?, clueVisibilityPolicy?}` | 2.1 |
| DELETE | `/api/missions/{mid}/nodes/{nid}` | — | 2.1 |
| PUT | `.../substages/{ssid}/play-mode` | `{playMode}` | 2.2 |
| POST | `.../substages/{ssid}/targets` | `{name, qrCode, sequenceOrder, isActive?, winnerScore?}` | 2.2 |
| PUT | `.../targets/{tid}` | `{name, qrCode, sequenceOrder, isActive, winnerScore?}` | 2.2 |
| DELETE | `.../targets/{tid}` | — | 2.2 |
| POST | `.../targets/{tid}/clue-association` | `{clueId}` | 2.2 |
| DELETE | `.../targets/{tid}/clue-association` | — | 2.2 |
| POST | `.../substages/{ssid}/trivia-quiz-selection` | `{triviaQuizId}` | 2.3 |
| PUT | `.../substages/{ssid}/trivia-quiz-selection` | `{triviaQuizId}` | 2.3 |
| GET | `/api/missions/{id}/readiness` | — | 2.3 |
| POST | `/api/missions/{id}/activate` | — (readiness-gated; **409** if already active) | 2.3 |
| DELETE | `/api/missions/{id}` | — (**409** if already deactivated) | P1 (surface 409) |

**Containment rules** (domain-enforced; UI surfaces rejections verbatim, never pre-empts):
`Stage` admits only `Substage`; `Substage` admits only `Clue`; `Clue` is a leaf; `Target` is
side-content of a `Substage`. Rejections return explicit messages, e.g. *"A {child} node cannot be
placed directly under a {parent} node."*

**Play-mode split:** `TreasureHunt` → targets (+ optional clue per target, max one). `Trivia` →
`triviaQuizSelection` + substage-level `winnerScore`; no targets/clues. Switching mode discards the
other mode's content — warn before switching (step 2.2).

---

## Architecture Decisions

1. **Break up the monolith as we go.** Rather than grow `MissionsPanel.tsx` past 1000 lines, extract
   the detail view into small components under `app/dashboard/mission/` (`MissionTree`,
   `SubstageEditor`, and per-concern editors). Each increment adds its component and wires it in.
2. **`onMutated` is the state seam.** Every mutation returns the full `MissionResponse`; a single
   callback `onMutated(updated: MissionDto) => void` (= `setSelectedMission`) is threaded from
   `MissionsPanel` down through the tree. A child mutates, gets the new mission, calls `onMutated`.
   No child owns panel state; no client-side tree patching.
3. **Group hierarchy calls in one module.** Add structure client functions to a new
   `app/lib/mission-structure.ts` and actions to `app/actions/mission-structure.ts` (keeps base CRUD
   files untouched). Reuse the existing `getIdentityHeaders(session)` + status→error mapping; add a
   **409** branch surfacing the backend `detail` verbatim.
4. **`definitions.ts` extended once in Phase 1** with all response DTOs + request-payload types.
5. **Rejection messages are first-class.** Invalid placements / readiness failures render the
   backend message verbatim; the UI invents no validation copy for these.
6. **No `SessionMode`, no `TriviaQuiz` as `SessionSource`** in any type, prop, or copy.

---

## Environment

**No new environment variables.** The structure client does not introduce config. Two caveats
verified against the existing code, *not* assumptions:

- The base URL is a **hardcoded module const**, not an env var: `missions.ts` declares
  `const MISSION_DESIGN_SERVICE_URL = 'http://localhost:5001'`. The new `mission-structure.ts`
  **redeclares the same const** (it is not exported).
- `getIdentityHeaders(session)` is **private to `missions.ts`** (and independently duplicated in
  `trivias.ts`). It is *not* exported, so it cannot be imported without editing base CRUD — which
  Architecture Decision 3 forbids this slice. The new module therefore **redeclares the same
  helper locally**, matching the established `trivias.ts` pattern. "Reuse" in this plan means
  *replicate the verified shape*, not `import`.

The status→error mapping (`401/403` → `IdentityError`, `404` → `Error('mission_not_found')`,
`!ok` → `IdentityError('unknown', …)`) is likewise replicated, extended with a **409** branch
(below). When the gateway base later moves to config, both files change together — tracked in
Out of Scope.

---

## data-testid Contract

Single source of truth for selectors; the e2e skeleton (Phase 3) and the AC→test map key off
these. Phase column = where each is introduced.

| testid | Element | Phase |
|---|---|---|
| `mission-tree` | MissionTree root | P1 |
| `mission-tree-empty` | "no stages yet" placeholder | P1 |
| `stage-node-{stageId}` | a Stage node | P1 |
| `substage-node-{substageId}` | a Substage node | P1 |
| `substage-playmode-{substageId}` | play-mode badge (`TreasureHunt`/`Trivia`) | P1 |
| `target-node-{targetId}` | a Target (TreasureHunt side-content) | P1 |
| `clue-node-{clueId}` | a Clue child | P1 |
| `add-stage-btn` / `confirm-add-stage-btn` | add-Stage trigger / confirm | 2.1 |
| `add-substage-btn-{stageId}` / `add-clue-btn-{substageId}` | add child triggers | 2.1 |
| `edit-node-btn-{nodeId}` / `remove-node-btn-{nodeId}` / `confirm-remove-node-btn-{nodeId}` | per-node controls | 2.1 |
| `node-title-input` / `node-sequence-input` / `clue-text-input` / `clue-visibility-input` | node form fields | 2.1 |
| `node-error` | rejection-message region (`role="alert"`) | 2.1 |
| `playmode-select-{substageId}` / `playmode-switch-warning` | play-mode selector + switch warning | 2.2 |
| `add-target-btn-{substageId}` / `target-*-input` / `associate-clue-btn-{targetId}` | target authoring | 2.2 |
| `trivia-quiz-select-{substageId}` / `winner-score-input-{substageId}` | trivia selection | 2.3 |
| `activation-bar` / `readiness-failure` / `activate-mission-btn` / `mission-error` | activation (409 surfaced via `mission-error`) | 2.3 |

`activate-mission-btn` already exists in `MissionsPanel`; 2.3 moves activation into `ActivationBar`
and reuses the same testid for continuity with `missions.spec.ts`.

---

## Phase 1 — Foundation (read path)

**Scope:** add the hierarchy DTOs + request types, extend `MissionDto`, add the 409 branch to the
two base mutations the contract marks (`activate`/`deactivate`), build the read-only `MissionTree`,
and delegate the detail view to it.

### `app/lib/definitions.ts` — response DTOs (verbatim from the contract table)

```ts
// --- HU-10A mission hierarchy (Phase 1) ---
export type MissionClueDto = {
  id: number
  sequenceOrder: number
  title: string
  text: string
  visibilityPolicy: string
}

export type MissionTargetDto = {
  id: number
  name: string
  qrCode: string
  sequenceOrder: number
  isActive: boolean
  clueId: number | null
}

export type TriviaQuizSelectionDto = { triviaQuizId: number }

export type MissionSubstageDto = {
  id: number
  title: string
  sequenceOrder: number
  playMode: 'TreasureHunt' | 'Trivia'   // JSON string enum
  winnerScore: number | null
  triviaQuizSelection: TriviaQuizSelectionDto | null
  targets: MissionTargetDto[]
  clues: MissionClueDto[]
}

export type MissionStageDto = {
  id: number
  title: string
  sequenceOrder: number
  substages: MissionSubstageDto[]
}

export type MissionReadinessDto = {
  missionId: number
  activationState: string
  isReady: boolean
  failures: string[]
}
```

### `app/lib/definitions.ts` — request payloads (added now, consumed 2.1–2.3)

```ts
export type AddMissionNodeRequest = {
  nodeType: 'Stage' | 'Substage' | 'Clue'
  title: string
  sequenceOrder: number
  stageId?: number        // parent when nodeType === 'Substage'
  substageId?: number     // parent when nodeType === 'Clue'
  playMode?: 'TreasureHunt' | 'Trivia'
  clueText?: string
  clueVisibilityPolicy?: string
}

export type UpdateMissionNodeRequest = {
  title: string
  sequenceOrder: number
  clueText?: string
  clueVisibilityPolicy?: string
}

export type AssignPlayModeRequest = { playMode: 'TreasureHunt' | 'Trivia' }

export type AddTargetRequest = {
  name: string
  qrCode: string
  sequenceOrder: number
  isActive?: boolean
  winnerScore?: number
}

export type UpdateTargetRequest = {
  name: string
  qrCode: string
  sequenceOrder: number
  isActive: boolean
  winnerScore?: number
}

export type TriviaQuizSelectionRequest = { triviaQuizId: number }
```

### `app/lib/definitions.ts` — extend `MissionDto`

`stages` is **non-optional**: every `MissionResponse` carries it (empty array when none), so
`getMissionById` is a type-only change.

```ts
export type MissionDto = {
  id: number
  name: string
  description: string
  difficulty: string
  maximumTimeMinutes: number
  isActive: boolean
  activationState: string
  isSourceReady: boolean
  stages: MissionStageDto[]   // ← added in P1
}
```

### `app/lib/missions.ts` — add the 409 branch (contract: `activate`/`deactivate`)

The backend now maps `MissionAlreadyDeactivatedException` → **409** (HU-09), *not* the 500 the
current `deactivateMission` comment assumes. Replace its generic `!ok` branch; mirror the
ProblemDetails-`detail` parse `activateMission` already does for its 400 body:

```ts
// deactivateMission — replace the `if (!response.ok)` tail:
if (response.status === 409) {
  let detail = 'Mission is already deactivated.'
  try {
    const problem = await response.json()
    if (typeof problem?.detail === 'string' && problem.detail.length > 0) detail = problem.detail
  } catch { /* keep fallback */ }
  throw new Error(detail)
}
if (!response.ok) {
  throw new IdentityError('unknown', `deactivateMission failed with status ${response.status}`)
}
```

Add the same 409 branch to `activateMission` (already-active), alongside its existing 400
readiness branch.

### `app/dashboard/mission/MissionTree.tsx` (new) — read-only render

Client component: it threads `onMutated` (a function prop) from the client `MissionsPanel`, so it
cannot be a Server Component. **Confirm against `frontend/.next-docs/server-and-client-components`
before writing.** `onMutated` is unused in P1 — threaded for 2.1.

```tsx
'use client'

import type { MissionDto } from '@/app/lib/definitions'
import styles from '../dashboard.module.css'   // confirm co-location vs a new module

export function MissionTree({
  mission,
  onMutated,
}: {
  mission: MissionDto
  onMutated: (updated: MissionDto) => void
}) {
  void onMutated   // consumed by NodeControls in 2.1
  return (
    <div className={styles.missionTree} data-testid="mission-tree">
      {mission.stages.length === 0 ? (
        <p data-testid="mission-tree-empty">No stages yet.</p>
      ) : (
        mission.stages.map((stage) => (
          <section key={stage.id} data-testid={`stage-node-${stage.id}`}>
            <h3>{stage.title}</h3>
            {stage.substages.map((substage) => (
              <div key={substage.id} data-testid={`substage-node-${substage.id}`}>
                <h4>
                  {substage.title}{' '}
                  <span className={styles.chip} data-testid={`substage-playmode-${substage.id}`}>
                    {substage.playMode}
                  </span>
                  {substage.playMode === 'Trivia' && substage.winnerScore !== null && (
                    <span> · winnerScore {substage.winnerScore}</span>
                  )}
                </h4>

                {/* TreasureHunt side-content */}
                <ul>
                  {substage.targets.map((target) => (
                    <li key={target.id} data-testid={`target-node-${target.id}`}>
                      {target.name} · {target.qrCode}
                      {target.clueId !== null && <span> · clue #{target.clueId}</span>}
                    </li>
                  ))}
                </ul>

                {/* Clue children */}
                <ul>
                  {substage.clues.map((clue) => (
                    <li key={clue.id} data-testid={`clue-node-${clue.id}`}>
                      {clue.title} — {clue.text} ({clue.visibilityPolicy})
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </section>
        ))
      )}
    </div>
  )
}
```

### `app/dashboard/MissionsPanel.tsx` — delegate the detail view

In the `if (view === 'detail' && selectedMission !== null)` block (currently `MissionsPanel.tsx:144`),
insert `<MissionTree>` after the `missionDetailInlineMeta` block (~line 193). The existing
activation/deactivate actions **stay** (they become `ActivationBar` in 2.3); `onMutated` is the
existing `setSelectedMission` setter (line 20):

```tsx
{/* after the <div className={styles.missionDetailInlineMeta}> … </div> */}
<MissionTree mission={selectedMission} onMutated={setSelectedMission} />
```

**Gate:** detail view renders the full tree for a seeded mission; deactivating an already-deactivated
mission surfaces the backend 409 message (not `deactivation_failed`); `npm run build` + typecheck
pass; no `SessionMode` in any new type or copy.

---

## Phase 2 — Authoring (incremental)

### 2.1 — Node authoring (stages · substages · clues)

**Scope:** add the three node client functions + their actions, and one `NodeControls` family wired
into `MissionTree`. Reorder is a PUT with a new `sequenceOrder` (see Open Questions — assumed, no
dedicated endpoint).

#### `app/lib/mission-structure.ts` (new)

Redeclares `MISSION_DESIGN_SERVICE_URL` + `getIdentityHeaders` (see Environment) and a shared
`mapStructureError` that adds the **409** branch surfacing the backend `detail` verbatim — the same
ProblemDetails parse `activateMission` uses. Every structure mutation returns the **full
`MissionResponse`** (contract), so all three resolve to `MissionDto`, **including DELETE**.

```ts
import 'server-only'
import {
  IdentityError,
  type MissionDto,
  type AddMissionNodeRequest,
  type UpdateMissionNodeRequest,
} from './definitions'
import { verifySession } from './dal'

const MISSION_DESIGN_SERVICE_URL = 'http://localhost:5001'

function getIdentityHeaders(session: {
  externalIdentityId: string
  displayName: string
  email: string
  role: string
}) {
  return {
    'X-User-Id': session.externalIdentityId,
    'X-User-Role': session.role,
    'X-User-Email': session.email,
  }
}

// 400 (invalid fields) and 409 (containment / state conflict) both carry a ProblemDetails
// `detail` the UI surfaces verbatim — it never invents placement copy (Architecture Decision 5).
async function mapStructureError(response: Response, fnName: string): Promise<never> {
  if (response.status === 400 || response.status === 409) {
    let detail = response.status === 409
      ? 'The request conflicts with the mission state.'
      : 'Invalid request.'
    try {
      const problem = await response.json()
      if (typeof problem?.detail === 'string' && problem.detail.length > 0) detail = problem.detail
    } catch { /* keep fallback */ }
    throw new Error(detail)
  }
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('mission_not_found')
  throw new IdentityError('unknown', `${fnName} failed with status ${response.status}`)
}

export async function addMissionNode(
  missionId: number,
  body: AddMissionNodeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${missionId}/nodes`, {
    method: 'POST',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) await mapStructureError(response, 'addMissionNode')
  return response.json()
}

export async function updateMissionNode(
  missionId: number,
  nodeId: number,
  body: UpdateMissionNodeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/missions/${missionId}/nodes/${nodeId}`,
    {
      method: 'PUT',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    },
  )
  if (!response.ok) await mapStructureError(response, 'updateMissionNode')
  return response.json()
}

export async function removeMissionNode(missionId: number, nodeId: number): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/missions/${missionId}/nodes/${nodeId}`,
    { method: 'DELETE', headers: getIdentityHeaders(session) },
  )
  if (!response.ok) await mapStructureError(response, 'removeMissionNode')
  return response.json()   // DELETE returns the full MissionResponse
}
```

#### `app/actions/mission-structure.ts` (new) — Administrator-gated

⚠️ **Import-alias gotcha** (same one hu-03 flags for `assignUserRole`): the lib functions share
names with the exported Server Actions. Import them **aliased** (`…Lib`) or the action shadows the
import and recurses into itself.

```ts
'use server'

import { verifySession } from '@/app/lib/dal'
import {
  addMissionNode as addMissionNodeLib,
  updateMissionNode as updateMissionNodeLib,
  removeMissionNode as removeMissionNodeLib,
} from '@/app/lib/mission-structure'
import { revalidatePath } from 'next/cache'
import type {
  MissionDto,
  AddMissionNodeRequest,
  UpdateMissionNodeRequest,
} from '@/app/lib/definitions'

export async function addMissionNode(
  missionId: number,
  body: AddMissionNodeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await addMissionNodeLib(missionId, body)
  revalidatePath('/dashboard')
  return result
}

// updateMissionNode(missionId, nodeId, body) and removeMissionNode(missionId, nodeId)
// follow the identical shape: verifySession → Administrator guard → …Lib → revalidate → return.
```

#### `app/dashboard/mission/NodeControls.tsx` (new) — one worked control

The add-Stage control, worked end-to-end. The Substage and Clue add-controls are the **same shape**,
differing only in `nodeType` + the parent id (`stageId` / `substageId`) and Clue's extra
`clueText` + `clueVisibilityPolicy` inputs. Edit (`updateMissionNode`) and remove
(`removeMissionNode` behind a confirm, mirroring the panel's deactivate pattern) reuse the same
`onMutated` seam.

```tsx
'use client'

import { useState, useTransition } from 'react'
import type { MissionDto } from '@/app/lib/definitions'
import { addMissionNode } from '@/app/actions/mission-structure'
import styles from '../dashboard.module.css'

export function AddStageControl({
  missionId,
  nextSequenceOrder,
  onMutated,
}: {
  missionId: number
  nextSequenceOrder: number
  onMutated: (updated: MissionDto) => void
}) {
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  function submit() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await addMissionNode(missionId, {
          nodeType: 'Stage',
          title,
          sequenceOrder: nextSequenceOrder,
        })
        onMutated(updated)   // full MissionResponse replaces panel state
        setTitle('')
        setOpen(false)
      } catch (e) {
        // Backend rejection (400/409 containment) surfaced verbatim; the UI never pre-empts it.
        setError(e instanceof Error ? e.message : 'Could not add stage.')
      }
    })
  }

  if (!open) {
    return (
      <button className={styles.inlineButton} data-testid="add-stage-btn"
        disabled={isPending} onClick={() => setOpen(true)} type="button">
        + Add stage
      </button>
    )
  }

  return (
    <div className={styles.confirmRow}>
      <input className={styles.inlineInput} data-testid="node-title-input" value={title}
        onChange={(e) => setTitle(e.target.value)} placeholder="Stage title" />
      <button className={styles.smallButton} data-testid="confirm-add-stage-btn"
        disabled={isPending || title.trim() === ''} onClick={submit} type="button">
        Save
      </button>
      <button className={styles.inlineButton} disabled={isPending}
        onClick={() => { setOpen(false); setError(null) }} type="button">
        Cancel
      </button>
      {error && <p className={styles.formError} role="alert" data-testid="node-error">{error}</p>}
    </div>
  )
}
```

Wire `AddStageControl` at the tree root in `MissionTree`; the Substage/Clue variants render at their
parent node. `nextSequenceOrder` = `max(siblings.sequenceOrder) + 1` (or `0` when empty).

**Gate:** add stage → substage → clue end-to-end, each replacing panel state via `onMutated`;
invalid placement (e.g. a clue under a stage) shows the backend's explicit
*"A {child} node cannot be placed directly under a {parent} node."* in `node-error`; build/typecheck
pass.

### 2.2 — Play-mode + treasure-hunt authoring (targets + clue association)
**Scope:** add `assignSubstagePlayMode`, `addTarget` / `updateTarget` / `removeTarget`,
`associateClueWithTarget` / `unassociateClueFromTarget`. New `SubstageEditor.tsx`: play-mode selector
with a **switch warning** (targets/winnerScore/trivia-selection are lost), and — for TreasureHunt —
a target form (name, qrCode, sequenceOrder, isActive, winnerScore) + clue→target association (max
one clue/target, enforced by the API, surfaced verbatim). Wire into `MissionTree`.
**Gate:** set a substage to TreasureHunt, add targets, associate a clue; switching mode shows the
warning; build/typecheck pass.

### 2.3 — Trivia selection + activation
**Scope:** add `setTriviaQuizSelection` / `updateTriviaQuizSelection` + `getMissionReadiness` (reuse
existing `activateMission`). Extend `SubstageEditor` for Trivia substages: quiz picker (see Open
Questions) + substage `winnerScore`, hiding target/clue controls. New `ActivationBar.tsx`:
fetch/display `readiness.failures`, **Activate** button disabled until `isReady`, surface the
**409** path verbatim; placed at mission-detail level.
**Gate:** assign a quiz to a Trivia substage; activation blocked-with-failures when not ready and
succeeds when ready; already-active/deactivated returns 409 surfaced as a message; build/typecheck
pass.

---

## Phase 3 — Integration & E2E
**Scope:** Playwright e2e under `frontend/tests/e2e` exercising the full flow: add/remove nodes →
play-mode → target authoring → clue association → trivia selection → readiness → activation. Final
gate sweep.
**Gate:** e2e green; `npm run build` + lint + typecheck pass; UI exposes Mission-as-wrapper +
MissionNode Composite + exactly one SubstagePlayMode + Target-based treasure hunt + optional Clue
(max one/target); rejection messages shown; **no `SessionMode`, no `TriviaQuiz` as `SessionSource`**.

---

## E2E Skeleton — `tests/e2e/mission-hierarchy.spec.ts`

Names only (bodies filled in their owning phase); uses the existing `adminPage` fixture from
`tests/fixtures/auth.ts` and the `data-testid` contract above. The full bodies land in Phase 3,
but each phase lands the test for the increment it completes (so coverage tracks the tracer-bullets,
not a big-bang at the end).

```ts
import { test, expect } from '../fixtures/auth'

// P1 — read path
test('admin sees the read-only mission tree for a seeded mission', ...)        // AC1
test('an empty mission shows the no-stages placeholder', ...)                   // AC1

// 2.1 — node authoring
test('admin adds a stage, then a substage, then a clue', ...)                   // AC1
test('invalid placement (clue under a stage) shows the backend message', ...)   // AC5
test('admin edits and removes a node', ...)                                     // AC1

// 2.2 — play-mode + treasure hunt
test('admin sets a substage to TreasureHunt and adds a target with a clue', ...) // AC2, AC3
test('switching play mode warns the other mode content is discarded', ...)       // AC2

// 2.3 — trivia + activation
test('admin assigns a trivia quiz and winnerScore to a Trivia substage', ...)    // AC4
test('activation is blocked with readiness failures, then succeeds when ready', ...) // AC6
test('deactivating an already-deactivated mission surfaces the 409 message', ...)    // AC6

// P3 — canon guard
test('no SessionMode / SessionSource copy appears anywhere in the authoring UI', ...) // AC7
```

---

## Acceptance Criteria → Test mapping

DES-15 criteria derived from the canon-realignment obligations (ADR-0004; mission-design-service
`CONTEXT.md` §Required Patterns). Each is covered by the spec above and gated by its phase.

| # | Acceptance criterion | Covered by | Phase |
|---|---|---|---|
| AC1 | Mission wraps an ordered Stage→Substage→Clue Composite, rendered as a tree | `sees the read-only mission tree`, `adds a stage… clue` | P1 / 2.1 |
| AC2 | Each Substage has exactly one play mode (`TreasureHunt`\|`Trivia`); switching discards the other | `sets a substage to TreasureHunt`, `switching play mode warns…` | 2.2 |
| AC3 | TreasureHunt substages own Targets (name/qrCode/order); ≤ one Clue per Target | `adds a target with a clue` | 2.2 |
| AC4 | Trivia substages carry a quiz selection + substage `winnerScore`; no targets/clues | `assigns a trivia quiz and winnerScore` | 2.3 |
| AC5 | Invalid containment is rejected with the backend message, surfaced verbatim | `invalid placement … shows the backend message` | 2.1 |
| AC6 | Activation is readiness-gated; failures shown; already-active/deactivated → 409 surfaced | `activation is blocked…`, `already-deactivated… 409` | P1 (409) / 2.3 |
| AC7 | No `SessionMode`, no `TriviaQuiz`-as-`SessionSource` in any type, prop, or copy | `no SessionMode / SessionSource copy…` | P3 |

---

## Commit Sequence
- `feat(frontend): mission hierarchy types + read-only tree view - HU-10A` (P1)
- `feat(frontend): stage/substage/clue node authoring - HU-10A` (2.1)
- `feat(frontend): play-mode + treasure-hunt target authoring - HU-10A` (2.2)
- `feat(frontend): trivia-quiz selection + mission activation - HU-10A` (2.3)
- `test(frontend): e2e mission hierarchy authoring - HU-10A` (P3)

Each carries: `Ref: HU-10A` / `Ref: DES-15` / `Ref: DES-62`.

---

## Open Questions / Dependencies
- **Trivia-quiz picker source (2.3):** no quizzes client exists on the frontend. Confirm a
  `trivia`/`quiz` gateway endpoint to list published quizzes (cross bounded-context) vs. an interim
  raw `triviaQuizId` input. Resolve before 2.3.
- **Reorder semantics:** `sequenceOrder` is a field on add/update — confirm reorder is a PUT with a
  new `sequenceOrder` (assumed) vs. a dedicated endpoint (none seen).

## Out of Scope
- Any backend change (HU-09 owns the full contract; verification-only this HU).
- Participant/operator play-time UI — authoring only, Administrator-gated.
- `SessionMode` / `TriviaQuiz`-as-`SessionSource` — excluded by the canon realignment.
- Hoisting the hardcoded `MISSION_DESIGN_SERVICE_URL` const and the duplicated `getIdentityHeaders`
  helper into shared/exported config. This slice replicates them (per Architecture Decision 3) to
  keep base CRUD untouched; centralizing them is a separate refactor.
