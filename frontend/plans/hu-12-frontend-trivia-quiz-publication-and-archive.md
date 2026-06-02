# Plan: HU-12 Frontend — Trivia Quiz Publication and Archive

**Ref:** HU-12
**Branch:** feature/hu-12-trivia-quiz-publication-and-archive
**Date:** 2026-06-02
**Builds on:** HU-14A frontend (TriviasPanel full question authoring, `app/lib/trivias.ts`,
`app/actions/trivias.ts`, `TriviaQuizDto`, `TriviaQuestionDto`, DashboardClient wiring)

---

## Context

Backend HU-12 (`mission-design-service`) is fully implemented. Two new endpoints are
available beyond what HU-14A covered:

- `POST /api/trivias/{id}/publish` — publish a Draft quiz; `Administrator` only
- `POST /api/trivias/{id}/archive` — archive a Draft or Published quiz; `Administrator` only

Both endpoints return `200 OK` with the full `TriviaQuizResponse`. HU-12 also exposes a
new field — `isSourceReady: boolean` — on every quiz response (catalog and detail). This
field is the backend's authoritative signal that a quiz is eligible for session assignment
(`isSourceReady = status === "Published"`).

HU-11 and HU-14A left four gaps that HU-12 must close:

1. **`isSourceReady` missing from frontend types.** Both `TriviaQuizSummaryDto` and
   `TriviaQuizDto` in `definitions.ts` do not declare `isSourceReady`. The backend has
   always serialized this field; the current types silently drop it. Adding it enables
   correct session-eligibility cues in both the list and detail views.
2. **No API client functions for publish and archive.** `lib/trivias.ts` has no
   `publishTriviaQuiz` or `archiveTriviaQuiz`.
3. **No Server Actions for lifecycle transitions.** `actions/trivias.ts` has no
   `publishTriviaQuiz` or `archiveTriviaQuiz`.
4. **`TriviasPanel` has no publish or archive UI.** The detail view shows a status chip
   and an "Edit" button. The admin has no way to trigger a lifecycle transition from the
   dashboard.

---

## Verified Backend Contract

### Common headers (trust envelope)

```
X-User-Id:    {session.externalIdentityId}
X-User-Role:  {session.role}
X-User-Email: {session.email}
```

---

### `POST /api/trivias/{id}/publish`

No request body. The `id` path parameter is the numeric quiz id.

**Response `200 OK`** — `TriviaQuizResponse`

```ts
{
  id: number
  title: string
  description: string
  status: "Published"     // confirmed by integration test
  isSourceReady: true     // confirmed: Published → isSourceReady = true
  questions: TriviaQuestionResponse[]
}
```

**Error cases**
- `401` — missing trust headers
- `403` — caller is not `Administrator`
- `404` — quiz not found
- `409 Conflict`, title `"Trivia quiz cannot be published in its current state."` — quiz is
  not in Draft status (already Published or Archived)
- `409 Conflict`, title `"Trivia quiz is not ready for publication."` — quiz is Draft but
  fails the publication policy:
  - No questions (`"at least one question"` in detail)
  - A question is missing `scoreValue`
  - A question is missing `timeLimitSeconds`

**Additional verified behaviour (from integration tests)**
- After publish, `GET /api/trivias/{id}` returns `status: "Published"` and `isSourceReady: true`.
- After publish, `GET /api/trivias/` catalog also reflects `status: "Published"` and `isSourceReady: true`.
- A quiz with at least one question (with score value, time limit, 2–4 options, one correct)
  passes the readiness check and publishes successfully.
- An empty quiz (no questions) returns `409` with title `"Trivia quiz is not ready for publication."`.
- A non-Administrator caller returns `403` with title `"Forbidden."`.

---

### `POST /api/trivias/{id}/archive`

No request body.

**Response `200 OK`** — `TriviaQuizResponse`

```ts
{
  id: number
  title: string
  description: string
  status: "Archived"      // confirmed by integration test
  isSourceReady: false    // confirmed: Archived → isSourceReady = false
  questions: TriviaQuestionResponse[]
}
```

