import { apiClient } from './client';

export type AuthenticateUserResultDto = {
  actor: {
    externalIdentityId: string;
    displayName: string;
    email: string;
    role: string;
    isActive: boolean;
  };
  access: {
    capability: string;
    isAllowed: boolean;
    reason: string;
  };
};

export type AuthenticatedActorProfileDto = {
  externalIdentityId: string;
  displayName: string;
  email: string;
  role: string;
  isActive: boolean;
};

export function bootstrapAuthenticatedUser(
  displayName: string,
): Promise<AuthenticateUserResultDto> {
  return apiClient.post<AuthenticateUserResultDto>('/api/users/authenticated', {
    displayName,
  });
}

export function getAuthenticatedProfile(): Promise<AuthenticatedActorProfileDto> {
  return apiClient.get<AuthenticatedActorProfileDto>('/api/users/me');
}

export type RegisterParticipantResultDto = {
  email: string;
  role: string;
};

// Anonymous self-registration (ADR-0016 §1): posts the custom-form fields to the backend register
// endpoint, which provisions the Keycloak account as a Participant and sends the verification email.
// No token is sent (the caller has no account yet); the role is server-fixed and never passed here.
export function registerParticipant(
  displayName: string,
  email: string,
  password: string,
): Promise<RegisterParticipantResultDto> {
  return apiClient.post<RegisterParticipantResultDto>('/api/users/register', {
    displayName,
    email,
    password,
  });
}
