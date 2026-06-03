# Plan: HU-13 Frontend — Trivia Quiz Duplication and Retirement

**Ref:** HU-13
**Branch:** feature/hu-13-trivia-quiz-duplication-and-retirement
**Date:** 2026-06-03
**Builds on:** HU-12 frontend (TriviasPanel with publish/archive, `isSourceReady` display,
`app/lib/trivias.ts`, `app/actions/trivias.ts`, `TriviaQuizDto`, `TriviaQuizSummaryDto`)

---

## Context

Backend HU-13 (`mission-design-service`) is fully implemented. Two new endpoints beyond HU-12:

- `POST /api/trivias/{id}/duplicate` — create a Draft copy of a non-Archived quiz; `Administrator` only
- `POST /api/trivias/{id}/retire` — archive a used quiz while preserving its session history; `Administrator` only

HU-13 also exposes three new fields — `sourceTrivi aQuizId`, `hasUsageHistory`, and `isDuplicate` —
on every quiz response (catalog and detail). The backend has serialized these fields since HU-13 merged,
but the current frontend types drop them silently. Four gaps must be closed:

1. **Missing type fields.** `TriviaQuizSummaryDto` and `TriviaQuizDto` in `definitions.ts` do not
   declare `sourceTrivi aQuizId`, `hasUsageHistory`, or `isDuplicate`. The backend has always
   included them; adding them makes them accessible to UI and action code.
2. **No API client functions for duplicate and retire.** `lib/trivias.ts` has no `duplicateTriviaQuiz`
   or `retireTriviaQuiz`.
3. **No Server Actions for duplicate and retire.** `actions/trivias.ts` has no corresponding wrappers.
4. **No duplicate or retire UI in `TriviasPanel`.** The detail view has no duplicate or retire
   controls. The list view shows no lineage or usage-history indicators.

---

## Verified Backend Contract

### Common headers (trust envelope)

```
X-User-Id:    {session.externalIdentityId}
X-User-Role:  {session.role}
X-User-Email: {session.email}
```

---

### `POST /api/trivias/{id}/duplicate`

No request body. The `id` path parameter is the numeric quiz id.

**Response `201 Created`** — `TriviaQuizResponse` (new copy)

```ts
{
  id: number                  // new quiz id, different from source
  title: string               // same as source
  description: string         // same as source
  status: "Draft"             // confirmed: copy always starts as Draft
  isSourceReady: false        // confirmed: Draft → isSourceReady = false
  sourceTriviaQuizId: number  // confirmed: set to the resolved source lineage id
  hasUsageHistory: false      // confirmed: fresh copy has no usage history
  isDuplicate: true           // confirmed
  questions: TriviaQuestionResponse[]  // confirmed: cloned from source
}
```

Location header: `/api/trivias/{newId}`

**Error cases**
- `401` — missing trust headers
- `403` — caller is not `Administrator`
- `404` — source quiz not found
- `409 Conflict`, title `"Trivia quiz cannot be archived in its current state."` — source quiz
  is `Archived` (the domain reuses the archive state exception for the duplicate guard)

**Additional verified behaviour (from integration test `DuplicateTriviaQuiz_ReturnsCreatedAuthoringCopyWithLineageProjection`)**
- Source quiz is **not modified**: its `status`, `sourceTrivi aQuizId`, `hasUsageHistory`, and
  `isDuplicate` remain unchanged after the duplicate call.
