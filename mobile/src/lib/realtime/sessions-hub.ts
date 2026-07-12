import {
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { getAccessToken } from '@/lib/auth/token-store';
import { hubBaseUrl } from '@/lib/host';
import type {
  ReconnectParticipantHubRequest,
  ReconnectParticipantResultDto,
  SessionStateChangedNotificationDto,
} from './sessions-hub-types';
import type { SessionTimerUpdatedNotificationDto } from './timer-types';
import type { ParticipantTeamBoardDto } from './team-board-types';
import type {
  QuestionActivatedNotificationDto,
  QuestionClosedNotificationDto,
  SubstageAdvancedNotificationDto,
} from './trivia-types';

const KEEP_ALIVE_INTERVAL_MS = 15_000;
const SERVER_TIMEOUT_MS = 30_000;

export type SessionsHubClient = {
  readonly connection: HubConnection;
  start: () => Promise<void>;
  stop: () => Promise<void>;
  reconnect: (
    liveSessionId: string,
    request: ReconnectParticipantHubRequest,
  ) => Promise<ReconnectParticipantResultDto>;
  onTimerUpdated: (
    cb: (notification: SessionTimerUpdatedNotificationDto) => void,
  ) => () => void;
  onStateChanged: (
    cb: (notification: SessionStateChangedNotificationDto) => void,
  ) => () => void;
  onQuestionActivated: (
    cb: (notification: QuestionActivatedNotificationDto) => void,
  ) => () => void;
  onQuestionClosed: (
    cb: (notification: QuestionClosedNotificationDto) => void,
  ) => () => void;
  onSubstageAdvanced: (
    cb: (notification: SubstageAdvancedNotificationDto) => void,
  ) => () => void;
  onTeamBoardUpdated: (
    cb: (board: ParticipantTeamBoardDto) => void,
  ) => () => void;
};

export function createSessionsHubConnection(): SessionsHubClient {
  const connection = new HubConnectionBuilder()
    .withUrl(hubBaseUrl(), {
      accessTokenFactory: async () => (await getAccessToken()) ?? '',
      transport: HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect()
    .withKeepAliveInterval(KEEP_ALIVE_INTERVAL_MS)
    .withServerTimeout(SERVER_TIMEOUT_MS)
    // Pin to Warning: SignalR's default `Information` level logs the connection
    // URL, which carries the `?access_token=` query param for the WS handshake.
    // Warning keeps real failures visible while never echoing the bearer token.
    .configureLogging(LogLevel.Warning)
    .build();

  return {
    connection,
    async start() {
      if (connection.state !== HubConnectionState.Disconnected) return;
      await connection.start();
    },
    async stop() {
      if (connection.state === HubConnectionState.Disconnected) return;
      await connection.stop();
    },
    reconnect(liveSessionId, request) {
      return connection.invoke<ReconnectParticipantResultDto>(
        'ReconnectAsync',
        liveSessionId,
        request,
      );
    },
    onTimerUpdated(cb) {
      connection.on('SessionTimerUpdated', cb);
      return () => connection.off('SessionTimerUpdated', cb);
    },
    onStateChanged(cb) {
      connection.on('SessionStateChanged', cb);
      return () => connection.off('SessionStateChanged', cb);
    },
    onQuestionActivated(cb) {
      connection.on('QuestionActivated', cb);
      return () => connection.off('QuestionActivated', cb);
    },
    onQuestionClosed(cb) {
      connection.on('QuestionClosed', cb);
      return () => connection.off('QuestionClosed', cb);
    },
    onSubstageAdvanced(cb) {
      connection.on('SubstageAdvanced', cb);
      return () => connection.off('SubstageAdvanced', cb);
    },
    onTeamBoardUpdated(cb) {
      connection.on('TeamBoardUpdated', cb);
      return () => connection.off('TeamBoardUpdated', cb);
    },
  };
}
