import type { TimerSnapshotError } from '@/lib/api/sessions';

/**
 * Participant-facing copy for a failed ranking-snapshot fetch (HU-25B). Shared by
 * every surface that renders live standings — the treasure-hunt board's TEAMS tab
 * and the trivia surface's ALL TEAMS panel — so the wording stays in one place.
 */
export function rankingErrorCopy(error: TimerSnapshotError): string {
  switch (error) {
    case 'network-error':
      return "Couldn't reach the standings — check your connection.";
    case 'unauthorized':
      return 'Your session expired — the standings couldn’t load.';
    case 'forbidden':
      return "You don't have access to this session's standings.";
    case 'not-found':
      return "This session's standings aren't available.";
    case 'timer-unavailable':
      return "Standings aren't ready yet — hang tight.";
    default:
      return "Couldn't load the standings.";
  }
}
