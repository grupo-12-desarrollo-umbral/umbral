import type { ActiveQuestionSnapshotDto } from './trivia-types';

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
};

export type SessionTimerUpdatedNotificationDto = {
  liveSessionId: string;
  remainingMilliseconds: number;
  isPaused: boolean;
  emittedAt: string;
  totalMilliseconds: number;
  isExpired: boolean;
  sessionState: string;
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
