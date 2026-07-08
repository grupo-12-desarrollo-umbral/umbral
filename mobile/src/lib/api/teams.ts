import { apiClient } from './client';

export type SessionTeamDto = {
  teamId: string;
  displayName: string;
  joinState: 'mine' | 'joinable' | 'locked';
};

export type SessionTeamLobbyDto = {
  liveSessionId: string;
  sessionCode: string;
  teams: SessionTeamDto[];
};

export type JoinSessionTeamResponse = {
  teamMembershipId: string;
};

export function listSessionTeams(
  sessionCode: string,
): Promise<SessionTeamLobbyDto> {
  return apiClient.get<SessionTeamLobbyDto>(
    `/api/sessions/by-code/${encodeURIComponent(sessionCode)}/teams/lobby`,
    {
      cache: 'no-store',
      headers: {
        'Cache-Control': 'no-cache',
        Pragma: 'no-cache',
      },
    },
  );
}

// Runtime teamId already binds the session, so the route carries everything — no body.
export function joinSessionTeam(
  sessionCode: string,
  teamId: string,
): Promise<JoinSessionTeamResponse> {
  return apiClient.post<JoinSessionTeamResponse>(
    `/api/sessions/by-code/${encodeURIComponent(sessionCode)}/teams/${encodeURIComponent(teamId)}/join`,
    undefined,
  );
}
