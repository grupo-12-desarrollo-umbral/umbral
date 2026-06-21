'use client'

import { useCallback, useEffect, useState, useTransition } from 'react'
import type { MissionDto, MissionReadinessDto } from '@/app/lib/definitions'
import { getMissionReadiness } from '@/app/actions/mission-structure'
import { activateMission } from '@/app/actions/missions'
import styles from '../dashboard.module.css'

export function ActivationBar({
  mission,
  onMutated,
}: {
  mission: MissionDto
  onMutated: (updated: MissionDto) => void
}) {
  const [readiness, setReadiness] = useState<MissionReadinessDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPending, startTransition] = useTransition()

  const refresh = useCallback(() => {
    getMissionReadiness(mission.id)
      .then(setReadiness)
      .catch(() => setReadiness(null))
  }, [mission.id])

  // Re-fetch readiness whenever the mission changes (every mutation replaces it
  // wholesale via onMutated), so the failure list and gate stay in step with the tree.
  useEffect(() => {
    refresh()
  }, [refresh, mission])

  const isReady = readiness?.isReady ?? false
  const failures = readiness?.failures ?? []
  // A mission only needs the activation affordance while it is still a Draft.
  const showActivate = mission.activationState === 'Draft'

  function activate() {
    startTransition(async () => {
      setError(null)
      try {
        const updated = await activateMission(mission.id)
        onMutated(updated)
        refresh()
      } catch (e) {
        // Readiness 400 and already-active 409 both arrive as a verbatim message.
        setError(e instanceof Error ? e.message : 'Activation failed. Try again.')
      }
    })
  }

  return (
    <div className={styles.activationBar} data-testid="activation-bar">
      {failures.length > 0 && (
        <ul className={styles.readinessFailures}>
          {failures.map((failure, index) => (
            <li key={index} role="alert" data-testid="readiness-failure">
              {failure}
            </li>
          ))}
        </ul>
      )}

      {showActivate && (
        <button
          className={styles.primaryButton}
          data-testid="activate-mission-btn"
          disabled={isPending || !isReady}
          onClick={activate}
          type="button"
        >
          Activate
        </button>
      )}

      {error && (
        <p className={styles.formError} role="alert" data-testid="mission-error">
          {error}
        </p>
      )}
    </div>
  )
}
