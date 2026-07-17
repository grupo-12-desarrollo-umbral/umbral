import { deriveCredentials, signInWithPassword, KeycloakError } from '@/lib/auth/keycloak';

jest.mock('expo/fetch', () => ({
  fetch: jest.fn(),
}));

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { fetch: mockFetch } = require('expo/fetch') as { fetch: jest.Mock };

function tokenWithClaims(claims: Record<string, unknown>): string {
  const payload = btoa(JSON.stringify(claims))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
  return `header.${payload}.signature`;
}

beforeEach(() => {
  mockFetch.mockReset();
  process.env.EXPO_PUBLIC_KEYCLOAK_URL = 'http://localhost:8080';
  process.env.EXPO_PUBLIC_KEYCLOAK_REALM = 'umbral';
  process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID = 'umbral-mobile';
});

test('invalid_grant error → wrong-credentials', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status: 400,
    json: () => Promise.resolve({ error: 'invalid_grant' }),
  });

  await expect(signInWithPassword('user@test.com', 'wrong')).rejects.toMatchObject({
    reason: 'wrong-credentials',
  });
});

test('HTTP 401 → wrong-credentials regardless of error body', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status: 401,
    json: () => Promise.resolve({ error: 'unauthorized' }),
  });

  await expect(signInWithPassword('user@test.com', 'wrong')).rejects.toMatchObject({
    reason: 'wrong-credentials',
  });
});

test('network failure → network', async () => {
  mockFetch.mockRejectedValueOnce(new TypeError('Network request failed'));

  await expect(signInWithPassword('user@test.com', 'pass')).rejects.toMatchObject({
    reason: 'network',
  });
});

test('unknown server error → unknown', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status: 500,
    json: () => Promise.resolve({ error: 'server_error' }),
  });

  const err = await signInWithPassword('user@test.com', 'pass').catch((e) => e);
  expect(err).toBeInstanceOf(KeycloakError);
  expect((err as KeycloakError).reason).toBe('unknown');
});

test('successful login returns mapped tokens', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: true,
    status: 200,
    json: () =>
      Promise.resolve({
        access_token: 'acc_abc',
        refresh_token: 'ref_xyz',
        id_token: 'hdr.payload.sig',
      }),
  });

  const tokens = await signInWithPassword('user@test.com', 'correct');
  expect(tokens).toEqual({
    accessToken: 'acc_abc',
    refreshToken: 'ref_xyz',
    idToken: 'hdr.payload.sig',
  });
});

test('uses the participant chosen name instead of the email-backed Keycloak username', () => {
  const credentials = deriveCredentials(
    tokenWithClaims({
      preferred_username: 'nina@example.com',
      name: 'Nina Participant',
      given_name: 'Nina Participant',
      email: 'nina@example.com',
    }),
  );

  expect(credentials).toEqual({
    displayName: 'Nina Participant',
    email: 'nina@example.com',
  });
});

test.each([
  [{ given_name: 'Nina', preferred_username: 'nina@example.com', email: 'nina@example.com' }, 'Nina'],
  [{ preferred_username: 'legacy-user', email: 'legacy@example.com' }, 'legacy-user'],
  [{ email: 'email-only@example.com' }, 'email-only@example.com'],
  [{}, 'User'],
])('falls back safely when a token has an incomplete profile: %j', (claims, expected) => {
  expect(deriveCredentials(tokenWithClaims(claims)).displayName).toBe(expected);
});
