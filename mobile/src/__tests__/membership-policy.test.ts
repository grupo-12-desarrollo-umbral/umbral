import {
  interpretDecision,
  interpretError,
} from '@/lib/membership/membership-policy';
import { ApiError } from '@/lib/api/client';
import type { ParticipantMembershipAccessDecisionDto } from '@/lib/api/membership';

function makeDecision(
  overrides: Partial<ParticipantMembershipAccessDecisionDto> = {},
): ParticipantMembershipAccessDecisionDto {
  return {
    capability: 'ParticipantExperience',
    isAllowed: overrides.isAllowed ?? true,
    reason: overrides.reason ?? 'Participant membership validated.',
    liveSessionId: overrides.liveSessionId ?? 'session-1',
    teamId: overrides.teamId ?? 'team-1',
  };
}

describe('interpretDecision', () => {
  test('allowed decision → allowed with the decision attached', () => {
    const decision = makeDecision({ isAllowed: true });
    expect(interpretDecision(decision)).toEqual({ kind: 'allowed', decision });
  });

  test('denied decision → denied carrying the structured reason', () => {
    const decision = makeDecision({
      isAllowed: false,
      reason: 'Join token has expired.',
    });
    expect(interpretDecision(decision)).toEqual({
      kind: 'denied',
      reason: 'Join token has expired.',
    });
  });
});

describe('interpretError', () => {
  test('403 → forbidden (foreign team / non-participant)', () => {
    expect(interpretError(new ApiError(403, 'api_error', 'Forbidden.'))).toEqual({
      kind: 'forbidden',
    });
  });

  test('401 → unauthorized (stale session)', () => {
    expect(interpretError(new ApiError(401, 'api_error', 'Unauthorized.'))).toEqual({
      kind: 'unauthorized',
    });
  });

  test('400 → invalid-input', () => {
    expect(interpretError(new ApiError(400, 'api_error', 'Validation failed.'))).toEqual({
      kind: 'invalid-input',
    });
  });

  test('status 0 → network-error', () => {
    expect(interpretError(new ApiError(0, 'network_error', 'Network request failed'))).toEqual({
      kind: 'network-error',
    });
  });

  test('unmapped HTTP status → error', () => {
    expect(interpretError(new ApiError(500, 'api_error', 'boom'))).toEqual({
      kind: 'error',
    });
  });

  test('non-ApiError throwable → error', () => {
    expect(interpretError(new Error('unexpected'))).toEqual({ kind: 'error' });
  });
});