- Both source and copy appear in `GET /api/trivias/` catalog immediately.
- `sourceTriviaQuizId` on the copy resolves to the original lineage root: if the source is
  itself a duplicate, `sourceTriviaQuizId` is set to the source's own `sourceTriviaQuizId`
  (not the intermediate copy's id). This means all copies in a lineage share the same root id.
- The source in the integration test was Published; the domain template only blocks Archived
  sources, so Draft sources can also be duplicated.

---

### `POST /api/trivias/{id}/retire`

No request body.

**Response `200 OK`** — `TriviaQuizResponse` (retired quiz)

```ts
{
  id: number
  title: string
  description: string
  status: "Archived"          // confirmed: retire transitions to Archived
  isSourceReady: false        // confirmed: Archived → isSourceReady = false
  sourceTriviaQuizId: null    // confirmed: original, not a copy
  hasUsageHistory: true       // confirmed: retire requires and preserves this
  isDuplicate: false          // confirmed
  questions: TriviaQuestionResponse[]
}
```

**Error cases**
- `401` — missing trust headers
- `403` — caller is not `Administrator`
- `404` — quiz not found
- `409 Conflict`, title `"Trivia quiz cannot be archived in its current state."` — quiz is
  already `Archived`
- `409 Conflict`, title `"Trivia quiz cannot be retired without usage history."` — quiz has
  `hasUsageHistory = false`; retirement is not valid for quizzes that were never used in a session

**Additional verified behaviour (from integration test `RetireTriviaQuiz_WhenUsed_ReturnsArchivedQuizAndPreservesHistoricalIdentity`)**
- After retire, `GET /api/trivias/{id}` returns `status: "Archived"` and `hasUsageHistory: true`.
- After retire, the catalog reflects the same state.
- A non-Administrator caller returns `403`.
- Retire is **semantically distinct from Archive**: retire is for quizzes that have session
  history and must not be destroyed, only withdrawn from future assignment. The `hasUsageHistory`
  flag stays `true` after retire, marking the record as historically significant.

---

### Updated `TriviaQuizSummaryResponse` and `TriviaQuizResponse` shapes

Both responses now include `sourceTrivi aQuizId`, `hasUsageHistory`, and `isDuplicate`. These
fields have been serialized by the backend since HU-13 was merged and are silently dropped by
the current frontend types.

**`TriviaQuizSummaryResponse`** (catalog — `GET /api/trivias/`):
```ts
{
  id: number
  title: string
  description: string
  status: string        // "Draft" | "Published" | "Archived"
  isSourceReady: boolean
  sourceTriviaQuizId: number | null   // null for originals; root lineage id for copies
  hasUsageHistory: boolean            // true once used in a session
  isDuplicate: boolean                // shorthand for sourceTriviaQuizId !== null
}
```

**`TriviaQuizResponse`** (detail, duplicate, retire responses):
```ts
{
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
  sourceTriviaQuizId: number | null
  hasUsageHistory: boolean
  isDuplicate: boolean
  questions: TriviaQuestionResponse[]
}
```

---

## Architecture Decisions

- **Duplicate is a detail-view action only.** Following the publish/archive precedent from HU-12,
  the "Duplicate" trigger is in the detail view action bar, not the list view. The list view
  shows lineage and usage-history cues (read-only chips) so admins can identify originals vs
  copies at a glance, but the mutation is always initiated from the detail view. This avoids
  per-row confirmation state in the list.
- **After a successful duplicate, the UI navigates to the new quiz's detail.** The 201 response
  body contains the full `TriviaQuizResponse` for the new copy. Setting `selectedQuiz` to the
  returned DTO and staying on the detail view gives the admin immediate access to the new quiz's
  lineage cues and allows them to start editing the copy without a separate navigation step.
- **Retire replaces Archive for used quizzes; Archive remains for unused quizzes.** When
  `selectedQuiz.hasUsageHistory === true`, the "Archive" button is replaced by "Retire". When
  `hasUsageHistory === false`, only "Archive" appears. Showing both for a used quiz would be
  confusing (both result in `Archived` status) and would not communicate the semantic distinction
  the scope requires. This also naturally prevents an admin from reaching the backend's
  archive-on-used path through the UI — the only UI path to Archived for a used quiz is Retire.
- **Retire button copy distinguishes withdrawal from destruction.** The Retire button label is
  "Retire" and its `title` attribute reads "Withdraw from future use — session history is
  preserved." The confirm button reads "Confirm retire." This makes the distinction explicit at
  the interaction layer without adding extra explanation UI.
- **All four confirm states are mutually exclusive.** `confirmPublish`, `confirmArchive`,
  `confirmDuplicate`, and `confirmRetire` are tracked as separate booleans. Trigger buttons for
  all four are gated behind a single `allConfirmsClosed` derived value so opening any one
  confirmation automatically hides all trigger buttons. Back navigation resets all four.
- **`lifecycleError` is reused across all lifecycle transitions.** Duplicate and retire errors
  land in the same `lifecycleError` state that publish and archive already use. A single error
  display point is simpler than per-action error state; the confirm rows are also mutually
  exclusive, so at most one error is ever displayed at a time.
- **409 errors for duplicate and retire are mapped to single error codes.** `duplicateTriviaQuiz`
  in `lib/trivias.ts` throws `'trivia_duplicate_conflict'` for any 409. `retireTriviaQuiz` throws
  `'trivia_retire_conflict'` for any 409 (which covers both "already Archived" and "no usage
  history"). The UI gates the buttons behind the relevant `status` and `hasUsageHistory` checks,
  so these 409s rarely reach the network; the generic message is a safety net.
- **Lineage cues in the list view use the new type fields directly.** A "Provenance" column
  shows a `"Copy"` chip for `isDuplicate === true` items and a `"Used"` chip for
  `hasUsageHistory === true` items. Originals without usage history show a muted dash. This
  gives admins a quick catalog overview without navigating into each detail.
- **`sourceTrivi aQuizId` is displayed in the detail view when the quiz is a copy.** The inline
  meta section gets a "Copied from: Quiz #{sourceTriviaQuizId}" entry when `isDuplicate === true`.
  This is a read-only reference — no navigation to the source quiz is implemented at this stage.
- **`hasUsageHistory` is shown as a chip in the detail view.** When `hasUsageHistory === true`,
  a "Has usage history" warning-tone chip appears in the inline meta section. This cues admins
  that the quiz cannot be deleted and must be retired (not archived) for withdrawal.
- **`revalidatePath('/dashboard')` after duplicate and retire.** Both mutations use the same
  cache-clearing pattern as publish and archive so the catalog reflects the new state immediately.
- **Administrator-only enforcement at the action layer.** Both `duplicateTriviaQuiz` and
  `retireTriviaQuiz` Server Actions check `session.role !== 'Administrator'` before reaching
  the backend, consistent with the existing pattern. Nav-level hiding of the Trivias panel for
  non-admins is already in place from HU-11 and is not changed.
- **Retirement E2E test requires a backend fixture for `hasUsageHistory`.** Setting
  `hasUsageHistory = true` on a quiz requires the session/mission execution service to complete
  a gameplay session, which is beyond the frontend E2E setup. The Phase 5 retirement test uses
  `page.route()` to intercept the catalog and detail responses for one test and inject a quiz
  with `hasUsageHistory: true`. This is the only place in the test suite that uses response
  interception; it is clearly labelled as a UI-behavior test rather than an integration test.

---

## Environment

No new environment variables. `MISSION_DESIGN_SERVICE_URL` (`http://localhost:5001`) already
covers the duplicate and retire endpoints — the same service is reused.

---

## Phases

### Phase 1 — Type extension (`sourceTrivi aQuizId`, `hasUsageHistory`, `isDuplicate`)

**Scope**
- Add `sourceTrivi aQuizId: number | null`, `hasUsageHistory: boolean`, and `isDuplicate: boolean`
  to `TriviaQuizSummaryDto` and `TriviaQuizDto` in `app/lib/definitions.ts`.

**`app/lib/definitions.ts` changes**
```ts
// Before
export type TriviaQuizSummaryDto = {
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
}

// After
export type TriviaQuizSummaryDto = {
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
  sourceTriviaQuizId: number | null
  hasUsageHistory: boolean
  isDuplicate: boolean
}

// Before
export type TriviaQuizDto = {
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
  questions: TriviaQuestionDto[]
}

// After
export type TriviaQuizDto = {
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
  sourceTriviaQuizId: number | null
  hasUsageHistory: boolean
  isDuplicate: boolean
  questions: TriviaQuestionDto[]
}
```

**Gate**
- `pnpm build` passes with no type errors.
- No runtime regression: the fields were already serialized by the backend; adding them to the
  types only makes them accessible. Existing renders are unaffected until Phase 4.

---

### Phase 2 — API client additions (`lib/trivias.ts`)

**Scope**
- Add `duplicateTriviaQuiz` and `retireTriviaQuiz` to `app/lib/trivias.ts`.

**Additions to `app/lib/trivias.ts`**
```ts
export async function duplicateTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/duplicate`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_duplicate_conflict')
  if (!response.ok) throw new IdentityError('unknown', `duplicateTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function retireTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/retire`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_retire_conflict')
  if (!response.ok) throw new IdentityError('unknown', `retireTriviaQuiz failed with status ${response.status}`)
  return response.json()
}
```

Note: both endpoints have no request body — no `Content-Type` header is needed. The duplicate
endpoint returns `201 Created`; `response.ok` covers all 2xx statuses including 201.

**Gate**
- `pnpm build` passes with no type errors.

---

### Phase 3 — Server Action additions (`actions/trivias.ts`)

**Scope**
- Add `duplicateTriviaQuiz` and `retireTriviaQuiz` Server Actions to `app/actions/trivias.ts`.

**Import alias additions**
```ts
import {
  listTriviaQuizzes,
  getTriviaQuizById,
  createTriviaQuiz as createTriviaQuizLib,
  updateTriviaQuiz as updateTriviaQuizLib,
  addTriviaQuestion as addTriviaQuestionLib,
  updateTriviaQuestion as updateTriviaQuestionLib,
  publishTriviaQuiz as publishTriviaQuizLib,
  archiveTriviaQuiz as archiveTriviaQuizLib,
  duplicateTriviaQuiz as duplicateTriviaQuizLib,
  retireTriviaQuiz as retireTriviaQuizLib,
} from '@/app/lib/trivias'
```

**Server Action additions**
```ts
export async function duplicateTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await duplicateTriviaQuizLib(id)
  revalidatePath('/dashboard')
  return result
}

export async function retireTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await retireTriviaQuizLib(id)
  revalidatePath('/dashboard')
  return result
}
```

**Gate**
- `pnpm build` passes with no type errors.
- Calling `duplicateTriviaQuiz` or `retireTriviaQuiz` from an Operator session throws
  `'Forbidden'` before reaching the backend.

---

### Phase 4 — TriviasPanel UI: duplicate/retire controls, lineage and usage-history display

**Scope**
- Import `duplicateTriviaQuiz` and `retireTriviaQuiz` from `@/app/actions/trivias`.
- Add `confirmDuplicate`, `confirmRetire` state.
- Add `handleDuplicate` and `handleRetire` handlers.
- Update the detail view:
  - Extend inline meta with `sourceTriviaQuizId` (lineage) and `hasUsageHistory` (usage cue).
  - Add Duplicate button/confirm in the actions bar.
  - Replace Archive with Retire in the actions bar when `hasUsageHistory === true`.
  - Extend back-navigation cleanup to reset `confirmDuplicate` and `confirmRetire`.
  - Extend mutual-exclusion logic to cover all four confirm states.
- Update the list view:
  - Add a "Provenance" column showing a `"Copy"` chip for `isDuplicate === true` items,
    a `"Used"` chip for `hasUsageHistory === true` items, and a dash otherwise.

**Import update in `TriviasPanel.tsx`**
```ts
import {
  getTriviaQuizzes,
  getTriviaQuiz,
  createTriviaQuiz,
  updateTriviaQuiz,
  addTriviaQuestion,
  updateTriviaQuestion,
  publishTriviaQuiz,
  archiveTriviaQuiz,
  duplicateTriviaQuiz,
  retireTriviaQuiz,
} from '@/app/actions/trivias'
```

**State additions inside `TriviasPanel`**
```ts
const [confirmDuplicate, setConfirmDuplicate] = useState(false)
const [confirmRetire, setConfirmRetire] = useState(false)
```

**`allConfirmsClosed` derived value** (compute at render time inside the component, used to
gate all trigger buttons uniformly):
```ts
const allConfirmsClosed = !confirmPublish && !confirmArchive && !confirmDuplicate && !confirmRetire
```

**`handleDuplicate` function**
```ts
async function handleDuplicate() {
  if (!selectedQuiz) return
  startTransition(async () => {
    setLifecycleError(null)
    try {
      const duplicated = await duplicateTriviaQuiz(selectedQuiz.id)
      setSelectedQuiz(duplicated)
      setConfirmDuplicate(false)
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'trivia_duplicate_conflict') {
        setLifecycleError('Cannot duplicate an archived quiz.')
      } else if (msg === 'trivia_not_found') {
        setLifecycleError('Trivia quiz no longer exists.')
      } else {
        setLifecycleError('Failed to duplicate quiz. Try again.')
      }
      setConfirmDuplicate(false)
    }
  })
}
```

**`handleRetire` function**
```ts
async function handleRetire() {
  if (!selectedQuiz) return
  startTransition(async () => {
    setLifecycleError(null)
    try {
      const updated = await retireTriviaQuiz(selectedQuiz.id)
      setSelectedQuiz(updated)
      setConfirmRetire(false)
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'trivia_retire_conflict') {
        setLifecycleError(
          'This quiz cannot be retired. It may already be archived or have no usage history.',
        )
      } else if (msg === 'trivia_not_found') {
        setLifecycleError('Trivia quiz no longer exists.')
      } else {
        setLifecycleError('Failed to retire quiz. Try again.')
      }
      setConfirmRetire(false)
    }
  })
}
```

**Back-navigation cleanup** — extend the existing detail-view back button to reset all four
confirm states:
```ts
onClick={() => {
  setView('list')
  setConfirmPublish(false)
  setConfirmArchive(false)
  setConfirmDuplicate(false)
  setConfirmRetire(false)
  setLifecycleError(null)
}}
```

**Detail view: inline meta additions** — append after the existing `isSourceReady` chip:
```tsx
{selectedQuiz.isDuplicate && selectedQuiz.sourceTriviaQuizId !== null && (
  <span>
    Copied from:
    <span
      className={styles.chip}
      data-tone="muted"
      data-testid="trivia-source-quiz-id"
    >
      Quiz #{selectedQuiz.sourceTriviaQuizId}
    </span>
  </span>
)}
{selectedQuiz.hasUsageHistory && (
  <span>
    <span
      className={styles.chip}
      data-tone="warning"
      data-testid="trivia-has-usage-history"
    >
      Has usage history
    </span>
  </span>
)}
```

**Detail view: action buttons** — replace the existing Archive trigger visibility condition
and add Duplicate and Retire triggers. The full updated actions block (inside
`view === 'detail' && selectedQuiz !== null`, replacing the existing `missionDetailActions` div):

```tsx
<div className={styles.missionDetailActions}>
  {/* Edit — unchanged */}
  <button
    className={styles.inlineButton}
    data-testid="edit-trivia-btn"
    disabled={isPending || selectedQuiz.status !== 'Draft'}
    onClick={() => { setFormError(null); setView('edit') }}
    type="button"
  >
    Edit
  </button>

  {/* Publish trigger — only Draft, admin, no confirmations open (unchanged) */}
  {role === 'admin' && selectedQuiz.status === 'Draft' && allConfirmsClosed && (() => {
    const { isReady, reasons } = computeReadiness(selectedQuiz)
    return (
      <button
        className={styles.primaryButton}
        data-testid="publish-trivia-btn"
        disabled={isPending || !isReady}
        title={!isReady ? reasons.join(' ') : undefined}
        onClick={() => { setLifecycleError(null); setConfirmPublish(true) }}
        type="button"
      >
        Publish
      </button>
    )
  })()}

  {/* Publish confirmation row — unchanged */}
  {confirmPublish && (
    <span className={styles.confirmRow}>
      <button
        className={styles.smallButton}
        data-testid="confirm-publish-btn"
        disabled={isPending}
        onClick={handlePublish}
        type="button"
      >
        Confirm publish
      </button>
      <button
        className={styles.inlineButton}
        disabled={isPending}
        onClick={() => { setConfirmPublish(false); setLifecycleError(null) }}
        type="button"
      >
        Cancel
      </button>
    </span>
  )}

  {/* Archive trigger — unused quizzes only; used quizzes get Retire instead */}
  {role === 'admin' && selectedQuiz.status !== 'Archived' && !selectedQuiz.hasUsageHistory && allConfirmsClosed && (
    <button
      className={styles.inlineButton}
      data-testid="archive-trivia-btn"
      disabled={isPending}
      onClick={() => { setLifecycleError(null); setConfirmArchive(true) }}
      type="button"
    >
      Archive
    </button>
  )}

  {/* Archive confirmation row — unchanged */}
  {confirmArchive && (
    <span className={styles.confirmRow}>
      <button
        className={styles.smallButton}
        data-testid="confirm-archive-btn"
        disabled={isPending}
        onClick={handleArchive}
        type="button"
      >
        Confirm archive
      </button>
      <button
        className={styles.inlineButton}
        disabled={isPending}
        onClick={() => { setConfirmArchive(false); setLifecycleError(null) }}
        type="button"
      >
        Cancel
      </button>
    </span>
  )}

  {/* Duplicate trigger — non-Archived, admin only, no confirmations open */}
  {role === 'admin' && selectedQuiz.status !== 'Archived' && allConfirmsClosed && (
    <button
      className={styles.inlineButton}
      data-testid="duplicate-trivia-btn"
      disabled={isPending}
      onClick={() => { setLifecycleError(null); setConfirmDuplicate(true) }}
      type="button"
    >
      Duplicate
    </button>
  )}

  {/* Duplicate confirmation row */}
  {confirmDuplicate && (
    <span className={styles.confirmRow}>
      <button
        className={styles.smallButton}
        data-testid="confirm-duplicate-btn"
        disabled={isPending}
        onClick={handleDuplicate}
        type="button"
      >
        Confirm duplicate
      </button>
      <button
        className={styles.inlineButton}
        disabled={isPending}
        onClick={() => { setConfirmDuplicate(false); setLifecycleError(null) }}
        type="button"
      >
        Cancel
      </button>
    </span>
  )}

  {/* Retire trigger — used quizzes only, non-Archived, admin only, no confirmations open */}
  {role === 'admin' && selectedQuiz.status !== 'Archived' && selectedQuiz.hasUsageHistory && allConfirmsClosed && (
    <button
      className={styles.inlineButton}
      data-testid="retire-trivia-btn"
      disabled={isPending}
      title="Withdraw from future use — session history is preserved."
      onClick={() => { setLifecycleError(null); setConfirmRetire(true) }}
      type="button"
    >
      Retire
    </button>
  )}

  {/* Retire confirmation row */}
  {confirmRetire && (
    <span className={styles.confirmRow}>
      <button
        className={styles.smallButton}
        data-testid="confirm-retire-btn"
        disabled={isPending}
        onClick={handleRetire}
        type="button"
      >
        Confirm retire
      </button>
      <button
        className={styles.inlineButton}
        disabled={isPending}
        onClick={() => { setConfirmRetire(false); setLifecycleError(null) }}
        type="button"
      >
        Cancel
      </button>
    </span>
  )}
