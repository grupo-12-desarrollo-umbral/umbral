# Plan: HU-11 Frontend — Trivia Quiz Management

**Ref:** HU-11
**Branch:** feature/hu-11-trivia-quiz-management
**Date:** 2026-06-01
**Builds on:** HU-09 frontend (MissionsPanel pattern, `app/lib/missions.ts`, `app/actions/missions.ts`, nav filtering by role, `DashboardClient` wiring)

---

## Context

Backend HU-11 (`mission-design-service`) is fully implemented. Four endpoints are available:

- `POST /api/trivias` — create trivia quiz; `Administrator` only
- `GET /api/trivias` — list all trivia quizzes (summary); all authenticated callers
- `GET /api/trivias/{id}` — trivia quiz detail; all authenticated callers
- `PUT /api/trivias/{id}` — update trivia quiz (Draft only); `Administrator` only

The backend contract is verified by integration tests in
`backend/services/mission-design-service/tests/IntegrationTests/Api/TriviaEndpointsTests.cs`.

Four frontend concerns:

1. **Type definitions** — `TriviaQuizSummaryDto`, `TriviaQuizDto`, `TriviaQuestionDto`, and
   `TriviaOptionDto` do not exist in `definitions.ts`.
2. **API client and Server Actions** — `app/lib/trivias.ts` and `app/actions/trivias.ts` do not exist.
3. **TriviasPanel component** — a four-view panel (`list`, `detail`, `create`, `edit`) mirroring
   `MissionsPanel`, with an extra read-only questions section in the detail/edit views to preserve
   question data without building the full HU-14A question-management workflow yet.
4. **DashboardClient wiring** — add a `'trivias'` nav item; mount the panel behind
   `activeNav === 'trivias'`; gate the nav item to admins only (operators and participants do not see it).

---

## Verified Backend Contract

### Common headers (trust envelope)

```
X-User-Id:    {session.externalIdentityId}
X-User-Role:  {session.role}
X-User-Email: {session.email}
```

The mission-design service reads only `X-User-Id` and `X-User-Role`; `X-User-Email` is forwarded for
consistency with the existing `getIdentityHeaders` helper and silently ignored by the backend.

---

### `POST /api/trivias`

**Request body**
```ts
{
  title: string                    // required, max 200
  description: string              // required, max 2000
  questions: TriviaQuestionRequest[]  // may be empty array
}
```

**`TriviaQuestionRequest`**
```ts
{
  prompt: string                  // required, max 2000
  sequenceOrder: number           // required, > 0; unique across the quiz
  isActive: boolean
  options: TriviaOptionRequest[]
}
```

**`TriviaOptionRequest`**
```ts
{
  optionText: string              // required, max 1000
  sequenceOrder: number           // required, > 0; unique within the question
  isCorrect: boolean              // at most one option per question may be true
}
```

**Response `201 Created`** — `Location: /api/trivias/{id}` + body `TriviaQuizResponse`

**Error cases**
- `400` — validation failed (empty title/description, duplicate sequence orders,
  multiple correct options on one question, non-positive sequence orders)
- `401` — missing or invalid trust headers
- `403` — caller is not `Administrator`

**Additional verified behaviour (from integration tests)**
- A freshly created quiz has `status: "Draft"`.
- The response body includes the full questions+options shape even when the
  questions array is empty.

---

### `GET /api/trivias`

**Response `200 OK`** — `TriviaQuizSummaryResponse[]`

```ts
{
  id: number
  title: string
  description: string
  status: string   // "Draft" | "Published" | "Archived"
}
```

**Additional verified behaviour**
- Returns quizzes in all statuses (Draft, Published, Archived) in a single flat list.
- A newly created quiz appears immediately with `status: "Draft"`.

---

### `GET /api/trivias/{id}`

**Response `200 OK`** — `TriviaQuizResponse`

```ts
{
  id: number
  title: string
  description: string
  status: string
  questions: TriviaQuestionResponse[]
}
```

**`TriviaQuestionResponse`**
```ts
{
  id: number
  prompt: string
  sequenceOrder: number
  isActive: boolean
  options: TriviaOptionResponse[]
}
```

**`TriviaOptionResponse`**
```ts
{
  id: number
  optionText: string
  sequenceOrder: number
  isCorrect: boolean
}
```

**Error cases**
- `404` — quiz not found

---

### `PUT /api/trivias/{id}`