**Error cases**
- `401` — missing trust headers
- `403` — caller is not `Administrator`
- `404` — quiz not found
- `409 Conflict`, title `"Trivia quiz cannot be archived in its current state."` — quiz is
  already Archived

**Additional verified behaviour (from integration tests)**
- A **Published** quiz can be archived (the integration test archives from Published).
- A **Draft** quiz can also be archived (domain: `ArchiveTriviaQuizLifecycleTemplate`
  only blocks when `status == Archived`).
- After archive, `GET /api/trivias/{id}` returns `status: "Archived"` and
  `isSourceReady: false`.
- After archive, the catalog reflects the same state.
- A non-Administrator caller returns `403`.

---

### Updated `TriviaQuizSummaryResponse` and `TriviaQuizResponse` shapes

Both responses now include `isSourceReady`. This field has been serialized by the backend
since HU-12 was merged and is silently dropped by the current frontend types.

**`TriviaQuizSummaryResponse`** (catalog):
```ts
{
  id: number
  title: string
  description: string
  status: string        // "Draft" | "Published" | "Archived"
  isSourceReady: boolean
}
```

**`TriviaQuizResponse`** (detail, publish, archive responses):
```ts
{
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
  questions: TriviaQuestionResponse[]
}
```

---

### Publication readiness policy (from domain, verified by `TriviaPublicationPolicy.cs`)

A Draft quiz passes the readiness check if **all** of the following hold:

1. `quiz.questions.length >= 1`
2. Every question has `scoreValue !== null`
3. Every question has `timeLimitSeconds !== null`
4. Every question has `2 <= options.length <= 4`
5. Every question has exactly one option with `isCorrect === true`

These are the same rules verified by the integration tests. The frontend readiness
computation in Phase 4 mirrors them to disable the Publish button before a round-trip.

---

## Architecture Decisions

- **`isSourceReady` is read from the backend, never computed client-side.** The UI reads
  `quiz.isSourceReady` from the DTO for its "session eligible" indicator — it does not
  compute `status === 'Published'` independently. If the backend's definition of readiness
  evolves, the frontend reflects it automatically without a code change.
- **Publish and archive use the same two-step confirmation pattern as deactivation.**
  Each lifecycle button shows a "Confirm / Cancel" pair in place of the trigger button
  so accidental clicks do not fire a network request. The confirm state is local to the
  detail view; the confirmation is reset on cancel or on a successful transition.
- **Publish button is disabled when the client-side readiness check fails.** The frontend
  mirrors `TriviaPublicationPolicy` using the `selectedQuiz.questions` data already in
  local state. A disabled button with a `title` attribute lists the unmet conditions.
  This prevents most 409 publish-readiness failures from reaching the backend. The button
  is only enabled when all conditions are met in local state.
- **Publish button is hidden when `status !== 'Draft'`.** A Published or Archived quiz
  cannot be re-published (the backend returns 409). Hiding the button is cleaner than
  showing it disabled; the status chip already communicates the current state.
- **Archive button is shown when `status !== 'Archived'`.** Both Draft and Published
  quizzes can be archived per the backend contract. The button is hidden only when the
  quiz is already Archived.
- **Confirm-publish and confirm-archive states are mutually exclusive.** Opening either
  confirmation automatically clears the other. This prevents a confusing state where both
  confirm rows are simultaneously visible.
- **`handlePublish` and `handleArchive` distinguish two 409 scenarios by error code.**
  `publishTriviaQuiz` in `lib/trivias.ts` throws `'trivia_publish_conflict'` for both
  wrong-state and readiness failures (both map to `409`); the UI message covers both.
  The client-side readiness check means the readiness variant rarely reaches the network.
  `archiveTriviaQuiz` throws `'trivia_archive_conflict'` for the wrong-state case.
- **`setSelectedQuiz` is updated from the API response after a lifecycle transition.**
  Both publish and archive return the full `TriviaQuizResponse`. Setting `selectedQuiz`
  to the returned DTO immediately updates the status chip, `isSourceReady` badge,
  and action button visibility without a follow-up GET.
- **`revalidatePath('/dashboard')` after publish and archive.** Clears the RSC cache so
  the quiz catalog reflects the new status on the next list load.
