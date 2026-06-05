'use client'

import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import type {
  QuestionActivatedNotificationDto,
  QuestionClosedNotificationDto,
  SessionStateChangedNotificationDto,
  SessionTimerUpdatedNotificationDto,
} from '@/app/lib/definitions'

export type SessionRealtimeStatus =
  | 'Connected'
  | 'Reconnecting'
  | 'Offline'
  | 'AuthExpired'

type SessionStateClientOptions = {
  liveSessionId: string
  onStatusChange: (status: SessionRealtimeStatus) => void
  onStateChanged: (notification: SessionStateChangedNotificationDto) => void
  onTimerUpdated?: (notification: SessionTimerUpdatedNotificationDto) => void
  onQuestionActivated?: (notification: QuestionActivatedNotificationDto) => void
  onQuestionClosed?: (notification: QuestionClosedNotificationDto) => void
  onReconnected?: () => void
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

function normalizeTimerNotification(raw: unknown): SessionTimerUpdatedNotificationDto {
  const n = raw as SessionTimerUpdatedNotificationDto & {
    LiveSessionId?: string
    RemainingMilliseconds?: number
    IsPaused?: boolean
    EmittedAt?: string
    TotalMilliseconds?: number
    IsExpired?: boolean
    SessionState?: string
  }
  return {
    liveSessionId: n.liveSessionId ?? n.LiveSessionId ?? '',
    remainingMilliseconds: n.remainingMilliseconds ?? n.RemainingMilliseconds ?? 0,
    isPaused: n.isPaused ?? n.IsPaused ?? false,
    emittedAt: n.emittedAt ?? n.EmittedAt ?? '',
    totalMilliseconds: n.totalMilliseconds ?? n.TotalMilliseconds ?? 0,
    isExpired: n.isExpired ?? n.IsExpired ?? false,
    sessionState: n.sessionState ?? n.SessionState ?? '',
  }
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

function normalizeQuestionActivated(raw: unknown): QuestionActivatedNotificationDto {
  const n = raw as QuestionActivatedNotificationDto & {
    LiveSessionId?: string
    QuestionIndex?: number
    SequenceOrder?: number
    Prompt?: string
    Options?: string[]
    TimeLimitSeconds?: number
    ActivatedAt?: string
  }
  return {
    liveSessionId: n.liveSessionId ?? n.LiveSessionId ?? '',
    questionIndex: n.questionIndex ?? n.QuestionIndex ?? 0,
    sequenceOrder: n.sequenceOrder ?? n.SequenceOrder ?? 0,
    prompt: n.prompt ?? n.Prompt ?? '',
    options: n.options ?? n.Options ?? [],
    timeLimitSeconds: n.timeLimitSeconds ?? n.TimeLimitSeconds ?? 0,
    activatedAt: n.activatedAt ?? n.ActivatedAt ?? '',
  }
}

function normalizeQuestionClosed(raw: unknown): QuestionClosedNotificationDto {
  const n = raw as QuestionClosedNotificationDto & {
    LiveSessionId?: string
    QuestionIndex?: number
    ClosedAt?: string
    WasExpiredByTimer?: boolean
  }
  return {
    liveSessionId: n.liveSessionId ?? n.LiveSessionId ?? '',
    questionIndex: n.questionIndex ?? n.QuestionIndex ?? 0,
    closedAt: n.closedAt ?? n.ClosedAt ?? '',
    wasExpiredByTimer: n.wasExpiredByTimer ?? n.WasExpiredByTimer ?? false,
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
  onTimerUpdated,
  onQuestionActivated,
  onQuestionClosed,
  onReconnected,
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

  if (onTimerUpdated) {
    connection.on('SessionTimerUpdated', (raw: unknown) => {
      onTimerUpdated(normalizeTimerNotification(raw))
    })
  }

  if (onQuestionActivated) {
    connection.on('QuestionActivated', (raw: unknown) => {
      onQuestionActivated(normalizeQuestionActivated(raw))
    })
  }

  if (onQuestionClosed) {
    connection.on('QuestionClosed', (raw: unknown) => {
      onQuestionClosed(normalizeQuestionClosed(raw))
    })
  }

  connection.onreconnecting((error) =>
    onStatusChange(isHubAuthFailure(error) ? 'AuthExpired' : 'Reconnecting'),
  )
  connection.onreconnected(async () => {
    onStatusChange('Connected')
    await invokeIfConnected(connection, 'JoinLiveSessionAsOperatorAsync', liveSessionId)
    onReconnected?.()
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
