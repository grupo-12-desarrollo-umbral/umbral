'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getMissions,
  getMission,
  createMission,
  updateMission,
  deactivateMission,
} from '@/app/actions/missions'
import type { MissionSummaryDto, MissionDto } from '@/app/lib/definitions'
import { MissionTree } from './mission/MissionTree'
import { ActivationBar } from './mission/ActivationBar'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type MissionPanelView = 'list' | 'detail' | 'create' | 'edit'

// Display labels for backend enums (values stay in English on the wire / in payloads).
const ACTIVATION_STATE_LABELS: Record<string, string> = {
  Draft: 'Borrador',
  Ready: 'Lista',
  Inactive: 'Inactiva',
  Active: 'Activa',
}
function activationStateLabel(state: string) {
  return ACTIVATION_STATE_LABELS[state] ?? state
}
const DIFFICULTY_LABELS: Record<string, string> = {
  Beginner: 'Principiante',
  Intermediate: 'Intermedio',
  Advanced: 'Avanzado',
}
function difficultyLabel(value: string) {
  return DIFFICULTY_LABELS[value] ?? value
}

export function MissionsPanel(_props: { role: DashboardRole }) {
  const [view, setView] = useState<MissionPanelView>('list')
  const [selectedMission, setSelectedMission] = useState<MissionDto | null>(null)
  const [listData, setListData] = useState<MissionSummaryDto[] | null>(null)
  const [isPending, startTransition] = useTransition()
  const [listError, setListError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [deactivateError, setDeactivateError] = useState<string | null>(null)
  const [confirmDeactivate, setConfirmDeactivate] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    startTransition(async () => {
      setListError(null)
      try {
        const result = await getMissions()
        setListData(result)
      } catch {
        setListError('No se pudieron cargar las misiones.')
      }
    })
  }, [refreshKey])

  async function handleOpenDetail(id: number) {
    startTransition(async () => {
      try {
        const mission = await getMission(id)
        setSelectedMission(mission)
        setConfirmDeactivate(false)
        setDeactivateError(null)
        setView('detail')
      } catch {
        setListError('No se pudieron cargar los detalles de la misión.')
      }
    })
  }

  async function handleCreate(
    name: string,
    description: string,
    difficulty: string,
    maximumTimeMinutes: number,
  ) {
    startTransition(async () => {
      setFormError(null)
      try {
        const created = await createMission(name, description, difficulty, maximumTimeMinutes)
        setSelectedMission(created)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'invalid_fields') {
          setFormError('Revisa todos los campos: el nombre, la descripción y la dificultad son obligatorios; el tiempo debe ser un número positivo.')
        } else {
          setFormError('No se pudo crear la misión. Inténtalo de nuevo.')
        }
      }
    })
  }

  async function handleUpdate(
    name: string,
    description: string,
    difficulty: string,
    maximumTimeMinutes: number,
  ) {
    if (!selectedMission) return
    startTransition(async () => {
      setFormError(null)
      try {
        const updated = await updateMission(selectedMission.id, name, description, difficulty, maximumTimeMinutes)
        setSelectedMission(updated)
        setView('detail')
        setRefreshKey((k) => k + 1)
      } catch (err) {
        const msg = err instanceof Error ? err.message : ''
        if (msg === 'invalid_fields') {
          setFormError('Revisa todos los campos: el nombre, la descripción y la dificultad son obligatorios; el tiempo debe ser un número positivo.')
        } else if (msg === 'mission_not_found') {
          setFormError('La misión ya no existe.')
        } else {
          setFormError('No se pudo actualizar la misión. Inténtalo de nuevo.')
        }
      }
    })
  }

  async function handleDeactivate(id: number) {
    startTransition(async () => {
      setDeactivateError(null)
      try {
        await deactivateMission(id)
        setSelectedMission((prev) =>
          prev ? { ...prev, isActive: false, activationState: 'Inactive', isSourceReady: false } : prev
        )
        setConfirmDeactivate(false)
        setRefreshKey((k) => k + 1)
      } catch (err) {
        // Backend surfaces the 409 (already-deactivated) message verbatim; fall back to a
        // generic line for unexpected failures.
        const message =
          err instanceof Error && err.message && err.message !== 'mission_not_found'
            ? err.message
            : 'Falló la desactivación. Inténtalo de nuevo.'
        setDeactivateError(message)
        setConfirmDeactivate(false)
      }
    })
  }

  if (view === 'detail' && selectedMission !== null) {
    const activationTone =
      selectedMission.activationState === 'Ready'
        ? 'success'
        : selectedMission.activationState === 'Draft'
          ? 'warning'
          : 'muted'

    return (
      <section className={styles.panel} data-testid="mission-detail">
        <button
          className={styles.inlineButton}
          onClick={() => {
            setView('list')
            setConfirmDeactivate(false)
            setDeactivateError(null)
          }}
          type="button"
        >
          ← Volver a misiones
        </button>

        <h2 className={styles.missionDetailTitle} data-testid="mission-detail-name">
          {selectedMission.name}
        </h2>

        <div className={styles.missionDetailDescCard}>
          <span className={styles.missionDetailDescLabel}>Descripción</span>
          <p data-testid="mission-detail-description">{selectedMission.description}</p>
        </div>

        <div className={styles.missionDetailInlineMeta}>
          <span data-testid="mission-detail-time">
            Tiempo máximo: <strong>{selectedMission.maximumTimeMinutes} min</strong>
          </span>
          <span data-testid="mission-detail-difficulty">
            Dificultad: <strong>{difficultyLabel(selectedMission.difficulty)}</strong>
          </span>
          <span>
            Estado:
            <span
              className={styles.chip}
              data-tone={activationTone}
              data-testid="mission-detail-status"
            >
              {activationStateLabel(selectedMission.activationState)}
            </span>
          </span>
        </div>

        {/* A deactivated mission is terminally retired (HU-09): the tree stays visible for
            inspection, but all authoring is removed to match the disabled Edit button and the
            backend guard that rejects structure edits on an inactive mission. */}
        <MissionTree
          mission={selectedMission}
          onMutated={setSelectedMission}
          readOnly={!selectedMission.isActive}
        />

        <ActivationBar
          mission={selectedMission}
          onMutated={(updated) => {
            setSelectedMission(updated)
            setRefreshKey((k) => k + 1)
          }}
        />

        <div className={styles.missionDetailActions}>
          <button
            className={styles.inlineButton}
            data-testid="edit-mission-btn"
            disabled={isPending || !selectedMission.isActive}
            onClick={() => { setFormError(null); setView('edit') }}
            type="button"
          >
            Editar
          </button>

          {selectedMission.isActive && !confirmDeactivate && (
            <button
              className={styles.inlineButton}
              data-testid="deactivate-mission-btn"
              disabled={isPending}
              onClick={() => setConfirmDeactivate(true)}
              type="button"
            >
              Desactivar
            </button>
          )}

          {confirmDeactivate && (
            <span className={styles.confirmRow}>
              <span>¿Desactivar esta misión?</span>
              <button
                className={styles.dangerButton}
                data-testid="confirm-deactivate-mission-btn"
                disabled={isPending}
                onClick={() => handleDeactivate(selectedMission.id)}
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
        {deactivateError && <p className={styles.formError} role="alert">{deactivateError}</p>}
      </section>
    )
  }

  if (view === 'create') {
    return (
      <section className={styles.panel} data-testid="missions-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setFormError(null); setView('list') }}
          type="button"
        >
          ← Volver a misiones
        </button>

        <h2 className={styles.missionDetailTitle}>Crear misión</h2>

        <MissionForm
          initial={{ name: '', description: '', difficulty: '', maximumTimeMinutes: 0 }}
          isPending={isPending}
          error={formError}
          onSubmit={handleCreate}
          onCancel={() => { setFormError(null); setView('list') }}
        />
      </section>
    )
  }

  if (view === 'edit' && selectedMission !== null) {
    return (
      <section className={styles.panel} data-testid="missions-panel">
        <button
          className={styles.inlineButton}
          onClick={() => { setFormError(null); setView('detail') }}
          type="button"
        >
          ← {selectedMission.name}
        </button>

        <h2 className={styles.missionDetailTitle}>Editar misión</h2>

        <MissionForm
          initial={{
            name: selectedMission.name,
            description: selectedMission.description,
            difficulty: selectedMission.difficulty,
            maximumTimeMinutes: selectedMission.maximumTimeMinutes,
          }}
          isPending={isPending}
          error={formError}
          onSubmit={handleUpdate}
          onCancel={() => { setFormError(null); setView('detail') }}
        />
      </section>
    )
  }

  // Default list view
  return (
    <section className={styles.panel} data-testid="missions-panel">
      <div className={styles.panelHeader}>
        <div>
          <h2>Misiones</h2>
        </div>
        <button
          className={styles.primaryButton}
          data-testid="create-mission-btn"
          disabled={isPending}
          onClick={() => { setFormError(null); setView('create') }}
          type="button"
        >
          Crear misión
        </button>
      </div>

      {listError && <p className={styles.formError}>{listError}</p>}

      {listData && (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Descripción</th>
              <th>Dificultad</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {listData.map((mission) => (
              <tr key={mission.id} data-testid={`mission-row-${mission.id}`}>
                <td data-label="Nombre">{mission.name}</td>
                <td data-label="Descripción">{mission.description}</td>
                <td data-label="Dificultad">
                  <span
                    className={styles.chip}
                    data-testid={`mission-difficulty-${mission.id}`}
                  >
                    {difficultyLabel(mission.difficulty)}
                  </span>
                </td>
                <td data-label="Estado">
                  <span
                    className={styles.chip}
                    data-tone={mission.isActive ? (mission.isSourceReady ? 'success' : 'warning') : 'muted'}
                    data-testid={`mission-status-${mission.id}`}
                  >
                    {activationStateLabel(mission.activationState)}
                  </span>
                </td>
                <td data-label="Acciones">
                  <button
                    className={styles.inlineButton}
                    data-testid={`view-mission-btn-${mission.id}`}
                    disabled={isPending}
                    onClick={() => handleOpenDetail(mission.id)}
                    type="button"
                  >
                    Ver detalles
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

const MISSION_DIFFICULTIES = ['Beginner', 'Intermediate', 'Advanced'] as const

// Keep in sync with the backend cap (MaximumTime.MaximumMinutes).
const MAX_MISSION_TIME_MINUTES = 30

function MissionForm({
  initial,
  isPending,
  error,
  onSubmit,
  onCancel,
}: {
  initial: { name: string; description: string; difficulty: string; maximumTimeMinutes: number }
  isPending: boolean
  error: string | null
  onSubmit: (name: string, description: string, difficulty: string, maximumTimeMinutes: number) => void
  onCancel: () => void
}) {
  const [name, setName] = useState(initial.name)
  const [description, setDescription] = useState(initial.description)
  const [difficulty, setDifficulty] = useState(initial.difficulty)
  const [maximumTimeMinutes, setMaximumTimeMinutes] = useState(
    initial.maximumTimeMinutes === 0 ? '' : String(initial.maximumTimeMinutes),
  )
  const [timeError, setTimeError] = useState<string | null>(null)

  return (
    <form
      className={styles.missionForm}
      data-testid="mission-form"
      onSubmit={(e) => {
        e.preventDefault()
        const time = parseInt(maximumTimeMinutes, 10)
        if (!Number.isInteger(time) || time < 1 || time > MAX_MISSION_TIME_MINUTES) {
          setTimeError(
            `El tiempo máximo debe ser un número entero entre 1 y ${MAX_MISSION_TIME_MINUTES} minutos.`,
          )
          return
        }
        setTimeError(null)
        onSubmit(name.trim(), description.trim(), difficulty, time)
      }}
    >
      {error && <p className={styles.formError} role="alert">{error}</p>}

      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Nombre de la misión</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="mission-name-input"
          disabled={isPending}
          maxLength={200}
          placeholder="Ingresa el nombre de la misión"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </div>

      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Descripción</span>
        <textarea
          className={styles.missionFormDescTextarea}
          data-testid="mission-description-input"
          disabled={isPending}
          maxLength={2000}
          placeholder="Describe la misión"
          required
          rows={4}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>

      <div className={styles.missionFormMetaRow}>
        <div className={styles.missionFormMetaItem}>
          <span className={styles.missionDetailDescLabel}>Dificultad</span>
          <select
            className={styles.formInput}
            data-testid="mission-difficulty-input"
            disabled={isPending}
            required
            value={difficulty}
            onChange={(e) => setDifficulty(e.target.value)}
          >
            <option value="" disabled>Selecciona la dificultad</option>
            {MISSION_DIFFICULTIES.map((d) => (
              <option key={d} value={d}>{difficultyLabel(d)}</option>
            ))}
          </select>
        </div>
        <div className={styles.missionFormMetaItem}>
          <span className={styles.missionDetailDescLabel}>Tiempo máximo</span>
          <div className={styles.missionFormTimeWrap}>
            <input
              aria-invalid={timeError !== null}
              className={styles.formInput}
              data-testid="mission-time-input"
              disabled={isPending}
              max={MAX_MISSION_TIME_MINUTES}
              min={1}
              placeholder="p. ej. 30"
              required
              step={1}
              type="number"
              value={maximumTimeMinutes}
              onChange={(e) => {
                setMaximumTimeMinutes(e.target.value)
                if (timeError) setTimeError(null)
              }}
            />
            <span className={styles.missionFormTimeUnit}>minutos</span>
          </div>
          {timeError && (
            <p className={styles.formError} data-testid="mission-time-error" role="alert">
              {timeError}
            </p>
          )}
        </div>
      </div>

      <div className={styles.missionDetailActions}>
        <button
          className={styles.primaryButton}
          data-testid="mission-submit-btn"
          disabled={isPending}
          type="submit"
        >
          Guardar
        </button>
        <button
          className={styles.inlineButton}
          disabled={isPending}
          type="button"
          onClick={onCancel}
        >
          Cancelar
        </button>
      </div>
    </form>
  )
}
