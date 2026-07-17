# Plan: HU-14A Frontend — Trivia Question and Options Management

**Ref:** HU-14A
**Branch:** feature/hu-14a-trivia-question-and-options-management
**Date:** 2026-06-01
**Builds on:** HU-11 frontend (TriviasPanel, `app/lib/trivias.ts`, `app/actions/trivias.ts`,
`TriviaQuestionDto`, `TriviaQuizDto`, DashboardClient wiring)

---

## Context

Backend HU-14A (`mission-design-service`) is fully implemented. Two new endpoints are
available beyond what HU-11 covered:

- `POST /api/trivias/{triviaQuizId}/questions` — add a question to a draft quiz; `Administrator` only
- `PUT /api/trivias/{triviaQuizId}/questions/{questionId}` — replace a question's data; `Administrator` only

Both return `200 OK` with the full `TriviaQuizResponse` so the panel can update state
in a single round-trip without a follow-up GET.

HU-11 left four gaps that HU-14A must close:

1. **`TriviaQuestionDto` is incomplete.** The backend response for `TriviaQuestionResponse`
   includes `scoreValue`, `timeLimitSeconds`, and `explanation` — fields absent from the
   current frontend type. The read-only questions table therefore cannot render them.
2. **No API client functions for the question endpoints.** `lib/trivias.ts` has no
   `addTriviaQuestion` or `updateTriviaQuestion`.
3. **No Server Actions for question mutation.** `actions/trivias.ts` has no
   `addTriviaQuestion` or `updateTriviaQuestion`.
4. **`TriviasPanel` shows questions read-only** with a placeholder footnote "Question
   management is available in a future release." This placeholder is replaced by the full
   question authoring flow.

---

## Verified Backend Contract

### Common headers (trust envelope)

```
X-User-Id:    {session.externalIdentityId}
X-User-Role:  {session.role}
X-User-Email: {session.email}
```

---

### `POST /api/trivias/{triviaQuizId}/questions`

**Request body**
```ts
{
  prompt: string           // required, max 2000
  sequenceOrder: number    // required, > 0; unique across quiz questions
  scoreValue: number       // required, > 0
  timeLimitSeconds: number // required, > 0
  explanation: string | null  // optional, max 4000
  isActive: boolean
  options: TriviaOptionRequest[]  // 2–4 entries
}
```

**`TriviaOptionRequest`**
```ts
{
  optionText: string   // required, max 1000
  sequenceOrder: number  // required, > 0; unique within question
  isCorrect: boolean   // exactly one entry per question must be true
}
```

**Response `200 OK`** — full `TriviaQuizResponse` (same shape as `GET /api/trivias/{id}`)

**Error cases**
- `400` — validation: empty prompt, duplicate sequence orders, < 2 or > 4 options,
  multiple or zero correct options, non-positive score/timer/sequence
- `401` — missing trust headers
- `403` — caller is not `Administrator`
- `404` — quiz not found

**Additional verified behaviour (from integration tests)**
- The returned quiz includes all existing questions plus the newly added question.
- A follow-up `GET /api/trivias/{id}` reflects the same data (persistence verified).

---

### `PUT /api/trivias/{triviaQuizId}/questions/{questionId}`

Identical request body shape to `POST /api/trivias/{triviaQuizId}/questions`.

**Response `200 OK`** — full `TriviaQuizResponse`

**Error cases**
- `400` — same validation rules as add
- `401` — missing trust headers
- `403` — caller is not `Administrator`
- `404` — quiz not found or question not found within that quiz

**Additional verified behaviour**
- The response and a follow-up GET both reflect the updated prompt, scoreValue,
  timeLimitSeconds, explanation, isActive, and the new options set.

---

### Updated `TriviaQuestionResponse` shape

The backend has always returned these fields, but the HU-11 frontend DTO was
declared without them. The canonical shape (as implemented in `TriviasEndpoints.cs`):

```ts
{
  id: number
  prompt: string
  sequenceOrder: number
  isActive: boolean
  options: TriviaOptionResponse[]
  scoreValue: number | null       // nullable: null when question was embedded in quiz body
  timeLimitSeconds: number | null // nullable: same
  explanation: string | null      // nullable
}
```

---

## Architecture Decisions

