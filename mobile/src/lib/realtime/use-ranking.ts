import { useCallback, useEffect, useState } from 'react';
import {
  getRanking,
  interpretTimerSnapshotError,
  type TimerSnapshotError,
} from '@/lib/api/sessions';
import type { RankingSnapshotDto } from './ranking-types';
import type { ScoringHubClient } from './scoring-hub';

export type UseRankingResult = {
  snapshot: RankingSnapshotDto | null;
  isLoading: boolean;
  error: TimerSnapshotError | null;
  refetch: () => void;
};

/**
 * HU-25B participant ranking hook. Fetches the session ranking snapshot on mount
 * and exposes a `refetch` trigger. The snapshot is null until the first successful
 * fetch. An empty snapshot (`rows: []`, `generatedAt: MinValue`) is treated as a
 * first-class "no standings yet" state — the caller distinguishes via
 * `isEmptySnapshot()` from `ranking-types`.
 *
 * When a `scoringClient` is provided (HU-25B Slice 3), subscribes to live
 * `RankingChanged` push events from the ScoringHub so standings update in
 * real time without polling.
 */
export function useRanking(
  liveSessionId: string,
  teamId: string,
  token?: string | null,
  scoringClient?: ScoringHubClient | null,
): UseRankingResult {
  const [snapshot, setSnapshot] = useState<RankingSnapshotDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<TimerSnapshotError | null>(null);
  const [fetchNonce, setFetchNonce] = useState(0);

  useEffect(() => {
    let active = true;
    // Fetch effect: enter the loading state before the request, then resolve it in the
    // promise callbacks below. Synchronizing UI with an async data source is the intended
    // use of an effect here.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(true);
    setError(null);

    getRanking(liveSessionId, teamId, token)
      .then(result => {
        if (!active) return;
        setSnapshot(result);
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
  }, [liveSessionId, teamId, token, fetchNonce]);

  useEffect(() => {
    if (!scoringClient) return;

    const offRankingChanged = scoringClient.onRankingChanged((pushed: RankingSnapshotDto) => {
      if (pushed.liveSessionId !== liveSessionId) return;
      setSnapshot(pushed);
      setIsLoading(false);
    });
    // After a transport auto-reconnect the group is re-joined, but any change during the outage was
    // missed (pushes only fire on change) — pull the current snapshot so standings aren't stale.
    const offReconnected = scoringClient.onReconnected(() => setFetchNonce(n => n + 1));
    // Connection closed for good: no more pushes will arrive, so surface it rather than leaving the
    // last snapshot displayed as live. (Render sites prefer a snapshot over an error, so this only
    // shows when there is nothing to display; the retry path re-establishes the stream.)
    const offClosed = scoringClient.onClosed(() => setError('network-error'));

    return () => {
      offRankingChanged();
      offReconnected();
      offClosed();
    };
  }, [scoringClient, liveSessionId]);

  const refetch = useCallback(() => {
    setFetchNonce(n => n + 1);
  }, []);

  return { snapshot, isLoading, error, refetch };
}
