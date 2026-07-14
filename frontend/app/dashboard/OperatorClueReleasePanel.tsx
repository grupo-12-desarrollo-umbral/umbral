'use client'

import { useState, useTransition } from 'react'
import type { SessionLifecycleState, ReleasableClueDto } from '@/app/lib/definitions'
import { releaseClueAction } from '@/app/actions/sessions'
import styles from './dashboard.module.css'

type ClueReleaseTeamOption = { teamId: string; displayName: string }

// HU-26/HU-28 operator clue release control. Self-contained (own useState/useTransition), nested in the
// operator hero. Fire-and-forget mutation whose durable effect (board reveal) lands on the mobile
// participant surface, not this dashboard — so no DashboardClient state seam and no revalidatePath.
// The clue is chosen from `releasableClues` (the active substage's still-releasable hidden clues),
// loaded by DashboardClient, so the operator never pastes a raw runtime Guid.
export function OperatorClueReleasePanel({
  liveSessionId,
  state,
  teams,
  releasableClues = [],
  onReleased,
}: {
  liveSessionId: string
  state: SessionLifecycleState
  teams: ClueReleaseTeamOption[]
  releasableClues?: ReleasableClueDto[]
  onReleased?: (label: string, count: number) => void
}) {
  const [selectedId, setSelectedId] = useState('')
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

  const selectedClue = releasableClues.find((c) => (c.targetId ?? c.clueId) === selectedId) ?? null

  function submit() {
    startTransition(async () => {
      setError(null)
      setSuccess(null)
      const clue = selectedClue
      if (!clue) return
      const result = await releaseClueAction(liveSessionId, {
        targetId: clue.targetId,
        clueId: clue.clueId,
        teamId: teamId || undefined, // omit ⇒ all teams
      })
      if ('data' in result) {
        const count = result.data.releasedTeamIds.length
        setSuccess(`Released to ${count} team${count === 1 ? '' : 's'}.`)
        setSelectedId('')
        const label = clue.targetName ?? `Pista ${clue.sequenceOrder}`
        onReleased?.(label, count)
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
        <div className={styles.panelMeta}>Reveal a hidden clue to one team or all teams.</div>
      </div>

      {releasableClues.length === 0 ? (
        <p className={styles.panelMeta} data-testid="clue-release-no-targets">
          No hidden clues are available to release in the active substage.
        </p>
      ) : (
        <>
          <div className={styles.clueFieldRow}>
            <label className={styles.clueField}>
              <span className={styles.clueFieldLabel}>Clue</span>
              <select
                className={styles.clueControl}
                data-testid="clue-release-target-select"
                value={selectedId}
                onChange={(e) => setSelectedId(e.target.value)}
              >
                <option value="">Select a clue…</option>
                {releasableClues.map((c) => {
                  const id = c.targetId ?? c.clueId!
                  const label = c.targetName ?? `Pista ${c.sequenceOrder}`
                  return (
                    <option key={id} value={id}>
                      {c.sequenceOrder}. {label}
                    </option>
                  )
                })}
              </select>
            </label>

            <label className={styles.clueField}>
              <span className={styles.clueFieldLabel}>Team</span>
              <select
                className={styles.clueControl}
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
          </div>

          {selectedClue && (
            <p className={styles.cluePreviewText} data-testid="clue-release-preview">
              {selectedClue.clueText}
            </p>
          )}

          <div className={styles.clueActions}>
            <button
              className={styles.primaryButton}
              data-testid="clue-release-submit"
              disabled={isPending || selectedId === ''}
              onClick={submit}
              type="button"
            >
              {isPending ? 'Releasing…' : 'Release'}
            </button>
          </div>
        </>
      )}

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
