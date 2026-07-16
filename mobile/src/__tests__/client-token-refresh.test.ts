import { apiClient, ApiError } from '@/lib/api/client';

const mockFetch = jest.fn();
const mockGetValidAccessToken = jest.fn();
const mockRefreshAccessToken = jest.fn();

jest.mock('expo/fetch', () => ({ fetch: (...args: unknown[]) => mockFetch(...args) }));
jest.mock('@/lib/auth/token-provider', () => ({
  getValidAccessToken: (...args: unknown[]) => mockGetValidAccessToken(...args),
  refreshAccessToken: (...args: unknown[]) => mockRefreshAccessToken(...args),
}));
jest.mock('@/lib/host', () => ({ apiBaseUrl: () => 'http://localhost:8000' }));

function unauthorized() {
  return {
    ok: false,
    status: 401,
    json: () => Promise.resolve({ code: 'unauthorized', message: 'Token expired' }),
  };
}

function ok(body: unknown) {
  return { ok: true, status: 200, json: () => Promise.resolve(body) };
}

function authHeaderOf(callIndex: number): string | undefined {
  const [, init] = mockFetch.mock.calls[callIndex] as [
    string,
    { headers: Record<string, string> },
  ];
  return init.headers['Authorization'];
}

beforeEach(() => {
  jest.clearAllMocks();
  mockGetValidAccessToken.mockResolvedValue('acc_stale');
});

test('attaches the token the provider vouches for', async () => {
  mockFetch.mockResolvedValueOnce(ok({ id: 1 }));

  await apiClient.get('/api/thing');

  expect(authHeaderOf(0)).toBe('Bearer acc_stale');
});

test('401 → refreshes once and replays the request with the new token', async () => {
  mockFetch.mockResolvedValueOnce(unauthorized()).mockResolvedValueOnce(ok({ id: 1 }));
  mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');
  mockGetValidAccessToken.mockResolvedValueOnce('acc_stale').mockResolvedValueOnce('acc_fresh');

  await expect(apiClient.get('/api/thing')).resolves.toEqual({ id: 1 });

  expect(mockRefreshAccessToken).toHaveBeenCalledTimes(1);
  expect(mockFetch).toHaveBeenCalledTimes(2);
  expect(authHeaderOf(0)).toBe('Bearer acc_stale');
  expect(authHeaderOf(1)).toBe('Bearer acc_fresh');
});

test('replays POST method and body unchanged', async () => {
  mockFetch.mockResolvedValueOnce(unauthorized()).mockResolvedValueOnce(ok({ ok: true }));
  mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');

  await apiClient.post('/api/thing', { answer: 'a' });

  const [, init] = mockFetch.mock.calls[1] as [string, { method: string; body: string }];
  expect(init.method).toBe('POST');
  expect(init.body).toBe(JSON.stringify({ answer: 'a' }));
});

test('a second 401 after refresh surfaces ApiError(401) instead of looping', async () => {
  mockFetch.mockResolvedValueOnce(unauthorized()).mockResolvedValueOnce(unauthorized());
  mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');

  const err = await apiClient.get('/api/thing').catch((e) => e);

  expect(err).toBeInstanceOf(ApiError);
  expect((err as ApiError).status).toBe(401);
  expect((err as ApiError).code).toBe('unauthorized');
  // Exactly one refresh and one replay — never a loop.
  expect(mockRefreshAccessToken).toHaveBeenCalledTimes(1);
  expect(mockFetch).toHaveBeenCalledTimes(2);
});

test('401 with an unrefreshable session surfaces the 401 without replaying', async () => {
  mockFetch.mockResolvedValueOnce(unauthorized());
  mockRefreshAccessToken.mockResolvedValueOnce(null);

  const err = await apiClient.get('/api/thing').catch((e) => e);

  expect((err as ApiError).status).toBe(401);
  expect(mockFetch).toHaveBeenCalledTimes(1);
});

test('non-401 errors are not retried and keep their ApiError shape', async () => {
  mockFetch.mockResolvedValueOnce({
    ok: false,
    status: 409,
    json: () => Promise.resolve({ code: 'conflict', message: 'Session not accepting' }),
  });

  const err = await apiClient.get('/api/thing').catch((e) => e);

  expect((err as ApiError).status).toBe(409);
  expect((err as ApiError).code).toBe('conflict');
  expect(mockRefreshAccessToken).not.toHaveBeenCalled();
  expect(mockFetch).toHaveBeenCalledTimes(1);
});

test('network failures keep ApiError(0, network_error)', async () => {
  mockFetch.mockRejectedValueOnce(new TypeError('Network request failed'));

  const err = await apiClient.get('/api/thing').catch((e) => e);

  expect((err as ApiError).status).toBe(0);
  expect((err as ApiError).code).toBe('network_error');
});

test('204 replayed after a refresh still resolves with no body', async () => {
  mockFetch
    .mockResolvedValueOnce(unauthorized())
    .mockResolvedValueOnce({ ok: true, status: 204, json: () => Promise.reject(new Error('no body')) });
  mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');

  await expect(apiClient.post('/api/thing', {})).resolves.toBeUndefined();
});