- **Readiness indicator section on the detail view.** When `status === 'Draft'` and the
  readiness check fails, a collapsible (or always-visible) section lists the unmet
  conditions. When the check passes, the section is hidden (or shows a "Ready to publish"
  success hint). This is a pure render from `computeReadiness(selectedQuiz)` — no
  additional network calls.
- **`isSourceReady` shown in both the list and the detail view.** The catalog table gains
  a "Source ready" column with a chip (`success` tone when `true`, `muted` when `false`).
  The detail view shows a `data-testid="trivia-source-ready"` chip alongside the status
  chip. Using the backend field directly ensures correctness even if the readiness
  definition changes.
- **No changes to existing question authoring flows.** The publish/archive buttons are
  additive within the detail view action area. The existing Edit, Add question, and
  Edit question flows are unaffected.
- **Server Actions enforce Administrator-only at the action layer.** Both
  `publishTriviaQuiz` and `archiveTriviaQuiz` in `actions/trivias.ts` check
  `session.role !== 'Administrator'` before reaching the backend, consistent with the
  existing pattern.

---

## Environment

No new environment variables. `MISSION_DESIGN_SERVICE_URL` (`http://localhost:5001`)
already covers the publish and archive endpoints — the same service is reused.

---

## Phases

### Phase 1 — Type updates (`isSourceReady`)

**Scope**
- Add `isSourceReady: boolean` to `TriviaQuizSummaryDto` and `TriviaQuizDto` in
  `app/lib/definitions.ts`.

**`app/lib/definitions.ts` changes**
```ts
// Before
export type TriviaQuizSummaryDto = {
  id: number
  title: string
  description: string
  status: string
}

// After
export type TriviaQuizSummaryDto = {
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
}

// Before
export type TriviaQuizDto = {
  id: number
  title: string
  description: string
  status: string
  questions: TriviaQuestionDto[]
}

// After
export type TriviaQuizDto = {
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
  questions: TriviaQuestionDto[]
}
```

**Gate**
- `pnpm build` passes with no type errors.
- No runtime regression: the field was already serialized by the backend; adding it to the
  type only makes it accessible. Existing renders are unaffected until Phase 4.

---

### Phase 2 — API client additions (`lib/trivias.ts`)

**Scope**
- Add `publishTriviaQuiz` and `archiveTriviaQuiz` to `app/lib/trivias.ts`.

**Additions to `app/lib/trivias.ts`**
```ts
export async function publishTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/publish`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_publish_conflict')
  if (!response.ok) throw new IdentityError('unknown', `publishTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function archiveTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/archive`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_archive_conflict')
  if (!response.ok) throw new IdentityError('unknown', `archiveTriviaQuiz failed with status ${response.status}`)
  return response.json()
}
```

Note: both endpoints have no request body — no `Content-Type` header is needed.
The `409` case for publish covers both wrong-state and not-ready scenarios; the
client-side readiness check (Phase 4) prevents the not-ready variant from reaching the
network in most cases.

**Gate**
- `pnpm build` passes with no type errors.

---

### Phase 3 — Server Action additions (`actions/trivias.ts`)

**Scope**
- Add `publishTriviaQuiz` and `archiveTriviaQuiz` Server Actions to
  `app/actions/trivias.ts`.

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
} from '@/app/lib/trivias'
```

**Server Action additions**
```ts
export async function publishTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await publishTriviaQuizLib(id)
  revalidatePath('/dashboard')
  return result
}

export async function archiveTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await archiveTriviaQuizLib(id)
  revalidatePath('/dashboard')
  return result
}
```

**Gate**
- `pnpm build` passes with no type errors.
- Calling `publishTriviaQuiz` or `archiveTriviaQuiz` from an Operator session throws
  `'Forbidden'` before reaching the backend.

---

### Phase 4 — TriviasPanel UI: publish/archive controls and `isSourceReady` display

**Scope**
- Import `publishTriviaQuiz` and `archiveTriviaQuiz` from `@/app/actions/trivias`.
- Add `confirmPublish`, `confirmArchive`, and `lifecycleError` state.
- Add `handlePublish` and `handleArchive` handlers.
- Add `computeReadiness` helper.
- Update the detail view: publish/archive buttons with confirmation flow, readiness
  indicator, and `isSourceReady` badge.