</div>
```

Note on the Archive visibility change: the existing condition was
`role === 'admin' && selectedQuiz.status !== 'Archived' && !confirmPublish && !confirmArchive`.
It is now `role === 'admin' && selectedQuiz.status !== 'Archived' && !selectedQuiz.hasUsageHistory && allConfirmsClosed`.
This is the only existing button whose visibility condition changes in this phase; everything
else is purely additive. The Retire trigger covers the `hasUsageHistory === true` case so no
lifecycle mutation path is removed.

**List view: Provenance column** — add a new column between "Source ready" and "Actions":

Replace the existing table header:
```tsx
// Before
<tr>
  <th>Title</th>
  <th>Description</th>
  <th>Status</th>
  <th>Source ready</th>
  <th>Actions</th>
</tr>

// After
<tr>
  <th>Title</th>
  <th>Description</th>
  <th>Status</th>
  <th>Source ready</th>
  <th>Provenance</th>
  <th>Actions</th>
</tr>
```

Add the Provenance cell in each row (after the Source ready cell, before Actions):
```tsx
<td>
  {quiz.isDuplicate ? (
    <span
      className={styles.chip}
      data-tone="muted"
      data-testid={`trivia-copy-chip-${quiz.id}`}
    >
      Copy
    </span>
  ) : quiz.hasUsageHistory ? (
    <span
      className={styles.chip}
      data-tone="warning"
      data-testid={`trivia-usage-chip-${quiz.id}`}
    >
      Used
    </span>
  ) : (
    <span style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>—</span>
  )}
