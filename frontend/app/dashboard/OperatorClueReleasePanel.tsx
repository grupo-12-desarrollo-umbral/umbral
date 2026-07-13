'use client'

import { useState, useTransition } from 'react'
import type { SessionLifecycleState } from '@/app/lib/definitions'
import { releaseClueAction } from '@/app/actions/sessions'
import styles from './dashboard.module.css'

type ClueReleaseTeamOption = { teamId: string; displayName: string }

// HU-26 operator clue release control. Self-contained (own useState/useTransition), nested in the
// operator hero. Fire-and-forget mutation whose durable effect (board reveal) lands on the mobile
// participant surface, not this dashboard — so no DashboardClient state seam and no revalidatePath.
export function OperatorClueReleasePanel({
  liveSessionId,
  state,
  teams,
  onReleased,
}: {
  liveSessionId: string
  state: SessionLifecycleState
  teams: ClueReleaseTeamOption[]
  onReleased?: (target: string, count: number) => void
}) {
  const [targetId, setTargetId] = useState('')
  const [teamId, setTeamId] = useState('') // '' ⇒ all teams
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  // Active-only render gate. The backend also enforces Active, so a stale race still surfaces as the
  // notActive branch below — the UI never pre-empts, it just hides a control that cannot work off an
  // inactive session.
  if (state !== 'Active') {
    return (
      <section className={styles.cluePanel} data-testid="clue-release-panel" aria-labelledby="clue-release-title">
        <div className={styles.panelHeader}>
          <h2 id="clue-release-title">Release clue</h2>
        </div>
        <p className={styles.panelMeta} data-testid="clue-release-inactive">
          Clue release is available once the session is Active.
        </p>
      </section>
    )
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      setSuccess(null)
      const result = await releaseClueAction(liveSessionId, {
        targetId: targetId.trim(),
        teamId: teamId || undefined, // omit ⇒ all teams
      })
      if ('data' in result) {
        const count = result.data.releasedTeamIds.length
        setSuccess(`Released to ${count} team${count === 1 ? '' : 's'}.`)
        setTargetId('')
        onReleased?.(result.data.targetId, count)
      } else if ('duplicate' in result) {
        setError('That clue is already released to that team for this target.')
      } else if ('notReleasable' in result) {
        setError('That target has no releasable hidden clue in the active substage.')
      } else if ('notActive' in result) {
        setError('The session must be Active to release clues.')
      } else if ('unauthorized' in result) {
        setError('You are not authorized to release clues for this session.')
      } else {
        setError(result.error)
      }
    })
  }

  return (
    <section className={styles.cluePanel} data-testid="clue-release-panel" aria-labelledby="clue-release-title">
      <div className={styles.panelHeader}>
        <h2 id="clue-release-title">Release clue</h2>
        <div className={styles.panelMeta}>Reveal a treasure-hunt target’s hidden clue to one team or all teams.</div>
      </div>

      <label className={styles.fieldLabel}>
        <span>Target id</span>
        <input
          className={styles.inlineInput}
          data-testid="clue-release-target-input"
          value={targetId}
          onChange={(e) => setTargetId(e.target.value)}
          placeholder="Target UUID"
        />
      </label>

      <label className={styles.fieldLabel}>
        <span>Team</span>
        <select
          className={styles.inlineSelect}
          data-testid="clue-release-team-select"
          value={teamId}
          onChange={(e) => setTeamId(e.target.value)}
        >
          <option value="">All teams</option>
          {teams.map((t) => (
            <option key={t.teamId} value={t.teamId}>
              {t.displayName}
            </option>
          ))}
        </select>
      </label>

      <button
        className={styles.primaryButton}
        data-testid="clue-release-submit"
        disabled={isPending || targetId.trim() === ''}
        onClick={submit}
        type="button"
      >
        {isPending ? 'Releasing…' : 'Release'}
      </button>

      {success && (
        <p className={styles.panelMeta} role="status" data-testid="clue-release-success">
          {success}
        </p>
      )}
      {error && (
        <p className={styles.errorBanner} role="alert" data-testid="clue-release-error">
          {error}
        </p>
      )}
    </section>
  )
}
