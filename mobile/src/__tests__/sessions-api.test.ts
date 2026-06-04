import { ApiError } from '@/lib/api/client';
import {
  getParticipantTimerSnapshot,
  interpretTimerSnapshotError,
} from '@/lib/api/sessions';

const mockGet = jest.fn();

jest.mock('@/lib/api/client', () => {
  const actual =
    jest.requireActual<typeof import('@/lib/api/client')>('@/lib/api/client');
  return { ...actual, apiClient: { get: (...args: unknown[]) => mockGet(...args) } };
});

describe('getParticipantTimerSnapshot', () => {
  beforeEach(() => jest.clearAllMocks());

  test('builds URL with teamId only when token is absent', async () => {
    const dto = { liveSessionId: 'sess-1', teamId: 'team-1' };
    mockGet.mockResolvedValueOnce(dto);

    const result = await getParticipantTimerSnapshot('sess-1', 'team-1');

    expect(mockGet).toHaveBeenCalledWith(
      '/api/sessions/sess-1/participants/timer?teamId=team-1',
      expect.objectContaining({ cache: 'no-store' }),
    );
    expect(result).toEqual(dto);
  });

  test('appends token when provided', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTimerSnapshot('sess-2', 'team-2', 'tok-abc');

    const [url] = mockGet.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain('teamId=team-2');
    expect(url).toContain('token=tok-abc');
  });

  test('omits token when null', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTimerSnapshot('sess-3', 'team-3', null);

    const [url] = mockGet.mock.calls[0] as [string, ...unknown[]];
    expect(url).not.toContain('token');
  });

  test('omits token when undefined', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTimerSnapshot('sess-4', 'team-4');

    const [url] = mockGet.mock.calls[0] as [string, ...unknown[]];
    expect(url).not.toContain('token');
  });

  test('URL-encodes liveSessionId in path', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTimerSnapshot('sess/with/slash', 'team-1');

    const [url] = mockGet.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain('sess%2Fwith%2Fslash');
  });

  test('sends no-cache headers', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTimerSnapshot('sess-1', 'team-1');

    const [, init] = mockGet.mock.calls[0] as [string, RequestInit];
    expect((init.headers as Record<string, string>)['Cache-Control']).toBe('no-cache');
    expect((init.headers as Record<string, string>)['Pragma']).toBe('no-cache');
  });
});

describe('interpretTimerSnapshotError', () => {
  test('status 0 → network-error', () => {
    expect(
      interpretTimerSnapshotError(new ApiError(0, 'network_error', 'fail')),
    ).toBe('network-error');
  });

  test('401 → unauthorized', () => {
    expect(
      interpretTimerSnapshotError(new ApiError(401, 'api_error', 'fail')),
    ).toBe('unauthorized');
  });

  test('403 → forbidden', () => {
    expect(
      interpretTimerSnapshotError(new ApiError(403, 'api_error', 'fail')),
    ).toBe('forbidden');
  });

  test('404 → not-found', () => {
    expect(
      interpretTimerSnapshotError(new ApiError(404, 'api_error', 'fail')),
    ).toBe('not-found');
  });

  test('409 → timer-unavailable', () => {
    expect(
      interpretTimerSnapshotError(new ApiError(409, 'api_error', 'conflict')),
    ).toBe('timer-unavailable');
  });

  test('other ApiError status → error', () => {
    expect(
      interpretTimerSnapshotError(new ApiError(500, 'api_error', 'fail')),
    ).toBe('error');
  });

  test('non-ApiError → error', () => {
    expect(interpretTimerSnapshotError(new Error('boom'))).toBe('error');
    expect(interpretTimerSnapshotError('string')).toBe('error');
    expect(interpretTimerSnapshotError(null)).toBe('error');
  });
});
