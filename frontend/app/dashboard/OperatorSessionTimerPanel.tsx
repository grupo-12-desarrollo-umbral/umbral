import type { SessionTimerSnapshotDto } from '@/app/lib/definitions'
import styles from './operatorSessionTimerPanel.module.css'

type OperatorSessionTimerPanelProps = {
  timer: SessionTimerSnapshotDto | null
  isLoading: boolean
  error: string | null
}

// The authoritative clock is the active trivia question window. No active question
// (treasure-hunt substage or between questions) means there is no countdown.
function deriveChipLabel(timer: SessionTimerSnapshotDto): string {
  if (timer.activeQuestion === null) return 'No question'
  if (timer.isExpired || timer.timerStatus === 'Expired') return 'Expired'
  if (timer.timerStatus === 'Advancing') return 'Running'
  const preStart = timer.sessionState === 'Scheduled' || timer.sessionState === 'Preparing'
  return preStart ? 'Not started' : 'Paused'
}

function formatRemaining(seconds: number): string {
  const s = Math.max(0, seconds)
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`
  return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`
}

function progressPercent(timer: SessionTimerSnapshotDto): number {
  if (timer.totalSeconds <= 0) return 0
  return Math.min(100, Math.max(0, (timer.remainingSeconds / timer.totalSeconds) * 100))
}

function chipTone(label: string): 'running' | 'frozen' | 'expired' | 'unavailable' {
  if (label === 'Running') return 'running'
  if (label === 'Expired') return 'expired'
  if (label === 'Not started' || label === 'Paused') return 'frozen'
  return 'unavailable'
}

export function OperatorSessionTimerPanel({ timer, isLoading, error }: OperatorSessionTimerPanelProps) {
  if (isLoading) {
    return (
      <div className={styles.timerPanel} data-testid="session-timer-panel">
        <div className={styles.timerLabel}>Question timer</div>
        <div className={styles.timerRow}>
          <span className={styles.timerValue} data-placeholder="true">--:--</span>
          <span className={styles.timerChip} data-tone="unavailable" data-testid="timer-chip">Loading</span>
        </div>
        <div className={styles.progressTrack} aria-hidden="true">
          <div className={styles.progressFill} data-tone="unavailable" style={{ width: '0%' }} />
        </div>
      </div>
    )
  }

  if (error !== null || timer === null) {
    return (
      <div className={styles.timerPanel} data-testid="session-timer-panel">
        <div className={styles.timerLabel}>Question timer</div>
        <div className={styles.timerRow}>
          <span className={styles.timerValue} data-placeholder="true" aria-live="polite">--:--</span>
          <span className={styles.timerChip} data-tone="unavailable" data-testid="timer-chip">Unavailable</span>
        </div>
        {error && <div className={styles.errorMessage}>{error}</div>}
        <div className={styles.progressTrack} aria-hidden="true">
          <div className={styles.progressFill} data-tone="unavailable" style={{ width: '0%' }} />
        </div>
      </div>
    )
  }

  const label = deriveChipLabel(timer)
  const tone = chipTone(label)

  // No active trivia question → no countdown (treasure-hunt substage or between questions).
  if (timer.activeQuestion === null) {
    return (
      <div className={styles.timerPanel} data-testid="session-timer-panel">
        <div className={styles.timerLabel}>Question timer</div>
        <div className={styles.timerRow} data-testid="timer-no-countdown">
          <span className={styles.timerValue} data-placeholder="true">--:--</span>
          <span className={styles.timerChip} data-tone={tone} data-testid="timer-chip">{label}</span>
        </div>
        <div className={styles.noCountdownNote}>No active question</div>
      </div>
    )
  }

  const percent = progressPercent(timer)

  return (
    <div className={styles.timerPanel} data-testid="session-timer-panel">
      <div className={styles.timerLabel}>Question timer</div>
      <div className={styles.timerRow}>
        <span
          className={styles.timerValue}
          data-testid="timer-remaining"
          aria-live="polite"
          aria-label={`${formatRemaining(timer.remainingSeconds)} remaining`}
        >
          {formatRemaining(timer.remainingSeconds)}
        </span>
        <span className={styles.timerChip} data-tone={tone} data-testid="timer-chip">{label}</span>
      </div>
      <div
        className={styles.progressTrack}
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        aria-label="Timer progress"
      >
        <div className={styles.progressFill} data-tone={tone} style={{ width: `${percent}%` }} />
      </div>
    </div>
  )
}

