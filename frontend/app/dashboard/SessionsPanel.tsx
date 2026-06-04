'use client'

import { useEffect, useState, useTransition } from 'react'
import { getPublishedTrivias, createTriviaSession } from '@/app/actions/sessions'
import type { TriviaQuizSummaryDto, TriviaSessionCreatedDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type PanelView = 'form' | 'created'

interface SessionsPanelProps {
  assignedSessions: Array<{
    id: string
    title: string
    subtitle: string
    district: string
    night: string
    state: 'live' | 'paused' | 'draft'
  }>
  selectedSessionId: string | null
  onSelectSession: (sessionId: string) => void
  onOpenLiveOperation: (sessionId: string) => void
}

const sessionStateLabels = {
  draft: 'Draft',
  live: 'Live',
  paused: 'Paused',
} as const

export function SessionsPanel({
  assignedSessions,
  selectedSessionId,
  onSelectSession,
  onOpenLiveOperation,
}: SessionsPanelProps) {
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

  const selectedAssignedSession =
    selectedSessionId == null
      ? null
      : assignedSessions.find((session) => session.id === selectedSessionId) ?? null

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

  return (
    <section className={`${styles.panel} ${styles.sessionsPanel}`} data-testid="sessions-panel">
      <div className={styles.panelHeader}>
        <div>
          <h2>My sessions</h2>
          <div className={styles.panelMeta}>
            Create and prepare sessions you are responsible for, then move into live operation.
          </div>
        </div>
      </div>

      <div className={styles.sessionWorkspaceGrid}>
        <section className={styles.assignmentCard} aria-labelledby="assigned-sessions-heading">
          <div className={styles.panelHeader}>
            <div>
              <h3 id="assigned-sessions-heading">Sessions you&apos;re responsible for</h3>
              <div className={styles.panelMeta}>
                Pick a session to continue setup or open its live controls.
              </div>
            </div>
          </div>

          {assignedSessions.length === 0 ? (
            <p className={styles.emptyStateCopy} data-testid="session-empty-state">
              You have no assigned sessions yet. Create a new session or ask an administrator to
              reassign one.
            </p>
          ) : (
            <div className={styles.sessionCards} data-testid="assigned-sessions-list">
              {assignedSessions.map((session) => (
                <button
                  key={session.id}
                  type="button"
                  className={styles.sessionButton}
                  data-current={selectedAssignedSession?.id === session.id}
                  onClick={() => onSelectSession(session.id)}
                  data-testid="assigned-session-button"
                >
                  <div className={styles.sessionCardHeader}>
                    <div>
                      <h3>{session.title}</h3>
                      <div className={styles.sessionCardMeta}>
                        {session.subtitle} • {session.district} • {session.night}
                      </div>
                    </div>
                    <span
                      className={styles.chip}
                      data-tone={
                        session.state === 'live'
                          ? 'success'
                          : session.state === 'paused'
                            ? 'warning'
                            : 'muted'
                      }
                    >
                      {sessionStateLabels[session.state]}
                    </span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </section>

        <section className={styles.assignmentCard} aria-labelledby="session-setup-heading">
          <div className={styles.panelHeader}>
            <div>
              <h3 id="session-setup-heading">Session setup</h3>
              <div className={styles.panelMeta}>
                {selectedAssignedSession
                  ? 'Review the current session before moving into live operation.'
                  : 'Select one of your sessions to continue setup and operation.'}
              </div>
            </div>
          </div>

          {selectedAssignedSession ? (
            <div className={styles.sessionsPanelStack}>
              <section className={styles.sessionCard} data-testid="selected-session-setup-card">
                <h3>{selectedAssignedSession.title}</h3>
                <dl className={styles.sessionMeta}>
                  <dt>Teams</dt>
                  <dd>{selectedAssignedSession.subtitle}</dd>

                  <dt>Location</dt>
                  <dd>{selectedAssignedSession.district}</dd>

                  <dt>Stage</dt>
                  <dd>{selectedAssignedSession.night}</dd>

                  <dt>Status</dt>
                  <dd>{sessionStateLabels[selectedAssignedSession.state]}</dd>
                </dl>
              </section>

              <button
                type="button"
                className={styles.primaryButton}
                onClick={() => onOpenLiveOperation(selectedAssignedSession.id)}
              >
                Open live operation
              </button>
            </div>
          ) : (
            <p className={styles.emptyStateCopy}>No session selected yet.</p>
          )}
        </section>
      </div>

      <section className={styles.assignmentCard} aria-labelledby="create-session-heading">
        <div className={styles.panelHeader}>
          <div>
            <h3 id="create-session-heading">Create session</h3>
            <div className={styles.panelMeta}>
              Start a new session from one published quiz. The session will be yours by default.
            </div>
          </div>
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

        {view === 'created' && createdSession ? (
          <div className={styles.sessionsPanelStack}>
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
          </div>
        ) : (
          <form
            className={styles.sessionForm}
            onSubmit={handleSubmit}
            data-testid="session-create-form"
          >
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
        )}
      </section>
    </section>
  )
}