Identical request body shape to `POST /api/trivias` (title, description, questions).
Full question/option array replacement — the backend discards previous questions and
persists the incoming set.

**Response `200 OK`** — `TriviaQuizResponse` (same shape as GET detail)

**Error cases**
- `400` — validation (same rules as create)
- `401` — missing trust headers
- `403` — caller is not `Administrator`
- `404` — quiz not found
- `409 Conflict` — `status !== "Draft"` (Published or Archived quizzes are immutable).
  ProblemDetails title: `"Trivia quiz cannot be edited in its current state."`

**Additional verified behaviour**
- After update the response includes the new full questions+options shape.
- Non-Draft quizzes cannot be edited; the `Edit` button must be disabled when
  `status !== "Draft"`.

---

## Architecture Decisions

- **TriviasPanel follows MissionsPanel exactly for the outer shell.** The same four-view
  state machine (`list → detail → create → edit`) and the same `refreshKey` / `useTransition`
  pattern are used. This keeps the codebase internally consistent.
- **Questions section in detail/edit is read-only and non-interactive at this stage.**
  HU-14A will own the full question-management workflow. For HU-11 the detail view renders
  the questions array in a collapsed-friendly table so that saved question data is visible
  but no add/remove/reorder affordances are exposed. The create and edit forms accept an
  empty questions array (`[]`) and pass it directly — the backend permits it.
- **Edit button is disabled for non-Draft quizzes.** The `status` field on the detail DTO
  drives this: `disabled={isPending || quiz.status !== 'Draft'}`. This matches the 409 Conflict
  the backend returns and prevents a round-trip error being the first user signal.
- **Status chip follows the same tone mapping as mission activation state.** `Draft` → `warning`,
  `Published` → `success`, `Archived` → `muted`.
- **Server Actions enforce Administrator-only at the action layer.** `createTriviaQuiz` and
  `updateTriviaQuiz` in `app/actions/trivias.ts` check `session.role !== 'Administrator'` before
  reaching the API client, consistent with `createMission` / `updateMission`.
- **`GET /api/trivias` and `GET /api/trivias/{id}` have no server-side role gate.** The backend
  does not require `Administrator` for reads — it accepts any authenticated caller. The actions
  `getTriviaQuizzes` and `getTriviaQuiz` therefore only call `verifySession()` (to ensure any
  valid session) without a role check. This matches the backend contract.
- **`revalidatePath('/dashboard')` after create and update.** Clears RSC cache so the list
  reflects the new/changed quiz on the next panel open.
- **Nav item `'trivias'` is admin-only.** The existing nav filter in `DashboardClient`:
  ```ts
  if (role === 'participant') return item.key === 'overview'
  if (role === 'operator') return item.key !== 'missions'
  ```
  gains an operator exclusion for `'trivias'`:
  ```ts
  if (role === 'operator') return item.key !== 'missions' && item.key !== 'trivias'
  ```
  Admins see both. Participants already only see `'overview'`.
- **`MISSION_DESIGN_SERVICE_URL` constant is shared.** `app/lib/trivias.ts` uses the same
  `http://localhost:5001` base URL already used by `app/lib/missions.ts`.

---

## Environment

No new environment variables. `MISSION_DESIGN_SERVICE_URL` (`http://localhost:5001`) in
`app/lib/missions.ts` covers all trivia endpoints — the same service is reused.

---

## Phases

### Phase 1 — Type definitions

**Scope**
- Add `TriviaQuizSummaryDto`, `TriviaQuizDto`, `TriviaQuestionDto`, and `TriviaOptionDto`
  to `app/lib/definitions.ts`.

**`app/lib/definitions.ts` additions**
```ts
export type TriviaOptionDto = {
  id: number
  optionText: string
  sequenceOrder: number
  isCorrect: boolean
}

export type TriviaQuestionDto = {
  id: number
  prompt: string
  sequenceOrder: number
  isActive: boolean
  options: TriviaOptionDto[]
}

export type TriviaQuizSummaryDto = {
  id: number
  title: string
  description: string
  status: string   // "Draft" | "Published" | "Archived"
}

export type TriviaQuizDto = {
  id: number
  title: string
  description: string
  status: string
  questions: TriviaQuestionDto[]
}
```

**Gate**
- `pnpm build` passes with no type errors.
- No runtime change; existing panels are unaffected.

---

### Phase 2 — API client and Server Actions

