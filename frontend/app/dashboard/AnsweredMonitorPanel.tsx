import styles from './answeredMonitorPanel.module.css'

// One row of the operator's pre-close answered/not-answered board. Carries ONLY leak-safe fields:
// there is no selected-option / correctness / points binding here by design (HU-36A no-leak invariant).
export type AnsweredTeamRow = {
  runtimeTeamId: string
  displayName: string
  teamCode: string
  answered: boolean
  answeredAt: string | null
}

type AnsweredMonitorPanelProps = {
  activeQuestionOrder: number | null // null ⇒ no active trivia question → empty state
  teams: AnsweredTeamRow[]
  unauthorized: boolean // true ⇒ render not-authorized state, render no team data
  error: string | null // non-null ⇒ transient/unexpected read failure → error state (not an auth problem)
  loading: boolean
}

export function AnsweredMonitorPanel({
  activeQuestionOrder,
  teams,
  unauthorized,
  error,
  loading,
}: AnsweredMonitorPanelProps) {
  if (unauthorized) {
    return (
      <section
        className={styles.panel}
        data-testid="answered-monitor-panel"
        aria-labelledby="answered-monitor-title"
      >
        <div className={styles.eyebrow} id="answered-monitor-title">Answered monitor</div>
        <p className={styles.stateNote} role="status" data-testid="answered-monitor-unauthorized">
          You are not authorized to monitor this session.
        </p>
      </section>
    )
  }

  // A transient read failure is NOT an authorization problem — surface it as a retryable error,
  // never as "not authorized" (which would misinform a legitimately-assigned operator).
  if (error !== null) {
    return (
      <section
        className={styles.panel}
        data-testid="answered-monitor-panel"
        aria-labelledby="answered-monitor-title"
      >
        <div className={styles.eyebrow} id="answered-monitor-title">Answered monitor</div>
        <p className={styles.stateNote} role="status" data-testid="answered-monitor-error">
          Couldn’t load answered status. It will refresh automatically.
        </p>
      </section>
    )
  }

  if (activeQuestionOrder === null) {
    return (
      <section
        className={styles.panel}
        data-testid="answered-monitor-panel"
        aria-labelledby="answered-monitor-title"
      >
        <div className={styles.eyebrow} id="answered-monitor-title">Answered monitor</div>
        <p className={styles.stateNote} data-testid="answered-monitor-empty">
          {loading ? 'Loading answered status…' : 'No active trivia question.'}
        </p>
      </section>
    )
  }

  // A question IS active but no team roster is known yet (snapshot not seeded / no teams associated).
  // Distinct from "no active question" so the operator isn't told the question is gone.
  if (teams.length === 0) {
    return (
      <section
        className={styles.panel}
        data-testid="answered-monitor-panel"
        aria-labelledby="answered-monitor-title"
      >
        <div className={styles.header}>
          <span className={styles.eyebrow} id="answered-monitor-title" data-testid="answered-monitor-active-question">
            Question {activeQuestionOrder}
          </span>
        </div>
        <p className={styles.stateNote} data-testid="answered-monitor-no-roster">
          Waiting for the team roster…
        </p>
      </section>
    )
  }

  const answeredCount = teams.reduce((total, team) => total + (team.answered ? 1 : 0), 0)

  return (
    <section
      className={styles.panel}
      data-testid="answered-monitor-panel"
      aria-labelledby="answered-monitor-title"
    >
      <div className={styles.header}>
        <span className={styles.eyebrow} id="answered-monitor-title" data-testid="answered-monitor-active-question">
          Question {activeQuestionOrder}
        </span>
        <span className={styles.count} data-testid="answered-monitor-count" aria-live="polite">
          {answeredCount} / {teams.length} answered
        </span>
      </div>
      <ul className={styles.list}>
        {teams.map((team) => (
          <li
            key={team.runtimeTeamId}
            className={styles.row}
            data-testid={`team-answer-status-${team.runtimeTeamId}`}
            data-answered={team.answered ? 'true' : 'false'}
          >
            <span className={styles.teamName}>{team.displayName}</span>
            <span className={styles.teamCode}>{team.teamCode}</span>
            <span className={styles.status} data-answered={team.answered ? 'true' : 'false'}>
              {team.answered ? 'Answered' : 'Not answered yet'}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}
