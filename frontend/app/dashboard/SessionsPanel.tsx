'use client'

import { useCallback, useEffect, useState, useTransition } from 'react'
import {
  getSessionAssociatedTeams,
  associateTeamToSession,
  getSessionHistoryAction,
} from '@/app/actions/sessions'
import { getActiveTeams } from '@/app/actions/teams'
import type {
  SessionAssociatedTeamsDto,
  SessionAssignmentSummaryDto,
  SessionHistoryRowDto,
  SessionLifecycleState,
  TeamDto,
} from '@/app/lib/definitions'
import { SessionHistoryPanel } from './SessionHistoryPanel'
import { lifecycleStateLabel } from '@/app/lib/session-lifecycle'
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

function stateLabel(state: string) {
  return lifecycleStateLabel[state as SessionLifecycleState] ?? state
}

const concludedStates = new Set<SessionLifecycleState>(['Finished', 'Cancelled'])

function isConcludedSession(session: SessionAssignmentSummaryDto) {
  return concludedStates.has(session.sessionState as SessionLifecycleState)
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString()
}

// The three outcomes of a history read, carried together with the session they describe so a late
// response for a session the operator has switched away from can be dropped at render.
type ConcludedSessionHistoryState = {
  liveSessionId: string
  events: SessionHistoryRowDto[]
  unauthorized: boolean
  error: string | null
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
  // HU-20 criterion 4: keep the primary list focused on active sessions and
  // surface finished/cancelled ones in a separate, read-only section.
  const activeSessions = assignedSessions.filter((session) => !isConcludedSession(session))
  const concludedSessions = assignedSessions.filter(isConcludedSession)
  const selectedIsConcluded =
    selectedAssignedSession != null && isConcludedSession(selectedAssignedSession)
  const selectedLiveSessionId = selectedAssignedSession?.liveSessionId ?? null
  const [associatedTeams, setAssociatedTeams] = useState<SessionAssociatedTeamsDto | null>(null)
  const [catalogTeams, setCatalogTeams] = useState<TeamDto[]>([])
  const [teamsError, setTeamsError] = useState<string | null>(null)
  const [associateError, setAssociateError] = useState<string | null>(null)
  const [isTeamsPending, startTeamsTransition] = useTransition()
  const [isAssociatePending, startAssociateTransition] = useTransition()
  const [sessionHistory, setSessionHistory] = useState<ConcludedSessionHistoryState | null>(null)
  const [isHistoryPending, startHistoryTransition] = useTransition()

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
        setTeamsError('No se pudieron cargar los equipos de la sesión.')
      }
    })
  }, [selectedLiveSessionId])

  // A concluded session is the only place the audit trail can be read: its live operation view never
  // opens, and QuestionClosed/SessionResultsFinalized events mostly exist on sessions that have finished.
  // The read stays authorized after conclusion — the operator assignment it checks outlives the session.
  const loadSessionHistory = useCallback(async (liveSessionId: string) => {
    startHistoryTransition(async () => {
      const result = await getSessionHistoryAction(liveSessionId)

      setSessionHistory({
        liveSessionId,
        events: 'data' in result ? result.data.events : [],
        unauthorized: 'unauthorized' in result,
        // A transient read failure is not an authorization problem — keep them distinguishable.
        error: 'error' in result ? result.error : null,
      })
    })
  }, [])

  useEffect(() => {
    if (selectedLiveSessionId == null || !selectedIsConcluded) {
      return
    }

    void loadSessionHistory(selectedLiveSessionId)
  }, [selectedLiveSessionId, selectedIsConcluded, loadSessionHistory])

  const visibleAssociatedTeams =
    selectedLiveSessionId != null && associatedTeams?.liveSessionId === selectedLiveSessionId
      ? associatedTeams
      : null
  const visibleSessionHistory =
    selectedLiveSessionId != null && sessionHistory?.liveSessionId === selectedLiveSessionId
      ? sessionHistory
      : null
  // Runtime teamId → display name, from the teams this session already loads. Every event a concluded
  // session carries today is session-wide (teamId null), so nothing consumes this yet — it is here so
  // team-scoped event types resolve to a name rather than "Unknown team" once the consumer records them.
  const historyTeamNames = Object.fromEntries(
    visibleAssociatedTeams?.teams.map((team) => [team.runtimeTeamId, team.displayName]) ?? [],
  )
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
            setAssociateError('Este equipo está inactivo y no puede asociarse.')
            break
          case 'duplicate_association':
            setAssociateError('Este equipo ya está asociado a la sesión.')
            break
          case 'session_not_scheduled':
            setAssociateError('Los equipos solo pueden asociarse mientras la sesión está programada.')
            break
          case 'not_found':
          case 'session_not_found':
            setAssociateError('La sesión o el equipo seleccionado ya no existe.')
            break
          default:
            setAssociateError('No se pudo asociar el equipo.')
            break
        }
      }
    })
  }

  return (
    <section className={`${styles.panel} ${styles.sessionsPanel}`} data-testid="sessions-panel">
      <div className={styles.panelHeader}>
        <div>
          <h2>Mis sesiones</h2>
          <div className={styles.panelMeta}>
            Opera las sesiones que un administrador te ha asignado. Selecciona una para revisarla y luego
            pasa a la operación en vivo.
          </div>
        </div>
      </div>

      <div className={styles.sessionWorkspaceGrid}>
        <section className={styles.assignmentCard} aria-labelledby="assigned-sessions-heading">
          <div className={styles.panelHeader}>
            <div>
              <h3 id="assigned-sessions-heading">Sesiones de las que eres responsable</h3>
              <div className={styles.panelMeta}>
                Elige una sesión para revisarla o abrir sus controles en vivo.
              </div>
            </div>
          </div>

          {assignedSessionsError && (
            <p className={styles.errorBanner} role="alert" data-testid="assigned-sessions-error">
              {assignedSessionsError}
            </p>
          )}

          {isLoadingAssignedSessions ? (
            <p className={styles.emptyStateCopy}>Cargando sesiones asignadas...</p>
          ) : activeSessions.length === 0 ? (
            <p className={styles.emptyStateCopy} data-testid="session-empty-state">
              {concludedSessions.length === 0
                ? 'Aún no tienes sesiones asignadas. Pide a un administrador que te asigne una.'
                : 'No tienes sesiones activas ahora mismo. Las sesiones anteriores están disponibles más abajo.'}
            </p>
          ) : (
            <div className={styles.sessionCards} data-testid="assigned-sessions-list">
              {activeSessions.map((session) => (
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
                        {session.sessionCode} • Programada {formatDateTime(session.scheduledAt)}
                      </div>
                    </div>
                    <span
                      className={styles.chip}
                      data-tone={getLifecycleTone(session.sessionState)}
                    >
                      {stateLabel(session.sessionState)}
                    </span>
                  </div>
                </button>
              ))}
            </div>
          )}

          {concludedSessions.length > 0 && (
            <details className={styles.concludedSessions} data-testid="concluded-sessions-section">
              <summary className={styles.concludedSessionsSummary}>
                Sesiones anteriores ({concludedSessions.length})
              </summary>
              <div className={styles.panelMeta}>
                Las sesiones finalizadas y canceladas son de solo lectura. Selecciona una para revisarla.
              </div>
              <div className={styles.sessionCards} data-testid="concluded-sessions-list">
                {concludedSessions.map((session) => (
                  <button
                    key={session.liveSessionId}
                    type="button"
                    className={styles.sessionButton}
                    data-current={selectedAssignedSession?.liveSessionId === session.liveSessionId}
                    onClick={() => onSelectSession(session.liveSessionId)}
                    data-testid="concluded-session-button"
                  >
                    <div className={styles.sessionCardHeader}>
                      <div>
                        <h3>{session.title}</h3>
                        <div className={styles.sessionCardMeta}>
                          {session.sessionCode} • Programada {formatDateTime(session.scheduledAt)}
                        </div>
                      </div>
                      <span
                        className={styles.chip}
                        data-tone={getLifecycleTone(session.sessionState)}
                      >
                        {stateLabel(session.sessionState)}
                      </span>
                    </div>
                  </button>
                ))}
              </div>
            </details>
          )}
        </section>

        <section className={styles.assignmentCard} aria-labelledby="session-setup-heading">
          <div className={styles.panelHeader}>
            <div>
              <h3 id="session-setup-heading">Configuración de la sesión</h3>
              <div className={styles.panelMeta}>
                {selectedAssignedSession
                  ? 'Revisa la sesión actual antes de pasar a la operación en vivo.'
                  : 'Selecciona una de tus sesiones para continuar con la configuración y la operación.'}
              </div>
            </div>
          </div>

          {selectedAssignedSession ? (
            <div className={styles.sessionsPanelStack}>
              <section className={styles.sessionCard} data-testid="selected-session-setup-card">
                <h3>{selectedAssignedSession.title}</h3>
                <dl className={styles.detailList}>
                  <dt>Código de sesión</dt>
                  <dd>{selectedAssignedSession.sessionCode}</dd>

                  <dt>Programada para</dt>
                  <dd>{formatDateTime(selectedAssignedSession.scheduledAt)}</dd>

                  <dt>Estado</dt>
                  <dd>{stateLabel(selectedAssignedSession.sessionState)}</dd>

                  <dt>Titularidad</dt>
                  <dd>{selectedAssignedSession.assignedOperatorUserId == null ? 'Sin asignar' : 'Asignada a ti'}</dd>
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
                    <h3>Equipos asociados</h3>
                    <div className={styles.panelMeta}>
                      Agrega equipos activos antes de pasar la sesión a la operación en vivo.
                    </div>
                  </div>
                  {isTeamsPending && <span className={styles.chip}>Cargando...</span>}
                </div>

                {visibleAssociatedTeams == null || visibleAssociatedTeams.teams.length === 0 ? (
                  <p className={styles.emptyStateCopy} data-testid="session-associated-teams-empty">
                    Aún no hay equipos asociados.
                  </p>
                ) : (
                  <div className={styles.sessionCards} data-testid="session-associated-teams-list">
                    {visibleAssociatedTeams.teams.map((team) => (
                      <section key={team.referenceTeamId} className={styles.sessionCard}>
                        <div className={styles.sessionCardHeader}>
                          <div>
                            <h3>{team.displayName}</h3>
                            <div className={styles.sessionCardMeta}>
                              {team.teamCode} • Estado de unión {team.joinStatus}
                            </div>
                          </div>
                          <span className={styles.chip}>{team.teamCode}</span>
                        </div>
                      </section>
                    ))}
                  </div>
                )}
              </section>

              {!selectedIsConcluded && (
              <section className={styles.assignmentCard} data-testid="session-team-catalog-card">
                <div className={styles.panelHeader}>
                  <div>
                    <h3>Equipos activos disponibles</h3>
                    <div className={styles.panelMeta}>
                      Aquí solo se muestran los equipos activos que aún no están asociados.
                    </div>
                  </div>
                </div>

                {availableTeams.length === 0 ? (
                  <p className={styles.emptyStateCopy} data-testid="session-team-catalog-empty">
                    No hay equipos activos adicionales disponibles para asociar.
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
                            Asociar
                          </button>
                        </div>
                      </section>
                    ))}
                  </div>
                )}
              </section>
              )}

              {selectedIsConcluded ? (
                <>
                  <p className={styles.emptyStateCopy} data-testid="concluded-session-readonly-note">
                    Esta sesión está {stateLabel(selectedAssignedSession.sessionState).toLowerCase()} y solo puede
                    revisarse en modo de solo lectura.
                  </p>
                  <SessionHistoryPanel
                    events={visibleSessionHistory?.events ?? []}
                    teamNames={historyTeamNames}
                    unauthorized={visibleSessionHistory?.unauthorized ?? false}
                    error={visibleSessionHistory?.error ?? null}
                    loading={isHistoryPending || visibleSessionHistory === null}
                    onRetry={() => void loadSessionHistory(selectedAssignedSession.liveSessionId)}
                  />
                </>
              ) : (
                <button
                  type="button"
                  className={styles.primaryButton}
                  onClick={() => onOpenLiveOperation(selectedAssignedSession.liveSessionId)}
                >
                  Abrir operación en vivo
                </button>
              )}
            </div>
          ) : (
            <p className={styles.emptyStateCopy}>Aún no hay ninguna sesión seleccionada.</p>
          )}
        </section>
      </div>
    </section>
  )
}
