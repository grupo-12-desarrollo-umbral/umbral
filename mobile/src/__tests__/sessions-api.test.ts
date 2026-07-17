import { ApiError } from '@/lib/api/client';
import {
  getParticipantTeamBoard,
  getParticipantTimerSnapshot,
  interpretTimerSnapshotError,
  registerTargetScan,
  RegisterTargetScanRejection,
  submitTriviaAnswer,
  SubmitTriviaAnswerRejection,
} from '@/lib/api/sessions';

const mockGet = jest.fn();
const mockFetch = jest.fn();
const mockGetValidAccessToken = jest.fn();
const mockRefreshAccessToken = jest.fn();

jest.mock('@/lib/api/client', () => {
  const actual =
    jest.requireActual<typeof import('@/lib/api/client')>('@/lib/api/client');
  return { ...actual, apiClient: { get: (...args: unknown[]) => mockGet(...args) } };
});

jest.mock('expo/fetch', () => ({ fetch: (...args: unknown[]) => mockFetch(...args) }));
jest.mock('@/lib/auth/token-provider', () => ({
  getValidAccessToken: (...args: unknown[]) => mockGetValidAccessToken(...args),
  refreshAccessToken: (...args: unknown[]) => mockRefreshAccessToken(...args),
}));
jest.mock('@/lib/host', () => ({ apiBaseUrl: () => 'http://localhost:8000' }));

beforeEach(() => {
  mockGetValidAccessToken.mockResolvedValue('test-token');
  mockRefreshAccessToken.mockResolvedValue(null);
});

// A gateway 401 carries no ProblemDetails `type` — the shared 401 refresh-and-replay path is what the
// POST helpers rely on before this ever reaches their typed rejections.
function unauthorized() {
  return { ok: false, status: 401, json: () => Promise.resolve({ title: 'Unauthorized' }) };
}

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