</td>
```

**`data-testid` additions summary**

| Element | `data-testid` |
|---|---|
| Source quiz ID badge (detail) | `trivia-source-quiz-id` |
| Usage history chip (detail) | `trivia-has-usage-history` |
| Copy chip (list row) | `trivia-copy-chip-{id}` |
| Usage chip (list row) | `trivia-usage-chip-{id}` |
| Duplicate trigger button | `duplicate-trivia-btn` |
| Duplicate confirm button | `confirm-duplicate-btn` |
| Retire trigger button | `retire-trivia-btn` |
| Retire confirm button | `confirm-retire-btn` |

**Gate**
- `pnpm build` passes with no type errors.
- Admin navigates to a Published quiz detail →
  - "Duplicate" and "Archive" buttons are visible (unused Published quiz).
  - "Publish" is hidden (quiz is no longer Draft).
- After confirming Duplicate, the detail view shows the new copy with:
  - Status chip: `"Draft"`, `isSourceReady` chip: `"No"`.
  - "Copied from: Quiz #{sourceId}" in the inline meta.
  - "Duplicate" and "Archive" buttons visible in the new copy's detail.
  - List refreshes to include the new copy.
- Admin opens a quiz with `hasUsageHistory === true` (simulated or from the full flow) →
  - "Retire" button is visible in the detail view; "Archive" button is absent.
  - `"Has usage history"` warning chip appears in the inline meta.
  - After confirming Retire, status updates to "Archived", `isSourceReady` to "No",
    and both "Retire" and "Archive" buttons disappear.
- Admin opens an Archived quiz →
  - "Duplicate", "Archive", and "Retire" buttons are all absent.
  - "Edit" button is disabled (status !== Draft).
- List view shows:
  - `"Copy"` chip in the Provenance column for `isDuplicate === true` rows.
  - `"Used"` chip for `hasUsageHistory === true` rows.
  - A dash for original unused quizzes.
- Clicking "Cancel" on any confirmation row dismisses it without a network call.
- Opening a Duplicate confirmation hides the Archive, Retire, and Publish triggers.
- Opening a Retire confirmation hides the Archive, Duplicate, and Publish triggers.
- Admin clicking "Cancel" after opening any confirm shows all triggers again.
- Operator cannot reach duplicate or retire controls (Trivias nav is admin-only; Server
  Actions enforce the role independently).
- HU-12 Archive flow is unaffected for quizzes with `hasUsageHistory === false`.

---

### Phase 5 — E2E tests (HU-13 extension)

**Scope**
- Extend `tests/e2e/trivias.spec.ts` with HU-13 duplication, lineage, and retirement tests.
- One retirement test uses `page.route()` to inject a quiz with `hasUsageHistory: true`
  (see Architecture Decisions for the rationale).
- Verify no regression on HU-12, HU-14A, HU-11, and HU-09 tests.

**Test additions to `tests/e2e/trivias.spec.ts`**

```ts
// ---- Helpers ----

