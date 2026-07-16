import {
  getValidAccessToken,
  refreshAccessToken,
  onSessionExpired,
} from '@/lib/auth/token-provider';

jest.mock('expo/fetch', () => ({ fetch: jest.fn() }));
jest.mock('@/lib/auth/token-store', () => ({
  getAccessToken: jest.fn(),
  getRefreshToken: jest.fn(),
  storeTokens: jest.fn().mockResolvedValue(undefined),
  clearTokens: jest.fn().mockResolvedValue(undefined),
}));

// eslint-disable-next-line @typescript-eslint/no-require-imports
const { fetch: mockFetch } = require('expo/fetch') as { fetch: jest.Mock };
const {
  getAccessToken: mockGetAccessToken,
  getRefreshToken: mockGetRefreshToken,
  storeTokens: mockStoreTokens,
  clearTokens: mockClearTokens,
  // eslint-disable-next-line @typescript-eslint/no-require-imports
} = require('@/lib/auth/token-store') as Record<string, jest.Mock>;

// Builds an access token whose `exp` claim sits `secondsFromNow` away, so the provider's skew
// window can be exercised without faking timers.
function accessTokenExpiringIn(secondsFromNow: number): string {
  const claims = { exp: Math.floor(Date.now() / 1000) + secondsFromNow };
  return `hdr.${btoa(JSON.stringify(claims))}.sig`;
}

function tokenResponse(accessToken: string, refreshToken: string) {
  return {
    ok: true,
    status: 200,
    json: () => Promise.resolve({ access_token: accessToken, refresh_token: refreshToken }),
  };
}

const flush = () => new Promise<void>((resolve) => setImmediate(() => resolve()));

beforeEach(() => {
  jest.clearAllMocks();
  process.env.EXPO_PUBLIC_KEYCLOAK_URL = 'http://localhost:8080';
  process.env.EXPO_PUBLIC_KEYCLOAK_REALM = 'umbral';
  process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID = 'umbral-mobile';
});

describe('getValidAccessToken', () => {
  test('returns the stored token untouched when it is comfortably live', async () => {
    const live = accessTokenExpiringIn(600);
    mockGetAccessToken.mockResolvedValue(live);

    await expect(getValidAccessToken()).resolves.toBe(live);
    expect(mockFetch).not.toHaveBeenCalled();
  });

  test('refreshes proactively inside the 60s skew window, before any 401', async () => {
    mockGetAccessToken.mockResolvedValue(accessTokenExpiringIn(30));
    mockGetRefreshToken.mockResolvedValue('ref_old');
    mockFetch.mockResolvedValueOnce(tokenResponse('acc_new', 'ref_rotated'));

    await expect(getValidAccessToken()).resolves.toBe('acc_new');
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  test('persists the rotated refresh token before resolving', async () => {
    mockGetAccessToken.mockResolvedValue(accessTokenExpiringIn(-10));
    mockGetRefreshToken.mockResolvedValue('ref_old');
    mockFetch.mockResolvedValueOnce(tokenResponse('acc_new', 'ref_rotated'));

    await getValidAccessToken();

    expect(mockStoreTokens).toHaveBeenCalledWith('acc_new', 'ref_rotated');
  });

  test('treats an unparseable token as expired rather than sending it', async () => {
    mockGetAccessToken.mockResolvedValue('not-a-jwt');
    mockGetRefreshToken.mockResolvedValue('ref_old');
    mockFetch.mockResolvedValueOnce(tokenResponse('acc_new', 'ref_rotated'));

    await expect(getValidAccessToken()).resolves.toBe('acc_new');
  });

  test('resolves null when nothing is stored', async () => {
    mockGetAccessToken.mockResolvedValue(null);
    mockGetRefreshToken.mockResolvedValue(null);

    await expect(getValidAccessToken()).resolves.toBeNull();
    expect(mockFetch).not.toHaveBeenCalled();
  });
});

describe('dead refresh token vs transient failure', () => {
  test('invalid_grant clears the store and notifies listeners', async () => {
    mockGetAccessToken.mockResolvedValue(accessTokenExpiringIn(-10));
    mockGetRefreshToken.mockResolvedValue('ref_dead');
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 400,
      json: () => Promise.resolve({ error: 'invalid_grant' }),
    });
    const listener = jest.fn();
    const unsubscribe = onSessionExpired(listener);

    await expect(getValidAccessToken()).resolves.toBeNull();

    expect(mockClearTokens).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledTimes(1);
    unsubscribe();
  });

  test('network failure keeps the stored session and never notifies', async () => {
    mockGetAccessToken.mockResolvedValue(accessTokenExpiringIn(-10));
    mockGetRefreshToken.mockResolvedValue('ref_live');
    mockFetch.mockRejectedValueOnce(new TypeError('Network request failed'));
    const listener = jest.fn();
    const unsubscribe = onSessionExpired(listener);

    await expect(getValidAccessToken()).resolves.toBeNull();

    expect(mockClearTokens).not.toHaveBeenCalled();
    expect(listener).not.toHaveBeenCalled();
    unsubscribe();
  });

  test('a blip inside the skew window falls back to the still-live token', async () => {
    const live = accessTokenExpiringIn(30);
    mockGetAccessToken.mockResolvedValue(live);
    mockGetRefreshToken.mockResolvedValue('ref_live');
    mockFetch.mockRejectedValueOnce(new TypeError('Network request failed'));

    await expect(getValidAccessToken()).resolves.toBe(live);
    expect(mockClearTokens).not.toHaveBeenCalled();
  });

  test('a missing refresh token never reports an expiry for a session that never existed', async () => {
    mockGetAccessToken.mockResolvedValue(null);
    mockGetRefreshToken.mockResolvedValue(null);
    const listener = jest.fn();
    const unsubscribe = onSessionExpired(listener);

    await getValidAccessToken();

    expect(listener).not.toHaveBeenCalled();
    expect(mockClearTokens).not.toHaveBeenCalled();
    unsubscribe();
  });
});

