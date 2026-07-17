'use client'

import {
  HttpTransportType,
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import type { RankingSnapshotDto } from '@/app/lib/definitions'
import type { SessionRealtimeStatus } from './session-state-client'

// HU-24B operator ranking client for /hubs/scoring. Separate connection from the /hubs/sessions client:
// different service (scoring-monitoring), different hub, independent lifecycle. Joins via the
// operator-scoped hub method — the participant JoinSessionGroup would deny an operator, who is a
// member of no team.
type RankingClientOptions = {
  liveSessionId: string
  onStatusChange: (status: SessionRealtimeStatus) => void
  onRankingChanged: (snapshot: RankingSnapshotDto) => void
  onReconnected?: () => void
}

export type RankingRealtimeClient = {
  start: () => Promise<void>
  stop: () => Promise<void>
}

const hubBaseUrl = process.env.NEXT_PUBLIC_API_GATEWAY_URL ?? ''
const hubPath = '/hubs/scoring'
const hubTokenPath = '/api/realtime/hub-token'
const HUB_AUTH_FAILED_ERROR = 'hub_auth_failed'
const JOIN_METHOD = 'JoinSessionGroupAsOperatorAsync'
const LEAVE_METHOD = 'LeaveSessionGroup'
// Bounded backoff for a failed group join (see joinSessionGroup). Long enough to ride out the
// operator-assignment projection catching up in scoring-monitoring right after assignment, short
// enough that a genuine authorization denial gives up quickly instead of spamming the server.
const JOIN_RETRY_DELAYS_MS = [1000, 2000, 4000]

function buildHubUrl() {
  return hubBaseUrl ? `${hubBaseUrl}${hubPath}` : hubPath
}

async function getHubAccessToken(): Promise<string> {
  const response = await fetch(hubTokenPath, {
    credentials: 'include',
    cache: 'no-store',
  })

  if (!response.ok) {
    throw new Error(HUB_AUTH_FAILED_ERROR)
  }

  const body = (await response.json()) as { accessToken?: unknown }
  if (typeof body.accessToken !== 'string' || body.accessToken.length === 0) {
    throw new Error(HUB_AUTH_FAILED_ERROR)
  }

  return body.accessToken
}

function isHubAuthFailure(error: unknown): boolean {
  return error instanceof Error && error.message === HUB_AUTH_FAILED_ERROR
}

// Same defensive camel ?? Pascal ?? default idiom as session-state-client's normalizers. Rows keep the
// backend's order and Position verbatim — RB-08 ordering is the backend's to decide, not ours.
function normalizeRankingSnapshot(raw: unknown): RankingSnapshotDto {
  const s = (raw ?? {}) as Record<string, unknown>
  const rawRows = (s.rows ?? s.Rows ?? []) as unknown[]
  return {
    liveSessionId: (s.liveSessionId ?? s.LiveSessionId ?? '') as string,
    generatedAt: (s.generatedAt ?? s.GeneratedAt ?? '') as string,
    calculationVersion: (s.calculationVersion ?? s.CalculationVersion ?? 0) as number,
    rows: rawRows.map((rawRow) => {
      const r = (rawRow ?? {}) as Record<string, unknown>
      return {
        teamId: (r.teamId ?? r.TeamId ?? '') as string,
        teamDisplayName: (r.teamDisplayName ?? r.TeamDisplayName ?? '') as string,
        position: (r.position ?? r.Position ?? 0) as number,
        totalScore: (r.totalScore ?? r.TotalScore ?? 0) as number,
        resolutionTime: (r.resolutionTime ?? r.ResolutionTime ?? null) as string | null,
      }
    }),
  }
}

async function invokeIfConnected(
  connection: HubConnection,
  methodName: string,
  liveSessionId: string,
) {
  if (connection.state !== HubConnectionState.Connected) return
  await connection.invoke(methodName, liveSessionId)
}

export function createRankingRealtimeClient({
  liveSessionId,
  onStatusChange,
  onRankingChanged,
  onReconnected,
}: RankingClientOptions): RankingRealtimeClient {
  const connection = new HubConnectionBuilder()
    .withUrl(buildHubUrl(), {
      accessTokenFactory: getHubAccessToken,
      // Pinned so negotiation cannot silently fall back to SSE/long-polling: RNF-03 requires
      // real-time to run over WebSockets, and a fallback is invisible from the UI.
      transport: HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect([0, 1500, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()

  let stopped = false
  let joinRetryTimer: ReturnType<typeof setTimeout> | null = null

  function clearJoinRetry() {
    if (joinRetryTimer !== null) {
      clearTimeout(joinRetryTimer)
      joinRetryTimer = null
    }
  }

  // Join the session group, retrying while the connection stays up. A single failed join must not
  // strand the connection out of the group forever: the join can fail transiently, or because the
  // operator-assignment projection in scoring-monitoring is still catching up in the seconds right
  // after the operator was assigned. `onreconnected` only fires on a transport drop — a failed join
  // is not one — so without this retry the connection would sit alive-but-unjoined, receive no
  // RankingChanged push, and the panel would silently go stale on its REST snapshot. Reports 'Offline'
  // on each failure so the UI can stop presenting the snapshot as live; bounded so a real denial (the
  // caller is not the assigned operator) gives up quickly instead of spamming the hub.
  async function joinSessionGroup(attempt = 0): Promise<void> {
    clearJoinRetry()
    if (stopped || connection.state !== HubConnectionState.Connected) return
    try {
      await connection.invoke(JOIN_METHOD, liveSessionId)
      onStatusChange('Connected')
      // Recovered after one or more failed joins: standings may have moved while we were out of the
      // group, so pull a fresh REST snapshot to close the gap.
      if (attempt > 0) onReconnected?.()
    } catch (error) {
      if (isHubAuthFailure(error)) {
        onStatusChange('AuthExpired')
        return
      }
      onStatusChange('Offline')
      if (stopped || attempt >= JOIN_RETRY_DELAYS_MS.length) return
      joinRetryTimer = setTimeout(() => {
        joinRetryTimer = null
        void joinSessionGroup(attempt + 1)
      }, JOIN_RETRY_DELAYS_MS[attempt])
    }
  }

  connection.on('RankingChanged', (raw: unknown) => {
    onRankingChanged(normalizeRankingSnapshot(raw))
  })

  connection.onreconnecting((error) =>
    onStatusChange(isHubAuthFailure(error) ? 'AuthExpired' : 'Reconnecting'),
  )
  connection.onreconnected(async () => {
    onStatusChange('Connected')
    // Group membership does not survive a reconnect — re-join before signalling the refetch.
    await joinSessionGroup()
    onReconnected?.()
  })
  connection.onclose((error) =>
    onStatusChange(isHubAuthFailure(error) ? 'AuthExpired' : 'Offline'),
  )

  return {
    async start() {
      stopped = false
      try {
        await connection.start()
        onStatusChange('Connected')
      } catch (error) {
        onStatusChange(isHubAuthFailure(error) ? 'AuthExpired' : 'Offline')
        return
      }
      await joinSessionGroup()
    },
    async stop() {
      stopped = true
      clearJoinRetry()
      try {
        await invokeIfConnected(connection, LEAVE_METHOD, liveSessionId)
        await connection.stop()
      } finally {
        onStatusChange('Offline')
      }
    },
  }
}
