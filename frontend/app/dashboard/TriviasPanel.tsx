'use client'

import { useEffect, useState, useTransition } from 'react'
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
import type { TriviaQuizSummaryDto, TriviaQuizDto, TriviaQuestionDto, TriviaQuestionRequest } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type TriviaPanelView = 'list' | 'detail' | 'create' | 'edit' | 'add-question' | 'edit-question'

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

export function TriviasPanel({ role }: { role: DashboardRole }) {
  const [view, setView] = useState<TriviaPanelView>('list')
  const [selectedQuiz, setSelectedQuiz] = useState<TriviaQuizDto | null>(null)
  const [selectedQuestion, setSelectedQuestion] = useState<TriviaQuestionDto | null>(null)
  const [listData, setListData] = useState<TriviaQuizSummaryDto[] | null>(null)
  const [isPending, startTransition] = useTransition()
  const [listError, setListError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [questionError, setQuestionError] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const [confirmPublish, setConfirmPublish] = useState(false)
  const [confirmArchive, setConfirmArchive] = useState(false)
  const [confirmDuplicate, setConfirmDuplicate] = useState(false)
  const [confirmRetire, setConfirmRetire] = useState(false)
  const [lifecycleError, setLifecycleError] = useState<string | null>(null)

  const allConfirmsClosed = !confirmPublish && !confirmArchive && !confirmDuplicate && !confirmRetire

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
        } else if (msg === 'question_sequence_conflict') {
          setQuestionError('A question with that sequence order already exists. Choose a different order.')
        } else {
          setQuestionError('Failed to add question. Try again.')
        }
      }
    })
  }

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
        } else if (msg === 'question_sequence_conflict') {
          setQuestionError('A question with that sequence order already exists. Choose a different order.')
        } else {
          setQuestionError('Failed to update question. Try again.')
        }
      }
    })
  }

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

  function statusTone(status: string): 'success' | 'warning' | 'muted' {
    if (status === 'Published') return 'success'
    if (status === 'Draft') return 'warning'
    return 'muted'
  }

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

  if (view === 'detail' && selectedQuiz !== null) {
    return (
      <section className={styles.panel} data-testid="trivia-detail">
        <button
          className={styles.inlineButton}
          onClick={() => { setView('list'); setConfirmPublish(false); setConfirmArchive(false); setConfirmDuplicate(false); setConfirmRetire(false); setLifecycleError(null) }}
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
        </div>

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

        {lifecycleError && (
          <p className={styles.formError} role="alert" data-testid="lifecycle-error">
            {lifecycleError}
          </p>
        )}

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

        {renderQuestionsSection(selectedQuiz.questions, selectedQuiz)}
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

        {renderQuestionsSection(selectedQuiz.questions, selectedQuiz)}
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
                <th>Source ready</th>
                <th>Provenance</th>
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
                    <span
                      className={styles.chip}
                      data-tone={quiz.isSourceReady ? 'success' : 'muted'}
                      data-testid={`trivia-source-ready-${quiz.id}`}
                    >
                      {quiz.isSourceReady ? 'Yes' : 'No'}
                    </span>
                  </td>
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
              style={{ marginLeft: '0.75rem' }}
              title="Mark as correct"
              type="radio"
              onChange={() => markCorrect(index)}
            />
            <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginLeft: '0.35rem' }}>Correct</span>
            {options.length > 2 && (
              <button
                className={styles.inlineButton}
                data-testid={`question-remove-option-btn-${index}`}
                disabled={isPending}
                onClick={() => removeOption(index)}
                style={{ marginLeft: '1rem' }}
                type="button"
              >
                ✕
              </button>
            )}
          </div>
        ))}
        {options.length < 4 && (
          <div style={{ width: '100%', marginTop: '1rem', paddingLeft: '0.25rem' }}>
            <button
              className={styles.inlineButton}
              data-testid="question-add-option-btn"
              disabled={isPending}
              onClick={addOption}
              type="button"
            >
              + Add option
            </button>
          </div>
        )}
      </div>

      {displayedError && (
        <p className={styles.formError} role="alert">{displayedError}</p>
      )}

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
