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

// Anonymous forgot-password (ADR-0016 §1): posts the email to the backend, which emails a reset link if
// an account exists. Always resolves void — the backend returns 202 with no distinguishing body, so the
// caller (and UI) can never tell whether the address is registered. Real API/network errors still
// reject; the empty 202 body is expected and ignored.
export async function requestPasswordReset(email: string): Promise<void> {
  // The endpoint returns 204 No Content (same response whether or not the email exists); the shared
  // client resolves void without parsing a body. Real failures still surface as ApiError.
  await apiClient.post<void>('/api/users/forgot-password', { email });
}