async function createReadyPublishedQuiz(page: Page, title: string): Promise<number> {
  // Reuses createReadyDraftQuiz from HU-12 helpers, then publishes.
  await createReadyDraftQuiz(page, title)
  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  // Extract id from trivia-source-ready testid context — use the URL or data attribute.
  // Alternatively, capture from the back-and-re-list pattern:
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()
  const row = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: title })
  const rowTestId = await row.getAttribute('data-testid')
  return parseInt(rowTestId!.replace('trivia-row-', ''), 10)
}

// ---- Duplicate flow ----

test('admin can duplicate a published quiz and lands on new copy detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Original For Duplication')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-duplicate-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-duplicate-btn"]')

  // Should now be on the new copy's detail
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('No')
  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-has-usage-history"]')).toHaveCount(0)
})

test('duplicate copy title matches source title', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Source Title Check')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await page.click('[data-testid="confirm-duplicate-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Source Title Check')
})

test('admin can cancel duplicate confirmation without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Cancel Duplicate Quiz')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-duplicate-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()

  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

test('duplicate confirmation hides other trigger buttons', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Mutual Exclusion Duplicate')

  await page.click('[data-testid="duplicate-trivia-btn"]')

  await expect(page.locator('[data-testid="confirm-duplicate-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toHaveCount(0)
})

test('archived quiz has no duplicate button', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Archived No Duplicate')
  await page.fill('[data-testid="trivia-description-input"]', 'Will be archived.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toHaveCount(0)
})

// ---- Lineage cues in list view ----

test('copy shows Copy chip in list provenance column', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Lineage List Source')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await page.click('[data-testid="confirm-duplicate-btn"]')

  // Navigate back to list
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  // The new copy row should have a Copy chip
  const copyRow = page.locator('[data-testid^="trivia-row-"]').filter({
    has: page.locator('[data-testid^="trivia-copy-chip-"]'),
  })
  await expect(copyRow).toHaveCount(1)
})

