import { useCallback, useState } from 'react';
import {
  listSessionTeams,
  type SessionTeamDto,
} from '@/lib/api/teams';
import { ApiError } from '@/lib/api/client';

export type TeamLobbyStatus = 'idle' | 'loading' | 'resolved' | 'error';

export type TeamLobbyFailure =
  | { kind: 'invalid-input'; message: string }
  | { kind: 'not-found'; message: string }
  | { kind: 'unauthorized' }
  | { kind: 'forbidden'; message: string }
  | { kind: 'network-error'; message: string }
  | { kind: 'error'; message: string };

export function interpretLobbyError(error: unknown): TeamLobbyFailure {
  if (error instanceof ApiError && error.status === 0) {
    return {
      kind: 'network-error',
      message: 'Network error. Check your connection and try again.',
    };
  }

  if (error instanceof ApiError && error.status === 400) {
    return {
      kind: 'invalid-input',
      message: 'Enter a valid 6-character session code.',
    };
  }

  if (error instanceof ApiError && error.status === 401) {
    return { kind: 'unauthorized' };
  }

  if (error instanceof ApiError && error.status === 403) {
    return {
      kind: 'forbidden',
      message: 'Your participant access is no longer available. Contact your operator.',
    };
  }

  if (error instanceof ApiError && error.status === 404) {
    return {
      kind: 'not-found',
      message: 'Session code not found. Check the code and try again.',
    };
  }

  return {
    kind: 'error',
    message: 'Failed to load teams. Please try again.',
  };
}

export function useTeamLobby(sessionCode: string) {
  const [status, setStatus] = useState<TeamLobbyStatus>('idle');
  const [liveSessionId, setLiveSessionId] = useState<string | null>(null);
  const [teams, setTeams] = useState<SessionTeamDto[]>([]);
  const [failure, setFailure] = useState<TeamLobbyFailure | null>(null);

  const load = useCallback(async (): Promise<void> => {
    if (!sessionCode.trim()) {
      setStatus('error');
      setLiveSessionId(null);
      setTeams([]);
      setFailure({
        kind: 'invalid-input',
        message: 'Session code is required.',
      });
      return;
    }

    setStatus('loading');
    setFailure(null);
    try {
      const result = await listSessionTeams(sessionCode.trim().toUpperCase());
      setLiveSessionId(result.liveSessionId);
      setTeams(result.teams);
      setStatus('resolved');
      setFailure(null);
    } catch (error) {
      setLiveSessionId(null);
      setTeams([]);
      setFailure(interpretLobbyError(error));
      setStatus('error');
    }
  }, [sessionCode]);

  return {
    status,
    liveSessionId,
    teams,
    failure,
    errorMessage: failure?.kind === 'unauthorized' ? null : (failure?.message ?? null),
    load,
  };
}