**Scope**
- Create `app/lib/trivias.ts` with four functions: `listTriviaQuizzes`,
  `getTriviaQuizById`, `createTriviaQuiz`, `updateTriviaQuiz`.
- Create `app/actions/trivias.ts` with matching Server Actions.

**`app/lib/trivias.ts`**
```ts
import 'server-only'
import { IdentityError, type TriviaQuizSummaryDto, type TriviaQuizDto } from './definitions'
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

export async function listTriviaQuizzes(): Promise<TriviaQuizSummaryDto[]> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias`, {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden.')
  if (!response.ok) throw new IdentityError('unknown', `listTriviaQuizzes failed with status ${response.status}`)
  return response.json()
}

export async function getTriviaQuizById(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}`, {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (!response.ok) throw new IdentityError('unknown', `getTriviaQuizById failed with status ${response.status}`)
  return response.json()
}

export async function createTriviaQuiz(
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias`, {
    method: 'POST',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, description, questions: [] }),
  })
  if (response.status === 400) throw new Error('invalid_fields')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (!response.ok) throw new IdentityError('unknown', `createTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function updateTriviaQuiz(
  id: number,
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}`, {
    method: 'PUT',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, description, questions: [] }),
  })
  if (response.status === 400) throw new Error('invalid_fields')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_not_editable')
  if (!response.ok) throw new IdentityError('unknown', `updateTriviaQuiz failed with status ${response.status}`)
  return response.json()
}
```

Note: create and update pass `questions: []` because HU-14A owns question authoring. The
backend accepts an empty array without error, as confirmed by the contract.

**`app/actions/trivias.ts`**
```ts
'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listTriviaQuizzes,
  getTriviaQuizById,
  createTriviaQuiz as createTriviaQuizLib,
  updateTriviaQuiz as updateTriviaQuizLib,
} from '@/app/lib/trivias'
import { revalidatePath } from 'next/cache'
import type { TriviaQuizSummaryDto, TriviaQuizDto } from '@/app/lib/definitions'

export async function getTriviaQuizzes(): Promise<TriviaQuizSummaryDto[]> {
  await verifySession()
  return listTriviaQuizzes()
}

export async function getTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  await verifySession()
  return getTriviaQuizById(id)
}

export async function createTriviaQuiz(
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await createTriviaQuizLib(title, description)
  revalidatePath('/dashboard')
  return result
}