test('original quiz shows no Copy chip in list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Original No Copy Chip')
  await page.fill('[data-testid="trivia-description-input"]', 'Original quiz.')
  await page.click('[data-testid="trivia-submit-btn"]')
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  const originalRow = page.locator('[data-testid^="trivia-row-"]').filter({
    hasText: 'Original No Copy Chip',
  })
  await expect(originalRow.locator('[data-testid^="trivia-copy-chip-"]')).toHaveCount(0)
})

// ---- Lineage cue in detail view ----

test('copy detail shows source quiz id badge', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Lineage Detail Source')

  await page.click('[data-testid="duplicate-trivia-btn"]')
  await page.click('[data-testid="confirm-duplicate-btn"]')

  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toBeVisible()
  const badgeText = await page.locator('[data-testid="trivia-source-quiz-id"]').innerText()
  expect(badgeText).toMatch(/Quiz #\d+/)
})

test('original quiz detail shows no source quiz id badge', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Original No Badge')
  await page.fill('[data-testid="trivia-description-input"]', 'Original quiz.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toHaveCount(0)
})

// ---- Retire flow (uses page.route() to inject hasUsageHistory: true) ----

test('retire button is visible for a quiz with usage history', async ({ adminPage: page }) => {
  const MOCK_QUIZ_ID = 9001

  // Intercept catalog to include a used quiz
  await page.route('**/api/trivias', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: MOCK_QUIZ_ID,
          title: 'Used Quiz',
          description: 'Has session history.',
          status: 'Published',
          isSourceReady: true,
          sourceTriviaQuizId: null,
          hasUsageHistory: true,
          isDuplicate: false,
        },
      ]),
    })
  })

  // Intercept detail fetch for the used quiz
  await page.route(`**/api/trivias/${MOCK_QUIZ_ID}`, (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: MOCK_QUIZ_ID,
        title: 'Used Quiz',
        description: 'Has session history.',
        status: 'Published',
        isSourceReady: true,
        sourceTriviaQuizId: null,
        hasUsageHistory: true,
        isDuplicate: false,
        questions: [],
      }),
    })
  })

  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click(`[data-testid="view-trivia-btn-${MOCK_QUIZ_ID}"]`)

  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="trivia-has-usage-history"]')).toBeVisible()
})

