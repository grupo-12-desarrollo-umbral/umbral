import {
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { getValidAccessToken } from '@/lib/auth/token-provider';
import { apiBaseUrl } from '@/lib/host';
import type { PenaltyAppliedDto } from './penalty-types';
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
  // Fires when an operator applies a penalty to a team in the joined session. Unlike RankingChanged, this
  // carries the true deduction magnitude and always fires — even when the resulting team total clamps to
  // zero and the ranking snapshot is unchanged — so it, not a score-decrease heuristic, drives the toast.
  onPenaltyApplied: (cb: (payload: PenaltyAppliedDto) => void) => () => void;
  // Fires after the transport auto-reconnects and the session group has been re-joined. Pushes only
  // arrive on a ranking *change*, so any change during the outage was missed — consumers use this to
  // re-fetch the current snapshot rather than showing stale standings until the next change.
  onReconnected: (cb: () => void) => () => void;
  // Fires when the connection closes for good (auto-reconnect exhausted). No further pushes will
  // arrive, so consumers surface this instead of leaving frozen standings displayed as live.
  onClosed: (cb: () => void) => () => void;
};

const REJOIN_MAX_ATTEMPTS = 3;
const REJOIN_BASE_DELAY_MS = 500;

const delay = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

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

  const reconnectedSubscribers = new Set<() => void>();
  const closedSubscribers = new Set<() => void>();

  connection.onreconnected(async () => {
    if (!joinedSessionId || !joinedTeamId) return;

    // Re-join the session group on the fresh connection. A failed invoke is NOT self-healing: pushes
    // only reach group members, so without a successful re-join there is no "next push" to retry on
    // and the stream is silently dead. Retry a bounded number of times before giving up.
    for (let attempt = 0; attempt < REJOIN_MAX_ATTEMPTS; attempt++) {
      try {
        await connection.invoke('JoinSessionGroup', joinedSessionId, joinedTeamId);
        reconnectedSubscribers.forEach((cb) => cb());
        return;
      } catch {
        if (attempt < REJOIN_MAX_ATTEMPTS - 1) {
          await delay(REJOIN_BASE_DELAY_MS * (attempt + 1));
        }
      }
    }

    // Re-join exhausted: the connection is up but the participant is not in the group, so no pushes
    // will arrive. Surface it like a closed stream so standings aren't shown as live.
    closedSubscribers.forEach((cb) => cb());
  });

  connection.onclose(() => {
    // Auto-reconnect exhausted (or an unexpected close). No further pushes — tell consumers so frozen
    // standings are not left displayed as live.
    closedSubscribers.forEach((cb) => cb());
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
    onPenaltyApplied(cb) {
      connection.on('PenaltyApplied', cb);
      return () => connection.off('PenaltyApplied', cb);
    },
    onReconnected(cb) {
      reconnectedSubscribers.add(cb);
      return () => reconnectedSubscribers.delete(cb);
    },
    onClosed(cb) {
      closedSubscribers.add(cb);
      return () => closedSubscribers.delete(cb);
    },
  };
}
