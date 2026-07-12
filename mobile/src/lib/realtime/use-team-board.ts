import { useEffect, useState } from 'react';
import {
  getParticipantTeamBoard,
  interpretTimerSnapshotError,
  type TimerSnapshotError,
} from '@/lib/api/sessions';
import type { ParticipantTeamBoardDto } from './team-board-types';
import type { SessionsHubClient } from './sessions-hub';

export type UseTeamBoardResult = {
  board: ParticipantTeamBoardDto | null;
  isLoading: boolean;
  error: TimerSnapshotError | null;
};

/**
 * HU-23 participant team-board hook. Mirrors `useSessionTimer`: on reconnect it
 * fetches the REST `team-board` snapshot, then applies pushed `TeamBoardUpdated`
 * payloads and re-fetches on `reconnectNonce` change. Pushes are filtered by
 * `liveSessionId` AND `teamId` (the connection is in exactly one `team:{id}`
 * group, but the guard is cheap and defends against a stale group after a team
 * switch). It does NOT own the timer countdown — the ticking value stays
 * `useSessionTimer.display`; `board.timer` is only a seed.
 */
export function useTeamBoard({
  client,
  liveSessionId,
  teamId,
  token,
  isReconnected,
  reconnectNonce,
}: {
  client: SessionsHubClient;
  liveSessionId: string;
  teamId: string;
  token?: string | null;
  isReconnected: boolean;
  reconnectNonce: number;
}): UseTeamBoardResult {
  const [board, setBoard] = useState<ParticipantTeamBoardDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<TimerSnapshotError | null>(null);

  useEffect(() => {
    if (!isReconnected) return;

    let active = true;
    // Snapshot fetch enters a loading state when reconnect recovery starts.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(true);
    setError(null);

    getParticipantTeamBoard(liveSessionId, teamId, token)
      .then(snapshot => {
        if (!active) return;
        setBoard(snapshot);
        setIsLoading(false);
      })
      .catch(err => {
        if (!active) return;
        setError(interpretTimerSnapshotError(err));
        setIsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [isReconnected, reconnectNonce, liveSessionId, teamId, token]);

  useEffect(() => {
    return client.onTeamBoardUpdated((pushed: ParticipantTeamBoardDto) => {
      if (pushed.liveSessionId !== liveSessionId) return;
      if (pushed.teamId !== teamId) return;
      setBoard(pushed);
    });
  }, [client, liveSessionId, teamId]);

  return { board, isLoading, error };
}
