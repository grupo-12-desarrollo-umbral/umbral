import type { ActiveQuestionSnapshotDto } from './trivia-types';
import type { ActiveRankingRevealSnapshotDto } from './ranking-reveal-types';

export type SessionTimerSnapshotDto = {
  liveSessionId: string;
  teamId: string | null;
  sessionState: string;
  totalSeconds: number;
  remainingSeconds: number;
  timerStatus: string;
  isAdvancing: boolean;
  isExpired: boolean;
  observedAt: string;
  advancingSince: string | null;
  expiredAt: string | null;
  // Active-question snapshot for the reconnect / late-join path; null when no question is active.
  // Backend added this to SessionTimerSnapshotDto (HU-33A); it was missing from this mobile type.
  activeQuestion?: ActiveQuestionSnapshotDto | null;
  // The mission deadline (D-4), so a reconnect mid-question can restore the whole-mission clock — not
  // just the active-question window above. Null until the deadline is seeded at session start.
  missionTotalSeconds?: number | null;
  missionRemainingSeconds?: number | null;
  // The substage ranking reveal on screen right now (D-3), or null/absent when none is active. Seeds
  // useRankingReveal so a reconnect mid-reveal restores the ranking without a new start push.
  activeRankingReveal?: ActiveRankingRevealSnapshotDto | null;
};

export type SessionTimerUpdatedNotificationDto = {
  liveSessionId: string;
  remainingMilliseconds: number;
  isPaused: boolean;
  emittedAt: string;
  totalMilliseconds: number;
  isExpired: boolean;
  sessionState: string;
  // The mission deadline (D-4/D-5), riding the same tick as the active-substage window above so the two
  // clocks never arrive out of step. During a treasure hunt these mirror remaining/totalMilliseconds
  // (that substage has no window of its own). The two absences are NOT equivalent: an explicit `null`
  // clears the deadline, whereas an omitted field (`undefined`, e.g. an older replica mid-deploy) carries
  // no mission info and preserves the current deadline — see useSessionTimer's tick handler.
  missionRemainingMilliseconds?: number | null;
  missionTotalMilliseconds?: number | null;
  // Explicit pre-game discriminator (D-x): `true` on the trivia pre-round countdown numeral, `false`
  // on a real substage/deadline window. Authoring permits question limits as short as 5s — exactly the
  // pre-game total — so duration alone cannot tell them apart. Absent/`undefined` means an older backend
  // that predates the field; the tick handler then falls back to the legacy short-window heuristic.
  isPregameCountdown?: boolean | null;
};

export type TimerTone = 'running' | 'paused' | 'expired' | 'unavailable';

export type TimerDisplay = {
  label: string;
  pct: number;
  tone: TimerTone;
};

export const UNAVAILABLE_TIMER_DISPLAY: TimerDisplay = {
  label: '--:--',
  pct: 0,
  tone: 'unavailable',
};

function formatSeconds(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds));
  const hours = Math.floor(s / 3600);
  const minutes = Math.floor((s % 3600) / 60);
  const seconds = s % 60;

  if (hours > 0) {
    return `${hours}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
  }
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function toTimerDisplay(
  remainingSeconds: number,
  totalSeconds: number,
  isPaused: boolean,
  isExpired: boolean,
): TimerDisplay {
  if (isExpired) {
    return { label: '00:00', pct: 0, tone: 'expired' };
  }

  const safeTotal = totalSeconds > 0 ? totalSeconds : 1;
  const pct = Math.min(100, Math.max(0, (remainingSeconds / safeTotal) * 100));
  const tone: TimerTone = isPaused ? 'paused' : 'running';

  return { label: formatSeconds(remainingSeconds), pct, tone };
}
