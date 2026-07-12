import type { SessionTimerSnapshotDto } from './timer-types';

// HU-23 participant team-board contract (verified against backend branch
// `feature/hu-23-live-team-board`, commit 9ad88ee). Wire fields are camelCase
// (ASP.NET System.Text.Json default), matching every existing mobile DTO.

export type VisibleClueDto = {
  targetSnapshotId: string;
  clueText: string;
  targetName: string;
};

// `playMode` is the substage-type branch key. Widen to string at the wire
// boundary and narrow here — the backend sends `SubstagePlayMode.ToString()`.
export type SubstagePlayMode = 'TreasureHunt' | 'Trivia';

export type ActiveSubstageContextDto = {
  substageSnapshotId: string;
  playMode: SubstagePlayMode;
  title: string;
  // Treasure-hunt target-progress denominator / numerator (both 0 for trivia).
  totalActiveTargets: number;
  resolvedTargets: number;
  // Trivia-only; null for treasure-hunt.
  activeQuestionSequenceOrder: number | null;
  activeQuestionTimeLimitSeconds: number | null;
};

// Ordered-sequence position of a substage relative to the live-substage pointer (#171).
// Backend sends `SubstageProgressStatus.ToString()`.
export type SubstageProgressStatus = 'Completed' | 'Active' | 'Upcoming';

// One entry in the ordered substage progress list (#171). `sequenceOrder` is a session-wide
// display ordinal (flattened across stages); `playMode` reuses the branch key above.
export type SubstageProgressDto = {
  substageSnapshotId: string;
  title: string;
  sequenceOrder: number;
  playMode: SubstagePlayMode;
  status: SubstageProgressStatus;
};

export type ParticipantTeamBoardDto = {
  liveSessionId: string;
  teamId: string;
  teamDisplayName: string;
  teamCode: string;
  // Current/session-owned score or 0 — no ledger/ranking (DES-31).
  currentScore: number;
  // Nested HU-22 snapshot. The ticking countdown is driven by `useSessionTimer`
  // (top-level snapshot + `SessionTimerUpdated`), not this copy — this carries a
  // usable seed only.
  timer: SessionTimerSnapshotDto;
  activeSubstage: ActiveSubstageContextDto | null;
  // The whole ordered substage sequence with per-item progress status (#171), so a
  // mixed-play-mode participant sees where they are, not just the active substage.
  substages: readonly SubstageProgressDto[];
  // Already-released, visible clues only; guidance, NOT progress.
  visibleClues: readonly VisibleClueDto[];
};

// Target progress is `resolvedTargets` / `totalActiveTargets` — a count, NOT
// coordinates. Returns {0,0} when no substage is active.
export function targetProgress(
  ctx: ActiveSubstageContextDto | null,
): { resolved: number; total: number } {
  if (!ctx) return { resolved: 0, total: 0 };
  return { resolved: ctx.resolvedTargets, total: ctx.totalActiveTargets };
}
