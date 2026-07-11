import { apiClient } from './client';

export type SessionTeamDto = {
  // Runtime Team.TeamId — the id the self-join route (`/teams/{id}/join`) targets.
  teamId: string;
  // Reference/catalog id identity-access keys registered_teams by — the id the membership guard on
  // validate/reconnect/answer-submit needs. Null only for teams with no reference association.
  referenceTeamId: string | null;
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
