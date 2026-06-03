# Plan: HU-16 Frontend — Create Trivia Session

**Ref:** HU-16
**Branch:** feature/hu-16-trivia-session-creation
**Date:** 2026-06-03
**Builds on:** HU-03 (role system, session types, identity headers), HU-11/12 (trivia catalog, TriviasPanel pattern), HU-09 (MissionsPanel pattern)

---

## Context

Backend HU-16 is fully implemented (phases X.1–X.4, commits `6d8172d`–`ad62522`). Two
backend services are involved:

- **session-operations** — `POST /api/sessions` creates the session and its immutable
  fixed-copy snapshot of the source quiz.
- **mission-design** — `GET /api/trivias` lists all quizzes; already wired in
  `TriviasPanel`. Reused here to load the published-quiz selector.

Three new frontend concerns beyond what HU-11/12 already handles:

1. **Published-quiz catalog view** — the trivia catalog is filtered to `Published` only
   before populating the selector; `Draft` and `Archived` quizzes must never appear.
2. **Session creation form** — the operator picks one published quiz, enters a session
   title, maximum time in minutes, and a scheduled-at datetime, then submits to
   `POST /api/sessions`.
3. **Created-session card** — on success, display `liveSessionId`, `sessionCode`, `title`,
   `sessionState` ("Scheduled"), `questionCount`, and `sourceTriviaQuizId` confirming the
   fixed-copy binding. On a 409 (quiz no longer Published at creation time), show an inline
   error banner and keep the form intact.

---

## Verified Backend Contracts

### `GET /api/trivias` — mission-design service

Already implemented and called from `app/lib/trivias.ts → listTriviaQuizzes()`. Reused
here; only the client-side filter to `Published` is new.

**Response array item**
```ts
{
  id: number
  title: string
  description: string
  status: string           // "Draft" | "Published" | "Archived"
  isSourceReady: boolean
  sourceTriviaQuizId: number | null
  hasUsageHistory: boolean
  isDuplicate: boolean
}
```

Filter: `status === 'Published'` — applied in the Server Action before the selector is
populated. The backend publication gate (409) is still the authoritative check.

---

### `POST /api/sessions` — session-operations service

**Request body**
```ts
{
  sourceTriviaQuizId: number   // int — the selected published quiz
  title: string
  maximumTimeMinutes: number   // positive int; UI constrains min=1
  scheduledAt: string          // ISO 8601, UTC (see §scheduledAt conversion below)
}
```

**Response `201 Created`**
```ts
{
  liveSessionId: string        // UUID string
  sessionCode: string          // e.g. "SES-A1B2C3D4E5F6"
  title: string
  sessionState: string         // always "Scheduled" at creation
  scheduledAt: string          // ISO 8601
  sourceTriviaQuizId: number
  questionCount: number        // active questions captured in the snapshot
}
```

`Location` header: `/api/sessions/{liveSessionId}` (not consumed by the frontend in HU-16).

**Error cases**
| Status | Cause | Thrown error token |
|--------|-------|--------------------|
| `400`  | Validation failure (empty title, non-positive maxTime, etc.) | `invalid_input` |
| `401`  | Missing trusted headers | `IdentityError('unauthorized', …)` |
| `403`  | Caller role is not `Operator` | `IdentityError('forbidden', …)` |
| `404`  | `sourceTriviaQuizId` does not exist | `quiz_not_found` |
| `409`  | Quiz exists but is not `Published` at creation time | `quiz_not_published` |

**Additional verified behaviour (from integration tests)**
- A created session is always in `Scheduled` state; no state transitions happen in HU-16.
- The `TriviaSessionSnapshot` (fixed copy) is written atomically with the session row;
  the snapshot is immutable — archiving or retiring the source quiz later has no effect.
- `questionCount` reflects only `isActive: true` questions from the source quiz.

---

## Architecture Decisions

- **Reuse `listTriviaQuizzes()` from `app/lib/trivias.ts`.** The sessions Server Action
  calls the existing function and filters to `status === 'Published'` before returning.
  No new HTTP client for the mission-design service is introduced.

