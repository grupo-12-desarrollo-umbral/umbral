import { useEffect, useState } from 'react';
import {
  getParticipantTimerSnapshot,
  interpretTimerSnapshotError,
  type TimerSnapshotError,
} from '@/lib/api/sessions';
import {
  toTimerDisplay,
  UNAVAILABLE_TIMER_DISPLAY,
  type SessionTimerSnapshotDto,
  type SessionTimerUpdatedNotificationDto,
  type TimerDisplay,
} from './timer-types';
import type { SessionStateChangedNotificationDto } from './sessions-hub-types';
import type { RevealSnapshotReconciliation } from './ranking-reveal-types';
import type { SessionsHubClient } from './sessions-hub';

type TimerState = {
  remainingSeconds: number;
  totalSeconds: number;
  isPaused: boolean;
  isExpired: boolean;
  sessionState: string;
  lastSyncedAt: string;
};

// The whole-mission deadline (D-4), distinct from the active-question window carried by `timer`. Kept
// separate so both clocks can show at once during a trivia question — where `timer` is the per-question
// countdown and the mission deadline would otherwise be invisible. Null until the backend seeds it at
// session start (older payloads omit the fields).
type MissionTimerState = {
  remainingSeconds: number;
  totalSeconds: number;
  isPaused: boolean;
};

export type UseSessionTimerResult = {
  timer: TimerState | null;
  isLoading: boolean;
  error: TimerSnapshotError | null;
  display: TimerDisplay;
  // The mission deadline for display, or null when no deadline is seeded yet (pre-start) or the backend
  // omits the fields. Distinct from `display`, which during a trivia question is the question countdown.
  missionDisplay: TimerDisplay | null;
  activeQuestion: SessionTimerSnapshotDto['activeQuestion'] | null;
  // The substage ranking reveal carried by the latest *successful* snapshot (D-3), paired with the
  // snapshot's `observedAt` so useRankingReveal can order it against live events by server time. Null
  // until the first snapshot lands; its `version` bumps only on success, so a failed re-fetch never
  // replays cached reveal data.
  revealReconciliation: RevealSnapshotReconciliation | null;
  sessionState: string | null;
  // The trivia pre-game countdown numeral (5→1), or null when not counting down. The backend emits it
  // as a short-window SessionTimerUpdated before a trivia round's first question; kept off the main
  // timer so those ticks never clobber the session clock (mirrors the operator dashboard, HU-M-countdown).
  pregameSecondsLeft: number | null;
  // Increments each time a snapshot fetch settles. A "landed" signal consumers can key a reconcile
  // on, so they act on fresh data rather than the stale value present when the fetch was triggered.
  snapshotVersion: number;
};

// Legacy fallback only: when the backend omits `isPregameCountdown` (an older replica mid-deploy), a
// SessionTimerUpdated whose total window is this small while Active is *inferred* to be a pre-game
// countdown tick. This heuristic is ambiguous — authoring permits 5–10s question windows that also
// fall here — so it is used strictly as a fallback behind the explicit discriminator below.
const PREGAME_MAX_TOTAL_MS = 10_000;

// Whether a tick is a trivia pre-game countdown numeral rather than a real substage/deadline window.
// Prefers the backend's explicit `isPregameCountdown` flag; only when it is absent (older backend) does
// it fall back to the ambiguous short-window heuristic, which a short question window would trip.
function isPregameCountdownTick(notification: SessionTimerUpdatedNotificationDto): boolean {
  if (notification.isPregameCountdown != null) {
    return notification.isPregameCountdown;
  }
  return (
    notification.totalMilliseconds <= PREGAME_MAX_TOTAL_MS &&
    notification.sessionState === 'Active'
  );
}

