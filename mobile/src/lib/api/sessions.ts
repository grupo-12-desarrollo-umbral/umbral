import { apiClient, ApiError } from './client';
import type { SessionTimerSnapshotDto } from '@/lib/realtime/timer-types';

export type TimerSnapshotError =
  | 'network-error'
  | 'unauthorized'
  | 'forbidden'
  | 'not-found'
  | 'timer-unavailable'
  | 'error';

export function interpretTimerSnapshotError(error: unknown): TimerSnapshotError {
  if (error instanceof ApiError) {
    if (error.status === 0) return 'network-error';
    if (error.status === 401) return 'unauthorized';
    if (error.status === 403) return 'forbidden';
    if (error.status === 404) return 'not-found';
    if (error.status === 409) return 'timer-unavailable';
    return 'error';
  }
  return 'error';
}

export function getParticipantTimerSnapshot(
  liveSessionId: string,
  teamId: string,
  token?: string | null,
): Promise<SessionTimerSnapshotDto> {
  const params = new URLSearchParams({ teamId });
  if (token) {
    params.set('token', token);
  }
  return apiClient.get<SessionTimerSnapshotDto>(
    `/api/sessions/${encodeURIComponent(liveSessionId)}/participants/timer?${params.toString()}`,
    {
      cache: 'no-store',
      headers: {
        'Cache-Control': 'no-cache',
        Pragma: 'no-cache',
      },
    },
  );
}
