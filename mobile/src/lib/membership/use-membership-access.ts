import { useState } from 'react';
import {
  validateParticipantMembershipAccess,
  type ValidateParticipantMembershipAccessRequest,
} from '@/lib/api/membership';
import {
  interpretDecision,
  interpretError,
  type MembershipAccessOutcome,
} from './membership-policy';

export type MembershipAccessStatus = 'idle' | 'validating' | 'resolved';

/**
 * Orchestrates the read-only membership-validation call and exposes a thin state
 * machine so screens stay declarative. Mirrors the auth provider's shape on a
 * smaller scale: the network call lives here, the mapping rules live in
 * `membership-policy`, and the screen only renders the resulting outcome.
 *
 * Validation is non-consuming by contract — calling this never burns the
 * participant's single-use join token, so it is safe to retry.
 */
export function useMembershipAccess() {
  const [status, setStatus] = useState<MembershipAccessStatus>('idle');
  const [outcome, setOutcome] = useState<MembershipAccessOutcome | null>(null);

  async function validate(
    request: ValidateParticipantMembershipAccessRequest,
  ): Promise<MembershipAccessOutcome> {
    setStatus('validating');
    setOutcome(null);
    let result: MembershipAccessOutcome;
    try {
      const decision = await validateParticipantMembershipAccess(request);
      result = interpretDecision(decision);
    } catch (error) {
      result = interpretError(error);
    }
    setOutcome(result);
    setStatus('resolved');
    return result;
  }

  function reset(): void {
    setStatus('idle');
    setOutcome(null);
  }

  return { status, outcome, validate, reset };
}
