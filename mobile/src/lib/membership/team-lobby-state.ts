import type { SessionTeamDto } from '@/lib/api/teams';

export type TeamLobbyCollectionState =
  | { kind: 'empty'; message: string }
  | { kind: 'assigned-team-unavailable'; message: string }
  | { kind: 'ready' };

export function evaluateTeamLobbyState(
  teams: SessionTeamDto[],
): TeamLobbyCollectionState {
  if (teams.length === 0) {
    return {
      kind: 'empty',
      message: 'No teams available yet. Ask your operator.',
    };
  }

  const hasMine = teams.some((team) => team.joinState === 'mine');
  const onlyLockedTeams = teams.every((team) => team.joinState === 'locked');

  if (!hasMine && onlyLockedTeams) {
    return {
      kind: 'assigned-team-unavailable',
      message:
        'Your assigned team is unavailable right now. Ask your operator to reactivate it or move you.',
    };
  }

  return { kind: 'ready' };
}
