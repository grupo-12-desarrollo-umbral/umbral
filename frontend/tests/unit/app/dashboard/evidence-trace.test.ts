import { describe, expect, it } from 'vitest'
import { emptyEvidence, evidenceReducer, type EvidenceState } from '@/app/dashboard/evidence-trace'
import type {
  EvidenceSubmissionRegisteredNotificationDto,
  EvidenceSubmissionResolvedNotificationDto,
  EvidenceTraceItemDto,
} from '@/app/lib/definitions'

// The REST trace is fed asynchronously over RabbitMQ while the SignalR pushes fire post-commit, so the
// two transports race: a push can beat its REST row, and a resolution can beat its own registration.
// These pin the merge that absorbs that — the part most likely to make the panel flaky.

const SUBMISSION_ID = '11111111-1111-1111-1111-111111111111'
const TEAM_ID = '22222222-2222-2222-2222-222222222222'
const SUBSTAGE_ID = '33333333-3333-3333-3333-333333333333'

const registered: EvidenceSubmissionRegisteredNotificationDto = {
  liveSessionId: 'session-1',
  evidenceSubmissionId: SUBMISSION_ID,
  teamId: TEAM_ID,
  activeSubstageId: SUBSTAGE_ID,
  submissionType: 'TreasureHuntQrScan',
  originReference: 'target:9f1c',
  submittedAt: '2026-07-16T10:01:05.000Z',
  validationState: 'Pending',
}

const rejected: EvidenceSubmissionResolvedNotificationDto = {
  liveSessionId: 'session-1',
  evidenceSubmissionId: SUBMISSION_ID,
  teamId: TEAM_ID,
  activeSubstageId: SUBSTAGE_ID,
  submissionType: 'TreasureHuntQrScan',
  submittedAt: '2026-07-16T10:01:05.000Z',
  validationState: 'Rejected',
  rejectionReason: 'Este objetivo ya fue resuelto por otro equipo.',
  resolvedAt: '2026-07-16T10:01:09.000Z',
}

const traceRow: EvidenceTraceItemDto = {
  evidenceSubmissionId: SUBMISSION_ID,
  teamId: TEAM_ID,
  activeSubstageId: SUBSTAGE_ID,
  submissionType: 'TreasureHuntQrScan',
  originReference: 'target:9f1c',
  submittedAt: '2026-07-16T10:01:05.000Z',
  validationState: 'Pending',
  rejectionReason: null,
  resolvedAt: null,
}

function reduce(state: EvidenceState, ...actions: Parameters<typeof evidenceReducer>[1][]): EvidenceState {
  return actions.reduce(evidenceReducer, state)
}

