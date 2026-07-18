import type {
  OperatorSessionPanelDto,
  OperatorTeamProgressDto,
  RankingSnapshotDto,
  SessionLifecycleState,
} from '@/app/lib/definitions'
import { lifecycleStateLabel } from '@/app/lib/session-lifecycle'
import styles from './operatorTeamProgressPanel.module.css'

type OperatorTeamProgressPanelProps = {
  panel: OperatorSessionPanelDto | null
  ranking: RankingSnapshotDto | null
  unauthorized: boolean // true ⇒ not-authorized state, no team data
  error: string | null // non-null ⇒ transient/unexpected read failure (not an auth problem)
  loading: boolean
}

// Target-based progress for treasure-hunt / no-question; active-question order for trivia — never
// clue-based. Score comes from scoring-monitoring's ranking, keyed by cross-context referenceTeamId.
// A separate released-clue tally (manual releases + active-substage initial clues) rides alongside.
function ProgressCell({ team }: { team: OperatorTeamProgressDto }) {
  const sub = team.activeSubstage
  if (sub && sub.playMode === 'Trivia' && sub.activeQuestionSequenceOrder !== null) {
    return (
      <span className={styles.progress} data-testid={`team-progress-question-${team.teamId}`}>
        Pregunta {sub.activeQuestionSequenceOrder}
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
  ranking,
  unauthorized,
  error,
  loading,
}: OperatorTeamProgressPanelProps) {
  if (unauthorized) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Panel de la sesión</div>
        <p className={styles.stateNote} role="status" data-testid="panel-unauthorized">
          No tienes autorización para monitorear esta sesión.
        </p>
      </section>
    )
  }

  // A transient read failure is NOT an authorization problem — surface it as retryable, never as
  // "not authorized" (which would misinform a legitimately-assigned operator).
  if (error !== null) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Panel de la sesión</div>
        <p className={styles.stateNote} role="status" data-testid="panel-error">
          No se pudo cargar el progreso de la sesión. Se actualizará automáticamente.
        </p>
      </section>
    )
  }

  if (panel === null) {
    return (
      <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
        <div className={styles.eyebrow} id="operator-session-panel-title">Panel de la sesión</div>
        <p className={styles.stateNote} data-testid="panel-no-teams">
          {loading ? 'Cargando el progreso de la sesión…' : 'Aún no hay datos de progreso.'}
        </p>
      </section>
    )
  }

  // Released-clue rollup: how many teams have at least one clue visible (the "to how many teams"
  // dimension the per-team tally naturally rolls up to). No distinct-clue total — the same initial
  // clue counts once per team, so summing would double-count.
  const teamsWithClues = panel.teamProgress.filter((team) => team.releasedClueCount > 0).length
  // `null` means ranking has not loaded, so retain the session projection as a temporary fallback.
  // Once an authoritative snapshot exists, a missing row means no score entries yet: zero points.
  const rankingScores = ranking === null
    ? null
    : new Map(ranking.rows.map((row) => [row.teamId, row.totalScore]))

  return (
    <section className={styles.panel} data-testid="operator-session-panel" aria-labelledby="operator-session-panel-title">
      <div className={styles.header}>
        <span className={styles.eyebrow} id="operator-session-panel-title">Panel de la sesión</span>
        <span className={styles.state} data-testid="panel-session-state" aria-live="polite">
          {lifecycleStateLabel[panel.state as SessionLifecycleState] ?? panel.state}
        </span>
      </div>
      {teamsWithClues > 0 && (
        <p className={styles.stateNote} data-testid="panel-clue-rollup">
          Pistas liberadas a {teamsWithClues} equipo{teamsWithClues === 1 ? '' : 's'}.
        </p>
      )}
      {panel.teamProgress.length === 0 ? (
        <p className={styles.stateNote} data-testid="panel-no-teams">Aún no hay equipos asociados.</p>
      ) : (
        <ul className={styles.list}>
          {panel.teamProgress.map((team) => {
            const score = rankingScores === null || team.referenceTeamId === null
              ? team.score
              : rankingScores.get(team.referenceTeamId) ?? 0

            return (
            <li key={team.teamId} className={styles.row} data-testid={`team-progress-${team.teamId}`}>
              <div className={styles.rowMain}>
                <span className={styles.teamName}>{team.displayName}</span>
                <span className={styles.teamCode}>{team.teamCode}</span>
                {team.releasedClueCount > 0 && (
                  <span className={styles.clues} data-testid={`team-progress-clues-${team.teamId}`}>
                    {team.releasedClueCount} pista{team.releasedClueCount === 1 ? '' : 's'}
                  </span>
                )}
                <span className={styles.score} data-testid={`team-progress-score-${team.teamId}`}>
                  {score} pts
                </span>
                <ProgressCell team={team} />
              </div>
              {team.activeSubstage?.title ? (
                <span
                  className={styles.substageTitle}
                  data-testid={`team-progress-substage-${team.teamId}`}
                >
                  {team.activeSubstage.title}
                </span>
              ) : null}
            </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
