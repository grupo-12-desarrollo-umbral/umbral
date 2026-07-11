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
