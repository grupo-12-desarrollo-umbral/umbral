'use client'

import { useEffect, useMemo, useState, useTransition } from 'react'
import {
  getAssignableOperators,
  listSessionsForAssignment,
  assignSessionOperator,
  getActiveMissions,
  createSession,
} from '@/app/actions/sessions'
import type {
  AssignableOperatorDto,
  MissionSummaryDto,
  SessionAssignmentSummaryDto,
} from '@/app/lib/definitions'
import { lifecycleStateLabel } from '@/app/lib/session-lifecycle'
import styles from './dashboard.module.css'

const backendRoleLabel: Record<string, string> = {
  Administrator: 'Administrador',
  Operator: 'Operador',
  Participant: 'Participante',
}
function displayRole(role: string) {
  return backendRoleLabel[role] ?? role
}
function stateLabel(state: string) {
  return lifecycleStateLabel[state as keyof typeof lifecycleStateLabel] ?? state
}

export function SessionOperatorPanel() {
  const [sessions, setSessions] = useState<SessionAssignmentSummaryDto[]>([])
  const [modalSessionId, setModalSessionId] = useState<string | null>(null)
  const [operators, setOperators] = useState<AssignableOperatorDto[]>([])
  const [pendingOperatorId, setPendingOperatorId] = useState<number | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [assignError, setAssignError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  const [missions, setMissions] = useState<MissionSummaryDto[] | null>(null)
  const [missionsError, setMissionsError] = useState<string | null>(null)
  const [selectedMissionId, setSelectedMissionId] = useState('')
  const [title, setTitle] = useState('')
  const [maxMinutes, setMaxMinutes] = useState('60')
  const [scheduledAt, setScheduledAt] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [isCreating, startCreateTransition] = useTransition()
  const sortedMissions = useMemo(
    () =>
      missions?.toSorted((a, b) => {
        if (a.isSourceReady !== b.isSourceReady) {
          return a.isSourceReady ? -1 : 1
        }

        return a.name.localeCompare(b.name)
      }) ?? null,
    [missions],
  )

  useEffect(() => {
    getActiveMissions()
      .then(setMissions)
      .catch(() => setMissionsError('No se pudieron cargar las misiones disponibles. Recarga la página.'))
  }, [])

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
            ? 'Se requiere el rol de Administrador.'
            : 'No se pudieron cargar las sesiones. Inténtalo de nuevo.',
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
            ? 'Se requiere el rol de Administrador.'
            : 'No se pudieron cargar las sesiones. Inténtalo de nuevo.',
        )
      }
    })
  }

  function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    setFormError(null)
    startCreateTransition(async () => {
      try {
        await createSession({
          missionId: Number(selectedMissionId),
          title: title.trim(),
          maximumTimeMinutes: Number(maxMinutes),
          scheduledAt: new Date(scheduledAt).toISOString(),
        })
        setSelectedMissionId('')
        setTitle('')
        setMaxMinutes('60')
        setScheduledAt('')
        refreshSessions()
      } catch (err) {
        if (err instanceof Error && err.message === 'mission_not_eligible') {
          setFormError(
            'La misión seleccionada está inactiva o no está lista para ejecución y no puede usarse para crear una sesión. ' +
            'Activa la misión y asegúrate de que todas las etapas, subetapas y targets estén configurados.',
          )
        } else if (err instanceof Error && err.message === 'mission_not_found') {
          setFormError('No se encontró la misión seleccionada. Recarga la página e inténtalo de nuevo.')
        } else if (err instanceof Error && err.message === 'invalid_input') {
          setFormError('Datos de sesión no válidos. Revisa todos los campos e inténtalo de nuevo.')
        } else {
          setFormError('Falló la creación de la sesión. Inténtalo de nuevo.')
        }
      }
    })
  }

  function currentOperatorLabel(s: SessionAssignmentSummaryDto): string {
    if (s.assignedOperatorUserId == null) return 'Sin asignar'
    const op = operators.find((o) => o.id === s.assignedOperatorUserId)
    return op ? `${op.displayName} (${op.email})` : `Usuario #${s.assignedOperatorUserId}`
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
            ? 'Ese usuario no puede asignarse como operador responsable.'
            : err instanceof Error && err.message === 'session_not_found'
              ? 'Sesión no encontrada.'
              : err instanceof Error && err.message.includes('Administrator')
                ? 'Se requiere el rol de Administrador.'
                : 'Falló la asignación. Inténtalo de nuevo.',
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
            <p className={styles.emptyList}>No hay sesiones activas para asignar.</p>
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
                    {stateLabel(session.sessionState)}
                  </span>
                </div>
                <div className={styles.sessionOperatorBody}>
                  <div className={styles.sessionOperatorInfo}>
                    <span className={styles.sessionOperatorLabel}>Operador actual</span>
                    <strong>{currentOperatorLabel(session)}</strong>
                  </div>
                  <button
                    type="button"
                    className={styles.primaryButton}
                    onClick={() => openAssignmentModal(session)}
                  >
                    {session.assignedOperatorUserId == null ? 'Asignar operador' : 'Cambiar operador'}
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
          <h2>Asignar operadores</h2>
          <div className={styles.panelMeta}>
            Asigna o cambia el operador responsable de una sesión existente.
          </div>
        </div>
        <div className={styles.confirmRow}>
          <button
            className={styles.smallButton}
            type="button"
            onClick={refreshSessions}
            disabled={isPending}
          >
            Actualizar
          </button>
          {isPending && <span className={styles.chip}>Cargando…</span>}
        </div>
      </div>

      {listError && (
        <div className={styles.errorBanner} role="alert" data-testid="session-operator-list-error">
          {listError}
        </div>
      )}

      <section className={styles.assignmentCard} aria-labelledby="create-session-heading">
        <div className={styles.panelHeader}>
          <div>
            <h3 id="create-session-heading">Crear sesión</h3>
            <div className={styles.panelMeta}>
              Crea una sesión a partir de una misión activa y luego asigna un operador responsable más abajo.
            </div>
          </div>
        </div>

        {missionsError && (
          <div className={styles.errorBanner} role="alert" data-testid="session-missions-error">
            {missionsError}
          </div>
        )}

        {formError && (
          <div className={styles.errorBanner} role="alert" data-testid="session-form-error">
            {formError}
          </div>
        )}

        <form onSubmit={handleCreate} data-testid="session-create-form">
          <div className={styles.formGroup}>
            <label htmlFor="session-mission-select">Misión</label>
            <select
              id="session-mission-select"
              data-testid="session-mission-select"
              className={styles.formInput}
              value={selectedMissionId}
              onChange={(e) => setSelectedMissionId(e.target.value)}
              required
              disabled={isCreating || !missions}
            >
              <option value="" disabled>— Selecciona una misión lista para ejecución —</option>
              {sortedMissions?.map((m) => (
                // A mission only becomes a valid session source once it is runtime-ready
                // (authored + activated). Draft missions still surface here so the admin can
                // see them, but they are disabled with the reason rather than silently failing
                // the create call with a 409.
                <option
                  key={m.id}
                  value={String(m.id)}
                  disabled={!m.isSourceReady}
                >
                  {m.isSourceReady ? m.name : `${m.name} — no está lista para ejecución`}
                </option>
              ))}
            </select>
          </div>

          {missions?.length === 0 && (
            <p className={styles.emptyList} data-testid="session-no-missions">
              No hay misiones activas disponibles. Activa una misión antes de crear una sesión.
            </p>
          )}

          {missions !== null && missions.length > 0 && !missions.some((m) => m.isSourceReady) && (
            <p className={styles.emptyList} data-testid="session-no-ready-missions">
              No hay misiones listas para ejecución disponibles. Termina de crear una misión y actívala
              antes de crear una sesión.
            </p>
          )}

          <div className={styles.formGroup}>
            <label htmlFor="session-title">Título de la sesión</label>
            <input
              id="session-title"
              data-testid="session-title-input"
              className={styles.formInput}
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              required
              disabled={isCreating}
            />
          </div>

          <div className={styles.formGroup}>
            <label htmlFor="session-max-time">Tiempo máximo (minutos)</label>
            <input
              id="session-max-time"
              data-testid="session-max-time-input"
              className={styles.formInput}
              type="number"
              min="1"
              max="480"
              value={maxMinutes}
              onChange={(e) => setMaxMinutes(e.target.value)}
              required
              disabled={isCreating}
            />
          </div>

          <div className={styles.formGroup}>
            <label htmlFor="session-scheduled-at">Programada para</label>
            <input
              id="session-scheduled-at"
              data-testid="session-scheduled-at-input"
              className={styles.formInput}
              type="datetime-local"
              value={scheduledAt}
              onChange={(e) => setScheduledAt(e.target.value)}
              required
              disabled={isCreating}
            />
          </div>

          <button
            type="submit"
            data-testid="session-submit-btn"
            className={styles.primaryButton}
            disabled={
              isCreating ||
              !selectedMissionId ||
              !title.trim() ||
              !maxMinutes ||
              !scheduledAt
            }
          >
            {isCreating ? 'Creando…' : 'Crear sesión'}
          </button>
        </form>
      </section>

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
                <h2 id="session-operator-modal-title">Asignación de operador</h2>
                <div className={styles.panelMeta}>
                  {selectedSession.title} · {selectedSession.sessionCode}
                </div>
              </div>
              <button
                className={styles.smallButton}
                type="button"
                onClick={handleCancel}
                disabled={isPending}
                aria-label="Cerrar el diálogo de asignación de operador"
              >
                Cerrar
              </button>
            </div>

            <div className={styles.sessionOperatorModalDetails}>
              <div className={styles.assignmentCard}>
                <span className={styles.sessionOperatorLabel}>Programada</span>
                <strong>{new Date(selectedSession.scheduledAt).toLocaleString()}</strong>
              </div>
              <div className={styles.assignmentCard}>
                <span className={styles.sessionOperatorLabel}>Operador actual</span>
                <strong>{currentOperatorLabel(selectedSession)}</strong>
              </div>
            </div>

            <p className={styles.panelMeta}>
              Esto cambia la responsabilidad de una sesión existente. No crea la sesión.
            </p>

            <div className={styles.formGroup}>
              <label htmlFor="operator-select">Operador</label>
              <select
                id="operator-select"
                data-testid="operator-select"
                className={styles.formInput}
                value={pendingOperatorId ?? ''}
                onChange={(e) => setPendingOperatorId(e.target.value ? Number(e.target.value) : null)}
                disabled={isPending}
              >
                <option value="" disabled>— Selecciona un operador —</option>
                {operators.map((op) => (
                  <option key={op.id} value={op.id}>
                    {op.displayName} ({op.email}) — {displayRole(op.role)}
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
                Guardar operador
              </button>
              <button
                className={styles.inlineButton}
                type="button"
                disabled={isPending}
                onClick={handleCancel}
              >
                Cancelar
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
