import type { AuthenticateUserResultDto } from '@/lib/api/identity';

export type RejectionReason = 'deactivated' | 'wrong-role' | 'access-denied';

export type AccessResult =
  | { allowed: true }
  | { allowed: false; reason: RejectionReason };

export function evaluateAccess(result: AuthenticateUserResultDto): AccessResult {
  if (result.actor.role !== 'Participant') {
    return { allowed: false, reason: 'wrong-role' };
  }
  if (!result.actor.isActive) {
    return { allowed: false, reason: 'deactivated' };
  }
  if (!result.access.isAllowed) {
    return { allowed: false, reason: 'access-denied' };
  }
  return { allowed: true };
}
