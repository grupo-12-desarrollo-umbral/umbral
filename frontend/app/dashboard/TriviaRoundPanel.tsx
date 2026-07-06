import type {
  QuestionActivatedNotificationDto,
  TriviaRoundPhase,
} from '@/app/lib/definitions'
import styles from './triviaRoundPanel.module.css'

type TriviaRoundPanelProps = {
  phase: TriviaRoundPhase
  pregameSecondsLeft: number | null
  activeQuestion: QuestionActivatedNotificationDto | null
  questionSecondsLeft: number | null
  substageOrdinal: number
  finalizing: boolean
}

function barTone(percent: number): 'normal' | 'warning' | 'critical' {
  if (percent < 25) return 'critical'
  if (percent < 50) return 'warning'
  return 'normal'
}

export function TriviaRoundPanel({
  phase,
  pregameSecondsLeft,
  activeQuestion,
  questionSecondsLeft,
  substageOrdinal,
  finalizing,
}: TriviaRoundPanelProps) {
  if (phase === 'idle') {
    return null
  }

  if (phase === 'pregame') {
    const seconds = pregameSecondsLeft ?? 0
    const percent = Math.min(100, Math.max(0, (seconds / 5) * 100))
    return (
      <div className={styles.panel} data-testid="trivia-round-panel" data-phase="pregame">
        <div className={styles.eyebrow}>Get ready</div>
        <div className={styles.pregameNumeral} aria-live="assertive" aria-label={`Starting in ${seconds}`}>
          {seconds}
        </div>
        <div
          className={styles.bar}
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={percent}
          aria-label="Pre-game countdown"
        >
          <div className={styles.barFill} data-tone="normal" style={{ width: `${percent}%` }} />
        </div>
      </div>
    )
  }

  if (phase === 'question-active' && activeQuestion) {
    const total = activeQuestion.timeLimitSeconds
    const left = questionSecondsLeft ?? 0
    const percent = total > 0 ? Math.min(100, Math.max(0, (left / total) * 100)) : 0
    const tone = barTone(percent)
    return (
      <div className={styles.panel} data-testid="trivia-round-panel" data-phase="question-active">
        <div className={styles.questionHeader}>
          <span className={styles.eyebrow}>Substage {substageOrdinal} · Question {activeQuestion.sequenceOrder}</span>
          <span className={styles.timeLeft} data-tone={tone}>
            {left}s
          </span>
        </div>
        <p className={styles.prompt} aria-live="polite">
          {activeQuestion.prompt}
        </p>
        <div
          className={styles.bar}
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={percent}
          aria-label="Time remaining for this question"
        >
          <div className={styles.barFill} data-tone={tone} style={{ width: `${percent}%` }} />
        </div>
      </div>
    )
  }

  if (phase === 'substage-advancing') {
    return (
      <div className={styles.panel} data-testid="trivia-round-panel" data-phase="substage-advancing">
        <div className={styles.transition} aria-live="polite">
          <span className={styles.transitionLine} aria-hidden="true" />
          <span className={styles.transitionText}>
            {finalizing ? 'Final substage complete — finishing session…' : 'Advancing to the next substage…'}
          </span>
          <span className={styles.transitionLine} aria-hidden="true" />
        </div>
      </div>
    )
  }

  if (phase === 'complete') {
    return (
      <div className={styles.panel} data-testid="trivia-round-panel" data-phase="complete">
        <div className={styles.eyebrow}>Session complete</div>
        <p className={styles.prompt} aria-live="polite">All substages finished. This session is complete.</p>
      </div>
    )
  }

  // between-questions
  return (
    <div className={styles.panel} data-testid="trivia-round-panel" data-phase="between-questions">
      <div className={styles.transition} aria-live="polite">
        <span className={styles.transitionLine} aria-hidden="true" />
        <span className={styles.transitionText}>Next question loading…</span>
        <span className={styles.transitionLine} aria-hidden="true" />
      </div>
    </div>
  )
}
