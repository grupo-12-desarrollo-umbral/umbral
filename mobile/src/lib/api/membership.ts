import { apiClient } from './client';

/**
 * Request body for the read-only membership-validation surface
 * (`POST /api/permissions/participant-membership-access`).
 *
 * `liveSessionId` is an opaque correlation id; `teamId` is the Identity
 * reference-data team id the participant was assigned to. `token` is the
 * one-time join token the operator handed the participant out-of-band — it is
 * optional: when omitted, Identity validates membership only.
 */
export type ValidateParticipantMembershipAccessRequest = {
  liveSessionId: string;
  teamId: string;
  token?: string;
};

/**
 * `AccessDecision` access fact returned by Identity. It informs admission but
 * does NOT admit the participant to the live session or the real-time hub —
 * that final decision belongs to session-operations (HU-07B).
 */
export type ParticipantMembershipAccessDecisionDto = {
  capability: string;
  isAllowed: boolean;
  reason: string;
  liveSessionId: string;
  teamId: string;
};

export function validateParticipantMembershipAccess(
  request: ValidateParticipantMembershipAccessRequest,
): Promise<ParticipantMembershipAccessDecisionDto> {
  return apiClient.post<ParticipantMembershipAccessDecisionDto>(
    '/api/permissions/participant-membership-access',
    request,
  );
}
