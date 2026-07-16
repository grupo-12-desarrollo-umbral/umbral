import {
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { getValidAccessToken } from '@/lib/auth/token-provider';
import { apiBaseUrl } from '@/lib/host';
import type { RankingSnapshotDto } from './ranking-types';

const KEEP_ALIVE_INTERVAL_MS = 15_000;
const SERVER_TIMEOUT_MS = 30_000;

function scoringHubBaseUrl(): string {
  return `${apiBaseUrl()}/hubs/scoring`;
}

export type ScoringHubClient = {
  readonly connection: HubConnection;
  start: () => Promise<void>;
  stop: () => Promise<void>;
  joinSessionGroup: (liveSessionId: string, teamId: string) => Promise<void>;
  leaveSessionGroup: (liveSessionId: string) => Promise<void>;
  onRankingChanged: (cb: (snapshot: RankingSnapshotDto) => void) => () => void;
};

export function createScoringHubConnection(): ScoringHubClient {
  const connection = new HubConnectionBuilder()
    .withUrl(scoringHubBaseUrl(), {
      // Renews on every automatic reconnect — see the note in sessions-hub.ts.
      accessTokenFactory: async () => (await getValidAccessToken()) ?? '',
      transport: HttpTransportType.WebSockets,
    })
    .withAutomaticReconnect()
    .withKeepAliveInterval(KEEP_ALIVE_INTERVAL_MS)
    .withServerTimeout(SERVER_TIMEOUT_MS)
    .configureLogging(LogLevel.Warning)
    .build();

  // The hub gates JoinSessionGroup on session-scoped team membership, so the re-join after a transport
  // reconnect must replay the same (session, team) pair — hold both from the last successful join.
  let joinedSessionId: string | null = null;
  let joinedTeamId: string | null = null;

  connection.onreconnected(async () => {
    if (joinedSessionId && joinedTeamId) {
      try {
        await connection.invoke('JoinSessionGroup', joinedSessionId, joinedTeamId);
      } catch {
        // Group re-join failed on transport reconnect; next push will try again
      }
    }
  });

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
    async joinSessionGroup(liveSessionId: string, teamId: string) {
      joinedSessionId = liveSessionId;
      joinedTeamId = teamId;
      await connection.invoke('JoinSessionGroup', liveSessionId, teamId);
    },
    leaveSessionGroup(liveSessionId: string) {
      return connection.invoke('LeaveSessionGroup', liveSessionId);
    },
    onRankingChanged(cb) {
      connection.on('RankingChanged', cb);
      return () => connection.off('RankingChanged', cb);
    },
  };
}
