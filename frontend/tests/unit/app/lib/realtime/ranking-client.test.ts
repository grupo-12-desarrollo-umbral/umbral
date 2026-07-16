// HU-24B: the operator ranking client subscribes to RankingChanged on /hubs/scoring and joins via the
// OPERATOR hub method — the participant join would deny an operator, who is a member of no team.
// Driven through the public factory (the normalizer is private, like its siblings) with a mocked
// SignalR connection that captures registered handlers and invocations.
import { describe, expect, it, vi, beforeEach } from 'vitest'

type Handler = (raw: unknown) => void

let lastConnection: {
  handlers: Map<string, Handler>
  invocations: { method: string; arg: unknown }[]
  reconnectedCb: (() => Promise<void>) | null
  on: (event: string, handler: Handler) => void
  onreconnecting: (cb: unknown) => void
  onreconnected: (cb: () => Promise<void>) => void
  onclose: (cb: unknown) => void
  start: () => Promise<void>
  stop: () => Promise<void>
  invoke: (method: string, arg: unknown) => Promise<void>
  state: number
}

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const handlers = new Map<string, Handler>()
      const invocations: { method: string; arg: unknown }[] = []
      lastConnection = {
        handlers,
        invocations,
        reconnectedCb: null,
        on: (event: string, handler: Handler) => { handlers.set(event, handler) },
        onreconnecting: () => {},
        onreconnected: (cb: () => Promise<void>) => { lastConnection.reconnectedCb = cb },
        onclose: () => {},
        start: async () => {},
        stop: async () => {},
        invoke: async (method: string, arg: unknown) => { invocations.push({ method, arg }) },
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

import { createRankingRealtimeClient } from '@/app/lib/realtime/ranking-client'

const baseOptions = {
  liveSessionId: 's1',
  onStatusChange: () => {},
  onRankingChanged: () => {},
}

const camelSnapshot = {
  liveSessionId: 's1',
  generatedAt: '2026-07-16T10:00:00Z',
  calculationVersion: 3,
  rows: [
    { teamId: 'ref-a', teamDisplayName: 'Alpha', position: 1, totalScore: 300, resolutionTime: '00:05:00' },
  ],
}

describe('createRankingRealtimeClient', () => {
  beforeEach(() => { lastConnection = undefined as never })

  it('joins with the operator hub method, not the participant one', async () => {
    const client = createRankingRealtimeClient(baseOptions)

    await client.start()

    expect(lastConnection.invocations).toEqual([
      { method: 'JoinSessionGroupAsOperatorAsync', arg: 's1' },
    ])
  })

  it('re-joins the group after a reconnect and signals a refetch', async () => {
    const reconnected: string[] = []
    const client = createRankingRealtimeClient({
      ...baseOptions,
      onReconnected: () => reconnected.push('refetch'),
    })
    await client.start()
    lastConnection.invocations.length = 0

    // Group membership does not survive a reconnect — the client must re-join.
    await lastConnection.reconnectedCb?.()

    expect(lastConnection.invocations).toEqual([
      { method: 'JoinSessionGroupAsOperatorAsync', arg: 's1' },
    ])
    expect(reconnected).toEqual(['refetch'])
  })

  it('retries a failed group join instead of stranding the connection out of the group', async () => {
    // A join can fail transiently or because scoring-monitoring's operator-assignment projection is
    // still catching up right after assignment. `onreconnected` never fires (the transport never
    // dropped), so without a retry the connection sits alive-but-unjoined and no push ever arrives.
    vi.useFakeTimers()
    try {
      const statuses: string[] = []
      const refetches: string[] = []
      const client = createRankingRealtimeClient({
        ...baseOptions,
        onStatusChange: (s) => statuses.push(s),
        onReconnected: () => refetches.push('refetch'),
      })

      let joinCalls = 0
      lastConnection.invoke = async (method: string, arg: unknown) => {
        lastConnection.invocations.push({ method, arg })
        if (method === 'JoinSessionGroupAsOperatorAsync') {
          joinCalls += 1
          if (joinCalls === 1) throw new Error('transient join failure')
        }
      }

      await client.start()

      // First join failed → reported Offline (not silently 'Connected').
      expect(statuses).toContain('Offline')
      expect(joinCalls).toBe(1)

      // The bounded backoff fires and the retry joins successfully.
      await vi.advanceTimersByTimeAsync(1000)

      expect(joinCalls).toBe(2)
      expect(statuses[statuses.length - 1]).toBe('Connected')
      // Recovering after a missed window pulls a fresh REST snapshot to close the gap.
      expect(refetches).toEqual(['refetch'])
    } finally {
      vi.useRealTimers()
    }
  })

  it('stops retrying a failed join once stopped', async () => {
    vi.useFakeTimers()
    try {
      const client = createRankingRealtimeClient(baseOptions)
      lastConnection.invoke = async (method: string, arg: unknown) => {
        lastConnection.invocations.push({ method, arg })
        if (method === 'JoinSessionGroupAsOperatorAsync') throw new Error('still denied')
      }

      await client.start()
      const joinsBeforeStop = lastConnection.invocations.filter(
        (i) => i.method === 'JoinSessionGroupAsOperatorAsync',
      ).length
      await client.stop()

      // No scheduled retry should fire after stop().
      await vi.advanceTimersByTimeAsync(10000)
      const joinsAfterStop = lastConnection.invocations.filter(
        (i) => i.method === 'JoinSessionGroupAsOperatorAsync',
      ).length
      expect(joinsAfterStop).toBe(joinsBeforeStop)
    } finally {
      vi.useRealTimers()
    }
  })

  it('passes a camelCase RankingChanged payload through to the caller', () => {
    const received: unknown[] = []
    createRankingRealtimeClient({ ...baseOptions, onRankingChanged: (s) => received.push(s) })

    lastConnection.handlers.get('RankingChanged')?.(camelSnapshot)

    expect(received).toEqual([camelSnapshot])
  })

  it('normalizes a PascalCase RankingChanged payload to the camelCase DTO', () => {
    const received: unknown[] = []
    createRankingRealtimeClient({ ...baseOptions, onRankingChanged: (s) => received.push(s) })

    lastConnection.handlers.get('RankingChanged')?.({
      LiveSessionId: 's1',
      GeneratedAt: '2026-07-16T10:00:00Z',
      CalculationVersion: 3,
      Rows: [
        { TeamId: 'ref-a', TeamDisplayName: 'Alpha', Position: 1, TotalScore: 300, ResolutionTime: '00:05:00' },
      ],
    })

    expect(received).toEqual([camelSnapshot])
  })

  it('preserves the backend row order verbatim (RB-08 is the backend policy call)', () => {
    const received: { rows: { teamDisplayName: string }[] }[] = []
    createRankingRealtimeClient({
      ...baseOptions,
      onRankingChanged: (s) => received.push(s as never),
    })

    // Rows arrive already ranked; a lower score leading is legitimate (resolution-time tiebreak).
    lastConnection.handlers.get('RankingChanged')?.({
      ...camelSnapshot,
      rows: [
        { teamId: 'c', teamDisplayName: 'Charlie', position: 1, totalScore: 50, resolutionTime: '00:01:00' },
        { teamId: 'd', teamDisplayName: 'Delta', position: 2, totalScore: 90, resolutionTime: '00:02:00' },
      ],
    })

    expect(received[0].rows.map((r) => r.teamDisplayName)).toEqual(['Charlie', 'Delta'])
  })

  it('defaults a missing resolutionTime to null rather than dropping the row', () => {
    const received: { rows: { resolutionTime: string | null }[] }[] = []
    createRankingRealtimeClient({
      ...baseOptions,
      onRankingChanged: (s) => received.push(s as never),
    })

    lastConnection.handlers.get('RankingChanged')?.({
      ...camelSnapshot,
      rows: [{ teamId: 'a', teamDisplayName: 'Alpha', position: 1, totalScore: 10 }],
    })

    expect(received[0].rows).toHaveLength(1)
    expect(received[0].rows[0].resolutionTime).toBeNull()
  })

  it('treats the empty snapshot as data, yielding no rows', () => {
    const received: { rows: unknown[] }[] = []
    createRankingRealtimeClient({
      ...baseOptions,
      onRankingChanged: (s) => received.push(s as never),
    })

    lastConnection.handlers.get('RankingChanged')?.({ liveSessionId: 's1', rows: [] })

    expect(received[0].rows).toEqual([])
  })
})