describe('getParticipantTeamBoard', () => {
  beforeEach(() => jest.clearAllMocks());

  test('builds team-board URL with teamId only when token is absent', async () => {
    const dto = { liveSessionId: 'sess-1', teamId: 'team-1' };
    mockGet.mockResolvedValueOnce(dto);

    const result = await getParticipantTeamBoard('sess-1', 'team-1');

    expect(mockGet).toHaveBeenCalledWith(
      '/api/sessions/sess-1/participants/team-board?teamId=team-1',
      expect.objectContaining({ cache: 'no-store' }),
    );
    expect(result).toEqual(dto);
  });

  test('appends token when provided', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTeamBoard('sess-2', 'team-2', 'tok-abc');

    const [url] = mockGet.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain('/participants/team-board?');
    expect(url).toContain('teamId=team-2');
    expect(url).toContain('token=tok-abc');
  });

  test('omits token when null', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTeamBoard('sess-3', 'team-3', null);

    const [url] = mockGet.mock.calls[0] as [string, ...unknown[]];
    expect(url).not.toContain('token');
  });

  test('URL-encodes liveSessionId in path', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTeamBoard('sess/with/slash', 'team-1');

    const [url] = mockGet.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain('sess%2Fwith%2Fslash');
  });

  test('sends no-cache headers', async () => {
    mockGet.mockResolvedValueOnce({});

    await getParticipantTeamBoard('sess-1', 'team-1');

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

describe('submitTriviaAnswer', () => {
  beforeEach(() => jest.clearAllMocks());

  const REQUEST = {
    teamId: 'team-1',
    triviaSubstageSnapshotId: 'substage-abc',
    questionSequenceOrder: 1,
    selectedOptionSequenceOrder: 0,
  };

  test('resolves with result on 200', async () => {
    const result = {
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      triviaSubstageSnapshotId: 'substage-abc',
      questionSequenceOrder: 1,
      answeredAt: '2026-07-11T10:00:00Z',
    };
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(result),
    });

    const response = await submitTriviaAnswer('sess-1', REQUEST);
    expect(response).toEqual(result);
  });

  test('throws SubmitTriviaAnswerRejection with reasonCode on 409 ProblemDetails', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: () =>
        Promise.resolve({
          type: 'duplicate-trivia-answer',
          detail: 'Already answered',
        }),
    });

    try {
      await submitTriviaAnswer('sess-1', REQUEST);
      fail('Expected rejection');
    } catch (e) {
      expect(e).toBeInstanceOf(SubmitTriviaAnswerRejection);
      const rejection = e as SubmitTriviaAnswerRejection;
      expect(rejection.reasonCode).toBe('duplicate-trivia-answer');
      expect(rejection.status).toBe(409);
    }
  });

  test('throws with unknown reasonCode on network failure', async () => {
    mockFetch.mockRejectedValueOnce(new TypeError('Network request failed'));

    try {
      await submitTriviaAnswer('sess-1', REQUEST);
      fail('Expected rejection');
    } catch (e) {
      expect(e).toBeInstanceOf(SubmitTriviaAnswerRejection);
      const rejection = e as SubmitTriviaAnswerRejection;
      expect(rejection.reasonCode).toBe('unknown');
      expect(rejection.status).toBe(0);
    }
  });

  test('401 → refreshes once, replays the POST unchanged and resolves', async () => {
    const result = { liveSessionId: 'sess-1', teamId: 'team-1', questionSequenceOrder: 1 };
    mockFetch
      .mockResolvedValueOnce(unauthorized())
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(result) });
    mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');

    await expect(submitTriviaAnswer('sess-1', REQUEST)).resolves.toEqual(result);

    expect(mockRefreshAccessToken).toHaveBeenCalledTimes(1);
    expect(mockFetch).toHaveBeenCalledTimes(2);
    const [, init] = mockFetch.mock.calls[1] as [string, RequestInit];
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual(REQUEST);
  });

  test('401 with an unrefreshable session rejects with unknown reasonCode and status 401', async () => {
    mockFetch.mockResolvedValueOnce(unauthorized());
    mockRefreshAccessToken.mockResolvedValueOnce(null);

    await expect(submitTriviaAnswer('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'unknown',
      status: 401,
    });
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  test('a second 401 after refresh surfaces the rejection instead of looping', async () => {
    mockFetch.mockResolvedValueOnce(unauthorized()).mockResolvedValueOnce(unauthorized());
    mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');

    await expect(submitTriviaAnswer('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'unknown',
      status: 401,
    });
    expect(mockRefreshAccessToken).toHaveBeenCalledTimes(1);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  test('409 rejection does not trigger a refresh', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: () => Promise.resolve({ type: 'duplicate-trivia-answer', detail: 'Already answered' }),
    });

    await expect(submitTriviaAnswer('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'duplicate-trivia-answer',
      detail: 'Already answered',
    });
    expect(mockRefreshAccessToken).not.toHaveBeenCalled();
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });
});

