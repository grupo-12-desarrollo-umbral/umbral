import { useEffect, useState } from 'react';
import {
  getParticipantTimerSnapshot,
  interpretTimerSnapshotError,
  type TimerSnapshotError,
} from '@/lib/api/sessions';
import {
  toTimerDisplay,
  UNAVAILABLE_TIMER_DISPLAY,
  type SessionTimerUpdatedNotificationDto,
  type TimerDisplay,
} from './timer-types';
import type { SessionsHubClient } from './sessions-hub';

type TimerState = {
  remainingSeconds: number;
  totalSeconds: number;
  isPaused: boolean;
  isExpired: boolean;
  sessionState: string;
  lastSyncedAt: string;
};

export type UseSessionTimerResult = {
  timer: TimerState | null;
  isLoading: boolean;
  error: TimerSnapshotError | null;
  display: TimerDisplay;
};

export function useSessionTimer({
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
}): UseSessionTimerResult {
  const [timer, setTimer] = useState<TimerState | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<TimerSnapshotError | null>(null);

  useEffect(() => {
    if (!isReconnected) return;

    let active = true;
    setIsLoading(true);
    setError(null);

    getParticipantTimerSnapshot(liveSessionId, teamId, token)
      .then(snapshot => {
        if (!active) return;
        setTimer({
          remainingSeconds: snapshot.remainingSeconds,
          totalSeconds: snapshot.totalSeconds,
          isPaused: !snapshot.isAdvancing && !snapshot.isExpired,
          isExpired: snapshot.isExpired,
          sessionState: snapshot.sessionState,
          lastSyncedAt: snapshot.observedAt,
        });
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
  // reconnectNonce intentionally triggers a re-fetch after hub transport reconnects
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isReconnected, reconnectNonce, liveSessionId, teamId, token]);

  useEffect(() => {
    return client.onTimerUpdated((notification: SessionTimerUpdatedNotificationDto) => {
      if (notification.liveSessionId !== liveSessionId) return;

      setTimer({
        remainingSeconds: notification.remainingMilliseconds / 1000,
        totalSeconds: notification.totalMilliseconds / 1000,
        isPaused: notification.isPaused,
        isExpired: notification.isExpired,
        sessionState: notification.sessionState,
        lastSyncedAt: notification.emittedAt,
      });
    });
  }, [client, liveSessionId]);

  const display = timer
    ? toTimerDisplay(timer.remainingSeconds, timer.totalSeconds, timer.isPaused, timer.isExpired)
    : UNAVAILABLE_TIMER_DISPLAY;

  return { timer, isLoading, error, display };
}