- **Per-question endpoints, not quiz-level replacement.** HU-14A uses
  `POST /api/trivias/{id}/questions` and `PUT /api/trivias/{id}/questions/{questionId}`
  for individual question authoring. The quiz-level `PUT /api/trivias/{id}` **omits**
  `questions` from the title/description edit form, so the details edit preserves the
  existing questions rather than replacing them (omitting the field means "leave questions
  as-is"; sending a collection — including `[]` — replaces them). The backend supports
  both paths; the per-question endpoints are semantically correct for incremental
  authoring and avoid accidental data loss from rebuilding the full array.
- **`TriviaPanelView` extended with `'add-question' | 'edit-question'`.**  The existing
  four-state machine gains two new leaf states. Both return to `'detail'` on success
  or cancel. `selectedQuestion` state carries the question being edited; it is `null`
  for add.
- **`TriviaQuestionDto` gains `scoreValue`, `timeLimitSeconds`, `explanation`.** These
  are `| null` because the backend serializes them as nullable when the question was
  created via the quiz-body path (no score/timer specified). The dedicated endpoints
  require non-null values, but the type must accommodate both code paths.
- **`TriviaQuestionRequest` and `TriviaOptionRequest` added to `definitions.ts`.**
  These are the write-side counterparts to the read-side DTOs. Defining them in
  `definitions.ts` avoids duplicating the shape across the lib and action layers.
- **Question authoring controls are gated on `quiz.status === 'Draft'`.** Matching
  the existing "Edit quiz" button gate — the "Add question" button and per-question
  "Edit" buttons are hidden when the quiz is Published or Archived, preventing a
  wasted round-trip against the backend's validation.
- **`TriviaQuestionForm` is a self-contained component inside `TriviasPanel.tsx`.**
  It manages its own option array draft state. Options start at 2 rows and can grow
  to 4. `isCorrect` is implemented as a radio group so only one option can be marked
  correct at a time. Client-side validation (2–4 options, one correct, non-empty texts)
  runs on submit before the network call.
- **Response replaces `selectedQuiz` directly.** Both add and update question actions
  return the full quiz DTO. The panel sets `setSelectedQuiz(updatedQuiz)` immediately
  so the detail view is consistent without a follow-up GET.
- **`revalidatePath('/dashboard')` after add and update question.** Clears the RSC
  cache so the quiz list and detail pages reflect the latest state on next nav.
- **`renderQuestionsSection` updated to show new fields.** The read-only table
  gains Score, Timer (s), and Explanation columns; the placeholder footnote about
  "future release" is removed; admin users see "Add question" and per-row "Edit"
  buttons.
- **No changes to `createTriviaQuiz` or `updateTriviaQuiz` in `lib/trivias.ts`.**
  Create passes `questions: []`; update omits `questions` so a details edit preserves
  the existing questions. Question management is exclusively via the dedicated endpoints.
- **Server Actions enforce Administrator-only at the action layer.** Both
  `addTriviaQuestion` and `updateTriviaQuestion` in `actions/trivias.ts` check
  `session.role !== 'Administrator'` before reaching the backend, consistent with
  the existing pattern.

---

## Environment

No new environment variables. `MISSION_DESIGN_SERVICE_URL` (`http://localhost:5001`)
already covers the new question endpoints.

---

## Phases

### Phase 1 — Type extensions

**Scope**
- Extend `TriviaQuestionDto` in `app/lib/definitions.ts` with `scoreValue`,
  `timeLimitSeconds`, and `explanation`.
- Add `TriviaOptionRequest` and `TriviaQuestionRequest` write-side types.

**`app/lib/definitions.ts` changes**
```ts
// Before
export type TriviaQuestionDto = {
  id: number
  prompt: string
  sequenceOrder: number
  isActive: boolean
  options: TriviaOptionDto[]
}

// After
export type TriviaQuestionDto = {
  id: number
  prompt: string
  sequenceOrder: number
  isActive: boolean
  options: TriviaOptionDto[]
  scoreValue: number | null
  timeLimitSeconds: number | null
  explanation: string | null
}

// New write-side types
export type TriviaOptionRequest = {
  optionText: string
  sequenceOrder: number
  isCorrect: boolean
}

export type TriviaQuestionRequest = {
  prompt: string
  sequenceOrder: number
  scoreValue: number
  timeLimitSeconds: number
  explanation: string | null
  isActive: boolean
  options: TriviaOptionRequest[]
}
```

**Gate**
- `pnpm build` passes with no type errors.
- No runtime change; existing sessions and panels are unaffected.

---

### Phase 2 — API client additions (`lib/trivias.ts`)

**Scope**
- Add `addTriviaQuestion` and `updateTriviaQuestion` to `app/lib/trivias.ts`.

**Additions**
```ts
import type {
  TriviaQuizSummaryDto,
  TriviaQuizDto,
  TriviaQuestionRequest,
} from './definitions'

export async function addTriviaQuestion(
  triviaQuizId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${triviaQuizId}/questions`,
    {
      method: 'POST',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify(question),
    },
  )
  if (response.status === 400) throw new Error('invalid_question')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (!response.ok) throw new IdentityError('unknown', `addTriviaQuestion failed with status ${response.status}`)
  return response.json()
}