// The whole-mission countdown (D-4), a separate clock from the active-question window above: it keeps
// running through treasure-hunt substages and the gaps between questions, where the question timer
// reads "No active question". Null mission fields mean the deadline is not seeded yet (pre-start).
function deriveMissionChipLabel(timer: SessionTimerSnapshotDto, remainingSeconds: number): string {
  // Terminal states win over the clock: a session that finished before its deadline still has time
  // on the mission timer, but it is over — it must read "Ended", not "Running".
  if (timer.sessionState === 'Finished' || timer.sessionState === 'Cancelled') return 'Ended'
  if (remainingSeconds <= 0) return 'Expired'
  if (timer.sessionState === 'Scheduled' || timer.sessionState === 'Preparing') return 'Not started'
  if (timer.sessionState === 'Paused') return 'Paused'
  return 'Running'
}

export function MissionSessionTimerPanel({ timer, isLoading, error }: OperatorSessionTimerPanelProps) {
  if (isLoading) {
    return (
      <div className={styles.timerPanel} data-testid="mission-timer-panel">
        <div className={styles.timerLabel}>Mission timer</div>
        <div className={styles.timerRow}>
          <span className={styles.timerValue} data-placeholder="true">--:--</span>
          <span className={styles.timerChip} data-tone="unavailable" data-testid="mission-timer-chip">Loading</span>
        </div>
      </div>
    )
  }

  // No snapshot, a read error, or a deadline not yet seeded (pre-start) all mean there is no mission
  // clock to show — render the same unavailable placeholder rather than a misleading 00:00.
  const hasMission =
    timer !== null &&
    timer.missionRemainingSeconds != null &&
    timer.missionTotalSeconds != null

  if (error !== null || !hasMission) {
    return (
      <div className={styles.timerPanel} data-testid="mission-timer-panel">
        <div className={styles.timerLabel}>Mission timer</div>
        <div className={styles.timerRow} data-testid="mission-timer-no-countdown">
          <span className={styles.timerValue} data-placeholder="true" aria-live="polite">--:--</span>
          <span className={styles.timerChip} data-tone="unavailable" data-testid="mission-timer-chip">Unavailable</span>
        </div>
        {error && <div className={styles.errorMessage}>{error}</div>}
      </div>
    )
  }

  const remaining = timer.missionRemainingSeconds as number
  const total = timer.missionTotalSeconds as number
  const label = deriveMissionChipLabel(timer, remaining)
  const tone = chipTone(label)
  const percent = total <= 0 ? 0 : Math.min(100, Math.max(0, (remaining / total) * 100))

  return (
    <div className={styles.timerPanel} data-testid="mission-timer-panel">
      <div className={styles.timerLabel}>Mission timer</div>
      <div className={styles.timerRow}>
        <span
          className={styles.timerValue}
          data-testid="mission-timer-remaining"
          aria-live="polite"
          aria-label={`${formatRemaining(remaining)} remaining in the mission`}
        >
          {formatRemaining(remaining)}
        </span>
        <span className={styles.timerChip} data-tone={tone} data-testid="mission-timer-chip">{label}</span>
      </div>
      <div
        className={styles.progressTrack}
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        aria-label="Mission timer progress"
      >
        <div className={styles.progressFill} data-tone={tone} style={{ width: `${percent}%` }} />
      </div>
    </div>
  )
}
