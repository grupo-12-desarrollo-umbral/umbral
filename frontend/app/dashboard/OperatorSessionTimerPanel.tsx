import type { SessionTimerSnapshotDto } from '@/app/lib/definitions'
import styles from './operatorSessionTimerPanel.module.css'

type OperatorSessionTimerPanelProps = {
  timer: SessionTimerSnapshotDto | null
  isLoading: boolean
  error: string | null
}

function deriveChipLabel(timer: SessionTimerSnapshotDto): string {
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
      <div className={styles.timerPanel}>
        <div className={styles.timerLabel}>Session timer</div>
        <div className={styles.timerRow}>
          <span className={styles.timerValue} data-placeholder="true">--:--</span>
          <span className={styles.timerChip} data-tone="unavailable">Loading</span>
        </div>
        <div className={styles.progressTrack} aria-hidden="true">
          <div className={styles.progressFill} data-tone="unavailable" style={{ width: '0%' }} />
        </div>
      </div>
    )
  }

  if (error !== null || timer === null) {
    return (
      <div className={styles.timerPanel}>
        <div className={styles.timerLabel}>Session timer</div>
        <div className={styles.timerRow}>
          <span className={styles.timerValue} data-placeholder="true" aria-live="polite">--:--</span>
          <span className={styles.timerChip} data-tone="unavailable">Unavailable</span>
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
  const percent = progressPercent(timer)

  return (
    <div className={styles.timerPanel}>
      <div className={styles.timerLabel}>Session timer</div>
      <div className={styles.timerRow}>
        <span
          className={styles.timerValue}
          aria-live="polite"
          aria-label={`${formatRemaining(timer.remainingSeconds)} remaining`}
        >
          {formatRemaining(timer.remainingSeconds)}
        </span>
        <span className={styles.timerChip} data-tone={tone}>{label}</span>
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
