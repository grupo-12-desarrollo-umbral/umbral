import React from 'react';
import { act, create } from 'react-test-renderer';
import { useSessionTimer, type UseSessionTimerResult } from '@/lib/realtime/use-session-timer';
import {
  UNAVAILABLE_TIMER_DISPLAY,
  type SessionTimerUpdatedNotificationDto,
} from '@/lib/realtime/timer-types';
import { ApiError } from '@/lib/api/client';

// --- Mocks ---

const mockGetSnapshot = jest.fn();

jest.mock('@/lib/api/sessions', () => {
  const actual = jest.requireActual<typeof import('@/lib/api/sessions')>('@/lib/api/sessions');
  return {
    ...actual,
    getParticipantTimerSnapshot: (...args: unknown[]) => mockGetSnapshot(...args),
  };
});

// --- Fake hub client ---

type TimerHandler = (n: SessionTimerUpdatedNotificationDto) => void;

let handlers: Set<TimerHandler>;

function makeClient() {
  handlers = new Set();
  return {
    connection: {} as never,
    start: jest.fn(),
    stop: jest.fn(),
    reconnect: jest.fn(),
    onTimerUpdated(cb: TimerHandler) {
      handlers.add(cb);
      return () => handlers.delete(cb);
    },
    onStateChanged: jest.fn(),
  };
}

function fireEvent(n: SessionTimerUpdatedNotificationDto) {
  handlers.forEach(cb => cb(n));
}

// --- Hook harness ---

type HookProps = Parameters<typeof useSessionTimer>[0];

function renderHook(initialProps: HookProps) {
  let result: UseSessionTimerResult | null = null;
  let currentProps = { ...initialProps };

  function Harness() {
    result = useSessionTimer(currentProps);
    return null;
  }

  let renderer: ReturnType<typeof create>;
  act(() => {
    renderer = create(React.createElement(Harness));
  });

  return {
    get: () => result!,
    rerender: async (patch: Partial<HookProps>) => {
      currentProps = { ...currentProps, ...patch };
      await act(async () => {
        renderer.update(React.createElement(Harness));
      });
    },
    unmount: () => act(() => renderer.unmount()),
  };
}

// --- Fixtures ---

const BASE_SNAPSHOT = {
  liveSessionId: 'sess-1',
  teamId: 'team-1',
  sessionState: 'Active',
  totalSeconds: 300,
  remainingSeconds: 180,
  timerStatus: 'Running',
  isAdvancing: true,
  isExpired: false,
  observedAt: '2026-06-04T10:00:00Z',
  advancingSince: '2026-06-04T09:55:00Z',
  expiredAt: null,
};

const BASE_EVENT: SessionTimerUpdatedNotificationDto = {
  liveSessionId: 'sess-1',
  remainingMilliseconds: 60_000,
  isPaused: false,
  emittedAt: '2026-06-04T10:01:00Z',
  totalMilliseconds: 300_000,
  isExpired: false,
  sessionState: 'Active',
};

// --- Tests ---

describe('useSessionTimer', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('display is unavailable before reconnection', () => {
    const client = makeClient();
    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: false,
      reconnectNonce: 0,
    });

    expect(hook.get().display).toEqual(UNAVAILABLE_TIMER_DISPLAY);
    expect(mockGetSnapshot).not.toHaveBeenCalled();

    hook.unmount();
  });

  test('fetches snapshot when isReconnected becomes true and sets running display', async () => {
    mockGetSnapshot.mockResolvedValueOnce(BASE_SNAPSHOT);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetSnapshot).toHaveBeenCalledWith('sess-1', 'team-1', undefined);
    expect(hook.get().timer?.remainingSeconds).toBe(180);
    expect(hook.get().timer?.totalSeconds).toBe(300);
    expect(hook.get().display.tone).toBe('running');
    expect(hook.get().display.label).toBe('03:00');

    hook.unmount();
  });

  test('converts ms to seconds from live event', async () => {
    mockGetSnapshot.mockResolvedValueOnce(BASE_SNAPSHOT);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    act(() => {
      fireEvent({ ...BASE_EVENT, remainingMilliseconds: 60_000, totalMilliseconds: 300_000 });
    });

    expect(hook.get().timer?.remainingSeconds).toBe(60);
    expect(hook.get().timer?.totalSeconds).toBe(300);
    expect(hook.get().display.label).toBe('01:00');
    expect(hook.get().display.pct).toBeCloseTo(20);

    hook.unmount();
  });

  test('paused event sets tone to paused', async () => {
    mockGetSnapshot.mockResolvedValueOnce(BASE_SNAPSHOT);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    act(() => {
      fireEvent({ ...BASE_EVENT, isPaused: true });
    });

    expect(hook.get().display.tone).toBe('paused');
    expect(hook.get().timer?.isPaused).toBe(true);

    hook.unmount();
  });

  test('expired event sets tone to expired and label to 00:00', async () => {
    mockGetSnapshot.mockResolvedValueOnce(BASE_SNAPSHOT);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    act(() => {
      fireEvent({ ...BASE_EVENT, isExpired: true, remainingMilliseconds: 0 });
    });

    expect(hook.get().display.tone).toBe('expired');
    expect(hook.get().display.label).toBe('00:00');
    expect(hook.get().display.pct).toBe(0);

    hook.unmount();
  });

  test('event with different liveSessionId is ignored', async () => {
    mockGetSnapshot.mockResolvedValueOnce(BASE_SNAPSHOT);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    const before = hook.get().timer?.remainingSeconds;

    act(() => {
      fireEvent({ ...BASE_EVENT, liveSessionId: 'other-sess', remainingMilliseconds: 1_000 });
    });

    expect(hook.get().timer?.remainingSeconds).toBe(before);

    hook.unmount();
  });

  test('changing reconnectNonce triggers a re-fetch', async () => {
    mockGetSnapshot
      .mockResolvedValueOnce(BASE_SNAPSHOT)
      .mockResolvedValueOnce({ ...BASE_SNAPSHOT, remainingSeconds: 90 });

    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetSnapshot).toHaveBeenCalledTimes(1);

    await hook.rerender({ reconnectNonce: 1 });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetSnapshot).toHaveBeenCalledTimes(2);
    expect(hook.get().timer?.remainingSeconds).toBe(90);

    hook.unmount();
  });

  test('snapshot error sets error token and display stays unavailable', async () => {
    mockGetSnapshot.mockRejectedValueOnce(new ApiError(404, 'not_found', 'not found'));
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(hook.get().error).toBe('not-found');
    expect(hook.get().display).toEqual(UNAVAILABLE_TIMER_DISPLAY);

    hook.unmount();
  });

  test('passes token to snapshot fetch', async () => {
    mockGetSnapshot.mockResolvedValueOnce(BASE_SNAPSHOT);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      token: 'tok-abc',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetSnapshot).toHaveBeenCalledWith('sess-1', 'team-1', 'tok-abc');

    hook.unmount();
  });

  test('snapshot isAdvancing=false sets isPaused=true on timer', async () => {
    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      isAdvancing: false,
      isExpired: false,
    });
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(hook.get().timer?.isPaused).toBe(true);
    expect(hook.get().display.tone).toBe('paused');

    hook.unmount();
  });
});
