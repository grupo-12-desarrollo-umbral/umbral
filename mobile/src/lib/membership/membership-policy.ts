import { ApiError } from '@/lib/api/client';
import type { ParticipantMembershipAccessDecisionDto } from '@/lib/api/membership';

/**
 * Typed outcome of asking Identity whether the participant may enter a team
 * context. Mirrors the auth-layer `access-policy`: a single, testable place that
 * turns the `AccessDecision` (or a transport failure) into a discriminated
 * union the UI renders without re-deriving any access rules.
 *
 * Identity returns an *access fact* — it does NOT admit the participant to the
 * live session or the real-time hub (that final admission belongs to
 * session-operations / HU-07B). Keep the vocabulary aligned: `allowed` means
 * "may enter the team context", never "joined".
 */
export type MembershipAccessOutcome =
  | { kind: 'allowed'; decision: ParticipantMembershipAccessDecisionDto }
  | { kind: 'denied'; reason: string }
  | { kind: 'forbidden' }
  | { kind: 'invalid-input' }
  | { kind: 'unauthorized' }
  | { kind: 'network-error' }
  | { kind: 'error' };

/**
 * Map a `200` `AccessDecision` body. The endpoint answers `200` even on a denial
 * caused by a bad / mismatched / expired / replayed join token — the structured
 * reason rides on the body, not the status. Foreign-team and non-participant
 * denials never reach here: the backend authorization proxy rejects those with a
 * `403` before any decision is built (see {@link interpretError}).
 */
export function interpretDecision(
  decision: ParticipantMembershipAccessDecisionDto,
): MembershipAccessOutcome {
  if (decision.isAllowed) {
    return { kind: 'allowed', decision };
  }
  return { kind: 'denied', reason: decision.reason };
}

/**
 * Map a transport/HTTP failure to an outcome. Status drives the mapping: the
 * service answers RFC-7807 ProblemDetails (no client `code`/`message` fields),
 * so `ApiError.status` is the only reliable signal.
 *
 * - `403` → foreign team, non-participant, or deactivated actor (proxy refusal).
 * - `401` → trusted-header identity missing/rejected → stale session.
 * - `400` → malformed session/team identifiers.
 * - `0`   → network failure (see `apiClient`).
 */
export function interpretError(error: unknown): MembershipAccessOutcome {
  if (error instanceof ApiError) {
    switch (error.status) {
      case 0:
        return { kind: 'network-error' };
      case 400:
        return { kind: 'invalid-input' };
      case 401:
        return { kind: 'unauthorized' };
      case 403:
        return { kind: 'forbidden' };
    }
  }
  return { kind: 'error' };
}
