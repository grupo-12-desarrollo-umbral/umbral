'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getAssignableOperators,
  listSessionsForAssignment,
  assignSessionOperator,
} from '@/app/actions/sessions'
import type { AssignableOperatorDto, SessionAssignmentSummaryDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

export function SessionOperatorPanel() {
  const [sessions, setSessions] = useState<SessionAssignmentSummaryDto[]>([])
  const [modalSessionId, setModalSessionId] = useState<string | null>(null)
  const [operators, setOperators] = useState<AssignableOperatorDto[]>([])
  const [pendingOperatorId, setPendingOperatorId] = useState<number | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [assignError, setAssignError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  useEffect(() => {
    startTransition(async () => {
      try {
        const [loadedOperators, loadedSessions] = await Promise.all([
          getAssignableOperators(),
          listSessionsForAssignment(),
        ])

        setOperators(loadedOperators)
        setSessions(loadedSessions)
        setModalSessionId(null)
        setPendingOperatorId(null)
        setAssignError(null)
        setListError(null)
      } catch (err) {
        setOperators([])
        setSessions([])
        setModalSessionId(null)
        setPendingOperatorId(null)
        setListError(
          err instanceof Error && err.message.includes('Administrator')
            ? 'Administrator role required.'
            : 'Failed to load sessions. Try again.',
        )
      }
    })
  }, [])

  const selectedSession =
    modalSessionId == null
      ? null
      : sessions.find((session) => session.liveSessionId === modalSessionId) ?? null

  function openAssignmentModal(session: SessionAssignmentSummaryDto) {
    setModalSessionId(session.liveSessionId)
    setPendingOperatorId(session.assignedOperatorUserId)
    setAssignError(null)
  }

  function closeAssignmentModal() {
    setModalSessionId(null)
    setPendingOperatorId(null)
    setAssignError(null)
  }

  function refreshSessions() {
    startTransition(async () => {
      setAssignError(null)
      try {
        const loadedSessions = await listSessionsForAssignment()
        const nextSelectedSessionId =
          modalSessionId && loadedSessions.some((session) => session.liveSessionId === modalSessionId)
            ? modalSessionId
            : null

        setSessions(loadedSessions)
        setModalSessionId(nextSelectedSessionId)
        setPendingOperatorId(
          nextSelectedSessionId == null
            ? null
            : loadedSessions.find((session) => session.liveSessionId === nextSelectedSessionId)
                ?.assignedOperatorUserId ?? null,
        )
        setListError(null)
      } catch (err) {
        setListError(
          err instanceof Error && err.message.includes('Administrator')
            ? 'Administrator role required.'
            : 'Failed to load sessions. Try again.',
        )
      }
    })
  }

  function currentOperatorLabel(s: SessionAssignmentSummaryDto): string {
    if (s.assignedOperatorUserId == null) return 'Unassigned'
    const op = operators.find((o) => o.id === s.assignedOperatorUserId)
    return op ? `${op.displayName} (${op.email})` : `User #${s.assignedOperatorUserId}`
  }

  function handleAssign() {
    if (!selectedSession || pendingOperatorId == null) return
    const previous = selectedSession.assignedOperatorUserId
    startTransition(async () => {
      setAssignError(null)
      try {
        const result = await assignSessionOperator(selectedSession.liveSessionId, pendingOperatorId)
        setSessions((current) =>
          current.map((session) =>
            session.liveSessionId === result.liveSessionId
              ? { ...session, assignedOperatorUserId: result.assignedOperatorUserId }
              : session,
          ),
        )
        closeAssignmentModal()
      } catch (err) {
        setPendingOperatorId(previous)
        setAssignError(
          err instanceof Error && err.message === 'ineligible_operator'
            ? 'That user cannot be assigned as the responsible operator.'
            : err instanceof Error && err.message === 'session_not_found'
              ? 'Session not found.'
              : err instanceof Error && err.message.includes('Administrator')
                ? 'Administrator role required.'
                : 'Assignment failed. Try again.',
        )
      }
    })
  }

  function handleCancel() {
    closeAssignmentModal()
  }

  function getSessionStateTone(state: SessionAssignmentSummaryDto['sessionState']) {
    switch (state.toLowerCase()) {
      case 'active':
        return 'success'
      case 'paused':
      case 'preparing':
        return 'warning'
      default:
        return 'muted'
    }
  }

  const sessionListContent =
    sessions.length === 0 && !listError
      ? (
          <div className={styles.assignmentCard}>
            <p className={styles.emptyList}>No active sessions to assign.</p>
          </div>
        )
      : (
          <>
            {sessions.map((session) => (
              <article
                key={session.liveSessionId}
                className={styles.sessionOperatorItem}
                data-testid="session-operator-item"
              >
                <div className={styles.sessionOperatorHeader}>
                  <div className={styles.sessionOperatorTitleBlock}>
                    <h3>{session.title}</h3>
                    <div className={styles.sessionCardMeta}>
                      <span className={styles.sessionCodePill}>{session.sessionCode}</span>
                      <span>{new Date(session.scheduledAt).toLocaleString()}</span>
                    </div>
                  </div>
                  <span className={styles.chip} data-tone={getSessionStateTone(session.sessionState)}>
                    {session.sessionState}
                  </span>
                </div>
                <div className={styles.sessionOperatorBody}>
                  <div className={styles.sessionOperatorInfo}>
                    <span className={styles.sessionOperatorLabel}>Current operator</span>
                    <strong>{currentOperatorLabel(session)}</strong>
                  </div>
                  <button
                    type="button"
                    className={styles.primaryButton}
                    onClick={() => openAssignmentModal(session)}
                  >
                    {session.assignedOperatorUserId == null ? 'Assign operator' : 'Change operator'}
                  </button>
                </div>
              </article>
            ))}
          </>
        )

  return (
    <section className={styles.panel} data-testid="session-operator-panel">
      <div className={styles.panelHeader}>
        <div>
          <h2>Assign operators</h2>
          <div className={styles.panelMeta}>
            Assign or change the responsible operator for an existing session.
          </div>
        </div>
        <div className={styles.confirmRow}>
          <button
            className={styles.smallButton}
            type="button"
            onClick={refreshSessions}
            disabled={isPending}
          >
            Refresh
          </button>
          {isPending && <span className={styles.chip}>Loading…</span>}
        </div>
      </div>

      {listError && (
        <div className={styles.errorBanner} role="alert" data-testid="session-operator-list-error">
          {listError}
        </div>
      )}

      <div className={styles.sessionOperatorList} data-testid="session-operator-list">
        {sessionListContent}
      </div>

      {selectedSession && (
        <div
          className={styles.modalScrim}
          role="presentation"
          onClick={handleCancel}
        >
          <div
            className={styles.sessionOperatorModal}
            role="dialog"
            aria-modal="true"
            aria-labelledby="session-operator-modal-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className={styles.panelHeader}>
              <div>
                <h2 id="session-operator-modal-title">Operator assignment</h2>
                <div className={styles.panelMeta}>
                  {selectedSession.title} · {selectedSession.sessionCode}
                </div>
              </div>
              <button
                className={styles.smallButton}
                type="button"
                onClick={handleCancel}
                disabled={isPending}
                aria-label="Close operator assignment dialog"
              >
                Close
              </button>
            </div>

            <div className={styles.sessionOperatorModalDetails}>
              <div className={styles.assignmentCard}>
                <span className={styles.sessionOperatorLabel}>Scheduled</span>
                <strong>{new Date(selectedSession.scheduledAt).toLocaleString()}</strong>
              </div>
              <div className={styles.assignmentCard}>
                <span className={styles.sessionOperatorLabel}>Current operator</span>
                <strong>{currentOperatorLabel(selectedSession)}</strong>
              </div>
            </div>

            <p className={styles.panelMeta}>
              This changes responsibility for an existing session. It does not create the session.
            </p>

            <div className={styles.formGroup}>
              <label htmlFor="operator-select">Operator</label>
              <select
                id="operator-select"
                data-testid="operator-select"
                className={styles.formInput}
                value={pendingOperatorId ?? ''}
                onChange={(e) => setPendingOperatorId(e.target.value ? Number(e.target.value) : null)}
                disabled={isPending}
              >
                <option value="" disabled>— Select an operator —</option>
                {operators.map((op) => (
                  <option key={op.id} value={op.id}>
                    {op.displayName} ({op.email}) — {op.role}
                  </option>
                ))}
              </select>
            </div>

            {assignError && (
              <div className={styles.errorBanner} role="alert" data-testid="assign-operator-error">
                {assignError}
              </div>
            )}

            <div className={styles.sessionOperatorModalActions}>
              <button
                data-testid="assign-operator-btn"
                className={styles.primaryButton}
                type="button"
                disabled={isPending || pendingOperatorId === selectedSession.assignedOperatorUserId}
                onClick={handleAssign}
              >
                Save operator
              </button>
              <button
                className={styles.inlineButton}
                type="button"
                disabled={isPending}
                onClick={handleCancel}
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
