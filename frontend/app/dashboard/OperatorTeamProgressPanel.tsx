import type { OperatorSessionPanelDto, OperatorTeamProgressDto } from '@/app/lib/definitions'
import styles from './operatorTeamProgressPanel.module.css'

type OperatorTeamProgressPanelProps = {
  panel: OperatorSessionPanelDto | null
  unauthorized: boolean // true ⇒ not-authorized state, no team data
  error: string | null // non-null ⇒ transient/unexpected read failure (not an auth problem)
  loading: boolean
}

// Target-based progress for treasure-hunt / no-question; active-question order for trivia. Never
// clue-based. Score is rendered verbatim (session-owned or zero) — no ranking, no ledger (HU-24B).
function ProgressCell({ team }: { team: OperatorTeamProgressDto }) {
  const sub = team.activeSubstage
  if (sub && sub.playMode === 'Trivia' && sub.activeQuestionSequenceOrder !== null) {
    return (
      <span className={styles.progress} data-testid={`team-progress-question-${team.teamId}`}>
        Question {sub.activeQuestionSequenceOrder}
      </span>
    )
  }
  const resolved = sub?.resolvedTargets ?? 0
  const total = sub?.totalActiveTargets ?? 0
  return (
    <span className={styles.progress} data-testid={`team-progress-targets-${team.teamId}`}>
      {resolved}/{total} targets
    </span>
  )
}

export function OperatorTeamProgressPanel({
  panel,
  unauthorized,
  error,
  loading,
}: OperatorTeamProgressPanelProps) {
  if (unauthorized) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Session panel</div>
        <p className={styles.stateNote} role="status" data-testid="panel-unauthorized">
          You are not authorized to monitor this session.
        </p>
      </section>
    )
  }

  // A transient read failure is NOT an authorization problem — surface it as retryable, never as
  // "not authorized" (which would misinform a legitimately-assigned operator).
  if (error !== null) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Session panel</div>
        <p className={styles.stateNote} role="status" data-testid="panel-error">
          Couldn’t load session progress. It will refresh automatically.
        </p>
      </section>
    )
  }

  if (panel === null) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Session panel</div>
        <p className={styles.stateNote} data-testid="panel-no-teams">
          {loading ? 'Loading session progress…' : 'No progress data yet.'}
        </p>
      </section>
    )
  }

  return (
    <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
      <div className={styles.header}>
        <span className={styles.eyebrow} id="operator-session-panel-title">Session panel</span>
        <span className={styles.state} data-testid="panel-session-state" aria-live="polite">
          {panel.state}
        </span>
      </div>
      {panel.teamProgress.length === 0 ? (
        <p className={styles.stateNote} data-testid="panel-no-teams">No teams associated yet.</p>
      ) : (
        <ul className={styles.list}>
          {panel.teamProgress.map((team) => (
            <li key={team.teamId} className={styles.row} data-testid={`team-progress-${team.teamId}`}>
              <span className={styles.teamName}>{team.displayName}</span>
              <span className={styles.teamCode}>{team.teamCode}</span>
              <span className={styles.score} data-testid={`team-progress-score-${team.teamId}`}>
                {team.score} pts
              </span>
              <ProgressCell team={team} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
