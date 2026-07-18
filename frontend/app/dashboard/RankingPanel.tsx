import type { RankingSnapshotDto } from '@/app/lib/definitions'
import { formatResolutionTime } from './resolution-time'
import styles from './rankingPanel.module.css'

type RankingPanelProps = {
  snapshot: RankingSnapshotDto | null
  unauthorized: boolean // true ⇒ not-authorized state, no ranking data
  error: string | null // non-null ⇒ transient/unexpected read failure (not an auth problem)
  loading: boolean
  live: boolean // false ⇒ the scoring hub is not delivering; the snapshot may be stale
}

// HU-24B live standings from the scoring-monitoring ledger — the real score, unlike the session-owned
// zero in OperatorTeamProgressPanel. Fed by the RankingChanged push with a REST snapshot behind it.
// RB-08: rows are rendered in backend order and labelled with the backend's Position. Never re-sort
// here — the desc-score / resolution-time tiebreak is the backend policy's call, and a client-side
// sort would silently diverge from it.
export function RankingPanel({ snapshot, unauthorized, error, loading, live }: RankingPanelProps) {
  if (unauthorized) {
    return (
      <section className={styles.panel} data-testid="ranking-panel" aria-labelledby="ranking-panel-title">
        <div className={styles.eyebrow} id="ranking-panel-title">Ranking</div>
        <p className={styles.stateNote} role="status" data-testid="ranking-unauthorized">
          No tienes autorización para ver el ranking de esta sesión.
        </p>
      </section>
    )
  }

  // A transient read failure is NOT an authorization problem — mirrors OperatorTeamProgressPanel.
  if (error !== null) {
    return (
      <section className={styles.panel} data-testid="ranking-panel" aria-labelledby="ranking-panel-title">
        <div className={styles.eyebrow} id="ranking-panel-title">Ranking</div>
        <p className={styles.stateNote} role="status" data-testid="ranking-error">
          No se pudo cargar el ranking. Se actualizará automáticamente.
        </p>
      </section>
    )
  }

  if (snapshot === null) {
    return (
      <section className={styles.panel} data-testid="ranking-panel" aria-labelledby="ranking-panel-title">
        <div className={styles.eyebrow} id="ranking-panel-title">Ranking</div>
        <p className={styles.stateNote} data-testid="ranking-empty">
          {loading ? 'Cargando el ranking…' : 'Aún no hay posiciones.'}
        </p>
      </section>
    )
  }

  return (
    <section className={styles.panel} data-testid="ranking-panel" aria-labelledby="ranking-panel-title">
      <div className={styles.header}>
        <span className={styles.eyebrow} id="ranking-panel-title">Ranking</span>
        {!live && (
          // The REST snapshot is present but the live scoring channel is down, so these standings
          // can lag a penalty/award until it recovers. Say so rather than passing stale rows off as live.
          <span className={styles.stateNote} role="status" data-testid="ranking-live-paused">
            Actualizaciones en vivo pausadas — reconectando.
          </span>
        )}
      </div>
      {snapshot.rows.length === 0 ? (
        // The backend's well-known empty snapshot: a live session simply has no score entries yet.
        <p className={styles.stateNote} data-testid="ranking-empty">Aún no hay posiciones.</p>
      ) : (
        <ul className={styles.list} aria-live="polite">
          {snapshot.rows.map((row) => (
            <li key={row.teamId} className={styles.row} data-testid={`ranking-row-${row.teamId}`}>
              <span className={styles.position} data-testid={`ranking-position-${row.teamId}`}>
                {row.position}
              </span>
              <span className={styles.teamName}>{row.teamDisplayName}</span>
              {row.resolutionTime !== null && (
                <span className={styles.resolutionTime} data-testid={`ranking-time-${row.teamId}`}>
                  {formatResolutionTime(row.resolutionTime)}
                </span>
              )}
              <span className={styles.score} data-testid={`ranking-score-${row.teamId}`}>
                {row.totalScore} pts
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