- Update the list view: add a "Source ready" column using the `isSourceReady` field.

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
} from '@/app/actions/trivias'
```

**State additions inside `TriviasPanel`**
```ts
const [confirmPublish, setConfirmPublish] = useState(false)
const [confirmArchive, setConfirmArchive] = useState(false)
const [lifecycleError, setLifecycleError] = useState<string | null>(null)
```

**`computeReadiness` helper** (pure function, defined outside the component)
```ts
function computeReadiness(quiz: TriviaQuizDto): { isReady: boolean; reasons: string[] } {
  const reasons: string[] = []
  if (quiz.questions.length === 0) {
    reasons.push('At least one question is required.')
  }
  for (const q of quiz.questions) {
    if (q.scoreValue === null) {
      reasons.push(`Question ${q.sequenceOrder}: score value is required.`)
    }
    if (q.timeLimitSeconds === null) {
      reasons.push(`Question ${q.sequenceOrder}: time limit is required.`)
    }
    if (q.options.length < 2 || q.options.length > 4) {
      reasons.push(`Question ${q.sequenceOrder}: must have 2–4 options.`)
    }
    if (q.options.filter((o) => o.isCorrect).length !== 1) {
      reasons.push(`Question ${q.sequenceOrder}: exactly one correct option required.`)
    }
  }
  return { isReady: reasons.length === 0, reasons }
}
```

**`handlePublish` function**
```ts
async function handlePublish() {
  if (!selectedQuiz) return
  startTransition(async () => {
    setLifecycleError(null)
    try {
      const updated = await publishTriviaQuiz(selectedQuiz.id)
      setSelectedQuiz(updated)
      setConfirmPublish(false)
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'trivia_publish_conflict') {
        setLifecycleError(
          'Publication failed. Ensure the quiz is in Draft status and all questions have score values, time limits, and valid options.',
        )
      } else if (msg === 'trivia_not_found') {
        setLifecycleError('Trivia quiz no longer exists.')
      } else {
        setLifecycleError('Failed to publish quiz. Try again.')
      }
      setConfirmPublish(false)
    }
  })
}
```

**`handleArchive` function**
```ts
async function handleArchive() {
  if (!selectedQuiz) return
  startTransition(async () => {
    setLifecycleError(null)
    try {
      const updated = await archiveTriviaQuiz(selectedQuiz.id)
      setSelectedQuiz(updated)
      setConfirmArchive(false)
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'trivia_archive_conflict') {
        setLifecycleError('This quiz cannot be archived in its current state.')
      } else if (msg === 'trivia_not_found') {
        setLifecycleError('Trivia quiz no longer exists.')
      } else {
        setLifecycleError('Failed to archive quiz. Try again.')
      }
      setConfirmArchive(false)
    }
  })
}
```

**Detail view updates** — replace the existing `missionDetailActions` block and add the
readiness indicator and `isSourceReady` badge. The updated detail section (inside the
`view === 'detail' && selectedQuiz !== null` branch):

```tsx
// Source ready badge — alongside status in the inline meta row
<div className={styles.missionDetailInlineMeta}>
  <span>
    Status:
    <span
      className={styles.chip}
      data-tone={statusTone(selectedQuiz.status)}
      data-testid="trivia-detail-status"
    >
      {selectedQuiz.status}
    </span>
  </span>
  <span>
    Source ready:
    <span
      className={styles.chip}
      data-tone={selectedQuiz.isSourceReady ? 'success' : 'muted'}
      data-testid="trivia-source-ready"
    >
      {selectedQuiz.isSourceReady ? 'Yes' : 'No'}
    </span>
  </span>
</div>

// Readiness indicator — only shown for Draft quizzes with unmet conditions
{selectedQuiz.status === 'Draft' && (() => {
  const { isReady, reasons } = computeReadiness(selectedQuiz)
  return !isReady ? (
    <div className={styles.missionDetailDescCard} data-testid="trivia-readiness-indicator">
      <span className={styles.missionDetailDescLabel}>Publication readiness</span>
      <ul style={{ margin: 0, paddingLeft: '1.2rem', color: 'var(--text-secondary)' }}>
        {reasons.map((r, i) => <li key={i}>{r}</li>)}
      </ul>
    </div>
  ) : null
})()}

