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
} from './sessions-hub-types';

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
  };
}