export async function updateTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${triviaQuizId}/questions/${questionId}`,
    {
      method: 'PUT',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify(question),
    },
  )
  if (response.status === 400) throw new Error('invalid_question')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (!response.ok) throw new IdentityError('unknown', `updateTriviaQuestion failed with status ${response.status}`)
  return response.json()
}
```

**Gate**
- `pnpm build` passes with no type errors.

---

### Phase 3 — Server Action additions (`actions/trivias.ts`)

**Scope**
- Add `addTriviaQuestion` and `updateTriviaQuestion` Server Actions to
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
} from '@/app/lib/trivias'
import type {
  TriviaQuizSummaryDto,
  TriviaQuizDto,
  TriviaQuestionRequest,
} from '@/app/lib/definitions'
```

**Server Action additions**
```ts
export async function addTriviaQuestion(
  triviaQuizId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await addTriviaQuestionLib(triviaQuizId, question)
  revalidatePath('/dashboard')
  return result
}

export async function updateTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await updateTriviaQuestionLib(triviaQuizId, questionId, question)
  revalidatePath('/dashboard')
  return result
}
```

**Gate**
- `pnpm build` passes with no type errors.
- Calling `addTriviaQuestion` from an Operator session throws `'Forbidden'` before
  reaching the backend.

---

### Phase 4 — Question authoring UI (`TriviasPanel.tsx`)

**Scope**
- Import `addTriviaQuestion` and `updateTriviaQuestion` from `@/app/actions/trivias`.
- Import `TriviaQuestionRequest`, `TriviaOptionRequest` from `@/app/lib/definitions`.
- Extend `TriviaPanelView` union.
- Add `selectedQuestion` and `questionError` state.
- Add `handleAddQuestion` and `handleUpdateQuestion` handlers.
- Update `renderQuestionsSection` to show new fields and authoring controls.
- Add `add-question` and `edit-question` rendered branches.
- Add `TriviaQuestionForm` component.

**Type and state additions**
```ts
type TriviaPanelView = 'list' | 'detail' | 'create' | 'edit' | 'add-question' | 'edit-question'

