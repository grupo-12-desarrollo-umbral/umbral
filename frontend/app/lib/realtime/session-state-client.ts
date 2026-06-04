'use client'

import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import type { SessionStateChangedNotificationDto } from '@/app/lib/definitions'

export type SessionRealtimeStatus =
  | 'Connected'
  | 'Reconnecting'
  | 'Offline'
  | 'AuthExpired'

type SessionStateClientOptions = {
  liveSessionId: string
  onStatusChange: (status: SessionRealtimeStatus) => void
  onStateChanged: (notification: SessionStateChangedNotificationDto) => void
}

export type SessionStateRealtimeClient = {
  start: () => Promise<void>
  stop: () => Promise<void>
}

const hubBaseUrl = process.env.NEXT_PUBLIC_API_GATEWAY_URL ?? ''
const hubPath = '/hubs/sessions'
const hubTokenPath = '/api/realtime/hub-token'
const HUB_AUTH_FAILED_ERROR = 'hub_auth_failed'

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

function normalizeNotification(
  notification: SessionStateChangedNotificationDto,
): SessionStateChangedNotificationDto {
  const raw = notification as SessionStateChangedNotificationDto & {
    LiveSessionId?: string
    PreviousState?: string
    CurrentState?: string
    ChangedAt?: string
  }

  return {
    liveSessionId: raw.liveSessionId ?? raw.LiveSessionId ?? '',
    previousState: raw.previousState ?? raw.PreviousState ?? '',
    currentState: raw.currentState ?? raw.CurrentState ?? '',
    changedAt: raw.changedAt ?? raw.ChangedAt ?? '',
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

export function createSessionStateRealtimeClient({
  liveSessionId,
  onStatusChange,
  onStateChanged,
}: SessionStateClientOptions): SessionStateRealtimeClient {
  const connection = new HubConnectionBuilder()
    .withUrl(buildHubUrl(), {
      accessTokenFactory: getHubAccessToken,
    })
    .withAutomaticReconnect([0, 1500, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('SessionStateChanged', (notification: SessionStateChangedNotificationDto) => {
    onStateChanged(normalizeNotification(notification))
  })

  connection.onreconnecting((error) =>
    onStatusChange(isHubAuthFailure(error) ? 'AuthExpired' : 'Reconnecting'),
  )
  connection.onreconnected(async () => {
    onStatusChange('Connected')
    await invokeIfConnected(connection, 'JoinLiveSessionAsOperatorAsync', liveSessionId)
  })
  connection.onclose((error) =>
    onStatusChange(isHubAuthFailure(error) ? 'AuthExpired' : 'Offline'),
  )

  return {
    async start() {
      try {
        await connection.start()
        onStatusChange('Connected')
        await invokeIfConnected(connection, 'JoinLiveSessionAsOperatorAsync', liveSessionId)
      } catch (error) {
        onStatusChange(isHubAuthFailure(error) ? 'AuthExpired' : 'Offline')
      }
    },
    async stop() {
      try {
        await invokeIfConnected(connection, 'LeaveLiveSessionAsync', liveSessionId)
        await connection.stop()
      } finally {
        onStatusChange('Offline')
      }
    },
  }
}
