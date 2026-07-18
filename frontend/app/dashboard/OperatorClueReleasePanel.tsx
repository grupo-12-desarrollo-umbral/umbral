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
          <h2 id="clue-release-title">Liberar pista</h2>
        </div>
        <p className={styles.panelMeta} data-testid="clue-release-inactive">
          La liberación de pistas está disponible una vez que la sesión esté Activa.
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
        setSuccess(`Liberada a ${count} equipo${count === 1 ? '' : 's'}.`)
        setSelectedId('')
        const label = clue.targetName ?? `Pista ${clue.sequenceOrder}`
        onReleased?.(label, count)
      } else if ('duplicate' in result) {
        setError('Esa pista ya fue liberada a ese equipo para este target.')
      } else if ('notReleasable' in result) {
        setError('Ese target no tiene ninguna pista oculta liberable en la subetapa activa.')
      } else if ('notActive' in result) {
        setError('La sesión debe estar Activa para liberar pistas.')
      } else if ('unauthorized' in result) {
        setError('No tienes autorización para liberar pistas en esta sesión.')
      } else {
        setError(result.error)
      }
    })
  }

  return (
    <section className={styles.cluePanel} data-testid="clue-release-panel" aria-labelledby="clue-release-title">
      <div className={styles.panelHeader}>
        <h2 id="clue-release-title">Liberar pista</h2>
        <div className={styles.panelMeta}>Revela una pista oculta a un equipo o a todos los equipos.</div>
      </div>

      {releasableClues.length === 0 ? (
        <p className={styles.panelMeta} data-testid="clue-release-no-targets">
          No hay pistas ocultas disponibles para liberar en la subetapa activa.
        </p>
      ) : (
        <>
          <div className={styles.clueFieldRow}>
            <label className={styles.clueField}>
              <span className={styles.clueFieldLabel}>Pista</span>
              <select
                className={styles.clueControl}
                data-testid="clue-release-target-select"
                value={selectedId}
                onChange={(e) => setSelectedId(e.target.value)}
              >
                <option value="">Selecciona una pista…</option>
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
              <span className={styles.clueFieldLabel}>Equipo</span>
              <select
                className={styles.clueControl}
                data-testid="clue-release-team-select"
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
              {isPending ? 'Liberando…' : 'Liberar'}
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
