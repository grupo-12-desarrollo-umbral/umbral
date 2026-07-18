'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getTeamsPage,
  getTeam,
  createTeam,
  updateTeam,
  deactivateTeam,
  getTeamParticipants,
  assignParticipantToTeam,
} from '@/app/actions/teams'
import { listSessionsForOperator, associateTeamToSession } from '@/app/actions/sessions'
import { getAssignableParticipants } from '@/app/actions/users'
import { lifecycleStateLabel, toLifecycleState } from '@/app/lib/session-lifecycle'
import type {
  AssignableParticipantDto,
  PagedResult,
  TeamDto,
  TeamMembershipDto,
  SessionAssignmentSummaryDto,
} from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type TeamPanelView = 'list' | 'detail' | 'create' | 'edit'

export function TeamsPanel({ role }: { role: DashboardRole }) {
  const canManageTeams = role === 'admin' || role === 'operator'
  const [view, setView] = useState<TeamPanelView>('list')
  const [selectedTeam, setSelectedTeam] = useState<TeamDto | null>(null)
  const [listData, setListData] = useState<PagedResult<TeamDto> | null>(null)
  const [page, setPage] = useState(1)
  const [isPending, startTransition] = useTransition()
  const [listError, setListError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [deactivateError, setDeactivateError] = useState<string | null>(null)
  const [confirmDeactivate, setConfirmDeactivate] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)
  const [participants, setParticipants] = useState<TeamMembershipDto[]>([])
  const [participantsError, setParticipantsError] = useState<string | null>(null)
  const [isAssignPending, startAssignTransition] = useTransition()
  const [showAssignForm, setShowAssignForm] = useState(false)
  const [participantUsers, setParticipantUsers] = useState<AssignableParticipantDto[]>([])
  const [selectedUserId, setSelectedUserId] = useState<number>(0)
  const [assignError, setAssignError] = useState<string | null>(null)
  const [sessionAssignmentTeam, setSessionAssignmentTeam] = useState<TeamDto | null>(null)
  const [operatorSessions, setOperatorSessions] = useState<SessionAssignmentSummaryDto[]>([])
  const [sessionsError, setSessionsError] = useState<string | null>(null)
  const [sessionAssignError, setSessionAssignError] = useState<string | null>(null)
  const [isSessionModalPending, startSessionModalTransition] = useTransition()
  const scheduledOperatorSessions = operatorSessions.filter(
    (session) => session.sessionState === 'Scheduled',
  )

  useEffect(() => {
    startTransition(async () => {
      setListError(null)
      try {
        const result = await getTeamsPage(page)
        setListData(result)
      } catch {
        setListError('No se pudieron cargar los equipos.')
      }
    })
  }, [page, refreshKey])

  useEffect(() => {
    if (view !== 'detail' || selectedTeam == null) return
    startAssignTransition(async () => {
      setParticipantsError(null)
      try {
        const result = await getTeamParticipants(selectedTeam.teamId)
        setParticipants(result)
      } catch {
        setParticipantsError('No se pudieron cargar los participantes.')
      }
    })
  }, [view, selectedTeam])

  async function handleDeactivate(id: string) {
    startTransition(async () => {
      setDeactivateError(null)
      try {
        const updated = await deactivateTeam(id)
        setSelectedTeam(updated)
        setConfirmDeactivate(false)
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'already_inactive') {
          setDeactivateError('Este equipo ya está inactivo.')
        } else {
          setDeactivateError('Falló la desactivación. Inténtalo de nuevo.')
        }
        setConfirmDeactivate(false)
      }
    })
  }

  async function handleCreate(displayName: string, teamCode: string) {
    startTransition(async () => {
      setFormError(null)
      try {
        const result = await createTeam(displayName, teamCode)
        const newTeam = await getTeam(result.teamId)
        setSelectedTeam(newTeam)
        setConfirmDeactivate(false)
        setDeactivateError(null)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'duplicate_team_code') {
          setFormError('Ya existe un equipo con este código.')
        } else {
          setFormError('No se pudo crear el equipo. Inténtalo de nuevo.')
        }
      }
    })
  }

  async function handleUpdate(displayName: string, teamCode: string) {
    if (!selectedTeam) return
    startTransition(async () => {
      setFormError(null)
      try {
        await updateTeam(selectedTeam.teamId, displayName, teamCode)
        const refreshed = await getTeam(selectedTeam.teamId)
        setSelectedTeam(refreshed)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'duplicate_team_code') {
          setFormError('Ya existe un equipo con este código.')
        } else if (msg === 'team_not_found') {
          setFormError('Este equipo ya no existe.')
        } else {
          setFormError('No se pudieron guardar los cambios. Inténtalo de nuevo.')
        }
      }
    })
  }

  function loadParticipantUsers() {
    startAssignTransition(async () => {
      try {
        setParticipantUsers(await getAssignableParticipants())
      } catch {
        // Selector will be empty; user can still try to submit if they know the id
      }
    })
  }

  async function handleAssign() {
    if (!selectedTeam || !selectedUserId) return
    startAssignTransition(async () => {
      setAssignError(null)
      try {
        await assignParticipantToTeam(selectedTeam.teamId, selectedUserId)
        // Optimistic update — append synthetic membership entry
        const assignedUser = participantUsers.find((u) => u.id === selectedUserId)
        setParticipants((prev) => [
          ...prev,
          {
            teamMembershipId: 'optimistic-' + Date.now(),
            teamId: selectedTeam.teamId,
            userId: selectedUserId,
            email: assignedUser?.email ?? '',
            displayName: assignedUser?.displayName ?? '',
          },
        ])
        setShowAssignForm(false)
        setSelectedUserId(0)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'team_not_active') {
          setAssignError('Este equipo está inactivo y no puede aceptar nuevos miembros.')
        } else if (msg === 'participant_already_assigned') {
          setAssignError('Este usuario ya está asignado al equipo.')
        } else if (msg === 'user_not_participant_role') {
          setAssignError('El usuario seleccionado no tiene el rol de Participante.')
        } else {
          setAssignError('Falló la asignación. Inténtalo de nuevo.')
        }
      }
    })
  }

  function closeSessionAssignmentModal() {
    setSessionAssignmentTeam(null)
    setOperatorSessions([])
    setSessionsError(null)
    setSessionAssignError(null)
  }

  function openSessionAssignmentModal(team: TeamDto) {
    setSessionAssignmentTeam(team)
    setOperatorSessions([])
    setSessionsError(null)
    setSessionAssignError(null)

    startSessionModalTransition(async () => {
      try {
        const sessions = await listSessionsForOperator()
        setOperatorSessions(sessions)
      } catch {
        setOperatorSessions([])
        setSessionsError('No se pudieron cargar tus sesiones asignadas.')
      }
    })
  }

  function handleAssociateTeamToSession(liveSessionId: string) {
    if (!sessionAssignmentTeam) return

    startSessionModalTransition(async () => {
      setSessionAssignError(null)
      try {
        await associateTeamToSession(liveSessionId, sessionAssignmentTeam.teamId)
        closeSessionAssignmentModal()
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'duplicate_association') {
          setSessionAssignError('Este equipo ya está asociado a esa sesión.')
        } else if (msg === 'inactive_team') {
          setSessionAssignError('Los equipos inactivos no pueden asignarse a una sesión.')
        } else if (msg === 'session_not_scheduled') {
          setSessionAssignError('Solo las sesiones programadas pueden aceptar nuevos equipos.')
        } else if (msg === 'not_found') {
          setSessionAssignError('El equipo o la sesión ya no existe.')
        } else {
          setSessionAssignError('No se pudo asignar el equipo a la sesión seleccionada.')
        }
      }
    })
  }

  if (view === 'detail' && selectedTeam !== null) {
    return (
      <section
        className={styles.panel}
        aria-labelledby="team-detail-title"
        data-testid="team-detail-panel"
      >
        <div className={styles.panelHeader}>
          <div>
            <button
              className={styles.inlineButton}
              data-testid="teams-back-btn"
              onClick={() => {
                setView('list')
                setConfirmDeactivate(false)
                setDeactivateError(null)
                setParticipants([])
                setParticipantsError(null)
                setShowAssignForm(false)
                setAssignError(null)
                setSelectedUserId(0)
              }}
              type="button"
            >
              ← Equipos
            </button>
            <h2 id="team-detail-title">{selectedTeam.displayName}</h2>
            <div className={styles.panelMeta}>{selectedTeam.teamCode}</div>
          </div>

          {canManageTeams && selectedTeam.isActive && (
            <div className={styles.panelActions}>
              {!confirmDeactivate && (
                <button
                  className={styles.inlineButton}
                  data-testid="edit-team-btn"
                  disabled={isPending}
                  onClick={() => {
                    setFormError(null)
                    setParticipants([])
                    setParticipantsError(null)
                    setShowAssignForm(false)
                    setAssignError(null)
                    setSelectedUserId(0)
                    setView('edit')
                  }}
                  type="button"
                >
                  Editar
                </button>
              )}
              {!confirmDeactivate ? (
                <button
                  className={styles.inlineButton}
                  data-testid="deactivate-team-btn"
                  disabled={isPending}
                  onClick={() => setConfirmDeactivate(true)}
                  type="button"
                >
                  Desactivar
                </button>
              ) : (
                <span className={styles.confirmRow}>
                  <button
                    className={styles.smallButton}
                    data-testid="confirm-deactivate-team-btn"
                    data-tone="critical"
                    disabled={isPending}
                    onClick={() => handleDeactivate(selectedTeam.teamId)}
                    type="button"
                  >
                    Confirmar
                  </button>
                  <button
                    className={styles.inlineButton}
                    disabled={isPending}
                    onClick={() => setConfirmDeactivate(false)}
                    type="button"
                  >
                    Cancelar
                  </button>
                </span>
              )}
            </div>
          )}
        </div>

        {deactivateError && (
          <div className={styles.chip} data-tone="critical">
            {deactivateError}
          </div>
        )}

        <dl className={styles.detailList} data-testid="team-detail-fields">
          <dt>Nombre visible</dt>
          <dd data-testid="detail-display-name">{selectedTeam.displayName}</dd>
          <dt>Código de equipo</dt>
          <dd data-testid="detail-team-code">{selectedTeam.teamCode}</dd>
          <dt>Estado</dt>
          <dd>
            <span
              className={styles.chip}
              data-tone={selectedTeam.isActive ? 'success' : 'critical'}
              data-testid="detail-status"
            >
              {selectedTeam.isActive ? 'Activo' : 'Inactivo'}
            </span>
          </dd>
          <dt>Creado</dt>
          <dd>{new Date(selectedTeam.createdAt).toLocaleDateString()}</dd>
          <dt>Última actualización</dt>
          <dd>{new Date(selectedTeam.updatedAt).toLocaleDateString()}</dd>
        </dl>

        <section
          aria-labelledby="participants-section-title"
          data-testid="participants-section"
        >
          <div className={styles.subsectionHeader}>
            <h3 id="participants-section-title">Participantes</h3>
            {canManageTeams && selectedTeam.isActive && (
              <button
                className={styles.inlineButton}
                data-testid="assign-participant-btn"
                disabled={isAssignPending || showAssignForm}
                onClick={() => {
                  setAssignError(null)
                  setShowAssignForm(true)
                  loadParticipantUsers()
                }}
                type="button"
              >
                + Asignar participante
              </button>
            )}
          </div>

          {showAssignForm && canManageTeams && (
            <div className={styles.formGroup} data-testid="assign-form">
              <label htmlFor="participant-select">Seleccionar participante</label>
              <select
                id="participant-select"
                className={styles.inlineSelect}
                data-testid="participant-select"
                value={selectedUserId}
                onChange={(e) => setSelectedUserId(Number(e.target.value))}
                disabled={isAssignPending}
              >
                <option value={0}>— Selecciona un participante —</option>
                {participantUsers.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.displayName} ({u.email})
                  </option>
                ))}
              </select>

              {assignError && (
                <span className={styles.fieldError} data-testid="assign-error">
                  {assignError}
                </span>
              )}

              <div className={styles.panelActions}>
                <button
                  className={styles.primaryButton}
                  data-testid="confirm-assign-btn"
                  disabled={!selectedUserId || isAssignPending}
                  onClick={handleAssign}
                  type="button"
                >
                  Asignar
                </button>
                <button
                  className={styles.inlineButton}
                  disabled={isAssignPending}
                  onClick={() => {
                    setShowAssignForm(false)
                    setAssignError(null)
                    setSelectedUserId(0)
                  }}
                  type="button"
                >
                  Cancelar
                </button>
              </div>
            </div>
          )}

          {participantsError && (
            <div className={styles.chip} data-tone="critical">
              {participantsError}
            </div>
          )}

          {isAssignPending && !participants.length && (
            <span className={styles.chip}>Cargando…</span>
          )}

          {!participantsError && !isAssignPending && participants.length === 0 && (
            <p className={styles.panelMeta} data-testid="no-participants-message">
              Aún no hay participantes asignados.
            </p>
          )}

          {participants.length > 0 && (
            <table className={styles.table} data-testid="participants-table">
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Correo</th>
                </tr>
              </thead>
              <tbody>
                {participants.map((m) => (
                  <tr key={m.teamMembershipId} data-testid={`participant-row-${m.teamMembershipId}`}>
                    <td data-label="User" data-testid={`participant-user-${m.userId}`}>{m.displayName}</td>
                    <td data-label="Email" data-testid={`participant-email-${m.userId}`}>{m.email}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </section>
    )
  }

  if (view === 'create' && canManageTeams) {
    return (
      <section
        className={styles.panel}
        aria-labelledby="create-team-title"
        data-testid="create-team-panel"
      >
        <div className={styles.panelHeader}>
          <div>
            <button
              className={styles.inlineButton}
              onClick={() => { setView('list'); setFormError(null) }}
              type="button"
            >
              ← Equipos
            </button>
            <h2 id="create-team-title">Nuevo equipo</h2>
          </div>
        </div>

        <TeamForm
          mode="create"
          isPending={isPending}
          formError={formError}
          onCancel={() => { setView('list'); setFormError(null) }}
          onSubmit={handleCreate}
        />
      </section>
    )
  }

  if (view === 'edit' && selectedTeam !== null && canManageTeams) {
    return (
      <section
        className={styles.panel}
        aria-labelledby="edit-team-title"
        data-testid="edit-team-panel"
      >
        <div className={styles.panelHeader}>
          <div>
            <button
              className={styles.inlineButton}
              onClick={() => { setView('detail'); setFormError(null) }}
              type="button"
            >
              ← {selectedTeam.displayName}
            </button>
            <h2 id="edit-team-title">Editar equipo</h2>
          </div>
        </div>

        <TeamForm
          mode="edit"
          initialValues={{
            displayName: selectedTeam.displayName,
            teamCode: selectedTeam.teamCode,
          }}
          isPending={isPending}
          formError={formError}
          onCancel={() => { setView('detail'); setFormError(null) }}
          onSubmit={handleUpdate}
        />
      </section>
    )
  }

  // Default list view
  return (
    <section
      className={styles.panel}
      aria-labelledby="teams-panel-title"
      data-testid="teams-panel"
    >
      <div className={styles.panelHeader}>
        <div>
          <h2 id="teams-panel-title">Equipos registrados</h2>
          <div className={styles.panelMeta}>
            {canManageTeams
              ? 'Registro de equipos. Los equipos inactivos se conservan para auditoría.'
              : 'Catálogo de equipos de solo lectura.'}
          </div>
        </div>
        <div className={styles.panelActions}>
          {isPending && <span className={styles.chip}>Loading…</span>}
          {canManageTeams && (
            <button
              className={styles.primaryButton}
              data-testid="create-team-btn"
              disabled={isPending}
              onClick={() => { setFormError(null); setView('create') }}
              type="button"
            >
              + Nuevo equipo
            </button>
          )}
        </div>
      </div>

      {listError && (
        <div className={styles.chip} data-tone="critical">
          {listError}
        </div>
      )}

      {listData && (
        <>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>Nombre</th>
                <th>Código</th>
                <th>Estado</th>
                <th>Creado</th>
                {role === 'operator' && <th>Acciones</th>}
              </tr>
            </thead>
            <tbody>
              {listData.items.map((team) => (
                <tr
                  key={team.teamId}
                  data-testid={`team-row-${team.teamId}`}
                  onClick={() => {
                    setSelectedTeam(team)
                    setConfirmDeactivate(false)
                    setDeactivateError(null)
                    setView('detail')
                  }}
                  style={{ cursor: 'pointer' }}
                >
                  <td data-label="Name">{team.displayName}</td>
                  <td data-label="Code">{team.teamCode}</td>
                  <td data-label="Status">
                    <span
                      className={styles.chip}
                      data-tone={team.isActive ? 'success' : 'critical'}
                    >
                      {team.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td data-label="Created">{new Date(team.createdAt).toLocaleDateString()}</td>
                  {role === 'operator' && (
                    <td data-label="Actions">
                      <div className={styles.teamRowActions}>
                        <button
                          type="button"
                          className={styles.inlineButton}
                          data-testid={`team-row-session-actions-${team.teamId}`}
                          disabled={!team.isActive || isSessionModalPending}
                          onClick={(event) => {
                            event.stopPropagation()
                            openSessionAssignmentModal(team)
                          }}
                          aria-label={`Asignar ${team.displayName} a una de tus sesiones`}
                        >
                          ...
                        </button>
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>

          <div className={styles.pagination} data-testid="teams-pagination">
            <span className={styles.panelMeta}>
              Página {listData.page} de {listData.totalPages} ({listData.totalCount} equipos)
            </span>
            <span className={styles.paginationButtons}>
              <button
                className={styles.inlineButton}
                disabled={!listData.hasPreviousPage || isPending}
                onClick={() => setPage((p) => p - 1)}
                type="button"
              >
                ← Anterior
              </button>
              <button
                className={styles.inlineButton}
                disabled={!listData.hasNextPage || isPending}
                onClick={() => setPage((p) => p + 1)}
                type="button"
              >
                Siguiente →
              </button>
            </span>
          </div>
        </>
      )}

      {sessionAssignmentTeam && (
        <div
          className={styles.modalScrim}
          role="presentation"
          onClick={closeSessionAssignmentModal}
        >
          <div
            className={styles.sessionOperatorModal}
            role="dialog"
            aria-modal="true"
            aria-labelledby="team-session-assignment-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className={styles.panelHeader}>
              <div>
                <h2 id="team-session-assignment-title">Asignar equipo a sesión</h2>
                <div className={styles.panelMeta}>
                  {sessionAssignmentTeam.displayName} · {sessionAssignmentTeam.teamCode}
                </div>
              </div>
              <button
                className={styles.smallButton}
                type="button"
                onClick={closeSessionAssignmentModal}
                disabled={isSessionModalPending}
                aria-label="Cerrar el diálogo de asignación de equipo a sesión"
              >
                Cerrar
              </button>
            </div>

            <div className={styles.sessionOperatorModalDetails}>
              <div className={styles.assignmentCard}>
                <span className={styles.sessionOperatorLabel}>Estado del equipo</span>
                <strong>{sessionAssignmentTeam.isActive ? 'Activo' : 'Inactivo'}</strong>
              </div>
              <div className={styles.assignmentCard}>
                <span className={styles.sessionOperatorLabel}>Sesiones disponibles</span>
                <strong>{scheduledOperatorSessions.length}</strong>
              </div>
            </div>

            <p className={styles.panelMeta}>
              Elige una de tus sesiones programadas. Las sesiones ya en preparación o en vivo no pueden
              aceptar nuevos equipos.
            </p>

            {sessionsError && (
              <div className={styles.errorBanner} role="alert" data-testid="team-session-list-error">
                {sessionsError}
              </div>
            )}

            {sessionAssignError && (
              <div className={styles.errorBanner} role="alert" data-testid="team-session-assign-error">
                {sessionAssignError}
              </div>
            )}

            {isSessionModalPending && scheduledOperatorSessions.length === 0 && !sessionsError ? (
              <span className={styles.chip}>Cargando…</span>
            ) : scheduledOperatorSessions.length === 0 ? (
              <p className={styles.emptyList} data-testid="team-session-empty">
                No tienes sesiones programadas disponibles para este equipo.
              </p>
            ) : (
              <div className={styles.sessionOperatorList} data-testid="team-session-list">
                {scheduledOperatorSessions.map((session) => (
                  <article key={session.liveSessionId} className={styles.sessionOperatorItem}>
                    <div className={styles.sessionOperatorHeader}>
                      <div className={styles.sessionOperatorTitleBlock}>
                        <h3>{session.title}</h3>
                        <div className={styles.sessionCardMeta}>
                          <span className={styles.sessionCodePill}>{session.sessionCode}</span>
                          <span>{new Date(session.scheduledAt).toLocaleString()}</span>
                        </div>
                      </div>
                      <span className={styles.chip} data-tone="muted">
                        {lifecycleStateLabel[toLifecycleState(session.sessionState) ?? 'Scheduled'] ?? session.sessionState}
                      </span>
                    </div>
                    <div className={styles.sessionOperatorBody}>
                      <div className={styles.sessionOperatorInfo}>
                        <span className={styles.sessionOperatorLabel}>Operador responsable</span>
                        <strong>Tú</strong>
                      </div>
                      <button
                        type="button"
                        className={styles.primaryButton}
                        data-testid={`team-session-assign-${session.liveSessionId}`}
                        disabled={isSessionModalPending}
                        onClick={() => handleAssociateTeamToSession(session.liveSessionId)}
                      >
                        Asignar a la sesión
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </section>
  )
}

function TeamForm({
  mode,
  initialValues,
  onSubmit,
  onCancel,
  isPending,
  formError,
}: {
  mode: 'create' | 'edit'
  initialValues?: { displayName: string; teamCode: string }
  onSubmit: (displayName: string, teamCode: string) => void
  onCancel: () => void
  isPending: boolean
  formError: string | null
}) {
  const [displayName, setDisplayName] = useState(initialValues?.displayName ?? '')
  const [teamCode, setTeamCode] = useState(initialValues?.teamCode ?? '')
  const [fieldErrors, setFieldErrors] = useState<{
    displayName?: string
    teamCode?: string
  }>({})

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const errors: { displayName?: string; teamCode?: string } = {}
    if (!displayName.trim()) errors.displayName = 'El nombre visible es obligatorio.'
    if (!teamCode.trim()) errors.teamCode = 'El código de equipo es obligatorio.'
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors)
      return
    }
    setFieldErrors({})
    onSubmit(displayName.trim(), teamCode.trim())
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      {formError && (
        <div className={styles.chip} data-tone="critical" data-testid="form-error">
          {formError}
        </div>
      )}

      <div className={styles.formGroup}>
        <label htmlFor="team-display-name">Nombre visible</label>
        <input
          id="team-display-name"
          className={styles.formInput}
          data-testid="team-display-name-input"
          type="text"
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          disabled={isPending}
        />
        {fieldErrors.displayName && (
          <span className={styles.fieldError} data-testid="display-name-error">
            {fieldErrors.displayName}
          </span>
        )}
      </div>

      <div className={styles.formGroup}>
        <label htmlFor="team-code">Código de equipo</label>
        <input
          id="team-code"
          className={styles.formInput}
          data-testid="team-code-input"
          type="text"
          value={teamCode}
          onChange={(e) => setTeamCode(e.target.value)}
          disabled={isPending}
        />
        {fieldErrors.teamCode && (
          <span className={styles.fieldError} data-testid="team-code-error">
            {fieldErrors.teamCode}
          </span>
        )}
      </div>

      <div className={styles.panelActions}>
        <button
          className={styles.primaryButton}
          data-testid="team-form-submit"
          disabled={isPending}
          type="submit"
        >
          {mode === 'create' ? 'Create team' : 'Save changes'}
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          onClick={onCancel}
          type="button"
        >
          Cancel
        </button>
      </div>
    </form>
  )
}
