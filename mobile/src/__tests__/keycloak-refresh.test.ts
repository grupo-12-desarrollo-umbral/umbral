import { refreshTokens, KeycloakError } from '@/lib/auth/keycloak';

jest.mock('expo/fetch', () => ({
  fetch: jest.fn(),
}));

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { fetch: mockFetch } = require('expo/fetch') as { fetch: jest.Mock };

beforeEach(() => {
  mockFetch.mockReset();
  process.env.EXPO_PUBLIC_KEYCLOAK_URL = 'http://localhost:8080';
  process.env.EXPO_PUBLIC_KEYCLOAK_REALM = 'umbral';
  process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID = 'umbral-mobile';
});

test('successful refresh returns the rotated token pair', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: true,
    status: 200,
    json: () =>
      Promise.resolve({ access_token: 'acc_new', refresh_token: 'ref_rotated' }),
  });

  await expect(refreshTokens('ref_old')).resolves.toEqual({
    accessToken: 'acc_new',
    refreshToken: 'ref_rotated',
  });
});

test('posts the public-client refresh grant', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: true,
    status: 200,
    json: () => Promise.resolve({ access_token: 'a', refresh_token: 'r' }),
  });

  await refreshTokens('ref_old');

  const [url, init] = mockFetch.mock.calls[0] as [string, { body: string }];
  expect(url).toBe('http://localhost:8080/realms/umbral/protocol/openid-connect/token');
  const body = new URLSearchParams(init.body);
  expect(body.get('grant_type')).toBe('refresh_token');
  expect(body.get('client_id')).toBe('umbral-mobile');
  expect(body.get('refresh_token')).toBe('ref_old');
  // Public client: a secret must never be sent.
  expect(body.get('client_secret')).toBeNull();
});

test('invalid_grant → session-expired (the only reason that ends a session)', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status: 400,
    json: () => Promise.resolve({ error: 'invalid_grant' }),
  });

  const err = await refreshTokens('ref_dead').catch((e) => e);
  expect(err).toBeInstanceOf(KeycloakError);
  expect((err as KeycloakError).reason).toBe('session-expired');
});

test('network failure → network, never session-expired', async () => {
  mockFetch.mockRejectedValueOnce(new TypeError('Network request failed'));

  const err = await refreshTokens('ref_live').catch((e) => e);
  expect((err as KeycloakError).reason).toBe('network');
});

test('server error → unknown, never session-expired', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status: 503,
    json: () => Promise.resolve({ error: 'server_error' }),
  });

  const err = await refreshTokens('ref_live').catch((e) => e);
  expect((err as KeycloakError).reason).toBe('unknown');
});

test('401 without invalid_grant stays transient', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status: 401,
    json: () => Promise.resolve({ error: 'unauthorized_client' }),
  });

  const err = await refreshTokens('ref_live').catch((e) => e);
  expect((err as KeycloakError).reason).toBe('unknown');
});
