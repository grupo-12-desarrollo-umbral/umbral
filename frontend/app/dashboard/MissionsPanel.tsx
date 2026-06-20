'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getMissions,
  getMission,
  createMission,
  updateMission,
  activateMission,
  deactivateMission,
} from '@/app/actions/missions'
import type { MissionSummaryDto, MissionDto } from '@/app/lib/definitions'
import { MissionTree } from './mission/MissionTree'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type MissionPanelView = 'list' | 'detail' | 'create' | 'edit'

export function MissionsPanel({ role }: { role: DashboardRole }) {
  const [view, setView] = useState<MissionPanelView>('list')
  const [selectedMission, setSelectedMission] = useState<MissionDto | null>(null)
  const [listData, setListData] = useState<MissionSummaryDto[] | null>(null)
  const [isPending, startTransition] = useTransition()
  const [listError, setListError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [deactivateError, setDeactivateError] = useState<string | null>(null)
  const [activateError, setActivateError] = useState<string | null>(null)
  const [confirmDeactivate, setConfirmDeactivate] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    startTransition(async () => {
      setListError(null)
      try {
        const result = await getMissions()
        setListData(result)
      } catch {
        setListError('Failed to load missions.')
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
        setActivateError(null)
        setView('detail')
      } catch {
        setListError('Failed to load mission details.')
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
          setFormError('Check all fields: name, description, and difficulty are required; time must be a positive number.')
        } else {
          setFormError('Failed to create mission. Try again.')
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
          setFormError('Check all fields: name, description, and difficulty are required; time must be a positive number.')
        } else if (msg === 'mission_not_found') {
          setFormError('Mission no longer exists.')
        } else {
          setFormError('Failed to update mission. Try again.')
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
            : 'Deactivation failed. Try again.'
        setDeactivateError(message)
        setConfirmDeactivate(false)
      }
    })
  }

  async function handleActivate(id: number) {
    startTransition(async () => {
      setActivateError(null)
      try {
        const updated = await activateMission(id)
        setSelectedMission(updated)
        setRefreshKey((k) => k + 1)
      } catch (err) {
        // The lib surfaces the backend readiness failures verbatim in the error message;
        // fall back to a generic line for unexpected failures.
        const message =
          err instanceof Error && err.message && err.message !== 'mission_not_found'
            ? err.message
            : 'Activation failed. Try again.'
        setActivateError(message)
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
            setActivateError(null)
          }}
          type="button"
        >
          ← Back to missions
        </button>

        <h2 className={styles.missionDetailTitle} data-testid="mission-detail-name">
          {selectedMission.name}
        </h2>

        <div className={styles.missionDetailDescCard}>
          <span className={styles.missionDetailDescLabel}>Description</span>
          <p data-testid="mission-detail-description">{selectedMission.description}</p>
        </div>

        <div className={styles.missionDetailInlineMeta}>
          <span data-testid="mission-detail-time">
            Maximum Time: <strong>{selectedMission.maximumTimeMinutes} min</strong>
          </span>
          <span data-testid="mission-detail-difficulty">
            Difficulty: <strong>{selectedMission.difficulty}</strong>
          </span>
          <span>
            Status:
            <span
              className={styles.chip}
              data-tone={activationTone}
              data-testid="mission-detail-status"
            >
              {selectedMission.activationState}
            </span>
          </span>
        </div>

        <MissionTree mission={selectedMission} onMutated={setSelectedMission} />

        <div className={styles.missionDetailActions}>
          <button
            className={styles.inlineButton}
            data-testid="edit-mission-btn"
            disabled={isPending || !selectedMission.isActive}
            onClick={() => { setFormError(null); setView('edit') }}
            type="button"
          >
            Edit
          </button>

          {selectedMission.isActive &&
            selectedMission.activationState === 'Draft' &&
            !confirmDeactivate && (
            <button
              className={styles.primaryButton}
              data-testid="activate-mission-btn"
              disabled={isPending}
              onClick={() => handleActivate(selectedMission.id)}
              type="button"
            >
              Activate
            </button>
          )}

          {selectedMission.isActive && !confirmDeactivate && (
            <button
              className={styles.inlineButton}
              data-testid="deactivate-mission-btn"
              disabled={isPending}
              onClick={() => setConfirmDeactivate(true)}
              type="button"
            >
              Deactivate
            </button>
          )}

          {confirmDeactivate && (
            <span className={styles.confirmRow}>
              <span>Deactivate this mission?</span>
              <button
                className={styles.dangerButton}
                data-testid="confirm-deactivate-mission-btn"
                disabled={isPending}
                onClick={() => handleDeactivate(selectedMission.id)}
                type="button"
              >
                Confirm
              </button>
              <button
                className={styles.inlineButton}
                disabled={isPending}
                onClick={() => setConfirmDeactivate(false)}
                type="button"
              >
                Cancel
              </button>
            </span>
          )}
        </div>
        {deactivateError && <p className={styles.formError} role="alert">{deactivateError}</p>}
        {activateError && (
          <p className={styles.formError} role="alert" data-testid="activate-mission-error">
            {activateError}
          </p>
        )}
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
          ← Back to missions
        </button>

        <h2 className={styles.missionDetailTitle}>Create mission</h2>

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

        <h2 className={styles.missionDetailTitle}>Edit mission</h2>

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
          <h2>Missions</h2>
        </div>
        <button
          className={styles.primaryButton}
          data-testid="create-mission-btn"
          disabled={isPending}
          onClick={() => { setFormError(null); setView('create') }}
          type="button"
        >
          Create mission
        </button>
      </div>

      {listError && <p className={styles.formError}>{listError}</p>}

      {listData && (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Name</th>
              <th>Description</th>
              <th>Difficulty</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {listData.map((mission) => (
              <tr key={mission.id} data-testid={`mission-row-${mission.id}`}>
                <td>{mission.name}</td>
                <td>{mission.description}</td>
                <td>
                  <span
                    className={styles.chip}
                    data-testid={`mission-difficulty-${mission.id}`}
                  >
                    {mission.difficulty}
                  </span>
                </td>
                <td>
                  <span
                    className={styles.chip}
                    data-tone={mission.isActive ? (mission.isSourceReady ? 'success' : 'warning') : 'muted'}
                    data-testid={`mission-status-${mission.id}`}
                  >
                    {mission.activationState}
                  </span>
                </td>
                <td>
                  <button
                    className={styles.inlineButton}
                    data-testid={`view-mission-btn-${mission.id}`}
                    disabled={isPending}
                    onClick={() => handleOpenDetail(mission.id)}
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

const MISSION_DIFFICULTIES = ['Beginner', 'Intermediate', 'Advanced'] as const

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

  return (
    <form
      className={styles.missionForm}
      data-testid="mission-form"
      onSubmit={(e) => {
        e.preventDefault()
        const time = parseInt(maximumTimeMinutes, 10)
        if (!time || time < 1) return
        onSubmit(name.trim(), description.trim(), difficulty, time)
      }}
    >
      {error && <p className={styles.formError} role="alert">{error}</p>}

      <div className={styles.missionFormNameCard}>
        <span className={styles.missionDetailDescLabel}>Mission Name</span>
        <input
          className={styles.missionFormNameInput}
          data-testid="mission-name-input"
          disabled={isPending}
          maxLength={200}
          placeholder="Enter mission name"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </div>

      <div className={styles.missionFormDescCard}>
        <span className={styles.missionDetailDescLabel}>Description</span>
        <textarea
          className={styles.missionFormDescTextarea}
          data-testid="mission-description-input"
          disabled={isPending}
          maxLength={2000}
          placeholder="Describe the mission"
          required
          rows={4}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </div>

      <div className={styles.missionFormMetaRow}>
        <div className={styles.missionFormMetaItem}>
          <span className={styles.missionDetailDescLabel}>Difficulty</span>
          <select
            className={styles.formInput}
            data-testid="mission-difficulty-input"
            disabled={isPending}
            required
            value={difficulty}
            onChange={(e) => setDifficulty(e.target.value)}
          >
            <option value="" disabled>Select difficulty</option>
            {MISSION_DIFFICULTIES.map((d) => (
              <option key={d} value={d}>{d}</option>
            ))}
          </select>
        </div>
        <div className={styles.missionFormMetaItem}>
          <span className={styles.missionDetailDescLabel}>Maximum Time</span>
          <div className={styles.missionFormTimeWrap}>
            <input
              className={styles.formInput}
              data-testid="mission-time-input"
              disabled={isPending}
              min={1}
              placeholder="e.g. 30"
              required
              type="number"
              value={maximumTimeMinutes}
              onChange={(e) => setMaximumTimeMinutes(e.target.value)}
            />
            <span className={styles.missionFormTimeUnit}>minutes</span>
          </div>
        </div>
      </div>

      <div className={styles.missionDetailActions}>
        <button
          className={styles.primaryButton}
          data-testid="mission-submit-btn"
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
