'use client'

import { useState, useTransition } from 'react'
import type { SessionLifecycleState } from '@/app/lib/definitions'
import { addOperativeClueAction } from '@/app/actions/sessions'
import styles from './dashboard.module.css'

const MAX_CLUE_LENGTH = 500 // backend validator: AddOperativeClueCommandValidator.MaximumClueTextLength

type OperativeClueTeamOption = { teamId: string; displayName: string }

// HU-28 operator operative-clue authoring control. Self-contained (own useState/useTransition), nested
// in the operator hero after OperatorClueReleasePanel. Fire-and-forget mutation whose durable effect
// (the board reveal) lands on the mobile participant surface, not this dashboard — so no DashboardClient
// state seam and no revalidatePath. Available in Active OR Paused (unlike release's Active-only gate).
export function OperativeCluePanel({
  liveSessionId,
  state,
  teams,
  onAdded,
}: {
  liveSessionId: string
  state: SessionLifecycleState
  teams: OperativeClueTeamOption[]
  onAdded?: (count: number) => void
}) {
  const [clueText, setClueText] = useState('')
  const [teamId, setTeamId] = useState('') // '' ⇒ all teams
  const [success, setSuccess] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  // Live gate (Architecture Decision 4): available in Active OR Paused. The backend also enforces it,
  // so a stale race still surfaces as the notLive branch below — the UI never pre-empts, it just hides
  // a control that cannot work off a non-live session.
  const isLive = state === 'Active' || state === 'Paused'
  if (!isLive) {
    return (
      <section className={styles.cluePanel} data-testid="operative-clue-panel" aria-labelledby="operative-clue-title">
        <div className={styles.panelHeader}>
          <h2 id="operative-clue-title">Pista operativa</h2>
        </div>
        <p className={styles.panelMeta} data-testid="operative-clue-inactive">
          Las pistas operativas se pueden asignar una vez que la sesión esté Activa o Pausada.
        </p>
      </section>
    )
  }

  function submit() {
    startTransition(async () => {
      setError(null)
      setSuccess(null)
      // '' ⇒ all teams: send every attached team id (no omit-for-all signal on the backend).
      const teamIds = teamId ? [teamId] : teams.map((t) => t.teamId)
      const result = await addOperativeClueAction(liveSessionId, {
        clueText: clueText.trim(),
        teamIds,
      })
      if ('data' in result) {
        const count = result.data.assignedTeamIds.length
        setSuccess(`Asignada a ${count} equipo${count === 1 ? '' : 's'}.`)
        setClueText('')
        setTeamId('')
        onAdded?.(count)
      } else if ('notLive' in result) {
        setError('La sesión debe estar Activa o Pausada para asignar pistas operativas.')
      } else if ('unauthorized' in result) {
        setError('No tienes autorización para asignar pistas operativas en esta sesión.')
      } else {
        setError(result.error)
      }
    })
  }

  const canSubmit = clueText.trim() !== '' && teams.length > 0 && !isPending

  return (
    <section className={styles.cluePanel} data-testid="operative-clue-panel" aria-labelledby="operative-clue-title">
      <div className={styles.panelHeader}>
        <h2 id="operative-clue-title">Pista operativa</h2>
        <div className={styles.panelMeta}>Escribe una pista de texto libre y asígnala a un equipo o a todos.</div>
      </div>

      <label className={`${styles.clueField} ${styles.clueFieldFull}`}>
        <span className={styles.clueFieldLabel}>Texto de la pista</span>
        <textarea
          className={`${styles.clueControl} ${styles.clueTextarea}`}
          data-testid="operative-clue-text-input"
          value={clueText}
          maxLength={MAX_CLUE_LENGTH}
          onChange={(e) => setClueText(e.target.value)}
          placeholder="p. ej. Busca debajo del banner azul."
          rows={3}
        />
      </label>

      <label className={styles.clueField}>
        <span className={styles.clueFieldLabel}>Asignar a</span>
        {teams.length === 0 ? (
          <p className={styles.panelMeta}>Aún no hay equipos asociados a esta sesión.</p>
        ) : (
          <select
            className={styles.clueControl}
            data-testid="operative-clue-team-select"
            value={teamId}
            onChange={(e) => setTeamId(e.target.value)}
          >
            <option value="">Todos los equipos</option>
            {teams.map((t) => (
              <option key={t.teamId} value={t.teamId}>
                {t.displayName}
              </option>
            ))}
          </select>
        )}
      </label>

      <div className={styles.clueActions}>
        <button
          className={styles.primaryButton}
          data-testid="operative-clue-submit"
          disabled={!canSubmit}
          onClick={submit}
          type="button"
        >
          {isPending ? 'Asignando…' : 'Asignar pista'}
        </button>
      </div>

      {success && (
        <p className={styles.panelMeta} role="status" data-testid="operative-clue-success">
          {success}
        </p>
      )}
      {error && (
        <p className={styles.errorBanner} role="alert" data-testid="operative-clue-error">
          {error}
        </p>
      )}
    </section>
  )
}