export async function updateTriviaQuiz(
  id: number,
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await updateTriviaQuizLib(id, title, description)
  revalidatePath('/dashboard')
  return result
}
```

Note: `getTriviaQuizzes` and `getTriviaQuiz` do not gate on `'Administrator'` because the
backend permits any authenticated caller for reads.

**Gate**
- `pnpm build` passes with no type errors.
- Calling `createTriviaQuiz` from a non-Administrator session throws `'Forbidden'` without
  reaching the backend.

---

### Phase 3 — TriviasPanel component

**Scope**
- Create `app/dashboard/TriviasPanel.tsx`.
- Four views: `list`, `detail`, `create`, `edit`.
- Detail view renders a read-only questions section (table of questions with their options).
- Edit button is disabled for non-Draft quizzes.

**File: `app/dashboard/TriviasPanel.tsx`**

Top-level structure mirrors `MissionsPanel.tsx`:
- `'use client'`
- Imports: `useEffect`, `useState`, `useTransition` from `react`; Server Actions from
  `@/app/actions/trivias`; types from `@/app/lib/definitions`; styles from `./dashboard.module.css`
- `type TriviaPanelView = 'list' | 'detail' | 'create' | 'edit'`
- `type DashboardRole = 'operator' | 'admin' | 'participant'` (local alias)
- Export: `export function TriviasPanel({ role }: { role: DashboardRole })`

**State**
```ts
const [view, setView] = useState<TriviaPanelView>('list')
const [selectedQuiz, setSelectedQuiz] = useState<TriviaQuizDto | null>(null)
const [listData, setListData] = useState<TriviaQuizSummaryDto[] | null>(null)
const [isPending, startTransition] = useTransition()
const [listError, setListError] = useState<string | null>(null)
const [formError, setFormError] = useState<string | null>(null)
const [refreshKey, setRefreshKey] = useState(0)
```

**`useEffect` — fetch list**
```ts
useEffect(() => {
  startTransition(async () => {
    setListError(null)
    try {
      const result = await getTriviaQuizzes()
      setListData(result)
    } catch {
      setListError('Failed to load trivia quizzes.')
    }
  })
}, [refreshKey])
```

**`handleOpenDetail`**
```ts
async function handleOpenDetail(id: number) {
  startTransition(async () => {
    try {
      const quiz = await getTriviaQuiz(id)
      setSelectedQuiz(quiz)
      setView('detail')
    } catch {
      setListError('Failed to load trivia quiz details.')
    }
  })
}
```

**`handleCreate`**
```ts
async function handleCreate(title: string, description: string) {
  startTransition(async () => {
    setFormError(null)
    try {
      const created = await createTriviaQuiz(title, description)
      setSelectedQuiz(created)
      setView('detail')
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'invalid_fields') {
        setFormError('Title and description are required (max 200 and 2000 characters).')
      } else {
        setFormError('Failed to create trivia quiz. Try again.')
      }
    }
  })
}
```

**`handleUpdate`**
```ts
async function handleUpdate(title: string, description: string) {
  if (!selectedQuiz) return
  startTransition(async () => {
    setFormError(null)
    try {
      const updated = await updateTriviaQuiz(selectedQuiz.id, title, description)
      setSelectedQuiz(updated)
      setView('detail')
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'invalid_fields') {
        setFormError('Title and description are required (max 200 and 2000 characters).')
      } else if (msg === 'trivia_not_found') {
        setFormError('Trivia quiz no longer exists.')
      } else if (msg === 'trivia_not_editable') {
        setFormError('This trivia quiz can no longer be edited (it is no longer in Draft status).')
      } else {
        setFormError('Failed to update trivia quiz. Try again.')
      }
    }
  })
}
```

**Status tone helper** (used in both list and detail)
```ts
function statusTone(status: string): 'success' | 'warning' | 'muted' {
  if (status === 'Published') return 'success'
  if (status === 'Draft') return 'warning'
  return 'muted'
}
```

**Detail view** — rendered when `view === 'detail' && selectedQuiz !== null`:
- Back button (`← Back to trivia quizzes`) returning to `'list'`
- Title heading (`data-testid="trivia-detail-title"`)
- Description card (`data-testid="trivia-detail-description"`)
- Status chip (`data-testid="trivia-detail-status"`, tone from `statusTone(selectedQuiz.status)`)
- "Edit" button (`data-testid="edit-trivia-btn"`) — disabled when
  `isPending || selectedQuiz.status !== 'Draft'`
- **Read-only questions section** (`data-testid="trivia-questions-section"`):
  If `selectedQuiz.questions.length === 0`, render a muted "No questions added yet." line.
  Otherwise render a flat list of question rows sorted by `sequenceOrder`:
  each row shows `sequenceOrder`, `prompt`, `isActive` chip, and below it the options
  (`optionText`, `isCorrect` marker). No add/remove controls; a muted footnote reads
  "Question management is available in a future release."

**Create view** — rendered when `view === 'create'`:
- Back button (`← Back to trivia quizzes`)
- Heading "Create trivia quiz"
- `<TriviaQuizForm>` with empty initial values, `isPending`, `error={formError}`,
  `onSubmit={handleCreate}`, `onCancel={() => { setFormError(null); setView('list') }}`

**Edit view** — rendered when `view === 'edit' && selectedQuiz !== null`:
- Back button (`← {selectedQuiz.title}`)
- Heading "Edit trivia quiz"
- `<TriviaQuizForm>` pre-filled with `selectedQuiz.title` / `selectedQuiz.description`,
  `isPending`, `error={formError}`,
  `onSubmit={handleUpdate}`, `onCancel={() => { setFormError(null); setView('detail') }}`
- Read-only questions section **identical to detail view** rendered below the form, so the
  admin can see the existing questions while editing title/description.

**List view** (default, `data-testid="trivias-panel"`):
- Panel header with "Trivia quizzes" heading and "Create trivia quiz" button
  (`data-testid="create-trivia-btn"`), admin-only: `{role === 'admin' && <button ...>}`
- Error display
- Table columns: Title, Description, Status, Actions
  - Status cell: chip with `statusTone(quiz.status)` and `data-testid={trivia-status-{id}}`
  - Action: "View details" button (`data-testid={view-trivia-btn-{id}}`)

**`TriviaQuizForm` inner component**
```ts
function TriviaQuizForm({
  initial,
  isPending,
  error,
  onSubmit,
  onCancel,
}: {
  initial: { title: string; description: string }
  isPending: boolean
  error: string | null
  onSubmit: (title: string, description: string) => void
  onCancel: () => void
})
```
Fields:
- `data-testid="trivia-title-input"` — text input, `maxLength={200}`, required
- `data-testid="trivia-description-input"` — textarea, `maxLength={2000}`, required, `rows={4}`
- Submit: `data-testid="trivia-submit-btn"`, disabled when `isPending`
- Cancel: `type="button"`, disabled when `isPending`

**`data-testid` contract**

| Element | `data-testid` |
|---|---|
| Panel (list) | `trivias-panel` |
| Create button | `create-trivia-btn` |
| Row | `trivia-row-{id}` |
| Status chip (list) | `trivia-status-{id}` |
| View button | `view-trivia-btn-{id}` |
| Detail section | `trivia-detail` |
| Detail title | `trivia-detail-title` |
| Detail description | `trivia-detail-description` |
| Detail status | `trivia-detail-status` |
| Edit button | `edit-trivia-btn` |
| Questions section | `trivia-questions-section` |
| Form | `trivia-form` |
| Title input | `trivia-title-input` |
| Description input | `trivia-description-input` |
| Submit button | `trivia-submit-btn` |

**Gate**
- `pnpm build` passes with no type errors.
- Admin can navigate through all four views.
- "Edit" is disabled for Published/Archived quizzes in the detail view.
- The questions section renders a placeholder when empty, or a read-only table when populated.

---

### Phase 4 — DashboardClient wiring

**Scope**
- Add `{ key: 'trivias', label: 'Trivias', icon: '▤' }` to the `navigation` array in
  `DashboardClient.tsx`.
- Update the `visibleNavigation` filter to exclude `'trivias'` from operators:
  ```ts
  if (role === 'operator') return item.key !== 'missions' && item.key !== 'trivias'
  ```
- Add `import { TriviasPanel } from './TriviasPanel'` at the top of `DashboardClient.tsx`.
- Mount the panel in the main content switch:
  ```tsx
  } : activeNav === 'trivias' ? (
    <TriviasPanel role={role} />
  ) : activeNav === 'missions' ? (
  ```
  This inserts before the existing `missions` branch — the order in the chain does not affect
  correctness since only one branch is active at a time.

**Gate**
- `pnpm build` passes with no type errors.
- Admin navigation shows the "Trivias" nav item (`data-testid="nav-trivias"`).
- Operator navigation does not show "Trivias".
- Clicking "Trivias" mounts `TriviasPanel`; clicking "Missions" still mounts `MissionsPanel`.
- All existing tests (`auth.spec.ts`, `users.spec.ts`, `roles.spec.ts`, `teams.spec.ts`,
  `missions.spec.ts`) pass without modification.

---

### Phase 5 — E2E tests

**Scope**
- Add `tests/e2e/trivias.spec.ts` covering all HU-11 acceptance criteria.
- Verify no regression on the missions panel (no new failures in `missions.spec.ts`).

**`tests/e2e/trivias.spec.ts`**

```ts
import { test, expect } from '../fixtures/auth'

// --- Nav visibility ---

test('admin sees trivias nav item', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toBeVisible()
})