export function useSessionTimer({
  client,
  liveSessionId,
  teamId,
  token,
  isReconnected,
  reconnectNonce,
  resyncNonce = 0,
}: {
  client: SessionsHubClient;
  liveSessionId: string;
  teamId: string;
  token?: string | null;
  isReconnected: boolean;
  reconnectNonce: number;
  // Bumped on a QuestionClosed re-sync (HU-M3) to force a fresh snapshot fetch, mirroring reconnect.
  resyncNonce?: number;
}): UseSessionTimerResult {
  const [timer, setTimer] = useState<TimerState | null>(null);
  const [missionTimer, setMissionTimer] = useState<MissionTimerState | null>(null);
  const [activeQuestion, setActiveQuestion] = useState<SessionTimerSnapshotDto['activeQuestion'] | null>(null);
  const [revealReconciliation, setRevealReconciliation] =
    useState<RevealSnapshotReconciliation | null>(null);
  const [sessionState, setSessionState] = useState<string | null>(null);
  const [pregameSecondsLeft, setPregameSecondsLeft] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<TimerSnapshotError | null>(null);
  const [snapshotVersion, setSnapshotVersion] = useState(0);

  useEffect(() => {
    if (!isReconnected) return;

    let active = true;
    // Existing timer snapshot fetch enters a loading state when reconnect recovery starts.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(true);
    setError(null);

    getParticipantTimerSnapshot(liveSessionId, teamId, token)
      .then(snapshot => {
        if (!active) return;
        // A fresh snapshot is the authoritative session clock — end any pre-game countdown display.
        setPregameSecondsLeft(null);
        setTimer({
          remainingSeconds: snapshot.remainingSeconds,
          totalSeconds: snapshot.totalSeconds,
          isPaused: !snapshot.isAdvancing && !snapshot.isExpired,
          isExpired: snapshot.isExpired,
          sessionState: snapshot.sessionState,
          lastSyncedAt: snapshot.observedAt,
        });
        setActiveQuestion(snapshot.activeQuestion ?? null);
        // Pair the reveal with the response's `observedAt`, and bump the reveal version — only here, on
        // success — so useRankingReveal applies this snapshot exactly once and orders it by server time.
        setRevealReconciliation(prev => ({
          reveal: snapshot.activeRankingReveal ?? null,
          observedAt: snapshot.observedAt,
          version: (prev?.version ?? 0) + 1,
        }));
        setMissionTimer(
          snapshot.missionRemainingSeconds != null && snapshot.missionTotalSeconds != null
            ? {
                remainingSeconds: snapshot.missionRemainingSeconds,
                totalSeconds: snapshot.missionTotalSeconds,
                // The mission deadline freezes whenever the session is paused, regardless of the
                // primary window's own advancing flag (which reads frozen during a trivia reveal).
                isPaused: snapshot.sessionState === 'Paused',
              }
            : null,
        );
        setSessionState(snapshot.sessionState);
        setIsLoading(false);
        setSnapshotVersion(v => v + 1);
      })
      .catch(err => {
        if (!active) return;
        setError(interpretTimerSnapshotError(err));
        setIsLoading(false);
        // A failed re-fetch must not strand a closed question on a stale interactive view; drop the
        // snapshot question and bump the version so a pending reconcile falls to waiting. It must NOT
        // touch `revealReconciliation`: a failed request carries no authoritative reveal, so replaying
        // the last snapshot's reveal here could reopen a reveal the live events have already closed.
        setActiveQuestion(null);
        setSnapshotVersion(v => v + 1);
      });

    return () => {
      active = false;
    };
  }, [isReconnected, reconnectNonce, resyncNonce, liveSessionId, teamId, token]);

  useEffect(() => {
    return client.onTimerUpdated((notification: SessionTimerUpdatedNotificationDto) => {
      if (notification.liveSessionId !== liveSessionId) return;

      // Pre-game countdown ticks ride SessionTimerUpdated on a short window — route them to the
      // countdown numeral and leave the session clock untouched (matches the web dashboard).
      if (isPregameCountdownTick(notification)) {
        setPregameSecondsLeft(Math.max(0, Math.ceil(notification.remainingMilliseconds / 1000)));
        setSessionState(notification.sessionState);
        return;
      }

      setPregameSecondsLeft(null);
      setTimer({
        remainingSeconds: notification.remainingMilliseconds / 1000,
        totalSeconds: notification.totalMilliseconds / 1000,
        isPaused: notification.isPaused,
        isExpired: notification.isExpired,
        sessionState: notification.sessionState,
        lastSyncedAt: notification.emittedAt,
      });
      // A tick that omits the mission fields carries no mission info — e.g. an older replica during a
      // rolling deploy — and must preserve the current deadline rather than blank the clock. `undefined`
      // (field absent) keeps prior state; an explicit `null` still clears it; two numbers set it.
      setMissionTimer(prev => {
        const rem = notification.missionRemainingMilliseconds;
        const total = notification.missionTotalMilliseconds;
        if (rem === undefined || total === undefined) return prev;
        if (rem === null || total === null) return null;
        return {
          remainingSeconds: rem / 1000,
          totalSeconds: total / 1000,
          isPaused: notification.isPaused,
        };
      });
      setSessionState(notification.sessionState);
    });
  }, [client, liveSessionId]);

  useEffect(() => {
    return client.onStateChanged((notification: SessionStateChangedNotificationDto) => {
      if (notification.liveSessionId !== liveSessionId) return;
      setSessionState(notification.currentState);
      if (notification.currentState === 'Paused') {
        setTimer(prev => (prev ? { ...prev, isPaused: true } : prev));
        setMissionTimer(prev => (prev ? { ...prev, isPaused: true } : prev));
      } else if (notification.currentState === 'Active') {
        setTimer(prev => (prev ? { ...prev, isPaused: false } : prev));
        setMissionTimer(prev => (prev ? { ...prev, isPaused: false } : prev));
      }
    });
  }, [client, liveSessionId]);

  const display = timer
    ? toTimerDisplay(timer.remainingSeconds, timer.totalSeconds, timer.isPaused, timer.isExpired)
    : UNAVAILABLE_TIMER_DISPLAY;

  const missionDisplay = missionTimer
    ? toTimerDisplay(
        missionTimer.remainingSeconds,
        missionTimer.totalSeconds,
        missionTimer.isPaused,
        missionTimer.remainingSeconds <= 0 && missionTimer.totalSeconds > 0,
      )
    : null;

  return { timer, isLoading, error, display, missionDisplay, activeQuestion, revealReconciliation, sessionState, pregameSecondsLeft, snapshotVersion };
}
