// HU-36A: the operator realtime client subscribes to the reused TeamAnswered hub event and
// normalizes it to the camelCase DTO. Driven through the public factory (the normalizer is
// private, like its siblings) with a mocked SignalR connection that captures registered handlers.
import { describe, expect, it, vi, beforeEach } from 'vitest'

type Handler = (raw: unknown) => void

// One fake connection per built client; the builder records the latest so a test can fire its handlers.
let lastConnection: {
  handlers: Map<string, Handler>
  on: (event: string, handler: Handler) => void
  onreconnecting: (cb: unknown) => void
  onreconnected: (cb: unknown) => void
  onclose: (cb: unknown) => void
  start: () => Promise<void>
  stop: () => Promise<void>
  invoke: () => Promise<void>
  state: number
}

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const handlers = new Map<string, Handler>()
      lastConnection = {
        handlers,
        on: (event: string, handler: Handler) => { handlers.set(event, handler) },
        onreconnecting: () => {},
        onreconnected: () => {},
        onclose: () => {},
        start: async () => {},
        stop: async () => {},
        invoke: async () => {},
        state: 1, // Connected
      }
      return lastConnection
    }
  }
  return {
    HubConnectionBuilder,
    HubConnectionState: { Connected: 1, Disconnected: 0 },
    LogLevel: { Warning: 3 },
  }
})

import { createSessionStateRealtimeClient } from '@/app/lib/realtime/session-state-client'

const baseOptions = {
  liveSessionId: 's1',
  onStatusChange: () => {},
  onStateChanged: () => {},
}

const expected = {
  liveSessionId: 's1',
  teamId: 't-42',
  triviaSubstageSnapshotId: 'sub-9',
  questionSequenceOrder: 3,
  answeredAt: '2026-07-10T10:00:00Z',
}

describe('createSessionStateRealtimeClient — TeamAnswered', () => {
  beforeEach(() => { lastConnection = undefined as never })

  it('normalizes a camelCase TeamAnswered payload to the camelCase DTO', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({ ...baseOptions, onTeamAnswered: (n) => received.push(n) })

    lastConnection.handlers.get('TeamAnswered')!({
      liveSessionId: 's1',
      teamId: 't-42',
      triviaSubstageSnapshotId: 'sub-9',
      questionSequenceOrder: 3,
      answeredAt: '2026-07-10T10:00:00Z',
    })

    expect(received).toEqual([expected])
  })

  it('normalizes a PascalCase (C# wire) TeamAnswered payload to the camelCase DTO', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({ ...baseOptions, onTeamAnswered: (n) => received.push(n) })

    lastConnection.handlers.get('TeamAnswered')!({
      LiveSessionId: 's1',
      TeamId: 't-42',
      TriviaSubstageSnapshotId: 'sub-9',
      QuestionSequenceOrder: 3,
      AnsweredAt: '2026-07-10T10:00:00Z',
    })

    expect(received).toEqual([expected])
  })

  it('registers no TeamAnswered handler when onTeamAnswered is omitted (backward-compatible)', () => {
    createSessionStateRealtimeClient({ ...baseOptions })
    expect(lastConnection.handlers.has('TeamAnswered')).toBe(false)
  })
})