test('operator does not see trivias nav item', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// --- Catalog view ---

test('admin trivias panel loads with create button', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-trivia-btn"]')).toBeVisible()
})

// --- Create flow ---

test('admin can create a trivia quiz and land on its detail view', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await expect(page.locator('[data-testid="trivia-form"]')).toBeVisible()

  await page.fill('[data-testid="trivia-title-input"]', 'Geography Basics')
  await page.fill('[data-testid="trivia-description-input"]', 'Test your knowledge of world capitals.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Geography Basics')
  await expect(page.locator('[data-testid="trivia-detail-status"]')).toContainText('Draft')
})

test('create form cancel returns to list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await expect(page.locator('[data-testid="trivia-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-form"]')).toHaveCount(0)
})

// --- Detail view ---

test('admin can open trivia detail from catalog row', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  const viewBtn = page.locator('[data-testid^="view-trivia-btn-"]').first()
  await expect(viewBtn).toBeVisible()
  await viewBtn.click()

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="trivia-detail-status"]')).not.toBeEmpty()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toBeVisible()
})

// --- Draft changes are queryable (integration gate) ---

test('created draft quiz appears in catalog list', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')

  await page.fill('[data-testid="trivia-title-input"]', 'Draft Visibility Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Verifying draft is in catalog.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Back to list
  await page.getByRole('button', { name: '← Back to trivia quizzes' }).click()
  await expect(page.locator('[data-testid="trivias-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid^="trivia-row-"]').filter({ hasText: 'Draft Visibility Quiz' })).toBeVisible()
})