describe('registerTargetScan', () => {
  beforeEach(() => jest.clearAllMocks());

  const REQUEST = { teamId: 'team-1', scannedValue: 'QR-ALPHA' };

  test('POSTs to the target-scans endpoint and resolves the result on 200', async () => {
    const result = {
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      activeSubstageId: 'sub-1',
      targetSnapshotId: 'target-1',
      isResolved: true,
      rejectionReason: null,
      submittedAt: '2026-07-14T10:00:00Z',
    };
    mockFetch.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(result) });

    const response = await registerTargetScan('sess-1', REQUEST);

    expect(response).toEqual(result);
    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://localhost:8000/api/sessions/sess-1/participants/target-scans');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual(REQUEST);
  });

  test('422 retained rejection carries the backend reason in detail', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 422,
      json: () =>
        Promise.resolve({
          type: 'target-scan-rejected',
          detail: 'The target has already been resolved by this team.',
        }),
    });

    try {
      await registerTargetScan('sess-1', REQUEST);
      fail('Expected rejection');
    } catch (e) {
      expect(e).toBeInstanceOf(RegisterTargetScanRejection);
      const rejection = e as RegisterTargetScanRejection;
      expect(rejection.reasonCode).toBe('retained-rejection');
      expect(rejection.status).toBe(422);
      expect(rejection.detail).toBe('The target has already been resolved by this team.');
    }
  });

  test.each([
    [400, 'invalid-scan'],
    [401, 'unauthorized'],
    [403, 'not-a-participant'],
    [404, 'session-not-found'],
    [409, 'session-not-accepting'],
    [500, 'unknown'],
  ])('status %i maps to reasonCode %s', async (status, reasonCode) => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status,
      json: () => Promise.resolve({ detail: 'nope' }),
    });

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode,
      status,
    });
  });

  // Two unrelated conflicts share status 409; only the ProblemDetails `type` separates them, and
  // reading a lost write race as "session paused" tells the participant the wrong thing.
  test('409 concurrent-modification maps to its own reasonCode, not session-not-accepting', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: () =>
        Promise.resolve({
          type: 'concurrent-modification',
          detail: 'The session changed while the request was in flight. Try again.',
        }),
    });

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'concurrent-modification',
      status: 409,
      detail: 'The session changed while the request was in flight. Try again.',
    });
  });

  test('409 with any other type stays session-not-accepting', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: () =>
        Promise.resolve({
          type: 'target-scan-requires-active-session',
          detail: 'The session is not accepting scans.',
        }),
    });

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'session-not-accepting',
      status: 409,
    });
  });

  test('non-JSON error body falls back to a status detail', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: () => Promise.reject(new Error('not json')),
    });

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'session-not-accepting',
      detail: 'HTTP 409',
    });
  });

  test('network failure throws with reasonCode network and status 0', async () => {
    mockFetch.mockRejectedValueOnce(new TypeError('Network request failed'));

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'network',
      status: 0,
    });
  });

  test('URL-encodes liveSessionId in the path', async () => {
    mockFetch.mockResolvedValueOnce({ ok: true, json: () => Promise.resolve({}) });

    await registerTargetScan('sess/with/slash', REQUEST);

    const [url] = mockFetch.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain('sess%2Fwith%2Fslash');
  });

  test('401 → refreshes once, replays the POST unchanged and resolves', async () => {
    const result = { liveSessionId: 'sess-1', teamId: 'team-1', isResolved: true };
    mockFetch
      .mockResolvedValueOnce(unauthorized())
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(result) });
    mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');

    await expect(registerTargetScan('sess-1', REQUEST)).resolves.toEqual(result);

    expect(mockRefreshAccessToken).toHaveBeenCalledTimes(1);
    expect(mockFetch).toHaveBeenCalledTimes(2);
    const [, init] = mockFetch.mock.calls[1] as [string, RequestInit];
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body as string)).toEqual(REQUEST);
  });

  test('401 with an unrefreshable session rejects as unauthorized without replaying', async () => {
    mockFetch.mockResolvedValueOnce(unauthorized());
    mockRefreshAccessToken.mockResolvedValueOnce(null);

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'unauthorized',
      status: 401,
    });
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  test('a second 401 after refresh surfaces the rejection instead of looping', async () => {
    mockFetch.mockResolvedValueOnce(unauthorized()).mockResolvedValueOnce(unauthorized());
    mockRefreshAccessToken.mockResolvedValueOnce('acc_fresh');

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'unauthorized',
      status: 401,
    });
    expect(mockRefreshAccessToken).toHaveBeenCalledTimes(1);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  test('422 retained rejection surfaces detail verbatim without a refresh', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 422,
      json: () =>
        Promise.resolve({ type: 'target-scan-rejected', detail: 'That QR is not a target here.' }),
    });

    await expect(registerTargetScan('sess-1', REQUEST)).rejects.toMatchObject({
      reasonCode: 'retained-rejection',
      status: 422,
      detail: 'That QR is not a target here.',
    });
    expect(mockRefreshAccessToken).not.toHaveBeenCalled();
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });
});
