export type ReconnectParticipantHubRequest = {
  teamId: string;
  displayName: string;
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
  token?: string | null;
  lastSeenAt?: string;
};

export type SessionStateChangedNotificationDto = {
  liveSessionId: string;
  previousState: string;
  currentState: string;
  changedAt: string;
};