// Lifecycle error display
{lifecycleError && (
  <p className={styles.formError} role="alert" data-testid="lifecycle-error">
    {lifecycleError}
  </p>
)}

// Action buttons
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

  {/* Publish trigger — only when Draft, admin only, not already confirming */}
  {role === 'admin' && selectedQuiz.status === 'Draft' && !confirmPublish && !confirmArchive && (() => {
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

  {/* Publish confirmation row */}
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

  {/* Archive trigger — when not Archived, admin only, not already confirming */}
  {role === 'admin' && selectedQuiz.status !== 'Archived' && !confirmPublish && !confirmArchive && (
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

  {/* Archive confirmation row */}
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
</div>
```

Note: the two `computeReadiness(selectedQuiz)` IIFE calls in the detail render are each
cheap (array scan). Alternatively, derive a `readiness` const at the top of the detail
branch to avoid the double call.

**List view updates** — add a "Source ready" column to the catalog table.

Replace the existing table header:
```tsx
// Before
<tr>
  <th>Title</th>
  <th>Description</th>
  <th>Status</th>
  <th>Actions</th>
</tr>

// After
<tr>
  <th>Title</th>
  <th>Description</th>
  <th>Status</th>
  <th>Source ready</th>
  <th>Actions</th>
</tr>
```

Add the "Source ready" cell in each row (after the Status cell):
```tsx
<td>
  <span
    className={styles.chip}
    data-tone={quiz.isSourceReady ? 'success' : 'muted'}
    data-testid={`trivia-source-ready-${quiz.id}`}
  >
    {quiz.isSourceReady ? 'Yes' : 'No'}
  </span>
</td>
```

**State cleanup on back navigation** — when the admin navigates back to the list from
the detail view, reset `confirmPublish`, `confirmArchive`, and `lifecycleError` so
stale confirmation rows do not appear if the user reopens the same detail:
```ts
// In the back button onClick of the detail view:
onClick={() => {
  setView('list')
  setConfirmPublish(false)
  setConfirmArchive(false)
  setLifecycleError(null)
}}
```

**`data-testid` additions**

| Element | `data-testid` |
|---|---|
| Source ready badge (detail) | `trivia-source-ready` |
| Source ready chip (list row) | `trivia-source-ready-{id}` |
| Readiness indicator section | `trivia-readiness-indicator` |
| Publish trigger button | `publish-trivia-btn` |
| Publish confirm button | `confirm-publish-btn` |
| Archive trigger button | `archive-trivia-btn` |
| Archive confirm button | `confirm-archive-btn` |
| Lifecycle error message | `lifecycle-error` |

**Gate**
- `pnpm build` passes with no type errors.
- Admin navigates to a Draft quiz detail → "Publish" and "Archive" buttons are visible.
- "Publish" is disabled when the quiz has no questions (readiness check); `title` attribute
  lists the reason.
- "Publish" is enabled when all readiness conditions are met.
- Clicking "Publish" shows "Confirm publish / Cancel"; clicking "Confirm publish" triggers
  the action, the status chip updates to "Published", `isSourceReady` chip shows "Yes".
- After publishing, "Publish" button is hidden (status is no longer Draft); "Archive"
  button remains visible.
- Clicking "Archive" shows "Confirm archive / Cancel"; on confirm, status updates to
  "Archived" and both Publish and Archive buttons disappear.
- Clicking "Cancel" on either confirmation row dismisses it without a network call.
- Archived quiz detail shows no Publish or Archive buttons.
- Operator navigating to the trivias panel (which is admin-only) is blocked by nav
  filtering; the Server Actions reject non-admin callers independently.
- List view shows a "Source ready" column; Published quizzes show "Yes" with success
  tone; Draft and Archived show "No" with muted tone.

---

### Phase 5 — E2E tests (HU-12 extension)

**Scope**
- Extend `tests/e2e/trivias.spec.ts` with HU-12 lifecycle tests.
- Verify no regression on HU-11, HU-14A, or HU-09 tests.

**Test additions to `tests/e2e/trivias.spec.ts`**

```ts
// ---- Helpers ----

async function createReadyDraftQuiz(page: Page, title: string): Promise<void> {
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', title)
  await page.fill('[data-testid="trivia-description-input"]', 'Lifecycle test quiz.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Add one question to satisfy the readiness check
  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'What is 2+2?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '100')
  await page.fill('[data-testid="question-timer-input"]', '30')
  await page.fill('[data-testid="question-option-text-0"]', '4')
  await page.fill('[data-testid="question-option-text-1"]', '5')
  await page.click('[data-testid="question-option-correct-0"]')
  await page.click('[data-testid="question-submit-btn"]')
  // now on detail view with one question
}

// ---- isSourceReady display ----

test('draft quiz shows source ready as No in list view', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Source Ready Test')
  await page.fill('[data-testid="trivia-description-input"]', 'Should be not ready.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Back to list
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  const row = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Source Ready Test' })
  const chip = row.locator('[data-testid^="trivia-source-ready-"]')
  await expect(chip).toContainText('No')
})

test('draft quiz detail shows source ready badge as No', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Badge Check Draft')
  await page.fill('[data-testid="trivia-description-input"]', 'Draft badge.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('No')
})

// ---- Publish button readiness gate ----

test('publish button is disabled for empty draft quiz', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Empty Draft')
  await page.fill('[data-testid="trivia-description-input"]', 'No questions.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toBeDisabled()
  await expect(page.locator('[data-testid="trivia-readiness-indicator"]')).toBeVisible()
})

test('publish button is enabled when all readiness conditions are met', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Ready To Publish Quiz')

  await expect(page.locator('[data-testid="publish-trivia-btn"]')).not.toBeDisabled()
  await expect(page.locator('[data-testid="trivia-readiness-indicator"]')).toHaveCount(0)
})

// ---- Publish flow ----

test('admin can publish a ready draft quiz', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Publishable Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-publish-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('Yes')
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
})

test('published quiz shows source ready as Yes in list view', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Published List Check')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')

  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()

  const row = page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Published List Check' })
  await expect(row.locator('[data-testid^="trivia-source-ready-"]')).toContainText('Yes')
})

test('admin can cancel publish confirmation without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Cancel Publish Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()

  // Should be back to showing the trigger buttons — status unchanged
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

// ---- Archive flow ----

test('admin can archive a published quiz', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Archive From Published')

  // Publish first
  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')

  // Archive
  await page.click('[data-testid="archive-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-archive-btn"]')).toBeVisible()
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Archived')
  await expect(page.locator('[data-testid="trivia-source-ready"]')).toContainText('No')
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="publish-trivia-btn"]')).toHaveCount(0)
})

