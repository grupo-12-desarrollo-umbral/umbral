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
 * `liveSessionId` only, because the connection is joined to exactly one
 * server-side `team:{perSessionId}` group that already scopes the push to this
 * team. A client-side `teamId` check would in fact drop every push: the DTO
 * carries the per-session team id (`Team.TeamId`), whereas the `teamId` prop —
 * used for the REST fetch/guard — is the identity-access reference id, so the
 * two never compare equal. It does NOT own the timer countdown — the ticking
 * value stays `useSessionTimer.display`; `board.timer` is only a seed.
 *
 * It also re-fetches when `sessionState` changes, so a participant who joins while
 * the session is still `Preparing` doesn't stay stuck on a stale
 * `activeSubstage: null` snapshot when the operator presses Start — the
 * `TimerUpdated` push flips `sessionState` to `Active`, and keying on that value
 * pulls the fresh treasure-hunt board.
 */
export function useTeamBoard({
  client,
  liveSessionId,
  teamId,
  token,
  isReconnected,
  reconnectNonce,
  sessionState,
  refreshNonce = 0,
}: {
  client: SessionsHubClient;
  liveSessionId: string;
  teamId: string;
  token?: string | null;
  isReconnected: boolean;
  reconnectNonce: number;
  // Live session state from `useSessionTimer`; a change (e.g. Preparing → Active) re-fetches the board.
  sessionState?: string | null;
  // Caller-driven re-fetch trigger. There is no board push after a target scan resolves (#223), so an
  // accepted scan bumps this to pull the advanced target-progress numerator. Any change re-fetches.
  refreshNonce?: number;
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
  }, [isReconnected, reconnectNonce, sessionState, refreshNonce, liveSessionId, teamId, token]);

  useEffect(() => {
    return client.onTeamBoardUpdated((pushed: ParticipantTeamBoardDto) => {
      if (pushed.liveSessionId !== liveSessionId) return;
      setBoard(pushed);
    });
  }, [client, liveSessionId]);

  return { board, isLoading, error };
}
