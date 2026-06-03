import { useState } from 'react';
import {
  joinSessionTeam,
  type JoinSessionTeamRequest,
  type JoinSessionTeamResponse,
} from '@/lib/api/teams';
import { ApiError } from '@/lib/api/client';

export type TeamJoinStatus = 'idle' | 'joining' | 'resolved';

export type TeamJoinOutcome =
  | { kind: 'joined'; response: JoinSessionTeamResponse }
  | { kind: 'unauthorized' }
  | { kind: 'failed'; message: string };

export function resolveTeamJoinError(error: unknown): TeamJoinOutcome {
  if (error instanceof ApiError && error.status === 401) {
    return { kind: 'unauthorized' };
  }

  if (error instanceof ApiError && error.status === 0) {
    return {
      kind: 'failed',
      message: 'Network error. Check your connection and try again.',
    };
  }

  if (error instanceof ApiError && error.status === 404) {
    return {
      kind: 'failed',
      message: 'This team is no longer available for the selected session.',
    };
  }

  if (
    error instanceof ApiError &&
    (error.status === 403 || error.status === 409)
  ) {
    return {
      kind: 'failed',
      message:
        "You don't belong to this team. You can only enter the team you were assigned to.",
    };
  }

  return {
    kind: 'failed',
    message: 'Failed to join team. Please try again.',
  };
}

export function useTeamJoin() {
  const [status, setStatus] = useState<TeamJoinStatus>('idle');
  const [outcome, setOutcome] = useState<TeamJoinOutcome | null>(null);

  async function join(
    teamId: string,
    request: JoinSessionTeamRequest,
  ): Promise<TeamJoinOutcome> {
    setStatus('joining');
    setOutcome(null);
    let result: TeamJoinOutcome;
    try {
      const response = await joinSessionTeam(teamId, request);
      result = { kind: 'joined', response };
    } catch (error) {
      result = resolveTeamJoinError(error);
    }
    setOutcome(result);
    setStatus('resolved');
    return result;
  }

  function reset(): void {
    setStatus('idle');
    setOutcome(null);
  }

  return { status, outcome, join, reset };
}