describe('single-flight', () => {
  test('concurrent callers share one refresh → exactly one token request', async () => {
    mockGetAccessToken.mockResolvedValue(accessTokenExpiringIn(-10));
    mockGetRefreshToken.mockResolvedValue('ref_old');

    let settleFetch!: (value: unknown) => void;
    mockFetch.mockReturnValueOnce(
      new Promise<unknown>((resolve) => {
        settleFetch = resolve;
      }),
    );

    const callers = [
      getValidAccessToken(),
      getValidAccessToken(),
      getValidAccessToken(),
      refreshAccessToken(),
    ];

    // Let every caller reach the refresh before it settles — that is the race being guarded.
    await flush();
    expect(mockFetch).toHaveBeenCalledTimes(1);

    settleFetch(tokenResponse('acc_new', 'ref_rotated'));

    await expect(Promise.all(callers)).resolves.toEqual([
      'acc_new',
      'acc_new',
      'acc_new',
      'acc_new',
    ]);
    expect(mockFetch).toHaveBeenCalledTimes(1);
    // One rotation persisted, not four.
    expect(mockStoreTokens).toHaveBeenCalledTimes(1);
  });

  test('a later refresh starts a fresh flight once the previous one settled', async () => {
    mockGetAccessToken.mockResolvedValue(accessTokenExpiringIn(-10));
    mockGetRefreshToken.mockResolvedValue('ref_old');
    mockFetch
      .mockResolvedValueOnce(tokenResponse('acc_1', 'ref_1'))
      .mockResolvedValueOnce(tokenResponse('acc_2', 'ref_2'));

    await expect(refreshAccessToken()).resolves.toBe('acc_1');
    await expect(refreshAccessToken()).resolves.toBe('acc_2');
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  test('a failed flight does not poison the next refresh', async () => {
    mockGetAccessToken.mockResolvedValue(accessTokenExpiringIn(-10));
    mockGetRefreshToken.mockResolvedValue('ref_old');
    mockFetch
      .mockRejectedValueOnce(new TypeError('Network request failed'))
      .mockResolvedValueOnce(tokenResponse('acc_recovered', 'ref_next'));

    await expect(refreshAccessToken()).resolves.toBeNull();
    await expect(refreshAccessToken()).resolves.toBe('acc_recovered');
  });
});
