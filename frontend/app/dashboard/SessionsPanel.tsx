'use client'

import { useState, useTransition } from 'react'
import { getSessionTeams, associateTeamToSession } from '@/app/actions/session-operations'
import { getTeamsPage } from '@/app/actions/teams'
import type {
  AssociatedSessionTeamDto,
  PagedResult,
  TeamDto,
} from '@/app/lib/definitions'
import styles from './dashboard.module.css'

type DashboardRole = 'operator' | 'admin' | 'participant'

type SessionPanelView = 'lookup' | 'teams'

export function SessionsPanel({ role }: { role: DashboardRole }) {
  const [view, setView] = useState<SessionPanelView>('lookup')
  const [sessionIdInput, setSessionIdInput] = useState<string>('')
  const [liveSessionId, setLiveSessionId] = useState<string | null>(null)
  const [associatedTeams, setAssociatedTeams] = useState<AssociatedSessionTeamDto[]>([])
  const [availableTeams, setAvailableTeams] = useState<PagedResult<TeamDto> | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [assignError, setAssignError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()
  const [isAssigning, startAssignTransition] = useTransition()

  function handleSessionLookup(id: string) {
    startTransition(async () => {
      setLookupError(null)
      try {
        const [sessionData, teamsData] = await Promise.all([
          getSessionTeams(id),
          getTeamsPage(1, 100),
        ])
        setAssociatedTeams(sessionData.teams)
        setAvailableTeams(teamsData)
        setLiveSessionId(id)
        setView('teams')
      } catch (err) {
        if (err instanceof Error && err.message === 'session_not_found') {
          setLookupError('Session not found. Check the session ID and try again.')
        } else {
          setLookupError('Failed to load session. Please try again.')
        }
      }
    })
  }

  function handleAssign(referenceTeamId: string) {
    if (!liveSessionId) return
    startAssignTransition(async () => {
      setAssignError(null)
      try {
        const result = await associateTeamToSession(liveSessionId, referenceTeamId)
        setAssociatedTeams((prev) => [
          ...prev,
          {
            runtimeTeamId: result.runtimeTeamId,
            referenceTeamId: result.referenceTeamId,
            displayName: result.displayName,
            teamCode: result.teamCode,
            joinStatus: 'Pending',
          },
        ])
      } catch (err) {
        if (!(err instanceof Error)) {
          setAssignError('Assignment failed. Please try again.')
          return
        }
        switch (err.message) {
          case 'team_not_found':
            setAssignError('Team not found in the catalog.')
            break
          case 'team_inactive':
            setAssignError('This team is inactive and cannot be assigned to a session.')
            break
          case 'duplicate_association':
            setAssignError('This team is already associated with this session.')
            break
          default:
            setAssignError('Assignment failed. Please try again.')
        }
      }
    })
  }

  function handleBack() {
    setView('lookup')
    setLiveSessionId(null)
    setAssociatedTeams([])
    setAvailableTeams(null)
    setAssignError(null)
    setLookupError(null)
  }

  if (view === 'lookup') {
    return (
      <section
        className={styles.panel}
        aria-labelledby="sessions-lookup-title"
        data-testid="sessions-panel"
      >
        <div className={styles.panelHeader}>
          <div>
            <h2 id="sessions-lookup-title">Session team setup</h2>
            <div className={styles.panelMeta}>
              Enter the session ID to load and manage team associations.
            </div>
          </div>
        </div>

        <form
          data-testid="session-lookup-form"
          onSubmit={(e) => {
            e.preventDefault()
            handleSessionLookup(sessionIdInput.trim())
          }}
        >
          <div className={styles.formGroup}>
            <label className={styles.fieldLabel} htmlFor="session-id-input">
              Session ID (UUID)
            </label>
            <input
              className={styles.textInput}
              data-testid="session-id-input"
              disabled={isPending}
              id="session-id-input"
              onChange={(e) => setSessionIdInput(e.target.value)}
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
              type="text"
              value={sessionIdInput}
            />
          </div>

          {lookupError && (
            <div className={styles.chip} data-testid="sessions-lookup-error" data-tone="critical">
              {lookupError}
            </div>
          )}

          <div className={styles.formActions}>
            <button
              className={styles.primaryButton}
              data-testid="session-lookup-submit"
              disabled={isPending || !sessionIdInput.trim()}
              type="submit"
            >
              {isPending ? 'Loading…' : 'Load session'}
            </button>
          </div>
        </form>
      </section>
    )
  }

  return (
    <section
      className={styles.panel}
      aria-labelledby="sessions-teams-title"
      data-testid="sessions-teams-panel"
    >
      <div className={styles.panelHeader}>
        <div>
          <h2 id="sessions-teams-title">Session teams</h2>
          <div className={styles.panelMeta}>
            Session: <code>{liveSessionId}</code>
            {' · '}{associatedTeams.length} team{associatedTeams.length !== 1 ? 's' : ''} associated
          </div>
        </div>
        <button
          className={styles.inlineButton}
          data-testid="sessions-back-btn"
          disabled={isPending || isAssigning}
          onClick={handleBack}
          type="button"
        >
          ← Change session
        </button>
      </div>

      {/* Already-associated teams */}
      <div className={styles.subsectionHeader}>
        <h3>Associated teams</h3>
      </div>

      {associatedTeams.length === 0 ? (
        <p className={styles.mutedText} data-testid="no-associated-teams">
          No teams associated yet.
        </p>
      ) : (
        <table className={styles.table} data-testid="associated-teams-table">
          <thead>
            <tr>
              <th>Team name</th>
              <th>Code</th>
              <th>Join status</th>
            </tr>
          </thead>
          <tbody>
            {associatedTeams.map((team) => (
              <tr key={team.runtimeTeamId} data-testid={`associated-team-${team.referenceTeamId}`}>
                <td>{team.displayName}</td>
                <td>{team.teamCode}</td>
                <td>
                  <span className={styles.chip}>{team.joinStatus}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {/* Team picker */}
      <div className={styles.subsectionHeader}>
        <h3>Assign a team</h3>
      </div>

      {assignError && (
        <div className={styles.chip} data-testid="sessions-assign-error" data-tone="critical">
          {assignError}
        </div>
      )}

      {availableTeams && (() => {
        const assignableTeams = availableTeams.items.filter(
          (t) => t.isActive && !associatedTeams.some((a) => a.referenceTeamId === t.teamId),
        )
        return assignableTeams.length === 0 ? (
          <p className={styles.mutedText} data-testid="no-assignable-teams">
            All active teams have been associated with this session.
          </p>
        ) : (
          <table className={styles.table} data-testid="assignable-teams-table">
            <thead>
              <tr>
                <th>Team name</th>
                <th>Code</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {assignableTeams.map((team) => (
                <tr key={team.teamId} data-testid={`assignable-team-${team.teamId}`}>
                  <td>{team.displayName}</td>
                  <td>{team.teamCode}</td>
                  <td>
                    <span className={styles.chip} data-tone="success">Active</span>
                  </td>
                  <td>
                    <button
                      className={styles.inlineButton}
                      data-testid={`assign-team-btn-${team.teamId}`}
                      disabled={isAssigning || isPending}
                      onClick={() => handleAssign(team.teamId)}
                      type="button"
                    >
                      Assign
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )
      })()}
    </section>
  )
}