describe('evidenceReducer', () => {
  it('adds a registered push as a pending row', () => {
    const state = reduce(emptyEvidence, { type: 'registered', data: registered })

    expect(state.items).toHaveLength(1)
    expect(state.items[0]).toMatchObject({
      evidenceSubmissionId: SUBMISSION_ID,
      validationState: 'Pending',
      originReference: 'target:9f1c',
      resolvedAt: null,
    })
  })

  it('flips a registered row to its terminal state when the resolution arrives', () => {
    const state = reduce(
      emptyEvidence,
      { type: 'registered', data: registered },
      { type: 'resolved', data: rejected },
    )

    expect(state.items).toHaveLength(1)
    expect(state.items[0].validationState).toBe('Rejected')
    expect(state.items[0].rejectionReason).toBe(rejected.rejectionReason)
    expect(state.items[0].resolvedAt).toBe(rejected.resolvedAt)
    // The resolution carries no origin — it must not erase what the registration established.
    expect(state.items[0].originReference).toBe('target:9f1c')
  })

  it('inserts a row for a resolution that arrives before its registration', () => {
    // Not a bug to drop: the registration is simply still in flight on the other transport.
    const state = reduce(emptyEvidence, { type: 'resolved', data: rejected })

    expect(state.items).toHaveLength(1)
    expect(state.items[0].validationState).toBe('Rejected')
    expect(state.items[0].originReference).toBeNull()
  })

  it('lets a late registration fill in the origin without reviving a resolved row', () => {
    const state = reduce(
      emptyEvidence,
      { type: 'resolved', data: rejected },
      { type: 'registered', data: registered },
    )

    expect(state.items).toHaveLength(1)
    // Once terminal, always terminal — the whole point of the merge.
    expect(state.items[0].validationState).toBe('Rejected')
    expect(state.items[0].rejectionReason).toBe(rejected.rejectionReason)
    // ...but the origin the registration alone knows still lands.
    expect(state.items[0].originReference).toBe('target:9f1c')
  })

  it('merges the REST snapshot in rather than replacing the pushes it has not caught up to', () => {
    const otherId = '44444444-4444-4444-4444-444444444444'
    const state = reduce(
      emptyEvidence,
      { type: 'registered', data: { ...registered, evidenceSubmissionId: otherId } },
      { type: 'snapshot', data: { liveSessionId: 'session-1', items: [traceRow] } },
    )

    // The pushed row the projection hasn't seen survives; the REST row it has is added.
    expect(state.items.map((item) => item.evidenceSubmissionId).sort()).toEqual([SUBMISSION_ID, otherId].sort())
  })

  it('does not let a stale REST snapshot revert a row a push already resolved', () => {
    const state = reduce(
      emptyEvidence,
      { type: 'registered', data: registered },
      { type: 'resolved', data: rejected },
      // The projection is behind: its row for this submission is still Pending.
      { type: 'snapshot', data: { liveSessionId: 'session-1', items: [traceRow] } },
    )

    expect(state.items).toHaveLength(1)
    expect(state.items[0].validationState).toBe('Rejected')
    expect(state.items[0].rejectionReason).toBe(rejected.rejectionReason)
  })

  it('applies a resolution the REST snapshot knows about and the pushes missed', () => {
    // The hub-was-down case: the reconnect refetch is the only way to learn what we missed.
    const resolvedRow: EvidenceTraceItemDto = {
      ...traceRow,
      validationState: 'Accepted',
      resolvedAt: '2026-07-16T10:01:09.000Z',
    }
    const state = reduce(
      emptyEvidence,
      { type: 'registered', data: registered },
      { type: 'snapshot', data: { liveSessionId: 'session-1', items: [resolvedRow] } },
    )

    expect(state.items[0].validationState).toBe('Accepted')
    expect(state.items[0].resolvedAt).toBe('2026-07-16T10:01:09.000Z')
  })

  it('is idempotent under a duplicate push', () => {
    const state = reduce(
      emptyEvidence,
      { type: 'registered', data: registered },
      { type: 'registered', data: registered },
      { type: 'resolved', data: rejected },
      { type: 'resolved', data: rejected },
    )

    expect(state.items).toHaveLength(1)
    expect(state.items[0].validationState).toBe('Rejected')
  })

  it('keeps trivia and QR submissions as distinct rows', () => {
    const triviaId = '55555555-5555-5555-5555-555555555555'
    const state = reduce(
      emptyEvidence,
      { type: 'registered', data: registered },
      {
        type: 'registered',
        data: {
          ...registered,
          evidenceSubmissionId: triviaId,
          submissionType: 'TriviaAnswer',
          originReference: null,
        },
      },
    )

    expect(state.items).toHaveLength(2)
    expect(state.items.map((item) => item.submissionType)).toEqual(['TreasureHuntQrScan', 'TriviaAnswer'])
  })

  it('keeps the rows it has when a read fails, and clears them on unauthorized or session switch', () => {
    const loaded = reduce(emptyEvidence, { type: 'registered', data: registered })

    // A transient blip must not blank a live feed.
    const failed = reduce(loaded, { type: 'failed', error: 'boom' })
    expect(failed.items).toHaveLength(1)
    expect(failed.error).toBe('boom')

    expect(reduce(loaded, { type: 'unauthorized' })).toEqual({ ...emptyEvidence, unauthorized: true })
    expect(reduce(loaded, { type: 'reset' })).toEqual(emptyEvidence)
  })

  it('clears a stale error once a later snapshot succeeds', () => {
    const state = reduce(
      emptyEvidence,
      { type: 'registered', data: registered },
      { type: 'failed', error: 'boom' },
      { type: 'snapshot', data: { liveSessionId: 'session-1', items: [traceRow] } },
    )

    expect(state.error).toBeNull()
    expect(state.loading).toBe(false)
  })
})
