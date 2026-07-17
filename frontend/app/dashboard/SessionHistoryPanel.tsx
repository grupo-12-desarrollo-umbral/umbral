import type { SessionHistoryRowDto } from '@/app/lib/definitions'
import styles from './sessionHistoryPanel.module.css'

type SessionHistoryPanelProps = {
  events: SessionHistoryRowDto[] // server-ordered (oldest first); this panel does not re-sort
  teamNames: Record<string, string> // runtime teamId → display name, from the operator panel rollup
  unauthorized: boolean // true ⇒ not-authorized state, no history data
  error: string | null // non-null ⇒ transient/unexpected read failure (not an auth problem)
  loading: boolean
  // Only for a mount with no automatic refresh behind it: supplying this swaps the "will refresh
  // automatically" promise for a control that actually can. Omit it where a reconnect refetches.
  onRetry?: () => void
}

// Spaces out the PascalCase event types the backend records ('SessionStateChanged' → 'Session state
// changed'). eventType is open-ended, so this formats whatever arrives rather than mapping a closed set.
function formatEventType(eventType: string): string {
  const spaced = eventType.replace(/([a-z0-9])([A-Z])/g, '$1 $2')
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase()
}

function formatOccurredAt(iso: string): string {
  const parsed = new Date(iso)
  if (Number.isNaN(parsed.getTime())) return ''
  return parsed.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

// RF-15 session audit history, fed by the REST read only — no SignalR push backs this projection, so
// there is no live/paused state to report: the list is a point-in-time read, refreshed on session select.
// Renders in the server's order (SessionEventHistoryRepository orders by OccurredAt ascending): an audit
// trail reads oldest-first, unlike EvidenceSubmissionsPanel's newest-first live feed.
export function SessionHistoryPanel({
  events,
  teamNames,
  unauthorized,
  error,
  loading,
  onRetry,
}: SessionHistoryPanelProps) {
  if (unauthorized) {
    return (
      <section className={styles.panel} data-testid="session-history-panel" aria-labelledby="session-history-panel-title">
        <div className={styles.eyebrow} id="session-history-panel-title">History</div>
        <p className={styles.stateNote} role="status" data-testid="session-history-unauthorized">
          You are not authorized to view this session’s history.
        </p>
      </section>
    )
  }

  // A transient read failure is NOT an authorization problem — mirrors RankingPanel.
  if (error !== null) {
    return (
      <section className={styles.panel} data-testid="session-history-panel" aria-labelledby="session-history-panel-title">
        <div className={styles.eyebrow} id="session-history-panel-title">History</div>
        <p className={styles.stateNote} role="status" data-testid="session-history-error">
          {onRetry === undefined
            ? 'Couldn’t load the session history. It will refresh automatically.'
            : 'Couldn’t load the session history.'}
        </p>
        {onRetry !== undefined && (
          <button
            type="button"
            className={styles.retryButton}
            onClick={onRetry}
            disabled={loading}
            data-testid="session-history-retry"
          >
            {loading ? 'Retrying…' : 'Retry'}
          </button>
        )}
      </section>
    )
  }

  return (
    <section className={styles.panel} data-testid="session-history-panel" aria-labelledby="session-history-panel-title">
      <div className={styles.header}>
        <span className={styles.eyebrow} id="session-history-panel-title">History</span>
      </div>
      {events.length === 0 ? (
        <p className={styles.stateNote} data-testid="session-history-empty">
          {loading ? 'Loading history…' : 'No events recorded yet.'}
        </p>
      ) : (
        <ul className={styles.list}>
          {events.map((event) => (
            <li
              key={event.sessionEventId}
              className={styles.row}
              data-testid={`session-history-row-${event.sessionEventId}`}
            >
              <span className={styles.eventType} data-testid={`session-history-type-${event.sessionEventId}`}>
                {formatEventType(event.eventType)}
              </span>
              <span className={styles.details}>
                <span className={styles.teamName}>
                  {/* A null teamId is a session-wide event that belongs to no single team — not an
                      unknown team. Only an unresolved id is genuinely unknown. */}
                  {event.teamId === null
                    ? 'Whole session'
                    : teamNames[event.teamId] ?? 'Unknown team'}
                </span>
                {/* Already a display string from the backend projection — render it, don't reinterpret it. */}
                <span className={styles.summary} data-testid={`session-history-summary-${event.sessionEventId}`}>
                  {event.payloadSummary}
                </span>
              </span>
              <span className={styles.occurredAt} data-testid={`session-history-time-${event.sessionEventId}`}>
                {formatOccurredAt(event.occurredAt)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
