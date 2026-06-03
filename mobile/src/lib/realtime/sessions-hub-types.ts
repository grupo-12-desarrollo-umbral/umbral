export type ReconnectParticipantHubRequest = {
  teamId: string;
  displayName: string;
  teamCapacity: number;
  token?: string | null;
};

export type ReconnectParticipantResultDto = {
  liveSessionId: string;
  teamId: string;
  teamDisplayName: string;
  sessionParticipantId: string;
  participantDisplayName: string;
  sessionState: string;
  isReconnect: boolean;
  joinedAt: string;
  lastSeenAt: string;
};

export type ReconnectContext = {
  liveSessionId: string;
  teamId: string;
  displayName: string;
  teamCapacity: number;
  token?: string | null;
  lastSeenAt?: string;
};