// HU-24A: the operator realtime client subscribes to OperatorSessionPanelUpdated and normalizes the
// full panel DTO (nested teamProgress / activeSubstage) from either wire casing.
describe('createSessionStateRealtimeClient — OperatorSessionPanelUpdated', () => {
  beforeEach(() => { lastConnection = undefined as never })

  const expectedPanel = {
    liveSessionId: 's1',
    state: 'Active',
    timer: { liveSessionId: 's1', teamId: null },
    teamProgress: [
      {
        teamId: 't-1',
        referenceTeamId: 'ref-1',
        teamCode: 'AAA',
        displayName: 'Alpha',
        score: 0,
        releasedClueCount: 2,
        activeSubstage: {
          substageSnapshotId: 'sub-1',
          playMode: 'TreasureHunt',
          title: 'Hunt',
          totalActiveTargets: 3,
          resolvedTargets: 0,
          activeQuestionSequenceOrder: null,
          activeQuestionTimeLimitSeconds: null,
        },
      },
    ],
  }

  it('normalizes a camelCase OperatorSessionPanelUpdated payload', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({ ...baseOptions, onOperatorPanel: (p) => received.push(p) })

    lastConnection.handlers.get('OperatorSessionPanelUpdated')!({
      liveSessionId: 's1',
      state: 'Active',
      timer: { liveSessionId: 's1', teamId: null },
      teamProgress: [
        {
          teamId: 't-1',
          referenceTeamId: 'ref-1',
          teamCode: 'AAA',
          displayName: 'Alpha',
          score: 0,
          releasedClueCount: 2,
          activeSubstage: {
            substageSnapshotId: 'sub-1',
            playMode: 'TreasureHunt',
            title: 'Hunt',
            totalActiveTargets: 3,
            resolvedTargets: 0,
            activeQuestionSequenceOrder: null,
            activeQuestionTimeLimitSeconds: null,
          },
        },
      ],
    })

    expect(received).toEqual([expectedPanel])
  })

  it('normalizes a PascalCase (C# wire) OperatorSessionPanelUpdated payload identically', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({ ...baseOptions, onOperatorPanel: (p) => received.push(p) })

    lastConnection.handlers.get('OperatorSessionPanelUpdated')!({
      LiveSessionId: 's1',
      State: 'Active',
      Timer: { liveSessionId: 's1', teamId: null },
      TeamProgress: [
        {
          TeamId: 't-1',
          TeamCode: 'AAA',
          DisplayName: 'Alpha',
          Score: 0,
          ReferenceTeamId: 'ref-1',
          ReleasedClueCount: 2,
          ActiveSubstage: {
            SubstageSnapshotId: 'sub-1',
            PlayMode: 'TreasureHunt',
            Title: 'Hunt',
            TotalActiveTargets: 3,
            ResolvedTargets: 0,
            ActiveQuestionSequenceOrder: null,
            ActiveQuestionTimeLimitSeconds: null,
          },
        },
      ],
    })

    expect(received).toEqual([expectedPanel])
  })

  it('coalesces a missing teamProgress to an empty array', () => {
    const received: Array<{ teamProgress: unknown[] }> = []
    createSessionStateRealtimeClient({ ...baseOptions, onOperatorPanel: (p) => received.push(p) })

    lastConnection.handlers.get('OperatorSessionPanelUpdated')!({
      liveSessionId: 's1',
      state: 'Preparing',
      timer: null,
    })

    expect(received[0]!.teamProgress).toEqual([])
  })

  it('preserves a null activeSubstage and a trivia active-question order', () => {
    const received: Array<{ teamProgress: Array<{ activeSubstage: unknown }> }> = []
    createSessionStateRealtimeClient({ ...baseOptions, onOperatorPanel: (p) => received.push(p) })

    lastConnection.handlers.get('OperatorSessionPanelUpdated')!({
      liveSessionId: 's1',
      state: 'Active',
      timer: null,
      teamProgress: [
        { teamId: 't-1', teamCode: 'AAA', displayName: 'Alpha', score: 5, activeSubstage: null },
        {
          teamId: 't-2',
          teamCode: 'BBB',
          displayName: 'Bravo',
          score: 0,
          activeSubstage: {
            substageSnapshotId: 'sub-2',
            playMode: 'Trivia',
            title: 'Quiz',
            totalActiveTargets: 0,
            resolvedTargets: 0,
            activeQuestionSequenceOrder: 2,
            activeQuestionTimeLimitSeconds: 30,
          },
        },
      ],
    })

    expect(received[0]!.teamProgress[0]!.activeSubstage).toBeNull()
    expect(received[0]!.teamProgress[1]!.activeSubstage).toMatchObject({
      playMode: 'Trivia',
      activeQuestionSequenceOrder: 2,
      activeQuestionTimeLimitSeconds: 30,
    })
  })

  it('registers no handler when onOperatorPanel is omitted (backward-compatible)', () => {
    createSessionStateRealtimeClient({ ...baseOptions })
    expect(lastConnection.handlers.has('OperatorSessionPanelUpdated')).toBe(false)
  })
})

