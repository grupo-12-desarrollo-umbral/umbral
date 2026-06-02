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
