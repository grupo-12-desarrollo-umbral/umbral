import { describe, expect, it } from 'vitest'
import {
  emptySessionActivity,
  sessionActivityReducer,
  type SessionActivityState,
} from '@/app/dashboard/session-activity'

// The operator activity feed is a pure projection of SignalR pushes into newest-first log lines. These
// pin the event→line mapping, the newest-first ordering, the 50-entry cap, and the session-switch reset.

const SESSION = 'session-1'
const TEAM_ID = '22222222-2222-2222-2222-222222222222'

function reduce(
  state: SessionActivityState,
  ...actions: Parameters<typeof sessionActivityReducer>[1][]
): SessionActivityState {
  return actions.reduce(sessionActivityReducer, state)
}

describe('sessionActivityReducer', () => {
  it('maps a state change to a whole-session line', () => {
    const state = reduce(emptySessionActivity, {
      type: 'state',
      data: { liveSessionId: SESSION, previousState: 'Active', currentState: 'Paused', changedAt: '2026-07-16T10:00:00.000Z' },
    })

    expect(state.entries).toHaveLength(1)
    expect(state.entries[0]).toMatchObject({
      kind: 'state',
      label: 'State',
      teamId: null,
      summary: 'Active → Paused',
      at: '2026-07-16T10:00:00.000Z',
    })
  })

  it('reports only the new state when the previous state is unknown', () => {
    const state = reduce(emptySessionActivity, {
      type: 'state',
      data: { liveSessionId: SESSION, previousState: '', currentState: 'Active', changedAt: '2026-07-16T10:00:00.000Z' },
    })

    expect(state.entries[0].summary).toBe('Now Active')
  })

  it('maps a question open, using the one-based sequence order', () => {
    const state = reduce(emptySessionActivity, {
      type: 'questionActivated',
      data: {
        liveSessionId: SESSION,
        questionIndex: 4,
        sequenceOrder: 5,
        prompt: 'p',
        options: [],
        timeLimitSeconds: 30,
        activatedAt: '2026-07-16T10:01:00.000Z',
      },
    })

    expect(state.entries[0]).toMatchObject({ kind: 'questionActivated', label: 'Question', summary: 'Question 5 opened' })
  })

  it('maps a question close from the zero-based index (+1) and flags a timer expiry', () => {
    const state = reduce(emptySessionActivity, {
      type: 'questionClosed',
      data: {
        liveSessionId: SESSION,
        questionIndex: 4,
        closedAt: '2026-07-16T10:02:00.000Z',
        wasExpiredByTimer: true,
        correctOptionSequenceOrder: 2,
        explanation: null,
      },
    })

    expect(state.entries[0].summary).toBe('Question 5 closed (time expired)')
  })

  it('stamps a substage advance with the injected receipt time and marks the final substage', () => {
    const advanced = reduce(emptySessionActivity, {
      type: 'substageAdvanced',
      data: { liveSessionId: SESSION, fromSubstageId: 's1', fromPlayMode: 'Trivia', toSubstageId: 's2' },
      receivedAt: '2026-07-16T10:03:00.000Z',
    })
    expect(advanced.entries[0]).toMatchObject({ kind: 'substageAdvanced', summary: 'Advanced from Trivia substage', at: '2026-07-16T10:03:00.000Z' })

    const final = reduce(emptySessionActivity, {
      type: 'substageAdvanced',
      data: { liveSessionId: SESSION, fromSubstageId: 's1', fromPlayMode: 'TreasureHunt', toSubstageId: null },
      receivedAt: '2026-07-16T10:03:00.000Z',
    })
    expect(final.entries[0].summary).toBe('Final TreasureHunt substage complete')
  })

  it('maps a team answer to a team-scoped line', () => {
    const state = reduce(emptySessionActivity, {
      type: 'teamAnswered',
      data: {
        liveSessionId: SESSION,
        teamId: TEAM_ID,
        triviaSubstageSnapshotId: 'snap',
        questionSequenceOrder: 3,
        answeredAt: '2026-07-16T10:04:00.000Z',
      },
    })

    expect(state.entries[0]).toMatchObject({ kind: 'teamAnswered', teamId: TEAM_ID, summary: 'Answered question 3' })
  })

  it('maps evidence registration and a rejection (with reason) to readable lines', () => {
    const registered = reduce(emptySessionActivity, {
      type: 'evidenceRegistered',
      data: {
        liveSessionId: SESSION,
        evidenceSubmissionId: 'ev1',
        teamId: TEAM_ID,
        activeSubstageId: 'sub',
        submissionType: 'TreasureHuntQrScan',
        originReference: null,
        submittedAt: '2026-07-16T10:05:00.000Z',
        validationState: 'Pending',
      },
    })
    expect(registered.entries[0]).toMatchObject({ kind: 'evidenceRegistered', teamId: TEAM_ID, summary: 'Submitted QR scan' })

    const rejected = reduce(emptySessionActivity, {
      type: 'evidenceResolved',
      data: {
        liveSessionId: SESSION,
        evidenceSubmissionId: 'ev1',
        teamId: TEAM_ID,
        activeSubstageId: 'sub',
        submissionType: 'TreasureHuntQrScan',
        submittedAt: '2026-07-16T10:05:00.000Z',
        validationState: 'Rejected',
        rejectionReason: 'Already resolved by another team.',
        resolvedAt: '2026-07-16T10:05:09.000Z',
      },
    })
    expect(rejected.entries[0].summary).toBe('QR scan rejected: Already resolved by another team.')
  })

  it('prepends newest first and assigns collision-free ids', () => {
    const state = reduce(
      emptySessionActivity,
      { type: 'questionActivated', data: { liveSessionId: SESSION, questionIndex: 0, sequenceOrder: 1, prompt: '', options: [], timeLimitSeconds: 30, activatedAt: '2026-07-16T10:00:00.000Z' } },
      { type: 'questionActivated', data: { liveSessionId: SESSION, questionIndex: 1, sequenceOrder: 2, prompt: '', options: [], timeLimitSeconds: 30, activatedAt: '2026-07-16T10:01:00.000Z' } },
    )

    expect(state.entries.map((e) => e.summary)).toEqual(['Question 2 opened', 'Question 1 opened'])
    expect(new Set(state.entries.map((e) => e.id)).size).toBe(2)
  })

  it('caps the feed at 50 entries, dropping the oldest, and keeps ids monotonic past the cap', () => {
    let state = emptySessionActivity
    for (let i = 1; i <= 60; i += 1) {
      state = sessionActivityReducer(state, {
        type: 'questionActivated',
        data: { liveSessionId: SESSION, questionIndex: i - 1, sequenceOrder: i, prompt: '', options: [], timeLimitSeconds: 30, activatedAt: '2026-07-16T10:00:00.000Z' },
      })
    }

    expect(state.entries).toHaveLength(50)
    expect(state.entries[0].summary).toBe('Question 60 opened')
    expect(state.entries[49].summary).toBe('Question 11 opened')
    // seq keeps climbing so a post-cap id can never collide with a surviving entry's id.
    expect(state.entries[0].id).toBe('activity-60')
  })

  it('clears the feed on reset (session switch)', () => {
    const populated = reduce(emptySessionActivity, {
      type: 'state',
      data: { liveSessionId: SESSION, previousState: 'Active', currentState: 'Paused', changedAt: '2026-07-16T10:00:00.000Z' },
    })

    expect(sessionActivityReducer(populated, { type: 'reset' })).toEqual(emptySessionActivity)
  })
})
