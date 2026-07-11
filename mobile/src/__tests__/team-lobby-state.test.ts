import { evaluateTeamLobbyState } from '@/lib/membership/team-lobby-state';
import type { SessionTeamDto } from '@/lib/api/teams';

function makeTeam(
  overrides: Partial<SessionTeamDto> = {},
): SessionTeamDto {
  return {
    teamId: overrides.teamId ?? 'team-1',
    referenceTeamId: overrides.referenceTeamId ?? 'team-1-reference',
    displayName: overrides.displayName ?? 'Blue Owls',
    joinState: overrides.joinState ?? 'joinable',
  };
}

describe('evaluateTeamLobbyState', () => {
  test('empty team list -> no teams message', () => {
    expect(evaluateTeamLobbyState([])).toEqual({
      kind: 'empty',
      message: 'No teams available yet. Ask your operator.',
    });
  });

  test('all visible teams locked and none mine -> assigned team unavailable', () => {
    expect(
      evaluateTeamLobbyState([
        makeTeam({ teamId: 'team-1', joinState: 'locked' }),
        makeTeam({ teamId: 'team-2', displayName: 'Red Foxes', joinState: 'locked' }),
      ]),
    ).toEqual({
      kind: 'assigned-team-unavailable',
      message:
        'Your assigned team is unavailable right now. Ask your operator to reactivate it or move you.',
    });
  });

  test('mine plus locked teams -> ready', () => {
    expect(
      evaluateTeamLobbyState([
        makeTeam({ teamId: 'team-1', joinState: 'mine' }),
        makeTeam({ teamId: 'team-2', displayName: 'Red Foxes', joinState: 'locked' }),
      ]),
    ).toEqual({ kind: 'ready' });
  });

  test('joinable teams -> ready', () => {
    expect(
      evaluateTeamLobbyState([
        makeTeam({ joinState: 'joinable' }),
        makeTeam({ teamId: 'team-2', displayName: 'Red Foxes', joinState: 'joinable' }),
      ]),
    ).toEqual({ kind: 'ready' });
  });
});