// Inside TriviasPanel:
const [selectedQuestion, setSelectedQuestion] = useState<TriviaQuestionDto | null>(null)
const [questionError, setQuestionError] = useState<string | null>(null)
```

**`handleAddQuestion`**
```ts
async function handleAddQuestion(question: TriviaQuestionRequest) {
  if (!selectedQuiz) return
  startTransition(async () => {
    setQuestionError(null)
    try {
      const updated = await addTriviaQuestion(selectedQuiz.id, question)
      setSelectedQuiz(updated)
      setView('detail')
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'invalid_question') {
        setQuestionError('Invalid question. Check all fields and ensure exactly one correct option.')
      } else if (msg === 'trivia_not_found') {
        setQuestionError('Trivia quiz no longer exists.')
      } else {
        setQuestionError('Failed to add question. Try again.')
      }
    }
  })
}
```

**`handleUpdateQuestion`**
```ts
async function handleUpdateQuestion(questionId: number, question: TriviaQuestionRequest) {
  if (!selectedQuiz) return
  startTransition(async () => {
    setQuestionError(null)
    try {
      const updated = await updateTriviaQuestion(selectedQuiz.id, questionId, question)
      setSelectedQuiz(updated)
      setView('detail')
      setRefreshKey((k) => k + 1)
    } catch (err) {
      const msg = err instanceof Error ? err.message : ''
      if (msg === 'invalid_question') {
        setQuestionError('Invalid question. Check all fields and ensure exactly one correct option.')
      } else if (msg === 'trivia_not_found') {
        setQuestionError('Trivia quiz no longer exists.')
      } else {
        setQuestionError('Failed to update question. Try again.')
      }
    }
  })
}
```

**`renderQuestionsSection` update**

Replace the current implementation. The updated version:
- Removes the "future release" placeholder footnote.
- Adds Score, Timer (s), and Explanation to the table header and each row.
- Adds an "Add question" button above the table, admin-only, gated on `quiz.status === 'Draft'`.
- Adds an "Edit" button per question row, admin-only, gated on `quiz.status === 'Draft'`.

```tsx
function renderQuestionsSection(
  questions: TriviaQuizDto['questions'],
  quiz: TriviaQuizDto,
) {
  const isDraft = quiz.status === 'Draft'

  return (
    <div data-testid="trivia-questions-section">
      <div className={styles.subsectionHeader}>
        <h3>Questions</h3>
        {role === 'admin' && isDraft && (
          <button
            className={styles.primaryButton}
            data-testid="add-question-btn"
            disabled={isPending}
            onClick={() => { setQuestionError(null); setSelectedQuestion(null); setView('add-question') }}
            type="button"
          >
            Add question
          </button>
        )}
      </div>

      {questions.length === 0 ? (
        <p className={styles.mutedText}>No questions added yet.</p>
      ) : (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Order</th>
              <th>Prompt</th>
              <th>Score</th>
              <th>Timer (s)</th>
              <th>Explanation</th>
              <th>Status</th>
              <th>Options</th>
              {role === 'admin' && isDraft && <th>Actions</th>}
            </tr>
          </thead>
          <tbody>
            {[...questions]
              .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
              .map((q) => (
                <tr key={q.id} data-testid={`question-row-${q.id}`}>
                  <td>{q.sequenceOrder}</td>
                  <td>{q.prompt}</td>
                  <td>{q.scoreValue ?? '—'}</td>
                  <td>{q.timeLimitSeconds ?? '—'}</td>
                  <td>{q.explanation ?? '—'}</td>
                  <td>
                    <span className={styles.chip} data-tone={q.isActive ? 'success' : 'muted'}>
                      {q.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>
                    <ul style={{ margin: 0, paddingLeft: '1.2rem', color: 'var(--text-secondary)' }}>
                      {[...q.options]
                        .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
                        .map((opt) => (
                          <li key={opt.id}>
                            {opt.optionText}
                            {opt.isCorrect && (
                              <span style={{ marginLeft: '0.4rem', color: 'var(--success)' }}>✓</span>
                            )}
                          </li>
                        ))}
                    </ul>
                  </td>
                  {role === 'admin' && isDraft && (
                    <td>
                      <button
                        className={styles.inlineButton}
                        data-testid={`edit-question-btn-${q.id}`}
                        disabled={isPending}
                        onClick={() => {
                          setQuestionError(null)
                          setSelectedQuestion(q)
                          setView('edit-question')
                        }}
                        type="button"
                      >
                        Edit
                      </button>
                    </td>
                  )}
                </tr>
              ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
```

Update the two call-sites of `renderQuestionsSection` to pass `selectedQuiz` as the
second argument:
```tsx
{renderQuestionsSection(selectedQuiz.questions, selectedQuiz)}
```

**`add-question` rendered branch** — insert before the existing `list` fallback:
```tsx
if (view === 'add-question' && selectedQuiz !== null) {
  return (
    <section className={styles.panel} data-testid="trivias-panel">
      <button
        className={styles.inlineButton}
        onClick={() => { setQuestionError(null); setView('detail') }}
        type="button"
      >
        ← {selectedQuiz.title}
      </button>

      <h2 className={styles.missionDetailTitle}>Add question</h2>

      <TriviaQuestionForm
        initial={null}
        isPending={isPending}
        error={questionError}
        onSubmit={(q) => handleAddQuestion(q)}
        onCancel={() => { setQuestionError(null); setView('detail') }}
      />
    </section>
  )
}
```

**`edit-question` rendered branch** — insert after `add-question`:
```tsx
if (view === 'edit-question' && selectedQuiz !== null && selectedQuestion !== null) {
  return (
    <section className={styles.panel} data-testid="trivias-panel">
      <button
        className={styles.inlineButton}
        onClick={() => { setQuestionError(null); setView('detail') }}
        type="button"
      >
        ← {selectedQuiz.title}
      </button>

      <h2 className={styles.missionDetailTitle}>Edit question</h2>

      <TriviaQuestionForm
        initial={selectedQuestion}
        isPending={isPending}
        error={questionError}
        onSubmit={(q) => handleUpdateQuestion(selectedQuestion.id, q)}
        onCancel={() => { setQuestionError(null); setView('detail') }}
      />
    </section>
  )
}
```

**`TriviaQuestionForm` component**

```tsx
type OptionDraft = {
  optionText: string
  sequenceOrder: number
  isCorrect: boolean
}

function TriviaQuestionForm({
  initial,
  isPending,
  error,
  onSubmit,
  onCancel,
}: {
  initial: TriviaQuestionDto | null
  isPending: boolean
  error: string | null
  onSubmit: (question: TriviaQuestionRequest) => void
  onCancel: () => void
}) {
  const [prompt, setPrompt] = useState(initial?.prompt ?? '')
  const [sequenceOrder, setSequenceOrder] = useState(initial?.sequenceOrder ?? 1)
  const [scoreValue, setScoreValue] = useState(initial?.scoreValue ?? 100)
  const [timeLimitSeconds, setTimeLimitSeconds] = useState(initial?.timeLimitSeconds ?? 30)
  const [explanation, setExplanation] = useState(initial?.explanation ?? '')
  const [isActive, setIsActive] = useState(initial?.isActive ?? true)
  const [options, setOptions] = useState<OptionDraft[]>(
    initial?.options && initial.options.length >= 2
      ? [...initial.options]
          .sort((a, b) => a.sequenceOrder - b.sequenceOrder)
          .map((opt) => ({
            optionText: opt.optionText,
            sequenceOrder: opt.sequenceOrder,
            isCorrect: opt.isCorrect,
          }))
      : [
          { optionText: '', sequenceOrder: 1, isCorrect: false },
          { optionText: '', sequenceOrder: 2, isCorrect: false },
        ],
  )
  const [formError, setFormError] = useState<string | null>(null)

  function addOption() {
    if (options.length >= 4) return
    const nextSeq = Math.max(...options.map((o) => o.sequenceOrder)) + 1
    setOptions([...options, { optionText: '', sequenceOrder: nextSeq, isCorrect: false }])
  }

  function removeOption(index: number) {
    if (options.length <= 2) return
    setOptions(options.filter((_, i) => i !== index))
  }

  function updateOption(index: number, patch: Partial<OptionDraft>) {
    setOptions(options.map((opt, i) => (i === index ? { ...opt, ...patch } : opt)))
  }

  function markCorrect(index: number) {
    setOptions(options.map((opt, i) => ({ ...opt, isCorrect: i === index })))
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)

    const correctCount = options.filter((o) => o.isCorrect).length
    if (correctCount !== 1) {
      setFormError('Exactly one option must be marked as correct.')
      return
    }
    if (options.some((o) => o.optionText.trim() === '')) {
      setFormError('All option texts are required.')
      return
    }
    if (options.length < 2 || options.length > 4) {
      setFormError('A question must have between 2 and 4 options.')
      return
    }

    onSubmit({
      prompt: prompt.trim(),
      sequenceOrder,
      scoreValue,
      timeLimitSeconds,
      explanation: explanation.trim() !== '' ? explanation.trim() : null,
      isActive,
      options: options.map((opt) => ({
        optionText: opt.optionText.trim(),
        sequenceOrder: opt.sequenceOrder,
        isCorrect: opt.isCorrect,
      })),
    })
  }

  const displayedError = error ?? formError

  return (
    <form
      className={styles.missionForm}
      data-testid="question-form"
      onSubmit={handleSubmit}
    >
      {displayedError && (
        <p className={styles.formError} role="alert">{displayedError}</p>
      )}

      {/* Prompt */}
      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Prompt</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="question-prompt-input"
          disabled={isPending}
          maxLength={2000}
          placeholder="Enter question prompt"
          required
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
        />
      </div>

      {/* Sequence order */}
      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Sequence order</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="question-sequence-order-input"
          disabled={isPending}
          min={1}
          required
          type="number"
          value={sequenceOrder}
          onChange={(e) => setSequenceOrder(Number(e.target.value))}
        />
      </div>

      {/* Score */}
      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Score value</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="question-score-value-input"
          disabled={isPending}
          min={1}
          required
          type="number"
          value={scoreValue}
          onChange={(e) => setScoreValue(Number(e.target.value))}
        />
      </div>

      {/* Timer */}
      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Time limit (seconds)</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="question-timer-input"
          disabled={isPending}
          min={1}
          required
          type="number"
          value={timeLimitSeconds}
          onChange={(e) => setTimeLimitSeconds(Number(e.target.value))}
        />
      </div>

      {/* Explanation */}
      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Explanation (optional)</span>
        <textarea
          className={styles.missionFormDescTextarea}
          data-testid="question-explanation-input"
          disabled={isPending}
          maxLength={4000}
          placeholder="Explain why the correct answer is right"
          rows={3}
          value={explanation}
          onChange={(e) => setExplanation(e.target.value)}
        />
      </div>

      {/* Active toggle */}
      <div className={styles.missionFormNameCard}>
        <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <input
            checked={isActive}
            data-testid="question-active-checkbox"
            disabled={isPending}
            type="checkbox"
            onChange={(e) => setIsActive(e.target.checked)}
          />
          <span className={styles.missionDetailDescLabel}>Active</span>
        </label>
      </div>

      {/* Options */}
      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Options (2–4)</span>
        {options.map((opt, index) => (
          <div
            key={index}
            className={styles.confirmRow}
            data-testid={`question-option-${index}`}
            style={{ marginBottom: '0.5rem' }}
          >
            <input
              className={styles.missionFormNameInput}
              data-testid={`question-option-text-${index}`}
              disabled={isPending}
              maxLength={1000}
              placeholder={`Option ${index + 1}`}
              required
              value={opt.optionText}
              onChange={(e) => updateOption(index, { optionText: e.target.value })}
            />
            <input
              checked={opt.isCorrect}
              data-testid={`question-option-correct-${index}`}
              disabled={isPending}
              name="correct-option"
              title="Mark as correct"
              type="radio"
              onChange={() => markCorrect(index)}
            />
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Correct</span>
            {options.length > 2 && (
              <button
                className={styles.inlineButton}
                data-testid={`question-remove-option-btn-${index}`}
                disabled={isPending}
                onClick={() => removeOption(index)}
                style={{ marginLeft: '0.25rem' }}
                type="button"
              >
                ✕
              </button>
            )}
          </div>
        ))}
        {options.length < 4 && (
          <button
            className={styles.inlineButton}
            data-testid="question-add-option-btn"
            disabled={isPending}
            onClick={addOption}
            type="button"
          >
            + Add option
          </button>
        )}
      </div>

      {/* Actions */}
      <div className={styles.missionDetailActions}>
        <button
          className={styles.primaryButton}
          data-testid="question-submit-btn"
          disabled={isPending}
          type="submit"
        >
          Save
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          type="button"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </form>
  )
}
```

**`data-testid` contract additions**

| Element | `data-testid` |
|---|---|
| Add question button | `add-question-btn` |
| Question row | `question-row-{id}` |
| Edit question button (per row) | `edit-question-btn-{id}` |
| Question form | `question-form` |
| Prompt input | `question-prompt-input` |
| Sequence order input | `question-sequence-order-input` |
| Score value input | `question-score-value-input` |
| Timer input | `question-timer-input` |
| Explanation textarea | `question-explanation-input` |
| Active checkbox | `question-active-checkbox` |
| Option container | `question-option-{index}` |
| Option text input | `question-option-text-{index}` |
| Option correct radio | `question-option-correct-{index}` |
| Add option button | `question-add-option-btn` |
| Remove option button | `question-remove-option-btn-{index}` |
| Submit button | `question-submit-btn` |

**Gate**
- `pnpm build` passes with no type errors.
- Admin opens a Draft quiz detail → "Add question" button is visible.
- Admin opens a Published quiz detail → "Add question" button is absent.
- Clicking "Add question" shows `TriviaQuestionForm` with 2 empty option rows.
- Clicking "Edit" on a question row shows `TriviaQuestionForm` pre-filled with the
  question's current values and options.
- Clicking "Cancel" from either question form returns to the detail view without a
  network call.
- Operator viewing the users panel sees the questions table but no "Add question" or
  "Edit" question buttons.

---

### Phase 5 — E2E tests (HU-14A extension)

**Scope**
- Extend `tests/e2e/trivias.spec.ts` with HU-14A question authoring tests.
- Verify no regression on HU-11 trivia quiz tests or HU-09 mission tests.

**Test additions to `tests/e2e/trivias.spec.ts`**

```ts
// --- Question authoring: add ---

test('admin can open add-question form from draft quiz detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Question Form Visibility')
  await page.fill('[data-testid="trivia-description-input"]', 'Check add question form appears.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="add-question-btn"]')).toBeVisible()
  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-option-0"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-option-1"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-option-2"]')).toHaveCount(0) // starts with 2
})

test('admin can add a question with 2 options and see it in detail', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Add Question E2E')
  await page.fill('[data-testid="trivia-description-input"]', 'Test adding a question.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'What is 1+1?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '50')
  await page.fill('[data-testid="question-timer-input"]', '20')
  await page.fill('[data-testid="question-option-text-0"]', '2')
  await page.fill('[data-testid="question-option-text-1"]', '3')
  await page.click('[data-testid="question-option-correct-0"]') // mark first option correct

  await page.click('[data-testid="question-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('What is 1+1?')
})

test('admin can add a question with 4 options', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Four Options Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Testing four options.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'Best planet?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '100')
  await page.fill('[data-testid="question-timer-input"]', '30')

  // Add two more options (starts with 2)
  await page.click('[data-testid="question-add-option-btn"]')
  await page.click('[data-testid="question-add-option-btn"]')

  await page.fill('[data-testid="question-option-text-0"]', 'Earth')
  await page.fill('[data-testid="question-option-text-1"]', 'Mars')
  await page.fill('[data-testid="question-option-text-2"]', 'Venus')
  await page.fill('[data-testid="question-option-text-3"]', 'Jupiter')
  await page.click('[data-testid="question-option-correct-0"]')

  // Add option button should be gone at 4
  await expect(page.locator('[data-testid="question-add-option-btn"]')).toHaveCount(0)

  await page.click('[data-testid="question-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
})

test('add-question cancel returns to detail without network call', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Cancel Question Test')
  await page.fill('[data-testid="trivia-description-input"]', 'No question saved.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await page.getByRole('button', { name: 'Cancel' }).click()

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-form"]')).toHaveCount(0)
})

// --- Question authoring: edit ---

test('admin can edit an existing question', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Edit Question Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Will have one question.')
  await page.click('[data-testid="trivia-submit-btn"]')

  // Add a question first
  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'Original prompt')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '50')
  await page.fill('[data-testid="question-timer-input"]', '20')
  await page.fill('[data-testid="question-option-text-0"]', 'A')
  await page.fill('[data-testid="question-option-text-1"]', 'B')
  await page.click('[data-testid="question-option-correct-0"]')
  await page.click('[data-testid="question-submit-btn"]')

  // Edit the question
  const editBtn = page.locator('[data-testid^="edit-question-btn-"]').first()
  await expect(editBtn).toBeVisible()
  await editBtn.click()

  await expect(page.locator('[data-testid="question-form"]')).toBeVisible()
  await expect(page.locator('[data-testid="question-prompt-input"]')).toHaveValue('Original prompt')

  await page.fill('[data-testid="question-prompt-input"]', 'Updated prompt')
  await page.fill('[data-testid="question-score-value-input"]', '75')
  await page.click('[data-testid="question-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toContainText('Updated prompt')
})

test('edit-question form is pre-filled with current values', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Prefill Check Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Check form pre-fill.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="add-question-btn"]')
  await page.fill('[data-testid="question-prompt-input"]', 'Capital of France?')
  await page.fill('[data-testid="question-sequence-order-input"]', '1')
  await page.fill('[data-testid="question-score-value-input"]', '100')
  await page.fill('[data-testid="question-timer-input"]', '45')
  await page.fill('[data-testid="question-explanation-input"]', 'Paris is the capital.')
  await page.fill('[data-testid="question-option-text-0"]', 'Paris')
  await page.fill('[data-testid="question-option-text-1"]', 'Berlin')
  await page.click('[data-testid="question-option-correct-0"]')
  await page.click('[data-testid="question-submit-btn"]')

  await page.locator('[data-testid^="edit-question-btn-"]').first().click()

  await expect(page.locator('[data-testid="question-prompt-input"]')).toHaveValue('Capital of France?')
  await expect(page.locator('[data-testid="question-score-value-input"]')).toHaveValue('100')
  await expect(page.locator('[data-testid="question-timer-input"]')).toHaveValue('45')
  await expect(page.locator('[data-testid="question-explanation-input"]')).toHaveValue('Paris is the capital.')
})