test('admin can archive a draft quiz directly', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Archive From Draft')
  await page.fill('[data-testid="trivia-description-input"]', 'Archiving draft directly.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Archived')
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
})

test('admin can cancel archive confirmation without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Cancel Archive Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')

  await page.click('[data-testid="archive-trivia-btn"]')
  await expect(page.locator('[data-testid="confirm-archive-btn"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).first().click()

  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Published')
})

// ---- Edit gate after lifecycle transitions ----

test('edit button is disabled for published quiz', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Published No Edit')

  await page.click('[data-testid="publish-trivia-btn"]')
  await page.click('[data-testid="confirm-publish-btn"]')

  await expect(page.locator('[data-testid="edit-trivia-btn"]')).toBeDisabled()
})

test('edit button is disabled for archived quiz', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Archived No Edit')
  await page.fill('[data-testid="trivia-description-input"]', 'Will be archived.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="archive-trivia-btn"]')
  await page.click('[data-testid="confirm-archive-btn"]')

  await expect(page.locator('[data-testid="edit-trivia-btn"]')).toBeDisabled()
})

// ---- Confirm-state mutual exclusion ----

test('opening publish confirmation hides archive trigger', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Mutual Exclusion Quiz')

  await page.click('[data-testid="publish-trivia-btn"]')

  // Publish confirm is visible, archive trigger must be gone
  await expect(page.locator('[data-testid="confirm-publish-btn"]')).toBeVisible()
  await expect(page.locator('[data-testid="archive-trivia-btn"]')).toHaveCount(0)
})

