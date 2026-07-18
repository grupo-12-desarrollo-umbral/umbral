import type { SessionActivityEntry } from './session-activity'
import styles from './sessionActivityPanel.module.css'

type SessionActivityPanelProps = {
  entries: SessionActivityEntry[] // newest first (the reducer prepends); this panel does not re-sort
  teamNames: Record<string, string> // runtime teamId → display name, from the operator panel rollup
}

function formatTime(iso: string): string {
  const parsed = new Date(iso)
  if (Number.isNaN(parsed.getTime())) return ''
  return parsed.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

// Operator live activity feed. Unlike SessionHistoryPanel (a REST audit read), this is a pure live
// projection of the SignalR pushes the operator client already handles — so it has no loading /
// unauthorized / error states: it is empty until the first event lands, then grows newest-first.
export function SessionActivityPanel({ entries, teamNames }: SessionActivityPanelProps) {
  return (
    <section className={styles.panel} data-testid="session-activity-panel" aria-labelledby="session-activity-panel-title">
      <div className={styles.header}>
        <span className={styles.eyebrow} id="session-activity-panel-title">Actividad en vivo</span>
      </div>
      {entries.length === 0 ? (
        <p className={styles.stateNote} data-testid="session-activity-empty">
          Aún no hay actividad en vivo. Los eventos aparecen aquí a medida que avanza la sesión.
        </p>
      ) : (
        <ul className={styles.list}>
          {entries.map((entry) => (
            <li key={entry.id} className={styles.row} data-testid={`session-activity-row-${entry.id}`}>
              <span className={styles.badge} data-kind={entry.kind}>{entry.label}</span>
              <span className={styles.details}>
                <span className={styles.teamName}>
                  {/* A null teamId is a session-wide event that belongs to no single team. Only an
                      unresolved id (team not in the panel rollup) is genuinely unknown. */}
                  {entry.teamId === null
                    ? 'Toda la sesión'
                    : teamNames[entry.teamId] ?? 'Equipo desconocido'}
                </span>
                <span className={styles.summary} data-testid={`session-activity-summary-${entry.id}`}>
                  {entry.summary}
                </span>
              </span>
              <span className={styles.occurredAt} data-testid={`session-activity-time-${entry.id}`}>
                {formatTime(entry.at)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
