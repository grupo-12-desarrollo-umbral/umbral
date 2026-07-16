import type {
  EvidenceTraceDto,
  EvidenceTraceItemDto,
  EvidenceSubmissionRegisteredNotificationDto,
  EvidenceSubmissionResolvedNotificationDto,
} from '@/app/lib/definitions'

// HU-24B evidence state. UNLIKE ranking, a snapshot does NOT replace: the REST trace is fed
// asynchronously by RabbitMQ consumers while the SignalR pushes fire post-commit, so a push routinely
// arrives before its REST row exists and a resolution can arrive before its registration. Everything
// merges by evidenceSubmissionId instead. Extracted from DashboardClient so the merge — the part most
// likely to produce a flaky panel — is directly testable.
export interface EvidenceState {
  loading: boolean
  unauthorized: boolean
  error: string | null
  items: EvidenceTraceItemDto[]
}

export const emptyEvidence: EvidenceState = {
  loading: false,
  unauthorized: false,
  error: null,
  items: [],
}

export type EvidenceAction =
  | { type: 'reset' }
  | { type: 'load' }
  | { type: 'snapshot'; data: EvidenceTraceDto } // REST trace — merged in, never replaces
  | { type: 'registered'; data: EvidenceSubmissionRegisteredNotificationDto }
  | { type: 'resolved'; data: EvidenceSubmissionResolvedNotificationDto }
  | { type: 'unauthorized' }
  | { type: 'failed'; error: string }

// The client-side twin of EvidenceTraceEntry.MergeFrom. The two facts feeding a row — registration and
// resolution — arrive in either order over two independent transports, so neither may clobber the
// other: registration owns the submission context, resolution owns the terminal state.
export function mergeEvidenceRow(
  existing: EvidenceTraceItemDto,
  incoming: EvidenceTraceItemDto,
): EvidenceTraceItemDto {
  // Only a registration carries an origin, so fill it where still unknown rather than taking the
  // incoming value — otherwise a resolution (origin-less by contract) would erase it.
  const originReference = existing.originReference ?? incoming.originReference

  // Once terminal, always terminal — a late registration or a stale REST row must not send a resolved
  // submission back to Pending.
  if (existing.validationState !== 'Pending') {
    return { ...existing, originReference }
  }

  return { ...existing, ...incoming, originReference }
}

export function upsertEvidenceRow(
  items: EvidenceTraceItemDto[],
  incoming: EvidenceTraceItemDto,
): EvidenceTraceItemDto[] {
  const index = items.findIndex((item) => item.evidenceSubmissionId === incoming.evidenceSubmissionId)
  // Unknown id: insert. A resolution for a submission we've never seen is normal, not a bug to drop —
  // its registration may still be in flight on the other transport.
  if (index === -1) return [...items, incoming]
  const next = [...items]
  next[index] = mergeEvidenceRow(items[index], incoming)
  return next
}

export function evidenceReducer(state: EvidenceState, action: EvidenceAction): EvidenceState {
  switch (action.type) {
    case 'reset':
      return emptyEvidence
    case 'load':
      return { ...state, loading: true, unauthorized: false, error: null }
    case 'snapshot': {
      // Fold the REST rows onto what pushes already delivered. Rows we hold that the projection hasn't
      // caught up to yet survive; rows it has that we missed get added.
      const items = action.data.items.reduce(upsertEvidenceRow, state.items)
      return { loading: false, unauthorized: false, error: null, items }
    }
    case 'registered':
      return {
        ...state,
        items: upsertEvidenceRow(state.items, {
          evidenceSubmissionId: action.data.evidenceSubmissionId,
          teamId: action.data.teamId,
          activeSubstageId: action.data.activeSubstageId,
          submissionType: action.data.submissionType,
          originReference: action.data.originReference,
          submittedAt: action.data.submittedAt,
          validationState: action.data.validationState,
          rejectionReason: null,
          resolvedAt: null,
        }),
      }
    case 'resolved':
      return {
        ...state,
        items: upsertEvidenceRow(state.items, {
          evidenceSubmissionId: action.data.evidenceSubmissionId,
          teamId: action.data.teamId,
          activeSubstageId: action.data.activeSubstageId,
          submissionType: action.data.submissionType,
          originReference: null, // the resolution events carry none; a registration or the REST row fills it
          submittedAt: action.data.submittedAt,
          validationState: action.data.validationState,
          rejectionReason: action.data.rejectionReason,
          resolvedAt: action.data.resolvedAt,
        }),
      }
    case 'unauthorized':
      return { ...emptyEvidence, unauthorized: true }
    case 'failed':
      // Keep the rows we have — a transient read blip must not blank a live feed.
      return { ...state, loading: false, error: action.error }
  }
}
