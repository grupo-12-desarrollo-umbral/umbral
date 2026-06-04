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

export type JoinSessionTeamRequest = {
  liveSessionId: string;
};

export type JoinSessionTeamResponse = {
  teamMembershipId: string;
};

export function listSessionTeams(
  sessionCode: string,
): Promise<SessionTeamLobbyDto> {
  return apiClient.get<SessionTeamLobbyDto>(
    `/api/sessions/${encodeURIComponent(sessionCode)}/teams`,
    {
      cache: 'no-store',
      headers: {
        'Cache-Control': 'no-cache',
        Pragma: 'no-cache',
      },
    },
  );
}

export function joinSessionTeam(
  teamId: string,
  request: JoinSessionTeamRequest,
): Promise<JoinSessionTeamResponse> {
  return apiClient.post<JoinSessionTeamResponse>(
    `/api/teams/${encodeURIComponent(teamId)}/participants/self`,
    request,
  );
}