// ---- Authorization ----

test('operator cannot reach publish or archive controls', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  // Operators do not see trivias nav — panel is unreachable
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// ---- Regression: HU-14A question authoring unaffected ----

test('HU-14A add-question flow still works after lifecycle wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression: Add Question')
  await page.fill('[data-testid="trivia-description-input"]', 'Question authoring regression.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
})

test('HU-14A edit-question flow still works after lifecycle wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await createReadyDraftQuiz(page, 'Regression: Edit Question')

  await page.locator('[data-testid^="edit-question-btn-"]').first().click()
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.fill('[data-testid="question-prompt-input"]', 'Updated prompt after lifecycle wiring')
  await page.click('[data-testid="question-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('Updated prompt after lifecycle wiring')
})

// ---- Regression: HU-11 quiz create/edit flows unaffected ----

test('HU-11 trivia create flow still works after publish/archive wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression HU-11 Create')
  await page.fill('[data-testid="trivia-description-input"]', 'HU-11 regression check.')
  await page.click('[data-testid="trivia-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

test('HU-11 trivia edit flow still works after publish/archive wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression HU-11 Edit')
  await page.fill('[data-testid="trivia-description-input"]', 'Before edit.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="edit-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression HU-11 Edit — Updated')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Regression HU-11 Edit — Updated')
})

// ---- Regression: HU-09 missions panel unaffected ----

test('HU-09 missions panel still reachable after lifecycle wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All HU-12 acceptance criteria are exercised:
  - Admin can publish a ready draft quiz; status updates to Published; `isSourceReady` updates to Yes.
  - Publish button is disabled when the readiness check fails; enabled when it passes.
  - Admin can archive a Draft or Published quiz; status updates to Archived; `isSourceReady` updates to No.
  - Confirmation flow is required for both publish and archive; cancel discards without a network call.
  - Archived quizzes show no lifecycle action buttons.
  - Operator cannot access any lifecycle mutation UI (panel is nav-gated; Server Actions enforce role).
  - `isSourceReady` is visible in both the list catalog and the detail view.
- All HU-14A, HU-11, and HU-09 regression assertions pass.

---

## Commit Sequence

```
feat(frontend): phase 1 — add isSourceReady to trivia quiz types (hu-12)
feat(frontend): phase 2 — api client for trivia quiz publish and archive (hu-12)
feat(frontend): phase 3 — server actions for trivia quiz publish and archive (hu-12)
feat(frontend): phase 4 — publish/archive controls and isSourceReady display in TriviasPanel (hu-12)
feat(frontend): phase 5 — e2e tests for HU-12 trivia lifecycle and regression checks (hu-12)

Ref: HU-12
```

---

## Out of Scope

- **Re-publishing an archived quiz.** The backend's `ArchiveTriviaQuizLifecycleTemplate`
  is a one-way transition — there is no "unarchive" endpoint. The frontend reflects this
  by hiding all lifecycle buttons for Archived quizzes.
- **Bulk lifecycle transitions.** The catalog list provides no mass-publish or mass-archive
  affordance. Actions remain per-quiz from the detail view.
- **Session assignment UI.** `isSourceReady` is displayed as a read-only cue so admins
  know which quizzes are eligible for session use. The actual session-assignment workflow
  belongs to a separate HU.
- **Publish readiness server-side re-validation error surfacing.** The client-side
  `computeReadiness` check mirrors `TriviaPublicationPolicy` and prevents the readiness
  409 in almost all cases. If the backend disagrees (e.g., concurrent state change), the
  generic `trivia_publish_conflict` error message is shown. A per-failure-reason error
  parse from the ProblemDetails body is not implemented.
- **Operator trivia read access.** The existing nav filter excludes operators from the
  trivias panel. If read access for operators is ever required, the nav filter and the
  `getTriviaQuizzes` / `getTriviaQuiz` actions are the right change points — no lifecycle
  logic changes are needed.
- **Any backend changes.** The backend is fully implemented and verified.
