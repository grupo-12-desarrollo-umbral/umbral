'use client'

import { useEffect, useState, useTransition } from 'react'
import { getSessionAssociatedTeams, associateTeamToSession } from '@/app/actions/sessions'
import { getActiveTeams } from '@/app/actions/teams'
import type {
  SessionAssociatedTeamsDto,
  SessionAssignmentSummaryDto,
  SessionLifecycleState,
  TeamDto,
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
  const selectedLiveSessionId = selectedAssignedSession?.liveSessionId ?? null
  const [associatedTeams, setAssociatedTeams] = useState<SessionAssociatedTeamsDto | null>(null)
  const [catalogTeams, setCatalogTeams] = useState<TeamDto[]>([])
  const [teamsError, setTeamsError] = useState<string | null>(null)
  const [associateError, setAssociateError] = useState<string | null>(null)
  const [isTeamsPending, startTeamsTransition] = useTransition()
  const [isAssociatePending, startAssociateTransition] = useTransition()

  useEffect(() => {
    if (selectedLiveSessionId == null) {
      return
    }

    startTeamsTransition(async () => {
      setTeamsError(null)
      setAssociateError(null)
      try {
        const [sessionTeams, activeTeams] = await Promise.all([
          getSessionAssociatedTeams(selectedLiveSessionId),
          getActiveTeams(),
        ])

        setAssociatedTeams(sessionTeams)
        setCatalogTeams(activeTeams)
      } catch {
        setTeamsError('Failed to load session teams.')
      }
    })
  }, [selectedLiveSessionId])

  const visibleAssociatedTeams =
    selectedLiveSessionId != null && associatedTeams?.liveSessionId === selectedLiveSessionId
      ? associatedTeams
      : null
  const associatedReferenceTeamIds = new Set(
    visibleAssociatedTeams?.teams.map((team) => team.referenceTeamId) ?? [],
  )
  const availableTeams = catalogTeams.filter(
    (team) => !associatedReferenceTeamIds.has(team.teamId),
  )

  async function handleAssociate(referenceTeamId: string) {
    if (selectedAssignedSession == null) return

    startAssociateTransition(async () => {
      setAssociateError(null)
      try {
        const result = await associateTeamToSession(
          selectedAssignedSession.liveSessionId,
          referenceTeamId,
        )

        setAssociatedTeams((current) => ({
          liveSessionId: selectedAssignedSession.liveSessionId,
          teams: [
            ...(current?.teams ?? []),
            {
              runtimeTeamId: result.runtimeTeamId,
              referenceTeamId: result.referenceTeamId,
              displayName: result.displayName,
              teamCode: result.teamCode,
              joinStatus: 'Open',
            },
          ],
        }))
      } catch (error) {
        const message = error instanceof Error ? error.message : ''
        switch (message) {
          case 'inactive_team':
            setAssociateError('This team is inactive and cannot be associated.')
            break
          case 'duplicate_association':
            setAssociateError('This team is already associated to the session.')
            break
          case 'session_not_scheduled':
            setAssociateError('Teams can only be associated while the session is scheduled.')
            break
          case 'not_found':
          case 'session_not_found':
            setAssociateError('The session or selected team no longer exists.')
            break
          default:
            setAssociateError('Failed to associate the team.')
            break
        }
      }
    })
  }

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

              {teamsError && (
                <p className={styles.errorBanner} role="alert" data-testid="session-teams-error">
                  {teamsError}
                </p>
              )}

              {associateError && (
                <p className={styles.errorBanner} role="alert" data-testid="session-associate-error">
                  {associateError}
                </p>
              )}

              <section className={styles.assignmentCard} data-testid="session-associated-teams-card">
                <div className={styles.panelHeader}>
                  <div>
                    <h3>Associated teams</h3>
                    <div className={styles.panelMeta}>
                      Add active teams before moving the session into live operation.
                    </div>
                  </div>
                  {isTeamsPending && <span className={styles.chip}>Loading...</span>}
                </div>

                {visibleAssociatedTeams == null || visibleAssociatedTeams.teams.length === 0 ? (
                  <p className={styles.emptyStateCopy} data-testid="session-associated-teams-empty">
                    No teams associated yet.
                  </p>
                ) : (
                  <div className={styles.sessionCards} data-testid="session-associated-teams-list">
                    {visibleAssociatedTeams.teams.map((team) => (
                      <section key={team.referenceTeamId} className={styles.sessionCard}>
                        <div className={styles.sessionCardHeader}>
                          <div>
                            <h3>{team.displayName}</h3>
                            <div className={styles.sessionCardMeta}>
                              {team.teamCode} • Join status {team.joinStatus}
                            </div>
                          </div>
                          <span className={styles.chip}>{team.teamCode}</span>
                        </div>
                      </section>
                    ))}
                  </div>
                )}
              </section>

              <section className={styles.assignmentCard} data-testid="session-team-catalog-card">
                <div className={styles.panelHeader}>
                  <div>
                    <h3>Available active teams</h3>
                    <div className={styles.panelMeta}>
                      Only active teams not already associated are shown here.
                    </div>
                  </div>
                </div>

                {availableTeams.length === 0 ? (
                  <p className={styles.emptyStateCopy} data-testid="session-team-catalog-empty">
                    No additional active teams available to associate.
                  </p>
                ) : (
                  <div className={styles.sessionCards} data-testid="session-team-catalog-list">
                    {availableTeams.map((team) => (
                      <section key={team.teamId} className={styles.sessionCard}>
                        <div className={styles.sessionCardHeader}>
                          <div>
                            <h3>{team.displayName}</h3>
                            <div className={styles.sessionCardMeta}>{team.teamCode}</div>
                          </div>
                          <button
                            type="button"
                            className={styles.smallButton}
                            onClick={() => handleAssociate(team.teamId)}
                            disabled={
                              isAssociatePending ||
                              selectedAssignedSession.sessionState !== 'Scheduled'
                            }
                            data-testid={`associate-team-btn-${team.teamId}`}
                          >
                            Associate
                          </button>
                        </div>
                      </section>
                    ))}
                  </div>
                )}
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
