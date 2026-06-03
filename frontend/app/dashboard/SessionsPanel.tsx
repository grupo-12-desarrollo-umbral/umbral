'use client'

import { useEffect, useState, useTransition } from 'react'
import { getPublishedTrivias, createTriviaSession } from '@/app/actions/sessions'
import type { TriviaQuizSummaryDto, TriviaSessionCreatedDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type PanelView = 'form' | 'created'

interface SessionsPanelProps {
  role: 'operator'
}

export function SessionsPanel({ role }: SessionsPanelProps) {
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

  useEffect(() => {
    getPublishedTrivias()
      .then(setQuizzes)
      .catch(() => setQuizzesError('Failed to load available quizzes. Reload the page.'))
  }, [])

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

  function handleReset() {
    setView('form')
    setCreatedSession(null)
    setSelectedQuizId('')
    setTitle('')
    setMaxMinutes('60')
    setScheduledAt('')
    setFormError(null)
  }

  if (view === 'created' && createdSession) {
    return (
      <section className={`${styles.panel} ${styles.sessionsPanel}`} data-testid="sessions-panel">
        <div className={styles.panelHeader}>
          <h2>Session Created</h2>
        </div>

        <section className={styles.sessionCard} data-testid="session-created-card">
          <h3 data-testid="session-created-title">{createdSession.title}</h3>
          <dl className={styles.sessionMeta}>
            <dt>Session code</dt>
            <dd data-testid="session-code">{createdSession.sessionCode}</dd>

            <dt>State</dt>
            <dd data-testid="session-state">{createdSession.sessionState}</dd>

            <dt>Source quiz ID</dt>
            <dd data-testid="session-source-quiz-id">{createdSession.sourceTriviaQuizId}</dd>

            <dt>Questions in snapshot</dt>
            <dd data-testid="session-question-count">{createdSession.questionCount}</dd>

            <dt>Scheduled at</dt>
            <dd data-testid="session-scheduled-at-display">
              {new Date(createdSession.scheduledAt).toLocaleString()}
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
      </section>
    )
  }

  return (
    <section className={`${styles.panel} ${styles.sessionsPanel}`} data-testid="sessions-panel">
      <div className={styles.panelHeader}>
        <h2>Create Session</h2>
      </div>

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
          <option value="" disabled>— Select a published quiz —</option>
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
    </section>
  )
}
