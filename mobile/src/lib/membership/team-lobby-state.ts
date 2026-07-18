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
      message: 'Todavía no hay equipos disponibles. Consulta con tu operador.',
    };
  }

  const hasMine = teams.some((team) => team.joinState === 'mine');
  const onlyLockedTeams = teams.every((team) => team.joinState === 'locked');

  if (!hasMine && onlyLockedTeams) {
    return {
      kind: 'assigned-team-unavailable',
      message:
        'Tu equipo asignado no está disponible en este momento. Pídele a tu operador que lo reactive o que te cambie de equipo.',
    };
  }

  return { kind: 'ready' };
}
