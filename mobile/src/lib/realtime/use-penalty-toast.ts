import { useCallback, useEffect, useRef, useState } from 'react';
import type { PenaltyAppliedDto } from './penalty-types';
import type { ScoringHubClient } from './scoring-hub';

// One penalty to surface in the toast. `id` is a client-side monotonic counter, not a domain id: it keys
// the toast component so React remounts it for each penalty — replaying the enter animation and resetting
// the auto-dismiss timer even when two penalties land back-to-back with the same magnitude.
export type PenaltyToastData = {
  id: number;
  magnitude: number;
  reason: string;
};

/**
 * Subscribes to the ScoringHub `PenaltyApplied` push and surfaces the most recent penalty against the
 * participant's own team so a toast can be shown. Replaces the earlier score-decrease heuristic, which
 * missed penalties whose resulting total clamps to zero and misreported the magnitude when the total was
 * only partially clamped — this event carries the true deduction and always fires.
 *
 * Filters by `liveSessionId` (the push reaches the whole session group) and by `ownTeamId`, matched
 * against the payload's `teamId` (the cross-context ReferenceTeamId, the same id ranking rows carry).
 *
 * A penalty pushed during a transport outage is missed (SignalR does not replay), which is acceptable:
 * the toast is a transient courtesy, and the recalculated ranking still reflects the deduction on rejoin.
 */
export function usePenaltyToast(
  liveSessionId: string,
  ownTeamId: string,
  scoringClient?: ScoringHubClient | null,
): { penalty: PenaltyToastData | null; clear: () => void } {
  const [penalty, setPenalty] = useState<PenaltyToastData | null>(null);
  const nextIdRef = useRef(0);

  useEffect(() => {
    if (!scoringClient) return;

    const off = scoringClient.onPenaltyApplied((pushed: PenaltyAppliedDto) => {
      if (pushed.liveSessionId !== liveSessionId) return;
      if (pushed.teamId !== ownTeamId) return;

      nextIdRef.current += 1;
      setPenalty({
        id: nextIdRef.current,
        magnitude: pushed.deductionMagnitude,
        reason: pushed.reason,
      });
    });

    return off;
  }, [scoringClient, liveSessionId, ownTeamId]);

  const clear = useCallback(() => setPenalty(null), []);

  return { penalty, clear };
}
