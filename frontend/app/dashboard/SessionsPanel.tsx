'use client'

import type {
  SessionAssignmentSummaryDto,
  SessionLifecycleState,
} from '@/app/lib/definitions'
import styles from './dashboard.module.css'

interface SessionsPanelProps {
  assignedSessions: SessionAssignmentSummaryDto[]
  isLoadingAssignedSessions: boolean
  assignedSessionsError: string | null
  selectedSessionId: string | null
  onSelectSession: (sessionId: string) => void
  onOpenLiveOperation: (sessionId: string) => void
}

const lifecycleTones: Record<SessionLifecycleState, 'success' | 'warning' | 'critical' | 'muted'> = {
  Scheduled: 'muted',
  Preparing: 'warning',
  Active: 'success',
  Paused: 'warning',
  Finished: 'muted',
  Cancelled: 'critical',
}

function getLifecycleTone(state: string) {
  return lifecycleTones[state as SessionLifecycleState] ?? 'muted'
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString()
}

export function SessionsPanel({
  assignedSessions,
  isLoadingAssignedSessions,
  assignedSessionsError,
  selectedSessionId,
  onSelectSession,
  onOpenLiveOperation,
}: SessionsPanelProps) {
  const selectedAssignedSession =
    selectedSessionId == null
      ? null
      : assignedSessions.find((session) => session.liveSessionId === selectedSessionId) ?? null

  return (
    <section className={`${styles.panel} ${styles.sessionsPanel}`} data-testid="sessions-panel">
      <div className={styles.panelHeader}>
        <div>
          <h2>My sessions</h2>
          <div className={styles.panelMeta}>
            Operate the sessions an administrator has assigned to you. Select one to review it, then
            move into live operation.
          </div>
        </div>
      </div>

      <div className={styles.sessionWorkspaceGrid}>
        <section className={styles.assignmentCard} aria-labelledby="assigned-sessions-heading">
          <div className={styles.panelHeader}>
            <div>
              <h3 id="assigned-sessions-heading">Sessions you&apos;re responsible for</h3>
              <div className={styles.panelMeta}>
                Pick a session to review it or open its live controls.
              </div>
            </div>
          </div>

          {assignedSessionsError && (
            <p className={styles.errorBanner} role="alert" data-testid="assigned-sessions-error">
              {assignedSessionsError}
            </p>
          )}

          {isLoadingAssignedSessions ? (
            <p className={styles.emptyStateCopy}>Loading assigned sessions...</p>
          ) : assignedSessions.length === 0 ? (
            <p className={styles.emptyStateCopy} data-testid="session-empty-state">
              You have no assigned sessions yet. Ask an administrator to assign one to you.
            </p>
          ) : (
            <div className={styles.sessionCards} data-testid="assigned-sessions-list">
              {assignedSessions.map((session) => (
                <button
                  key={session.liveSessionId}
                  type="button"
                  className={styles.sessionButton}
                  data-current={selectedAssignedSession?.liveSessionId === session.liveSessionId}
                  onClick={() => onSelectSession(session.liveSessionId)}
                  data-testid="assigned-session-button"
                >
                  <div className={styles.sessionCardHeader}>
                    <div>
                      <h3>{session.title}</h3>
                      <div className={styles.sessionCardMeta}>
                        {session.sessionCode} • Scheduled {formatDateTime(session.scheduledAt)}
                      </div>
                    </div>
                    <span
                      className={styles.chip}
                      data-tone={getLifecycleTone(session.sessionState)}
                    >
                      {session.sessionState}
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
                  <dt>Session code</dt>
                  <dd>{selectedAssignedSession.sessionCode}</dd>

                  <dt>Scheduled at</dt>
                  <dd>{formatDateTime(selectedAssignedSession.scheduledAt)}</dd>

                  <dt>Status</dt>
                  <dd>{selectedAssignedSession.sessionState}</dd>

                  <dt>Ownership</dt>
                  <dd>{selectedAssignedSession.assignedOperatorUserId == null ? 'Unassigned' : 'Assigned to you'}</dd>
                </dl>
              </section>

              <button
                type="button"
                className={styles.primaryButton}
                onClick={() => onOpenLiveOperation(selectedAssignedSession.liveSessionId)}
              >
                Open live operation
              </button>
            </div>
          ) : (
            <p className={styles.emptyStateCopy}>No session selected yet.</p>
          )}
        </section>
      </div>
    </section>
  )
}
