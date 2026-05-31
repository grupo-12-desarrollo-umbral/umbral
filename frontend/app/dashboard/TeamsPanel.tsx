'use client'

import { useEffect, useState, useTransition } from 'react'
import {
  getTeamsPage,
  getTeam,
  createTeam,
  updateTeam,
  deactivateTeam,
} from '@/app/actions/teams'
import type { PagedResult, TeamDto } from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'
type TeamPanelView = 'list' | 'detail' | 'create' | 'edit'

export function TeamsPanel({ role }: { role: DashboardRole }) {
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

  useEffect(() => {
    startTransition(async () => {
      setListError(null)
      try {
        const result = await getTeamsPage(page)
        setListData(result)
      } catch {
        setListError('Failed to load teams.')
      }
    })
  }, [page, refreshKey])

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
          setDeactivateError('This team is already inactive.')
        } else {
          setDeactivateError('Deactivation failed. Try again.')
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
          setFormError('A team with this code already exists.')
        } else {
          setFormError('Failed to create team. Try again.')
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
          setFormError('A team with this code already exists.')
        } else if (msg === 'team_not_found') {
          setFormError('This team no longer exists.')
        } else {
          setFormError('Failed to save changes. Try again.')
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
              onClick={() => { setView('list'); setConfirmDeactivate(false); setDeactivateError(null) }}
              type="button"
            >
              ← Teams
            </button>
            <h2 id="team-detail-title">{selectedTeam.displayName}</h2>
            <div className={styles.panelMeta}>{selectedTeam.teamCode}</div>
          </div>

          {role === 'admin' && selectedTeam.isActive && (
            <div className={styles.panelActions}>
              {!confirmDeactivate && (
                <button
                  className={styles.inlineButton}
                  data-testid="edit-team-btn"
                  disabled={isPending}
                  onClick={() => { setFormError(null); setView('edit') }}
                  type="button"
                >
                  Edit
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
                  Deactivate
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
          )}
        </div>

        {deactivateError && (
          <div className={styles.chip} data-tone="critical">
            {deactivateError}
          </div>
        )}

        <dl className={styles.detailList} data-testid="team-detail-fields">
          <dt>Display name</dt>
          <dd data-testid="detail-display-name">{selectedTeam.displayName}</dd>
          <dt>Team code</dt>
          <dd data-testid="detail-team-code">{selectedTeam.teamCode}</dd>
          <dt>Status</dt>
          <dd>
            <span
              className={styles.chip}
              data-tone={selectedTeam.isActive ? 'success' : 'critical'}
              data-testid="detail-status"
            >
              {selectedTeam.isActive ? 'Active' : 'Inactive'}
            </span>
          </dd>
          <dt>Created</dt>
          <dd>{new Date(selectedTeam.createdAt).toLocaleDateString()}</dd>
          <dt>Last updated</dt>
          <dd>{new Date(selectedTeam.updatedAt).toLocaleDateString()}</dd>
        </dl>
      </section>
    )
  }

  if (view === 'create' && role === 'admin') {
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
              ← Teams
            </button>
            <h2 id="create-team-title">New team</h2>
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

  if (view === 'edit' && selectedTeam !== null && role === 'admin') {
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
            <h2 id="edit-team-title">Edit team</h2>
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
          <h2 id="teams-panel-title">Registered teams</h2>
          <div className={styles.panelMeta}>
            {role === 'admin'
              ? 'Team registry. Inactive teams are preserved for audit.'
              : 'Read-only team catalog.'}
          </div>
        </div>
        <div className={styles.panelActions}>
          {isPending && <span className={styles.chip}>Loading…</span>}
          {role === 'admin' && (
            <button
              className={styles.primaryButton}
              data-testid="create-team-btn"
              disabled={isPending}
              onClick={() => { setFormError(null); setView('create') }}
              type="button"
            >
              + New team
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
                <th>Name</th>
                <th>Code</th>
                <th>Status</th>
                <th>Created</th>
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
                  <td>{team.displayName}</td>
                  <td>{team.teamCode}</td>
                  <td>
                    <span
                      className={styles.chip}
                      data-tone={team.isActive ? 'success' : 'critical'}
                    >
                      {team.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td>{new Date(team.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className={styles.pagination} data-testid="teams-pagination">
            <span className={styles.panelMeta}>
              Page {listData.page} of {listData.totalPages} ({listData.totalCount} teams)
            </span>
            <span className={styles.paginationButtons}>
              <button
                className={styles.inlineButton}
                disabled={!listData.hasPreviousPage || isPending}
                onClick={() => setPage((p) => p - 1)}
                type="button"
              >
                ← Previous
              </button>
              <button
                className={styles.inlineButton}
                disabled={!listData.hasNextPage || isPending}
                onClick={() => setPage((p) => p + 1)}
                type="button"
              >
                Next →
              </button>
            </span>
          </div>
        </>
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
    if (!displayName.trim()) errors.displayName = 'Display name is required.'
    if (!teamCode.trim()) errors.teamCode = 'Team code is required.'
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
        <label htmlFor="team-display-name">Display name</label>
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
        <label htmlFor="team-code">Team code</label>
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