// HU-24B: the operator realtime client subscribes to the two evidence/submission events on the same
// hub (they ride the operator-only group this connection already joins) and normalizes them from
// either wire casing.
describe('createSessionStateRealtimeClient — EvidenceSubmission events', () => {
  beforeEach(() => { lastConnection = undefined as never })

  const expectedRegistered = {
    liveSessionId: 's1',
    evidenceSubmissionId: 'sub-1',
    teamId: 't-42',
    activeSubstageId: 'substage-9',
    submissionType: 'TreasureHuntQrScan',
    originReference: 'target:9f1c',
    submittedAt: '2026-07-16T10:01:05Z',
    validationState: 'Pending',
  }

  const expectedResolved = {
    liveSessionId: 's1',
    evidenceSubmissionId: 'sub-1',
    teamId: 't-42',
    activeSubstageId: 'substage-9',
    submissionType: 'TreasureHuntQrScan',
    submittedAt: '2026-07-16T10:01:05Z',
    validationState: 'Rejected',
    rejectionReason: 'Ya resuelto.',
    resolvedAt: '2026-07-16T10:01:09Z',
  }

  it('normalizes a camelCase EvidenceSubmissionRegistered payload', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({
      ...baseOptions,
      onEvidenceSubmissionRegistered: (n) => received.push(n),
    })

    lastConnection.handlers.get('EvidenceSubmissionRegistered')!({ ...expectedRegistered })

    expect(received).toEqual([expectedRegistered])
  })

  it('normalizes a PascalCase (C# wire) EvidenceSubmissionRegistered payload', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({
      ...baseOptions,
      onEvidenceSubmissionRegistered: (n) => received.push(n),
    })

    lastConnection.handlers.get('EvidenceSubmissionRegistered')!({
      LiveSessionId: 's1',
      EvidenceSubmissionId: 'sub-1',
      TeamId: 't-42',
      ActiveSubstageId: 'substage-9',
      SubmissionType: 'TreasureHuntQrScan',
      OriginReference: 'target:9f1c',
      SubmittedAt: '2026-07-16T10:01:05Z',
      ValidationState: 'Pending',
    })

    expect(received).toEqual([expectedRegistered])
  })

  // A trivia registration contributes no origin; absent and null must both land as null so the
  // reducer's `existing.originReference ?? incoming.originReference` fill works.
  it('coalesces an absent origin to null', () => {
    const received: { originReference: string | null }[] = []
    createSessionStateRealtimeClient({
      ...baseOptions,
      onEvidenceSubmissionRegistered: (n) => received.push(n),
    })

    lastConnection.handlers.get('EvidenceSubmissionRegistered')!({
      liveSessionId: 's1',
      evidenceSubmissionId: 'sub-2',
      teamId: 't-42',
      activeSubstageId: 'substage-9',
      submissionType: 'TriviaAnswer',
      submittedAt: '2026-07-16T10:01:05Z',
      validationState: 'Pending',
    })

    expect(received[0]!.originReference).toBeNull()
  })

  it('normalizes a camelCase EvidenceSubmissionResolved payload', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({
      ...baseOptions,
      onEvidenceSubmissionResolved: (n) => received.push(n),
    })

    lastConnection.handlers.get('EvidenceSubmissionResolved')!({ ...expectedResolved })

    expect(received).toEqual([expectedResolved])
  })

  it('normalizes a PascalCase (C# wire) EvidenceSubmissionResolved payload', () => {
    const received: unknown[] = []
    createSessionStateRealtimeClient({
      ...baseOptions,
      onEvidenceSubmissionResolved: (n) => received.push(n),
    })

    lastConnection.handlers.get('EvidenceSubmissionResolved')!({
      LiveSessionId: 's1',
      EvidenceSubmissionId: 'sub-1',
      TeamId: 't-42',
      ActiveSubstageId: 'substage-9',
      SubmissionType: 'TreasureHuntQrScan',
      SubmittedAt: '2026-07-16T10:01:05Z',
      ValidationState: 'Rejected',
      RejectionReason: 'Ya resuelto.',
      ResolvedAt: '2026-07-16T10:01:09Z',
    })

    expect(received).toEqual([expectedResolved])
  })

  // An acceptance carries no reason; it must arrive as null rather than undefined.
  it('coalesces an absent rejection reason to null on an acceptance', () => {
    const received: { rejectionReason: string | null }[] = []
    createSessionStateRealtimeClient({
      ...baseOptions,
      onEvidenceSubmissionResolved: (n) => received.push(n),
    })

    lastConnection.handlers.get('EvidenceSubmissionResolved')!({
      liveSessionId: 's1',
      evidenceSubmissionId: 'sub-1',
      teamId: 't-42',
      activeSubstageId: 'substage-9',
      submissionType: 'TreasureHuntQrScan',
      submittedAt: '2026-07-16T10:01:05Z',
      validationState: 'Accepted',
      resolvedAt: '2026-07-16T10:01:09Z',
    })

    expect(received[0]!.rejectionReason).toBeNull()
  })

  it('registers no evidence handlers when the callbacks are omitted (backward-compatible)', () => {
    createSessionStateRealtimeClient({ ...baseOptions })
    expect(lastConnection.handlers.has('EvidenceSubmissionRegistered')).toBe(false)
    expect(lastConnection.handlers.has('EvidenceSubmissionResolved')).toBe(false)
  })
})
