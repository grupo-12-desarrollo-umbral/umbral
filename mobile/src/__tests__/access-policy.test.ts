import { evaluateAccess } from '@/lib/auth/access-policy';
import type { AuthenticateUserResultDto } from '@/lib/api/identity';

function makeDto(
  overrides: Partial<{ role: string; isActive: boolean; isAllowed: boolean }> = {},
): AuthenticateUserResultDto {
  return {
    actor: {
      externalIdentityId: 'uid-1',
      displayName: 'Test User',
      email: 'test@example.com',
      role: overrides.role ?? 'Participant',
      isActive: overrides.isActive ?? true,
    },
    access: {
      capability: 'access_app',
      isAllowed: overrides.isAllowed ?? true,
      reason: 'ok',
    },
  };
}

test('active participant with access allowed → allowed', () => {
  expect(evaluateAccess(makeDto())).toEqual({ allowed: true });
});

test('Administrator role → wrong-role', () => {
  expect(evaluateAccess(makeDto({ role: 'Administrator' }))).toEqual({
    allowed: false,
    reason: 'wrong-role',
  });
});

test('Operator role → wrong-role', () => {
  expect(evaluateAccess(makeDto({ role: 'Operator' }))).toEqual({
    allowed: false,
    reason: 'wrong-role',
  });
});

test('deactivated Participant → deactivated', () => {
  expect(evaluateAccess(makeDto({ isActive: false }))).toEqual({
    allowed: false,
    reason: 'deactivated',
  });
});

test('access.isAllowed false → access-denied', () => {
  expect(evaluateAccess(makeDto({ isAllowed: false }))).toEqual({
    allowed: false,
    reason: 'access-denied',
  });
});

test('wrong-role check runs before deactivated check', () => {
  expect(evaluateAccess(makeDto({ role: 'Operator', isActive: false }))).toEqual({
    allowed: false,
    reason: 'wrong-role',
  });
});
