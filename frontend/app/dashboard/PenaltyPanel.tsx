'use client'

import { useState, useTransition } from 'react'
import type { SessionLifecycleState } from '@/app/lib/definitions'
import { applyPenaltyAction } from '@/app/actions/sessions'
import styles from './dashboard.module.css'

type PenaltyTeamOption = { teamId: string; displayName: string }

// HU-38 operator justified-penalty control. Self-contained (own useState/useTransition), nested in
// the operator hero beside OperativeCluePanel. The applied penalty is an append-only ScoreEntry
// deduction: the success note confirms THE ENTRY (−amount), it is not a team total, and this
// component never does arithmetic on a score.
export function PenaltyPanel({
  liveSessionId,
  state,
  teams,
  onApplied,
}: {
  liveSessionId: string
  state: SessionLifecycleState
  teams: PenaltyTeamOption[]
  onApplied?: (teamId: string, amount: number) => void
}) {
  const [teamId, setTeamId] = useState('')
  const [reason, setReason] = useState('')
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  // Live gate: a penalty only makes sense against a running session. Mirrors OperativeCluePanel —
  // the UI hides a control that cannot work, but never pre-empts a backend rejection.
  const isLive = state === 'Active' || state === 'Paused'
  if (!isLive) {
    return (
      <section className={styles.cluePanel} data-testid="penalty-panel" aria-labelledby="penalty-title">
        <div className={styles.panelHeader}>
          <h2 id="penalty-title">Penalty</h2>
        </div>
        <p className={styles.panelMeta} data-testid="penalty-inactive">
          Penalties can be applied once the session is Active or Paused.
        </p>
      </section>
    )
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      setSuccess(null)
      const result = await applyPenaltyAction(liveSessionId, { teamId, reason: reason.trim() })
      if ('data' in result) {
        // penaltyAmount is a positive magnitude; the deduction is the entry's type. Render the sign.
        setSuccess(`Penalty applied: −${result.data.penaltyAmount} pts.`)
        setReason('')
        setTeamId('')
        onApplied?.(result.data.teamId, result.data.penaltyAmount)
      } else if ('invalidReason' in result) {
        setError('A penalty requires an explicit reason.')
      } else if ('unauthorized' in result) {
        setError('You are not authorized to penalize teams in this session.')
      } else {
        setError(result.error)
      }
    })
  }

  // Reason-required: the backend validator is authoritative, this is the UX gate in front of it.
  const canSubmit = reason.trim() !== '' && teamId !== '' && !isPending

  return (
    <section className={styles.cluePanel} data-testid="penalty-panel" aria-labelledby="penalty-title">
      <div className={styles.panelHeader}>
        <h2 id="penalty-title">Penalty</h2>
        <div className={styles.panelMeta}>Deduct points from a team, with a justification.</div>
      </div>

      <label className={styles.clueField}>
        <span className={styles.clueFieldLabel}>Team</span>
        {teams.length === 0 ? (
          <p className={styles.panelMeta}>No teams are attached to this session yet.</p>
        ) : (
          <select
            className={styles.clueControl}
            data-testid="penalty-team-select"
            value={teamId}
            onChange={(e) => setTeamId(e.target.value)}
          >
            <option value="">Select a team…</option>
            {teams.map((t) => (
              <option key={t.teamId} value={t.teamId}>
                {t.displayName}
              </option>
            ))}
          </select>
        )}
      </label>

      <label className={`${styles.clueField} ${styles.clueFieldFull}`}>
        <span className={styles.clueFieldLabel}>Reason</span>
        <textarea
          className={`${styles.clueControl} ${styles.clueTextarea}`}
          data-testid="penalty-reason-input"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="e.g. Team used a phone during a no-device substage."
          rows={3}
        />
      </label>

      <div className={styles.clueActions}>
        <button
          className={styles.primaryButton}
          data-testid="penalty-submit"
          disabled={!canSubmit}
          onClick={submit}
          type="button"
        >
          {isPending ? 'Applying…' : 'Apply penalty'}
        </button>
      </div>

      {success && (
        <p className={styles.panelMeta} role="status" data-testid="penalty-success">
          {success}
        </p>
      )}
      {error && (
        <p className={styles.errorBanner} role="alert" data-testid="penalty-error">
          {error}
        </p>
      )}
    </section>
  )
}
