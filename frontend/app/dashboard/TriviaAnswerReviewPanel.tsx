import styles from './triviaAnswerReviewPanel.module.css'

// One row of the operator's post-close answer review board. Carries the reveal fields
// that AnsweredMonitorPanel deliberately withholds: selected option, correctness, and points.
// The nullable fields mirror the wire shape: a team that never answered arrives as explicit nulls.
export type AnswerReviewTeamRow = {
  teamId: string
  displayName: string
  teamCode: string
  selectedOptionSequenceOrder: number | null
  isCorrect: boolean | null
  scoreValue: number | null
}

type TriviaAnswerReviewPanelProps = {
  questionSequenceOrder: number | null
  teams: AnswerReviewTeamRow[]
  unauthorized: boolean
  error: string | null
  loading: boolean
}

export function TriviaAnswerReviewPanel({
  questionSequenceOrder,
  teams,
  unauthorized,
  error,
  loading,
}: TriviaAnswerReviewPanelProps) {
  if (unauthorized) {
    return (
      <section
        className={styles.panel}
        data-testid="trivia-answer-review-panel"
        aria-labelledby="trivia-answer-review-title"
      >
        <div className={styles.eyebrow} id="trivia-answer-review-title">Revisión de respuestas</div>
        <p className={styles.stateNote} role="status" data-testid="trivia-answer-review-unauthorized">
          No tienes autorización para revisar esta sesión.
        </p>
      </section>
    )
  }

  if (error !== null) {
    return (
      <section
        className={styles.panel}
        data-testid="trivia-answer-review-panel"
        aria-labelledby="trivia-answer-review-title"
      >
        <div className={styles.eyebrow} id="trivia-answer-review-title">Revisión de respuestas</div>
        <p className={styles.stateNote} role="status" data-testid="trivia-answer-review-error">
          {error}
        </p>
      </section>
    )
  }

  if (questionSequenceOrder === null) {
    return (
      <section
        className={styles.panel}
        data-testid="trivia-answer-review-panel"
        aria-labelledby="trivia-answer-review-title"
      >
        <div className={styles.eyebrow} id="trivia-answer-review-title">Revisión de respuestas</div>
        <p className={styles.stateNote} data-testid="trivia-answer-review-empty">
          {loading ? 'Cargando la revisión de respuestas…' : 'No hay ninguna pregunta cerrada para revisar.'}
        </p>
      </section>
    )
  }

  if (teams.length === 0) {
    return (
      <section
        className={styles.panel}
        data-testid="trivia-answer-review-panel"
        aria-labelledby="trivia-answer-review-title"
      >
        <div className={styles.header}>
          <span className={styles.eyebrow} id="trivia-answer-review-title" data-testid="trivia-answer-review-question">
            Pregunta {questionSequenceOrder}
          </span>
        </div>
        <p className={styles.stateNote} data-testid="trivia-answer-review-no-roster">
          Esperando la lista de equipos…
        </p>
      </section>
    )
  }

  return (
    <section
      className={styles.panel}
      data-testid="trivia-answer-review-panel"
      aria-labelledby="trivia-answer-review-title"
    >
      <div className={styles.header}>
        <span className={styles.eyebrow} id="trivia-answer-review-title" data-testid="trivia-answer-review-question">
          Pregunta {questionSequenceOrder}
        </span>
      </div>
      <ul className={styles.list}>
        {teams.map((team) => {
          const hasAnswer = team.selectedOptionSequenceOrder != null
          const badgeLabel = team.isCorrect === true
            ? 'Correcta'
            : team.isCorrect === false
              ? 'Incorrecta'
              : 'Sin respuesta'
          const badgeTone = team.isCorrect === true
            ? 'success'
            : team.isCorrect === false
              ? 'critical'
              : 'muted'

          return (
            <li
              key={team.teamId}
              className={styles.row}
              data-testid={`trivia-answer-review-row-${team.teamId}`}
              data-has-answer={hasAnswer ? 'true' : 'false'}
            >
              <span className={styles.teamName}>{team.displayName}</span>
              <span className={styles.teamCode}>{team.teamCode}</span>
              <span className={styles.option}>
                {hasAnswer ? `Opción ${team.selectedOptionSequenceOrder}` : '—'}
              </span>
              <span
                className={styles.badge}
                data-tone={badgeTone}
                data-testid={`trivia-answer-review-correct-${team.teamId}`}
              >
                {badgeLabel}
              </span>
              <span className={styles.points}>
                {team.scoreValue != null ? `${team.scoreValue} pts` : '—'}
              </span>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