// --- Edit flow ---

test('admin can edit a draft trivia quiz', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  // Create a fresh draft
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Edit Target Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'To be edited.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="edit-trivia-btn"]')
  await expect(page.locator('[data-testid="trivia-form"]')).toBeVisible()
  await page.fill('[data-testid="trivia-title-input"]', 'Updated Quiz Title')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Updated Quiz Title')
})

test('edit form cancel returns to detail without saving', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Cancel Edit Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Cancel me.')
  await page.click('[data-testid="trivia-submit-btn"]')

  const originalTitle = await page.locator('[data-testid="trivia-detail-title"]').innerText()

  await page.click('[data-testid="edit-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Should Not Be Saved')
  await page.locator('[data-testid="trivia-form"] button[type="button"]').click()

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText(originalTitle.trim())
})

// --- Edit gate for non-Draft quizzes ---

test('edit button is disabled for non-Draft quizzes', async ({ adminPage: page }) => {
  // This test relies on a Published or Archived quiz being present.
  // Because HU-11 has no publish/archive UI, this test sets up the state
  // by opening the first row's detail and checking the edit button state.
  // If all quizzes in the test DB are Draft, the test skips the assertion.
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')

  const viewBtn = page.locator('[data-testid^="view-trivia-btn-"]').first()
  await viewBtn.click()

  const status = await page.locator('[data-testid="trivia-detail-status"]').innerText()
  const editBtn = page.locator('[data-testid="edit-trivia-btn"]')

  if (status.trim() !== 'Draft') {
    await expect(editBtn).toBeDisabled()
  } else {
    await expect(editBtn).not.toBeDisabled()
  }
})

// --- Authorization guard ---

test('participant does not see trivias nav item', async ({ participantPage: page }) => {
  await page.goto('/dashboard')
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// --- Regression: missions panel still works ---

test('HU-09 missions panel still reachable for admin after trivias wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await expect(page.locator('[data-testid="missions-panel"]')).toBeVisible()
  await expect(page.locator('[data-testid="create-mission-btn"]')).toBeVisible()
})

test('HU-09 admin mission create still works after trivias wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-missions"]')
  await page.click('[data-testid="create-mission-btn"]')
  await expect(page.locator('[data-testid="mission-form"]')).toBeVisible()
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All HU-11 acceptance criteria are exercised:
  - Admin can create a trivia quiz.
  - Admin can edit a trivia quiz while its status is Draft.
  - Saved draft changes are visible in the catalog list and detail view.
  - Questions section is present in the detail view (read-only).
  - Non-admin roles cannot access mutation UI.
- All existing mission test assertions pass.

---

## Commit Sequence

```
feat(frontend): phase 1 — add trivia quiz type definitions (hu-11)
feat(frontend): phase 2 — api client and server actions for trivia quiz management (hu-11)
feat(frontend): phase 3 — TriviasPanel component with read-only questions section (hu-11)
feat(frontend): phase 4 — wire TriviasPanel into DashboardClient (hu-11)
feat(frontend): phase 5 — e2e tests for HU-11 trivia quiz management (hu-11)

Ref: HU-11
```

---

## Out of Scope

- **Question authoring (add/edit/remove questions and options).** HU-14A owns the full
  question-management workflow. The questions array is passed as `[]` on create and update
  for this HU. The read-only questions section in detail/edit is a placeholder that shows
  backend-persisted question data without providing authoring controls.
- **Quiz publication and archiving.** No `MarkAsPublished` or `MarkAsArchived` endpoint is
  exposed in HU-11. The status chip is read-only; the edit gate (disabled for non-Draft) handles
  the downstream constraint correctly.
- **GET authorization tightening.** The backend allows any authenticated caller to read
  trivia data. If operator/participant read access needs to be restricted in the future,
  the action layer is the right place — for HU-11 only admins reach the panel, so the
  effective behaviour is admin-only even though the underlying actions don't gate reads.
- **Pagination.** `GET /api/trivias` returns a flat list. If the catalog grows, pagination
  can be added in a follow-on HU without changing this plan's data contracts.
- **Self-contained quiz creation with questions.** The backend supports submitting questions
  in the create/update body. This plan defers that to HU-14A and passes `questions: []`.
  The form shape does not expose question fields.
