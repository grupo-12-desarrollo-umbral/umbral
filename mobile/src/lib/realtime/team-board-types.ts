import type { SessionTimerSnapshotDto } from './timer-types';

// HU-23 participant team-board contract (verified against backend branch
// `feature/hu-23-live-team-board`, commit 9ad88ee). Wire fields are camelCase
// (ASP.NET System.Text.Json default), matching every existing mobile DTO.

// HU-23 target clues carry a target; HU-28 operative clues are operator-authored free text with NO
// target (both target fields null) but a stable operativeClueId. Substage clues (trivia initial or
// released hidden clues, #145 / HU-28) carry clueSnapshotId with no target and no operativeClueId.
// Backend VisibleClueDto: `Guid? TargetSnapshotId, Guid? OperativeClueId, Guid? ClueSnapshotId,
// string ClueText, string? TargetName`.
export type VisibleClueDto = {
  targetSnapshotId: string | null;
  clueText: string;
  targetName: string | null;
  operativeClueId: string | null; // set only for operative clues; null for target and substage clues
  clueSnapshotId?: string | null; // set for substage clues (trivia initial / released); absent/null otherwise
};

// The four clue kinds distinguished on the wire. A target clue carries targetSnapshotId; an
// operative clue carries operativeClueId; a substage clue carries clueSnapshotId; a mission
// (substage-initial) clue carries none of the three (fallback, should not occur after D-4).
export type ClueKind = 'target' | 'operative' | 'substage' | 'mission';
export const clueKind = (c: VisibleClueDto): ClueKind => {
  if (c.targetSnapshotId != null) return 'target';
  if (c.operativeClueId != null) return 'operative';
  if (c.clueSnapshotId != null) return 'substage';
  return 'mission';
};

// Stable list/dedup key for a visible clue. Target/operative/substage clues key on their id;
// mission clues have no id on the wire, so they fall back to a kind-prefixed clue-text composite
// (kept unique by the projection's per-substage ordering). Kind prefix keeps the buckets from ever
// colliding.
export const clueKey = (c: VisibleClueDto): string => {
  switch (clueKind(c)) {
    case 'target':
      return `target:${c.targetSnapshotId}`;
    case 'operative':
      return `operative:${c.operativeClueId}`;
    case 'substage':
      return `substage:${c.clueSnapshotId}`;
    case 'mission':
      return `mission:${c.clueText}`;
  }
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

// One active target of the live treasure-hunt substage, with its display coordinates (#154/#156).
// Backend ActiveTargetDto: `Guid TargetSnapshotId, string Name, int SequenceOrder, double Latitude,
// double Longitude`. Coordinates are display/context only — QR validation still owns resolution, and
// there is no geofencing. The list is ordered by `sequenceOrder`, so `activeTargets[0]` is the target
// the map centres on. Empty when no treasure-hunt substage is active.
export type ActiveTargetDto = {
  targetSnapshotId: string;
  name: string;
  sequenceOrder: number;
  latitude: number;
  longitude: number;
};

export type ParticipantTeamBoardDto = {
  liveSessionId: string;
  // The mission's name (session-level), shown to the participant alongside the substage for every
  // play mode. Session-scoped, so it rides the board itself, not the nullable activeSubstage.
  missionTitle: string;
  teamId: string;
  teamDisplayName: string;
  teamCode: string;
  // Current/session-owned score from the ledger (DES-99).
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
  // Active treasure-hunt targets with coordinates for the Map tab (#156); empty for trivia.
  activeTargets: readonly ActiveTargetDto[];
};

// Target progress is `resolvedTargets` / `totalActiveTargets` — a count, NOT
// coordinates. Returns {0,0} when no substage is active.
export function targetProgress(
  ctx: ActiveSubstageContextDto | null,
): { resolved: number; total: number } {
  if (!ctx) return { resolved: 0, total: 0 };
  return { resolved: ctx.resolvedTargets, total: ctx.totalActiveTargets };
}
