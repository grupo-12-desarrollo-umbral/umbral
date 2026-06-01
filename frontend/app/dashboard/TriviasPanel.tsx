'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getTriviaQuizzes,
  getTriviaQuiz,
  createTriviaQuiz,
  updateTriviaQuiz,
} from '@/app/actions/trivias'
import type { TriviaQuizSummaryDto, TriviaQuizDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type TriviaPanelView = 'list' | 'detail' | 'create' | 'edit'

export function TriviasPanel({ role }: { role: DashboardRole }) {
  const [view, setView] = useState<TriviaPanelView>('list')
  const [selectedQuiz, setSelectedQuiz] = useState<TriviaQuizDto | null>(null)
  const [listData, setListData] = useState<TriviaQuizSummaryDto[] | null>(null)
  const [isPending, startTransition] = useTransition()
  const [listError, setListError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)

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

  function statusTone(status: string): 'success' | 'warning' | 'muted' {
    if (status === 'Published') return 'success'
    if (status === 'Draft') return 'warning'
    return 'muted'
  }

  function renderQuestionsSection(questions: TriviaQuizDto['questions']) {
    if (questions.length === 0) {
      return (
        <div data-testid="trivia-questions-section">
          <p className={styles.mutedText}>No questions added yet.</p>
        </div>
      )
    }

    const sorted = [...questions].sort((a, b) => a.sequenceOrder - b.sequenceOrder)

    return (
      <div data-testid="trivia-questions-section">
        <div className={styles.subsectionHeader}>
          <h3>Questions</h3>
        </div>
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Order</th>
              <th>Prompt</th>
              <th>Status</th>
              <th>Options</th>
            </tr>
          </thead>
          <tbody>
            {sorted.map((q) => (
              <tr key={q.id}>
                <td>{q.sequenceOrder}</td>
                <td>{q.prompt}</td>
                <td>
                  <span
                    className={styles.chip}
                    data-tone={q.isActive ? 'success' : 'muted'}
                  >
                    {q.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td>
                  <ul style={{ margin: 0, paddingLeft: '1.2rem', color: 'var(--text-secondary)' }}>
                    {q.options.sort((a, b) => a.sequenceOrder - b.sequenceOrder).map((opt) => (
                      <li key={opt.id}>
                        {opt.optionText}
                        {opt.isCorrect && <span style={{ marginLeft: '0.4rem', color: 'var(--success)' }}>✓</span>}
                      </li>
                    ))}
                  </ul>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className={styles.mutedText} style={{ marginTop: '0.75rem' }}>
          Question management is available in a future release.
        </p>
      </div>
    )
  }

  if (view === 'detail' && selectedQuiz !== null) {
    return (
      <section className={styles.panel} data-testid="trivia-detail">
        <button
          className={styles.inlineButton}
          onClick={() => setView('list')}
          type="button"
        >
          ← Back to trivia quizzes
        </button>

        <h2 className={styles.missionDetailTitle} data-testid="trivia-detail-title">
          {selectedQuiz.title}
        </h2>

        <div className={styles.missionDetailDescCard}>
          <span className={styles.missionDetailDescLabel}>Description</span>
          <p data-testid="trivia-detail-description">{selectedQuiz.description}</p>
        </div>

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
        </div>

        <div className={styles.missionDetailActions}>
          <button
            className={styles.inlineButton}
            data-testid="edit-trivia-btn"
            disabled={isPending || selectedQuiz.status !== 'Draft'}
            onClick={() => { setFormError(null); setView('edit') }}
            type="button"
          >
            Edit
          </button>
        </div>

        {renderQuestionsSection(selectedQuiz.questions)}
      </section>
    )
  }

  if (view === 'create') {
    return (
      <section className={styles.panel} data-testid="trivias-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setFormError(null); setView('list') }}
          type="button"
        >
          ← Back to trivia quizzes
        </button>

        <h2 className={styles.missionDetailTitle}>Create trivia quiz</h2>

        <TriviaQuizForm
          initial={{ title: '', description: '' }}
          isPending={isPending}
          error={formError}
          onSubmit={handleCreate}
          onCancel={() => { setFormError(null); setView('list') }}
        />
      </section>
    )
  }

  if (view === 'edit' && selectedQuiz !== null) {
    return (
      <section className={styles.panel} data-testid="trivias-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setFormError(null); setView('detail') }}
          type="button"
        >
          ← {selectedQuiz.title}
        </button>

        <h2 className={styles.missionDetailTitle}>Edit trivia quiz</h2>

        <TriviaQuizForm
          initial={{ title: selectedQuiz.title, description: selectedQuiz.description }}
          isPending={isPending}
          error={formError}
          onSubmit={handleUpdate}
          onCancel={() => { setFormError(null); setView('detail') }}
        />

        {renderQuestionsSection(selectedQuiz.questions)}
      </section>
    )
  }

  // Default list view
  return (
    <section className={styles.panel} data-testid="trivias-panel">
      <div className={styles.panelHeader}>
        <div>
          <h2>Trivia quizzes</h2>
        </div>
        {role === 'admin' && (
          <button
            className={styles.primaryButton}
            data-testid="create-trivia-btn"
            disabled={isPending}
            onClick={() => { setFormError(null); setView('create') }}
            type="button"
          >
            Create trivia quiz
          </button>
        )}
      </div>

      {listError && <p className={styles.formError}>{listError}</p>}

      {listData && (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Title</th>
              <th>Description</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {listData.map((quiz) => (
              <tr key={quiz.id} data-testid={`trivia-row-${quiz.id}`}>
                <td>{quiz.title}</td>
                <td>{quiz.description}</td>
                <td>
                  <span
                    className={styles.chip}
                    data-tone={statusTone(quiz.status)}
                    data-testid={`trivia-status-${quiz.id}`}
                  >
                    {quiz.status}
                  </span>
                </td>
                <td>
                  <button
                    className={styles.inlineButton}
                    data-testid={`view-trivia-btn-${quiz.id}`}
                    disabled={isPending}
                    onClick={() => handleOpenDetail(quiz.id)}
                    type="button"
                  >
                    View details
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

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
}) {
  const [title, setTitle] = useState(initial.title)
  const [description, setDescription] = useState(initial.description)

  return (
    <form
      className={styles.missionForm}
      data-testid="trivia-form"
      onSubmit={(e) => {
        e.preventDefault()
        onSubmit(title.trim(), description.trim())
      }}
    >
      {error && <p className={styles.formError} role="alert">{error}</p>}

      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Quiz Title</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="trivia-title-input"
          disabled={isPending}
          maxLength={200}
          placeholder="Enter quiz title"
          required
          value={title}
          onChange={(e) => setTitle(e.target.value)}
        />
      </div>

      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Description</span>
        <textarea
          className={styles.missionFormDescTextarea}
          data-testid="trivia-description-input"
          disabled={isPending}
          maxLength={2000}
          placeholder="Describe the quiz"
          required
          rows={4}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>

      <div className={styles.missionDetailActions}>
        <button
          className={styles.primaryButton}
          data-testid="trivia-submit-btn"
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