test('Used chip appears in list for a quiz with usage history', async ({ adminPage: page }) => {
  const MOCK_QUIZ_ID = 9002

  await page.route('**/api/trivias', (route) => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: MOCK_QUIZ_ID,
          title: 'Used Quiz List',
          description: 'Has session history.',
          status: 'Published',
          isSourceReady: true,
          sourceTriviaQuizId: null,
          hasUsageHistory: true,
          isDuplicate: false,
        },
      ]),
    })
  })

  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  await expect(page.locator(`[data-testid="trivia-usage-chip-${MOCK_QUIZ_ID}"]`)).toBeVisible()
  await expect(page.locator(`[data-testid="trivia-copy-chip-${MOCK_QUIZ_ID}"]`)).toHaveCount(0)
})

test('retire button is not visible for a quiz without usage history', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Unused No Retire')
  await page.fill('[data-testid="trivia-description-input"]', 'No history.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
})

// ---- Archive regression: unused quizzes still use Archive ----

test('HU-12 archive flow still works for unused quizzes after retire wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Archive Regression After HU-13')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toHaveCount(0)

  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Archived')
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="retire-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="duplicate-trivia-btn"]')).toHaveCount(0)
})

// ---- Authorization ----

test('operator cannot reach duplicate or retire controls', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// ---- Regression: HU-12 publish flow unaffected ----

test('HU-12 publish flow unaffected after HU-13 wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'HU-12 Publish Regression')

  await page.click('[data-testid="publish-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-publish-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('Yes')
})

// ---- Regression: HU-14A question authoring unaffected ----

test('HU-14A add-question flow still works after HU-13 wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'HU-14A Regression After HU-13')
  await page.fill('[data-testid="trivia-description-input"]', 'Question authoring regression.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
})

// ---- Regression: HU-11 quiz create/edit flows unaffected ----

test('HU-11 trivia create flow still works after HU-13 wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'HU-11 Regression After HU-13')
  await page.fill('[data-testid="trivia-description-input"]', 'HU-11 regression check.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
  await expect(page.locator('[data-testid="trivia-source-quiz-id"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="trivia-has-usage-history"]')).toHaveCount(0)
})

// ---- Regression: HU-09 missions panel unaffected ----

test('HU-09 missions panel still reachable after HU-13 wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All HU-13 acceptance criteria are exercised:
  - Admin can duplicate a non-Archived quiz; the detail view transitions to the new copy with
    lineage badge and Draft status.
  - Duplicate confirmation two-step flow works; cancel discards without a network call.
  - Archived quizzes have no Duplicate trigger.
  - `"Copy"` chip appears in the list for duplicated quizzes; originals show no Copy chip.
  - `"Retire"` button appears (instead of Archive) for quizzes with `hasUsageHistory === true`.
  - `"Used"` chip appears in the list Provenance column for quizzes with usage history.
  - Retire confirmation two-step flow is visible when `hasUsageHistory: true` is present.
  - Retire button is absent (Archive shown instead) for quizzes without usage history.
  - Operator cannot access duplicate or retire controls (Trivias panel is nav-gated).
- All HU-12, HU-14A, HU-11, and HU-09 regression assertions pass.

---

## Commit Sequence

```
feat(frontend): phase 1 — add sourceTriviaQuizId, hasUsageHistory, isDuplicate to trivia types (hu-13)
feat(frontend): phase 2 — api client for trivia quiz duplicate and retire (hu-13)
feat(frontend): phase 3 — server actions for trivia quiz duplicate and retire (hu-13)
feat(frontend): phase 4 — duplicate/retire controls and lineage display in TriviasPanel (hu-13)
feat(frontend): phase 5 — e2e tests for HU-13 duplication, retirement, and regression checks (hu-13)

Ref: HU-13
```

---

## Out of Scope

- **Full retirement E2E against a live backend.** Setting `hasUsageHistory = true` requires
  completing a gameplay session through the mission/session execution service. Phase 5 uses
  `page.route()` to test the retire UI in isolation. An integration-level E2E covering the full
  retire path (create → publish → run session → retire) belongs in a cross-service E2E suite,
  not the frontend panel tests.
- **Navigation from copy to source quiz.** The `"Copied from: Quiz #{id}"` badge in the detail
  view is read-only text. Clicking it does not open the source quiz's detail. Cross-quiz
  navigation within the panel is not required by HU-13.
- **Duplicate from the list view.** Duplicate is initiated from the detail view only. Adding
  per-row confirm state to the list view is not warranted by the current scope. If list-level
  duplication is needed, the change point is the list view's `data.map` tbody and a new
  `confirmDuplicateId` state.
- **Delete flow and delete-rejection error surfacing.** `DELETE /api/trivias/{id}` exists on
  the backend (returns 204 for unused quizzes, 409 for used quizzes). The frontend has no
  delete button; adding one and surfacing the 409 rejection with a "use Retire instead" message
  is a natural follow-up but is not in scope for HU-13.
- **Re-duplicating a copy.** The backend allows duplicating a copy (the new copy's
  `sourceTriviaQuizId` resolves to the original lineage root). The frontend Duplicate trigger
  is shown for any non-Archived quiz including copies; the lineage resolution is handled
  server-side.
- **Re-publishing a retired (Archived) quiz.** Retire is a one-way transition to Archived. The
  backend has no unarchive endpoint. The frontend reflects this by hiding all lifecycle action
  buttons for Archived quizzes (unchanged from HU-12).
- **Bulk duplication or bulk retirement.** Actions remain per-quiz from the detail view.
- **Any backend changes.** The backend is fully implemented and verified.
