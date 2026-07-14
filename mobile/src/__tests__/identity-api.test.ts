import { registerParticipant } from '@/lib/api/identity';
import { ApiError } from '@/lib/api/client';

const mockFetch = jest.fn();

jest.mock('expo/fetch', () => ({ fetch: (...args: unknown[]) => mockFetch(...args) }));
jest.mock('@/lib/auth/token-store', () => ({ getAccessToken: jest.fn().mockResolvedValue(null) }));
jest.mock('@/lib/host', () => ({ apiBaseUrl: () => 'http://localhost:8000' }));

describe('registerParticipant', () => {
  beforeEach(() => jest.clearAllMocks());

  test('POSTs the custom-form fields to the anonymous register endpoint', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: () => Promise.resolve({ email: 'new@example.com', role: 'Participant' }),
    });

    const result = await registerParticipant('New User', 'new@example.com', 'sup3rsecret');

    expect(result).toEqual({ email: 'new@example.com', role: 'Participant' });

    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://localhost:8000/api/users/register');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual({
      displayName: 'New User',
      email: 'new@example.com',
      password: 'sup3rsecret',
    });
  });

  test('sends no Authorization header (caller has no account yet)', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: () => Promise.resolve({ email: 'new@example.com', role: 'Participant' }),
    });

    await registerParticipant('New User', 'new@example.com', 'sup3rsecret');

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  test('surfaces a 409 conflict as an ApiError', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: () => Promise.resolve({ type: 'email-already-registered', detail: 'exists' }),
    });

    const err = await registerParticipant('N', 'dup@example.com', 'sup3rsecret').catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(409);
  });
});
