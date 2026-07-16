import type { EvidenceTraceItemDto } from '@/app/lib/definitions'
import styles from './evidenceSubmissionsPanel.module.css'

type EvidenceSubmissionsPanelProps = {
  items: EvidenceTraceItemDto[] // already merged (REST snapshot + pushes); ordering is this panel's call
  teamNames: Record<string, string> // runtime teamId → display name, from the operator panel rollup
  unauthorized: boolean // true ⇒ not-authorized state, no evidence data
  error: string | null // non-null ⇒ transient/unexpected read failure (not an auth problem)
  loading: boolean
  live: boolean // false ⇒ the session hub is not delivering; the list may be stale
}

const typeLabels: Record<string, string> = {
  TreasureHuntQrScan: 'QR scan',
  TriviaAnswer: 'Trivia answer',
}

const stateLabels: Record<string, string> = {
  Pending: 'Pending',
  Accepted: 'Accepted',
  Rejected: 'Rejected',
}

function formatSubmittedAt(iso: string): string {
  const parsed = new Date(iso)
  if (Number.isNaN(parsed.getTime())) return ''
  return parsed.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

// HU-24B operator evidence/submission trace. Fed by the EvidenceSubmissionRegistered/Resolved pushes
// with the REST trace snapshot behind it. Shows BOTH evidence forms — a trivia answer is a submission
// too — typed by submissionType, because AC #5 names "evidencias/envíos" together and this is the honest
// trace surface; AnsweredMonitorPanel answers a different question (who has answered the live question).
// Unlike the ranking the backend assigns no order here, so newest-first is this panel's own choice: the
// operator watches the head of a live feed.
export function EvidenceSubmissionsPanel({
  items,
  teamNames,
  unauthorized,
  error,
  loading,
  live,
}: EvidenceSubmissionsPanelProps) {
  if (unauthorized) {
    return (
      <section className={styles.panel} data-testid="evidence-panel" aria-labelledby="evidence-panel-title">
        <div className={styles.eyebrow} id="evidence-panel-title">Evidence</div>
        <p className={styles.stateNote} role="status" data-testid="evidence-unauthorized">
          You are not authorized to view this session’s submissions.
        </p>
      </section>
    )
  }

  // A transient read failure is NOT an authorization problem — mirrors RankingPanel.
  if (error !== null) {
    return (
      <section className={styles.panel} data-testid="evidence-panel" aria-labelledby="evidence-panel-title">
        <div className={styles.eyebrow} id="evidence-panel-title">Evidence</div>
        <p className={styles.stateNote} role="status" data-testid="evidence-error">
          Couldn’t load the submissions. They will refresh automatically.
        </p>
      </section>
    )
  }

  // Newest first. Sorting a copy keeps this render pure — `items` is reducer-owned state.
  const ordered = [...items].sort((a, b) => {
    const byTime = b.submittedAt.localeCompare(a.submittedAt)
    // Two submissions can share a timestamp; break the tie on the key so the order can't jitter
    // between renders as pushes merge in.
    return byTime !== 0 ? byTime : a.evidenceSubmissionId.localeCompare(b.evidenceSubmissionId)
  })

  return (
    <section className={styles.panel} data-testid="evidence-panel" aria-labelledby="evidence-panel-title">
      <div className={styles.header}>
        <span className={styles.eyebrow} id="evidence-panel-title">Evidence</span>
        {!live && (
          // The snapshot is present but the live channel is down, so a submission can be missing until
          // it recovers. Say so rather than passing a stale list off as live.
          <span className={styles.stateNote} role="status" data-testid="evidence-live-paused">
            Live updates paused — reconnecting.
          </span>
        )}
      </div>
      {ordered.length === 0 ? (
        <p className={styles.stateNote} data-testid="evidence-empty">
          {loading ? 'Loading submissions…' : 'No submissions yet.'}
        </p>
      ) : (
        <ul className={styles.list} aria-live="polite">
          {ordered.map((item) => (
            <li
              key={item.evidenceSubmissionId}
              className={styles.row}
              data-testid={`evidence-row-${item.evidenceSubmissionId}`}
            >
              <span className={styles.evidenceThumb} aria-hidden="true">
                {typeLabels[item.submissionType] ?? item.submissionType}
              </span>
              <span className={styles.details}>
                <span className={styles.teamName}>
                  {teamNames[item.teamId] ?? 'Unknown team'}
                </span>
                {item.originReference !== null && (
                  // qr:{scannedValue} echoes raw scanned input on an unmatched scan. JSX escapes it as
                  // text — never render this as markup.
                  <span className={styles.origin} data-testid={`evidence-origin-${item.evidenceSubmissionId}`}>
                    {item.originReference}
                  </span>
                )}
                {item.rejectionReason !== null && (
                  // Already a display message from TargetResolutionRejectionReason.ToMessage() — render
                  // it, don't map it to copy of our own.
                  <span className={styles.reason} data-testid={`evidence-reason-${item.evidenceSubmissionId}`}>
                    {item.rejectionReason}
                  </span>
                )}
              </span>
              <span className={styles.submittedAt} data-testid={`evidence-time-${item.evidenceSubmissionId}`}>
                {formatSubmittedAt(item.submittedAt)}
              </span>
              <span
                className={`${styles.state} ${styles[`state${item.validationState}`] ?? ''}`}
                data-testid={`evidence-state-${item.evidenceSubmissionId}`}
              >
                {stateLabels[item.validationState] ?? item.validationState}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
