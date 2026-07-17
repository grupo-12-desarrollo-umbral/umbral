import { useRef, useState } from 'react';
import {
  joinSessionTeam,
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

// The in-flight join, identified by a stable object so completion can prove it still owns the guard,
// plus the monotonic generation it was armed under so an invalidated attempt cannot publish hook state.
type JoinAttempt = {
  generation: number;
  promise: Promise<TeamJoinOutcome>;
};

export function useTeamJoin() {
  const [status, setStatus] = useState<TeamJoinStatus>('idle');
  const [outcome, setOutcome] = useState<TeamJoinOutcome | null>(null);

  // Synchronous re-entry guard, mirroring the answer- and target-scan hooks. `status` updates are async,
  // so a fast double tap can fire two `join`s before the first `setStatus('joining')` is observed — a bare
  // status check would let both POST. The ref holds the in-flight attempt so a concurrent call collapses
  // onto it (one POST, same resolved outcome) rather than starting a second. The API call is deferred to a
  // microtask so the ref is armed *before* request construction can run — the guard is never assigned after
  // the POST has begun. The server stays authoritative on membership; this only collapses the same-device
  // double tap.
  const joiningRef = useRef<JoinAttempt | null>(null);
  // A reset (or a superseding attempt) bumps this. Every completion checks it before publishing so an
  // invalidated attempt cannot overwrite idle state or a newer attempt's outcome. `reset()` invalidates
  // late client-side completion; it does not cancel the HTTP request, which may still land.
  const generationRef = useRef(0);

  function join(
    sessionCode: string,
    teamId: string,
  ): Promise<TeamJoinOutcome> {
    if (joiningRef.current) {
      return joiningRef.current.promise;
    }

    const generation = (generationRef.current += 1);

    // Defer the API invocation to a microtask: the attempt is assigned to `joiningRef` synchronously
    // below before this body runs, so request construction can never begin before the guard is armed.
    const attempt: JoinAttempt = {
      generation,
      promise: Promise.resolve().then(async () => {
        let result: TeamJoinOutcome;
        try {
          const response = await joinSessionTeam(sessionCode, teamId);
          result = { kind: 'joined', response };
        } catch (error) {
          result = resolveTeamJoinError(error);
        }
        // Identity-safe cleanup: only clear the guard if it still points to this exact attempt, so an
        // older completion cannot clear a newer request's guard.
        if (joiningRef.current === attempt) {
          joiningRef.current = null;
        }
        // Only publish if this attempt still owns the current generation. A reset or a superseding
        // attempt has bumped the generation, and this late completion must not touch hook state.
        if (generationRef.current === generation) {
          setOutcome(result);
          setStatus('resolved');
        }
        return result;
      }),
    };

    joiningRef.current = attempt;
    setStatus('joining');
    setOutcome(null);
    return attempt.promise;
  }

  function reset(): void {
    // Invalidate any in-flight attempt (its late completion becomes a no-op) and restore idle UI state.
    generationRef.current += 1;
    joiningRef.current = null;
    setStatus('idle');
    setOutcome(null);
  }

  return { status, outcome, join, reset };
}