// --- Authorization ---

test('operator sees no add-question or edit-question buttons', async ({ operatorPage: page }) => {
  await page.goto('/dashboard')
  // Operators do not see trivias nav so they cannot reach the panel.
  await expect(page.locator('[data-testid="nav-trivias"]')).toHaveCount(0)
})

// --- Regression: HU-11 quiz flows unaffected ---

test('HU-11 trivia create flow still works after question authoring wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Regression check.')
  await page.click('[data-testid="trivia-submit-btn"]')
  await expect(page.locator('[data-testid="trivia-detail"]')).toBeVisible()
  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Regression Quiz')
  await expect(page.locator('[data-testid="trivia-questions-section"]')).toBeVisible()
})

test('HU-11 trivia edit flow still works after question authoring wiring', async ({ adminPage: page }) => {
  await page.goto('/dashboard')
  await page.click('[data-testid="nav-trivias"]')
  await page.click('[data-testid="create-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression Edit Quiz')
  await page.fill('[data-testid="trivia-description-input"]', 'Before edit.')
  await page.click('[data-testid="trivia-submit-btn"]')

  await page.click('[data-testid="edit-trivia-btn"]')
  await page.fill('[data-testid="trivia-title-input"]', 'Regression Edit Quiz — Updated')
  await page.click('[data-testid="trivia-submit-btn"]')

  await expect(page.locator('[data-testid="trivia-detail-title"]')).toContainText('Regression Edit Quiz — Updated')
})
```

**Gate**
- `pnpm exec playwright test` passes.
- All HU-14A acceptance criteria are exercised by automated tests.
- All HU-11, HU-09 regression assertions pass.

---

## Commit Sequence

```
feat(frontend): phase 1 — extend TriviaQuestionDto and add request types (hu-14a)
feat(frontend): phase 2 — api client for question add and update (hu-14a)
feat(frontend): phase 3 — server actions for question add and update (hu-14a)
feat(frontend): phase 4 — question authoring UI in TriviasPanel (hu-14a)
feat(frontend): phase 5 — e2e tests for HU-14A question management (hu-14a)

Ref: HU-14A
```

---

## Out of Scope

- **Question deletion.** No `DELETE /api/trivias/{id}/questions/{questionId}` endpoint
  exists in HU-14A. The delete affordance is omitted.
- **Question reordering via drag-and-drop.** The `sequenceOrder` field is editable
  via the form; no drag-and-drop reordering widget is added.
- **Quiz publication and archiving.** No `MarkAsPublished` or `MarkAsArchived` UI is
  added; the status chip remains read-only. The question authoring controls are gated
  on `status === 'Draft'` which is sufficient for the backend constraint.
- **Inline option text editing from the questions table.** The read-only table renders
  option data; editing always goes through the question form.
- **Question authoring for non-Draft quizzes.** Even though some backend endpoints may
  not explicitly block this, the frontend conservatively hides all authoring controls
  when the quiz status is not `Draft`, consistent with the existing quiz-edit gate.
- **Updating `createTriviaQuiz`/`updateTriviaQuiz` to accept questions.** Create keeps
  passing `questions: []` and update keeps omitting `questions`. Full quiz+question creation
  in one request is not required by HU-14A.
