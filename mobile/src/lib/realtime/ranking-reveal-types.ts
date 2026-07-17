// Substage ranking reveal contract (mirrors SubstageRankingRevealStartedNotificationDto.cs in
// session-operations-service). Wire fields are camelCase (ASP.NET System.Text.Json default).
//
// Carries no ranking by design: that projection belongs to scoring-monitoring, and clients already
// hold it live (GET /api/sessions/{id}/ranking + the scoring hub's RankingChanged). This is only the
// cue to present the data they already have, full-screen, until `revealUntil`.

export type SubstageRankingRevealStartedNotificationDto = {
  liveSessionId: string;
  substageSnapshotId: string;
  playMode: string;
  // ISO-8601 instant the reveal window closes.
  revealUntil: string;
  // Marks the last substage: the mission finishes on this reveal instead of advancing off it.
  isTerminal: boolean;
  emittedAt: string;
};

// The active substage ranking reveal carried by the session snapshot (nested in SessionTimerSnapshotDto;
// mirrors ActiveRankingRevealSnapshotDto.cs). The start push opens the reveal with low latency; this is
// the recovery source, so a participant reconnecting mid-reveal restores the same ranking screen. Present
// while paused and past the wall-clock revealUntil — the backend clears it only on committed advance/finish.
export type ActiveRankingRevealSnapshotDto = {
  substageSnapshotId: string;
  playMode: string;
  revealUntil: string;
  isTerminal: boolean;
  emittedAt: string;
};

// Client-internal reconciliation payload from `useSessionTimer` to `useRankingReveal`. Pairs the reveal
// carried by a snapshot with the server time it was observed at (`observedAt`), so the reveal state and
// the timestamp that orders it always describe the *same* response. `version` bumps once per successful
// fetch (never on a failed one), letting the consumer apply each snapshot exactly once without a failed
// request replaying cached reveal data.
export type RevealSnapshotReconciliation = {
  reveal: ActiveRankingRevealSnapshotDto | null;
  observedAt: string;
  version: number;
};

/**
 * Reveal duration in ms, measured server-side as `revealUntil - emittedAt`.
 *
 * Both instants come from the same payload, so the span is immune to client clock skew — comparing
 * `revealUntil` against the device clock is not, and a phone minutes off would cut the reveal to
 * nothing or strand it open. Returns null when either instant is unparseable or the window has
 * already elapsed, which the caller treats as "no reveal to show".
 */
export function revealDurationMs(
  notification: SubstageRankingRevealStartedNotificationDto,
): number | null {
  const until = Date.parse(notification.revealUntil);
  const emitted = Date.parse(notification.emittedAt);
  if (Number.isNaN(until) || Number.isNaN(emitted)) return null;
  const duration = until - emitted;
  return duration > 0 ? duration : null;
}