- **New `app/lib/sessions.ts` owns only POST /api/sessions.** Catalog loading is a
  read concern on a different service and belongs to the trivias lib.

- **`SESSION_OPERATIONS_SERVICE_URL` is a new environment variable.** Added to
  `.env.local`; follows the same pattern as `IDENTITY_SERVICE_URL` and
  `MISSION_DESIGN_SERVICE_URL`.

- **Two-view panel: `'form'` → `'created'`.** HU-16 scope is creation only — there is no
  session list or detail navigation. After a successful creation the operator sees a
  session card with all fields. "Create another" resets the panel to the form.

- **Server 409 is the authoritative publication gate (AC #4).** The form populates the
  selector from Published quizzes to reduce the probability of a 409, but the quiz can be
  archived between catalog load and form submit. The 409 path renders an accessible inline
  error banner (`role="alert"`) and keeps all form fields intact; the screen never breaks.

- **Sessions nav item is operator-only for HU-16.** The backend rejects non-Operator
  callers with 403. Admin and Participant do not see the Sessions nav item in this HU.
  Admin read-only access to a session list may be added in a later HU.

- **Action-layer role guard.** Both `getPublishedTrivias` and `createTriviaSession` in
  `app/actions/sessions.ts` call `verifySession()` and check `session.role === 'Operator'`
  before reaching any backend. A direct action call from a non-operator throws
  `Error('Forbidden')` immediately.

- **`scheduledAt` UTC conversion.** `<input type="datetime-local">` returns
  `YYYY-MM-DDTHH:mm` in local wall-clock time with no timezone suffix. The submit handler
  converts via `new Date(scheduledAt).toISOString()`, which interprets the value as local
  time and produces an unambiguous UTC ISO 8601 string for the backend. This is the
  standard approach; explicit timezone selection is out of scope for HU-16.

- **No optimistic update on the list.** HU-16 has no session list, so there is no list
  state to patch. The success card is driven directly by the 201 response body.

- **Empty-catalog guard.** When `GET /api/trivias` returns no Published items, the selector
  shows a disabled placeholder option and the submit button remains disabled. The operator
  sees a message directing them to publish a quiz first.

---

## Environment

New variable:

```dotenv
# .env.local
SESSION_OPERATIONS_SERVICE_URL=http://localhost:5003
```

No other environment changes. `MISSION_DESIGN_SERVICE_URL` already covers
`GET /api/trivias` via the existing `trivias.ts` lib.

---

## Phases

### Phase 1 — Type definitions

**Scope**
- Add `CreateTriviaSessionRequest` and `TriviaSessionCreatedDto` to
  `app/lib/definitions.ts`.
- No other file changes in this phase; the types will be consumed in Phases 2 and 3.

**`app/lib/definitions.ts` additions**
```ts
export type CreateTriviaSessionRequest = {
  sourceTriviaQuizId: number
  title: string
  maximumTimeMinutes: number
  scheduledAt: string          // ISO 8601 UTC string
}

export type TriviaSessionCreatedDto = {
  liveSessionId: string        // UUID
  sessionCode: string          // e.g. "SES-A1B2C3D4E5F6"
  title: string
  sessionState: string         // "Scheduled"
  scheduledAt: string          // ISO 8601
  sourceTriviaQuizId: number
  questionCount: number
}
```

**Gate**
- `pnpm build` passes with no type errors.
- No runtime change; no existing type is modified.

---

### Phase 2 — API client and Server Actions

**Scope**
- Create `app/lib/sessions.ts` with `createTriviaSession`.
- Create `app/actions/sessions.ts` with `getPublishedTrivias` and `createTriviaSession`.

**`app/lib/sessions.ts`** (new file)
```ts
import { verifySession, getIdentityHeaders } from '@/app/lib/dal'
import { IdentityError } from '@/app/lib/dal'
import type { CreateTriviaSessionRequest, TriviaSessionCreatedDto } from '@/app/lib/definitions'

const SESSION_OPERATIONS_SERVICE_URL = process.env.SESSION_OPERATIONS_SERVICE_URL!

export async function createTriviaSession(
  req: CreateTriviaSessionRequest,
): Promise<TriviaSessionCreatedDto> {
  const session = await verifySession()
  const response = await fetch(`${SESSION_OPERATIONS_SERVICE_URL}/api/sessions`, {
    method: 'POST',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(req),
  })

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('forbidden', 'Operator role required.')
  if (response.status === 404) throw new Error('quiz_not_found')
  if (response.status === 409) throw new Error('quiz_not_published')
  if (!response.ok) {
    throw new IdentityError('unknown', `createTriviaSession failed with status ${response.status}`)
  }

  return response.json() as Promise<TriviaSessionCreatedDto>
}
```

Note: import paths for `verifySession`, `getIdentityHeaders`, and `IdentityError` must
match the exact exports in `app/lib/dal.ts` — confirm before committing.

**`app/actions/sessions.ts`** (new file)
```ts
'use server'

import { verifySession } from '@/app/lib/dal'
import { listTriviaQuizzes } from '@/app/lib/trivias'
import { createTriviaSession as createTriviaSessionLib } from '@/app/lib/sessions'
import type {
  TriviaQuizSummaryDto,
  CreateTriviaSessionRequest,
  TriviaSessionCreatedDto,
} from '@/app/lib/definitions'

export async function getPublishedTrivias(): Promise<TriviaQuizSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const all = await listTriviaQuizzes()
  return all.filter((q) => q.status === 'Published')
}

export async function createTriviaSession(
  req: CreateTriviaSessionRequest,
): Promise<TriviaSessionCreatedDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  return createTriviaSessionLib(req)
}
```

Note: the lib import is aliased `as createTriviaSessionLib` to avoid naming conflict with
the exported Server Action, following the same aliasing pattern used in `actions/users.ts`.

**Gate**
- `pnpm build` passes with no type errors.
- Calling `createTriviaSession` (action) from an Operator session reaches the lib.
- Calling it from a non-Operator session throws `'Forbidden'` before any fetch.

---

### Phase 3 — SessionsPanel component

**Scope**
- Create `app/dashboard/SessionsPanel.tsx`.
- Views: `'form'` (default) and `'created'` (post-success).
- Form: quiz selector (Published only), title, maximumTimeMinutes, scheduledAt.
- Created card: session details + source-quiz binding confirming AC #3.
- 409 inline error banner with form preserved (AC #4).

**Props**
```ts
interface SessionsPanelProps {
  role: 'operator'   // only operator reaches this panel in HU-16
}
```

**State**
```ts
type PanelView = 'form' | 'created'

const [view, setView] = useState<PanelView>('form')
const [quizzes, setQuizzes] = useState<TriviaQuizSummaryDto[] | null>(null)
const [quizzesError, setQuizzesError] = useState<string | null>(null)
const [selectedQuizId, setSelectedQuizId] = useState<string>('')
const [title, setTitle] = useState('')
const [maxMinutes, setMaxMinutes] = useState<string>('60')
const [scheduledAt, setScheduledAt] = useState<string>('')
const [formError, setFormError] = useState<string | null>(null)
const [createdSession, setCreatedSession] = useState<TriviaSessionCreatedDto | null>(null)
const [isPending, startTransition] = useTransition()
```

**Catalog load on mount**
```ts
useEffect(() => {
  getPublishedTrivias()
    .then(setQuizzes)
    .catch(() => setQuizzesError('Failed to load available quizzes. Reload the page.'))
}, [])
```

**Submit handler**
```ts
function handleSubmit(e: React.FormEvent) {
  e.preventDefault()
  setFormError(null)
  startTransition(async () => {
    try {
      const result = await createTriviaSession({
        sourceTriviaQuizId: Number(selectedQuizId),
        title: title.trim(),
        maximumTimeMinutes: Number(maxMinutes),
        scheduledAt: new Date(scheduledAt).toISOString(),
      })
      setCreatedSession(result)
      setView('created')
    } catch (err) {
      if (err instanceof Error && err.message === 'quiz_not_published') {
        setFormError(
          'The selected quiz is no longer published and cannot be used for session creation. ' +
          'Choose another quiz or ask an admin to republish it.',
        )
      } else if (err instanceof Error && err.message === 'quiz_not_found') {
        setFormError('The selected quiz was not found. Reload the page and try again.')
      } else if (err instanceof Error && err.message === 'invalid_input') {
        setFormError('Invalid session data. Check all fields and try again.')
      } else {
        setFormError('Session creation failed. Try again.')
      }
    }
  })
}
```

**Reset handler (used by "Create another" button)**
```ts
function handleReset() {
  setView('form')
  setCreatedSession(null)
  setSelectedQuizId('')
  setTitle('')
  setMaxMinutes('60')
  setScheduledAt('')
  setFormError(null)
}
```

**JSX — `view === 'form'`**
```tsx
<div className={styles.sessionsPanel} data-testid="sessions-panel">
  <h2 className={styles.panelTitle}>Create Session</h2>

  {quizzesError && (
    <p className={styles.errorBanner} role="alert" data-testid="session-quizzes-error">
      {quizzesError}
    </p>
  )}

  {formError && (
    <p className={styles.errorBanner} role="alert" data-testid="session-form-error">
      {formError}
    </p>
  )}

  <form className={styles.sessionForm} onSubmit={handleSubmit} data-testid="session-create-form">
    <label htmlFor="session-quiz-select">Quiz</label>
    <select
      id="session-quiz-select"
      data-testid="session-quiz-select"
      value={selectedQuizId}
      onChange={(e) => setSelectedQuizId(e.target.value)}
      required
      disabled={isPending || !quizzes}
    >
      <option value="">— Select a published quiz —</option>
      {quizzes?.map((q) => (
        <option key={q.id} value={String(q.id)}>
          {q.title}
        </option>
      ))}
    </select>

    {quizzes?.length === 0 && (
      <p className={styles.emptyStateCopy} data-testid="session-no-quizzes">
        No published quizzes available. Publish a quiz before creating a session.
      </p>
    )}

    <label htmlFor="session-title">Session title</label>
    <input
      id="session-title"
      data-testid="session-title-input"
      type="text"
      value={title}
      onChange={(e) => setTitle(e.target.value)}
      required
      disabled={isPending}
    />

    <label htmlFor="session-max-time">Maximum time (minutes)</label>
    <input
      id="session-max-time"
      data-testid="session-max-time-input"
      type="number"
      min="1"
      max="480"
      value={maxMinutes}
      onChange={(e) => setMaxMinutes(e.target.value)}
      required
      disabled={isPending}
    />

    <label htmlFor="session-scheduled-at">Scheduled at</label>
    <input
      id="session-scheduled-at"
      data-testid="session-scheduled-at-input"
      type="datetime-local"
      value={scheduledAt}
      onChange={(e) => setScheduledAt(e.target.value)}
      required
      disabled={isPending}
    />

    <button
      type="submit"
      data-testid="session-submit-btn"
      className={styles.primaryButton}
      disabled={
        isPending ||
        !selectedQuizId ||
        !title.trim() ||
        !maxMinutes ||
        !scheduledAt ||
        quizzes?.length === 0
      }
    >
      {isPending ? 'Creating…' : 'Create session'}
    </button>
  </form>
</div>
```

**JSX — `view === 'created'`** (rendered instead of form after 201)
```tsx
<div className={styles.sessionsPanel} data-testid="sessions-panel">
  <h2 className={styles.panelTitle}>Session Created</h2>

  <section className={styles.sessionCard} data-testid="session-created-card">
    <h3 data-testid="session-created-title">{createdSession!.title}</h3>
    <dl className={styles.sessionMeta}>
      <dt>Session code</dt>
      <dd data-testid="session-code">{createdSession!.sessionCode}</dd>

      <dt>State</dt>
      <dd data-testid="session-state">{createdSession!.sessionState}</dd>

      <dt>Source quiz ID</dt>
      <dd data-testid="session-source-quiz-id">{createdSession!.sourceTriviaQuizId}</dd>

      <dt>Questions in snapshot</dt>
      <dd data-testid="session-question-count">{createdSession!.questionCount}</dd>

      <dt>Scheduled at</dt>
      <dd data-testid="session-scheduled-at-display">
        {new Date(createdSession!.scheduledAt).toLocaleString()}
      </dd>
    </dl>
  </section>

  <button
    type="button"
    data-testid="session-create-another-btn"
    className={styles.inlineButton}
    onClick={handleReset}
  >
    Create another session
  </button>
</div>
```

**CSS additions to `dashboard.module.css`**
- `.sessionsPanel` — panel root; same padding/layout as `.triviasPanel` or equivalent.
- `.sessionForm` — flex-column with `gap: var(--space-3)` for label/input pairs.
- `.sessionCard` — card container; `border: 1px solid var(--border)`,
  `border-radius: var(--radius)`, `padding: var(--space-4)`, `background: var(--surface)`.
- `.sessionMeta` — `<dl>` two-column grid: `grid-template-columns: max-content 1fr`,
  `gap: var(--space-1) var(--space-3)`, small font.
- `.errorBanner` — if not already present: `color: var(--critical)`,
  `background: color-mix(in srgb, var(--critical) 10%, transparent)`,
  `border: 1px solid var(--critical)`, `border-radius: var(--radius-sm)`,
  `padding: var(--space-2) var(--space-3)`, `font-size: 0.875rem`.
- `.primaryButton` — if not already present: filled button; `background: var(--accent)`,
  `color: white`, `border: none`, `border-radius: var(--radius-sm)`,
  `padding: 0.4rem 1rem`, `cursor: pointer`. `:disabled` reduces opacity.

**`data-testid` contract**
| Testid | Element |
|--------|---------|
| `sessions-panel` | Panel root `<div>` |
| `session-create-form` | `<form>` element |
| `session-quiz-select` | Quiz `<select>` |
| `session-title-input` | Title `<input>` |
| `session-max-time-input` | Max-time `<input type="number">` |
| `session-scheduled-at-input` | Scheduled-at `<input type="datetime-local">` |
| `session-submit-btn` | Submit `<button>` |
| `session-form-error` | Inline error `<p>` (visible on 4xx) |
| `session-quizzes-error` | Catalog-load error `<p>` |
| `session-no-quizzes` | Empty-catalog notice `<p>` |
| `session-created-card` | Success card `<section>` |
| `session-created-title` | Created session title `<h3>` |
| `session-code` | Session code `<dd>` |
| `session-state` | Session state `<dd>` |
| `session-source-quiz-id` | Source quiz ID `<dd>` |
| `session-question-count` | Question count `<dd>` |
| `session-scheduled-at-display` | Formatted scheduled-at `<dd>` |
| `session-create-another-btn` | Reset `<button>` |

**Gate**
- `pnpm build` passes with no type errors.
- `SessionsPanel` renders with a selector populated from the Published catalog.
- Submitting a valid form transitions to the created card showing all five fields.
- A 409 response renders the inline error banner; the form remains visible and all
  previously entered field values are preserved.
- No Published quiz available → selector placeholder disabled, submit button disabled,
  empty-catalog notice visible.

---

### Phase 4 — Dashboard integration

**Scope**
- Add `'sessions'` nav item to the `navigation` array in `DashboardClient.tsx`.
- Update `visibleNavigation` filter to expose sessions only to operator.
- Wire `activeNav === 'sessions'` to `<SessionsPanel role={role} />`.
- Import `SessionsPanel` from `./SessionsPanel`.

**Navigation item addition** (insert after the `'missions'` entry or in whichever position
is contextually appropriate for session management):
```ts
{ key: 'sessions', label: 'Sessions', icon: '▶' }
```

**Visibility filter update**

Current filter (approximate — match the exact existing code):
```ts
const visibleNavigation = navigation.filter((item) => {
  if (role === 'participant') return item.key === 'overview'
  if (role === 'operator') return item.key !== 'missions' && item.key !== 'trivias'
  return true // admin sees all
})
```

Updated filter — operator keeps sessions; admin does not see sessions in HU-16:
```ts
const visibleNavigation = navigation.filter((item) => {
  if (role === 'participant') return item.key === 'overview'
  if (role === 'admin') return item.key !== 'sessions'
  if (role === 'operator') return item.key !== 'missions' && item.key !== 'trivias'
  return true
})
```

The operator branch is unchanged — it already excludes `missions` and `trivias`; `sessions`
is not in that exclusion list so operators see it automatically once the nav item is added.

**Routing addition** (in the main content area, alongside existing `activeNav` checks):
```tsx
activeNav === 'sessions' ? (
  <SessionsPanel role={role as 'operator'} />
) : activeNav === 'trivias' ? (
  <TriviasPanel role={role} />
) : ...
```

The `role as 'operator'` cast is safe here because the `visibleNavigation` filter ensures
only operators ever receive a `sessions` nav item and can set `activeNav` to `'sessions'`.

**Gate**
- `pnpm build` passes.
- Operator navigating to `/dashboard` sees the Sessions nav item and the creation form.
- Admin navigating to `/dashboard` does not see Sessions in the nav.
- Participant navigating to `/dashboard` does not see Sessions in the nav.
- Selecting Sessions nav → `[data-testid="sessions-panel"]` becomes visible.
- Selecting any other nav item (e.g. "Overview") → `[data-testid="sessions-panel"]` is gone.

---

### Phase 5 — E2E tests

**Scope**
- Add `tests/e2e/sessions.spec.ts` covering all HU-16 acceptance criteria.
- Verify no regressions in HU-11/12 (trivias panel) and HU-03 (role chip, operator flow).

**`tests/e2e/sessions.spec.ts`**
```ts
import { test, expect } from '../fixtures/auth'

// --- Nav visibility ---

test('operator sees sessions nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toBeVisible()
})

test('admin does not see sessions nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toHaveCount(0)
})

test('participant does not see sessions nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-sessions"]')).toHaveCount(0)
})

// --- Panel rendering ---

test('operator sessions panel loads and shows create form', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
})

// AC #1 — only Published quizzes appear

test('session quiz selector contains only published quizzes', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const select = page.locator('[data-testid="session-quiz-select"]')
  await expect(select).toBeVisible()
  // All non-placeholder options must correspond to Published quizzes.
  // Verify there are no options with "Draft" or "Archived" in their text
  // (relies on the test environment having known fixture quizzes).
  const options = select.locator('option:not([value=""])')
  const count = await options.count()
  for (let i = 0; i < count; i++) {
    const text = await options.nth(i).textContent()
    expect(text).not.toContain('Draft')
    expect(text).not.toContain('Archived')
  }
})

// --- Submit button disabled state ---

test('session submit button is disabled when no quiz is selected', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
})

test('session submit button is disabled until all fields are filled', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')
  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  // Select a quiz but leave title empty
  const firstOption = select.locator('option:not([value=""])').first()
  const optionValue = await firstOption.getAttribute('value')
  if (optionValue) await select.selectOption(optionValue)
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill title — still missing scheduledAt
  await page.fill('[data-testid="session-title-input"]', 'Test Session')
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeDisabled()
  // Fill scheduledAt
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-01T10:00')
  // Now submit should be enabled
  await expect(page.locator('[data-testid="session-submit-btn"]')).toBeEnabled()
})

// --- Happy path (AC #2 + AC #3) ---

test('operator can create a session from a published quiz and sees created card', async ({
  operatorPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  const optionValue = await firstOption.getAttribute('value')
  expect(optionValue).not.toBeNull()
  await select.selectOption(optionValue!)

  await page.fill('[data-testid="session-title-input"]', 'E2E Trivia Night')
  await page.fill('[data-testid="session-max-time-input"]', '45')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-15T18:00')

  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-created-card"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-created-title"]')).toContainText('E2E Trivia Night')
  await expect(page.locator('[data-testid="session-state"]')).toContainText('Scheduled')
})

// AC #3 — created session shows source quiz binding

test('created session card shows source quiz binding', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  const optionValue = await firstOption.getAttribute('value')
  await select.selectOption(optionValue!)
  await page.fill('[data-testid="session-title-input"]', 'Binding Check')
  await page.fill('[data-testid="session-max-time-input"]', '30')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-20T09:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-source-quiz-id"]')).toHaveText(optionValue!)
  await expect(page.locator('[data-testid="session-code"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="session-question-count"]')).not.toBeEmpty()
})

// --- Reset flow ---

test('operator can create another session after a successful creation', async ({
  operatorPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  await select.selectOption((await firstOption.getAttribute('value'))!)
  await page.fill('[data-testid="session-title-input"]', 'First Session')
  await page.fill('[data-testid="session-max-time-input"]', '20')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-22T14:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-created-card"]')).toBeVisible()

  await page.click('[data-testid="session-create-another-btn"]')

  // Form resets and is visible
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-created-card"]')).toHaveCount(0)
  await expect(page.locator('[data-testid="session-title-input"]')).toHaveValue('')
})

// AC #4 — 409 surfaced without broken screen
// This test intercepts the POST at the network level to simulate a 409.

test('session form shows error banner on 409 and keeps form intact', async ({
  operatorPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-sessions"]')

  // Intercept the Server Action's outbound POST to session-operations.
  // In Playwright, intercept the Next.js server action call (POST to /dashboard).
  await page.route('**/dashboard', async (route) => {
    const body = await route.request().postData()
    if (body && body.includes('createTriviaSession')) {
      await route.fulfill({ status: 409, body: JSON.stringify({ type: 'quiz_not_published' }) })
    } else {
      await route.continue()
    }
  })

  const select = page.locator('[data-testid="session-quiz-select"]')
  await select.waitFor()
  const firstOption = select.locator('option:not([value=""])').first()
  await select.selectOption((await firstOption.getAttribute('value'))!)
  await page.fill('[data-testid="session-title-input"]', '409 Test')
  await page.fill('[data-testid="session-max-time-input"]', '10')
  await page.fill('[data-testid="session-scheduled-at-input"]', '2026-12-30T12:00')
  await page.click('[data-testid="session-submit-btn"]')

  await expect(page.locator('[data-testid="session-form-error"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-create-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="session-created-card"]')).toHaveCount(0)
})

// --- HU-11/12 regression ---

test('HU-11 trivias panel still renders for admin after sessions integration', async ({
  adminPage: page,
}) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
})

// --- HU-03 regression ---

test('HU-03 operator role chip is visible and correct', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="role-chip"]')).toBeVisible()
  await expect(page.locator('[data-testid="role-chip"]')).toContainText('operator')
})
```

**Note on 409 test:** Server Action calls are Next.js internal requests. The exact
intercept pattern for the 409 scenario should be verified against Playwright's handling
of Server Action POSTs in the test environment. An alternative is to use a test-fixture
quiz known to be non-published (Draft or Archived ID planted in the test DB) and bypass
the client-side filter by calling the action directly.

**Gate**
- `pnpm exec playwright test tests/e2e/sessions.spec.ts` passes.
- All four HU-16 acceptance criteria (AC #1–#4) are covered by automated tests.
- HU-11/12 and HU-03 regression tests pass unmodified.

---

## Commit Sequence

```
feat(frontend): phase 1 — add session creation types to definitions (hu-16)
feat(frontend): phase 2 — api client and server actions for session creation (hu-16)
feat(frontend): phase 3 — SessionsPanel component with form and created card (hu-16)
feat(frontend): phase 4 — wire sessions nav and panel into DashboardClient (hu-16)
feat(frontend): phase 5 — e2e tests for HU-16 session creation flow (hu-16)

Ref: HU-16
```

---

## Out of Scope

- **Session list view.** A paginated list of all sessions created by the operator is not
  required by HU-16; the panel only shows the creation form and the just-created session.
- **Session detail / edit.** State transitions (Preparing → Active → Finished) and
  per-session drill-down are reserved for later HUs.
- **Timezone picker.** `scheduledAt` is converted from local machine time via
  `new Date().toISOString()`. An explicit timezone selector is not required by HU-16.
- **Admin session read access.** Admin visibility into the Sessions panel is deferred to
  a later HU when a read-only session list is introduced.
- **`GET /api/sessions`** — not yet wired in this HU; no session polling or refresh.
- **Session code sharing UI.** The `sessionCode` is displayed in the created card for
  operator reference; no clipboard-copy button or QR code is required by HU-16.
- **Self-service participant enrollment.** Participants joining a session via its code is
  a separate flow (HU-20 or later).
- **Any backend changes.** Backend is fully implemented and must not be modified.
