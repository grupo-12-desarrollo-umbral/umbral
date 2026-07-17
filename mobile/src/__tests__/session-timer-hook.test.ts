import React from 'react';
import { act, create } from 'react-test-renderer';
import { useSessionTimer, type UseSessionTimerResult } from '@/lib/realtime/use-session-timer';
import {
  UNAVAILABLE_TIMER_DISPLAY,
  type SessionTimerUpdatedNotificationDto,
} from '@/lib/realtime/timer-types';
import type { SessionStateChangedNotificationDto } from '@/lib/realtime/sessions-hub-types';
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
type StateChangedHandler = (n: SessionStateChangedNotificationDto) => void;

let handlers: Set<TimerHandler>;
let stateChangedHandlers: Set<StateChangedHandler>;

function makeClient() {
  handlers = new Set();
  stateChangedHandlers = new Set();
  return {
    connection: {} as never,
    start: jest.fn(),
    stop: jest.fn(),
    reconnect: jest.fn(),
    onTimerUpdated(cb: TimerHandler) {
      handlers.add(cb);
      return () => handlers.delete(cb);
    },
    onStateChanged(cb: StateChangedHandler) {
      stateChangedHandlers.add(cb);
      return () => stateChangedHandlers.delete(cb);
    },
    onQuestionActivated: jest.fn(),
    onQuestionClosed: jest.fn(),
    onSubstageAdvanced: jest.fn(),
    onTeamBoardUpdated: jest.fn(),
    onSubstageRankingRevealStarted: jest.fn(() => () => {}),
  };
}

function fireEvent(n: SessionTimerUpdatedNotificationDto) {
  handlers.forEach(cb => cb(n));
}

function fireStateChanged(n: SessionStateChangedNotificationDto) {
  stateChangedHandlers.forEach(cb => cb(n));
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

const SNAPSHOT_ACTIVE_QUESTION = {
  liveSessionId: 'sess-1',
  questionIndex: 0,
  sequenceOrder: 1,
  prompt: 'Which door opens first?',
  options: ['Red', 'Blue'],
  timeLimitSeconds: 30,
  remainingSeconds: 20,
  activatedAt: '2026-06-04T10:00:00Z',
  triviaSubstageSnapshotId: 'substage-abc',
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

  test('surfaces active question and session state from the timer snapshot', async () => {
    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      activeQuestion: SNAPSHOT_ACTIVE_QUESTION,
      sessionState: 'Paused',
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

    expect(hook.get().activeQuestion).toEqual(SNAPSHOT_ACTIVE_QUESTION);
    expect(hook.get().sessionState).toBe('Paused');

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

  test('routes a short-window Active tick to the pre-game countdown, leaving the session clock intact', async () => {
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

    expect(hook.get().pregameSecondsLeft).toBeNull();
    expect(hook.get().timer?.remainingSeconds).toBe(180);

    // The 5s pre-game countdown arrives as a short total window while Active.
    act(() => {
      fireEvent({ ...BASE_EVENT, remainingMilliseconds: 5_000, totalMilliseconds: 5_000 });
    });

    expect(hook.get().pregameSecondsLeft).toBe(5);
    // The session clock is NOT clobbered by the countdown tick.
    expect(hook.get().timer?.remainingSeconds).toBe(180);

    hook.unmount();
  });

  test('a short-window tick flagged isPregameCountdown:false updates the question clock, not the countdown', async () => {
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

    // A 5s trivia question window is byte-identical to the pre-game total, but the explicit flag
    // disambiguates: it is a real question window and must drive the session clock, not the numeral.
    act(() => {
      fireEvent({
        ...BASE_EVENT,
        remainingMilliseconds: 5_000,
        totalMilliseconds: 5_000,
        isPregameCountdown: false,
      });
    });

    expect(hook.get().pregameSecondsLeft).toBeNull();
    expect(hook.get().timer?.remainingSeconds).toBe(5);
    expect(hook.get().timer?.totalSeconds).toBe(5);

    hook.unmount();
  });

  test('a tick flagged isPregameCountdown:true routes to the countdown even on a non-short window', async () => {
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

    // The explicit flag is authoritative regardless of window size; the session clock stays untouched.
    act(() => {
      fireEvent({
        ...BASE_EVENT,
        remainingMilliseconds: 4_000,
        totalMilliseconds: 5_000,
        isPregameCountdown: true,
      });
    });

    expect(hook.get().pregameSecondsLeft).toBe(4);
    expect(hook.get().timer?.remainingSeconds).toBe(180);

    hook.unmount();
  });

  test('a normal timer event clears the pre-game countdown and updates the clock', async () => {
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
      fireEvent({ ...BASE_EVENT, remainingMilliseconds: 3_000, totalMilliseconds: 5_000 });
    });
    expect(hook.get().pregameSecondsLeft).toBe(3);

    act(() => {
      fireEvent({ ...BASE_EVENT, remainingMilliseconds: 120_000, totalMilliseconds: 300_000 });
    });
    expect(hook.get().pregameSecondsLeft).toBeNull();
    expect(hook.get().timer?.remainingSeconds).toBe(120);

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

  test('changing resyncNonce triggers a re-fetch and applies the new snapshot', async () => {
    mockGetSnapshot
      .mockResolvedValueOnce({
        ...BASE_SNAPSHOT,
        activeQuestion: SNAPSHOT_ACTIVE_QUESTION,
        sessionState: 'Active',
      })
      .mockResolvedValueOnce({
        ...BASE_SNAPSHOT,
        activeQuestion: null,
        sessionState: 'Finished',
        remainingSeconds: 0,
      });

    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
      resyncNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetSnapshot).toHaveBeenCalledTimes(1);
    expect(hook.get().activeQuestion).toEqual(SNAPSHOT_ACTIVE_QUESTION);
    expect(hook.get().sessionState).toBe('Active');

    await hook.rerender({ resyncNonce: 1 });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetSnapshot).toHaveBeenCalledTimes(2);
    expect(hook.get().activeQuestion).toBeNull();
    expect(hook.get().sessionState).toBe('Finished');

    hook.unmount();
  });

  test('event-driven timer updates are unregressed by the resync input', async () => {
    mockGetSnapshot.mockResolvedValueOnce(BASE_SNAPSHOT);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
      resyncNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    act(() => {
      fireEvent({ ...BASE_EVENT, remainingMilliseconds: 30_000 });
    });

    expect(mockGetSnapshot).toHaveBeenCalledTimes(1);
    expect(hook.get().timer?.remainingSeconds).toBe(30);

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

  test('SessionStateChanged to Paused freezes the timer and updates sessionState', async () => {
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

    expect(hook.get().sessionState).toBe('Active');
    expect(hook.get().display.tone).toBe('running');

    act(() => {
      fireStateChanged({
        liveSessionId: 'sess-1',
        previousState: 'Active',
        currentState: 'Paused',
        changedAt: '2026-07-14T12:00:00Z',
      });
    });

    expect(hook.get().sessionState).toBe('Paused');
    expect(hook.get().timer?.isPaused).toBe(true);
    expect(hook.get().display.tone).toBe('paused');

    hook.unmount();
  });

  test('SessionStateChanged to Active from Paused resumes the timer', async () => {
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
      fireStateChanged({
        liveSessionId: 'sess-1',
        previousState: 'Active',
        currentState: 'Paused',
        changedAt: '2026-07-14T12:00:00Z',
      });
    });

    expect(hook.get().timer?.isPaused).toBe(true);
    expect(hook.get().display.tone).toBe('paused');

    act(() => {
      fireStateChanged({
        liveSessionId: 'sess-1',
        previousState: 'Paused',
        currentState: 'Active',
        changedAt: '2026-07-14T12:01:00Z',
      });
    });

    expect(hook.get().sessionState).toBe('Active');
    expect(hook.get().timer?.isPaused).toBe(false);
    expect(hook.get().display.tone).toBe('running');

    hook.unmount();
  });

  test('surfaces the mission deadline from the snapshot, distinct from the question window', async () => {
    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      totalSeconds: 30,
      remainingSeconds: 20,
      missionTotalSeconds: 600,
      missionRemainingSeconds: 540,
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

    // The primary display is the 30s question window; the mission display is the 10-minute deadline.
    expect(hook.get().display.label).toBe('00:20');
    expect(hook.get().missionDisplay?.label).toBe('09:00');
    expect(hook.get().missionDisplay?.tone).toBe('running');

    hook.unmount();
  });

  test('missionDisplay is null when the snapshot carries no deadline', async () => {
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

    expect(hook.get().missionDisplay).toBeNull();

    hook.unmount();
  });

  test('a live event updates the mission deadline alongside the question window', async () => {
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
      fireEvent({
        ...BASE_EVENT,
        remainingMilliseconds: 20_000,
        totalMilliseconds: 30_000,
        missionRemainingMilliseconds: 480_000,
        missionTotalMilliseconds: 600_000,
      });
    });

    expect(hook.get().display.label).toBe('00:20');
    expect(hook.get().missionDisplay?.label).toBe('08:00');

    hook.unmount();
  });

  test('a live tick that omits the mission fields preserves the current deadline', async () => {
    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      missionTotalSeconds: 600,
      missionRemainingSeconds: 540,
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

    expect(hook.get().missionDisplay?.label).toBe('09:00');

    // A full-window tick from an older replica (mid rolling deploy) carries no mission fields. It must
    // update the primary clock but leave the mission deadline standing rather than blank it.
    act(() => {
      fireEvent({ ...BASE_EVENT, remainingMilliseconds: 120_000, totalMilliseconds: 300_000 });
    });

    expect(hook.get().missionDisplay?.label).toBe('09:00');

    // An explicit null still clears the deadline (e.g. a substage with no mission window).
    act(() => {
      fireEvent({
        ...BASE_EVENT,
        remainingMilliseconds: 120_000,
        totalMilliseconds: 300_000,
        missionRemainingMilliseconds: null,
        missionTotalMilliseconds: null,
      });
    });

    expect(hook.get().missionDisplay).toBeNull();

    hook.unmount();
  });

  test('a pre-game countdown tick leaves the mission deadline intact', async () => {
    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      missionTotalSeconds: 600,
      missionRemainingSeconds: 540,
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

    expect(hook.get().missionDisplay?.label).toBe('09:00');

    // The short-window pre-game tick routes to the countdown numeral and must not clobber the mission clock.
    act(() => {
      fireEvent({ ...BASE_EVENT, remainingMilliseconds: 5_000, totalMilliseconds: 5_000 });
    });

    expect(hook.get().pregameSecondsLeft).toBe(5);
    expect(hook.get().missionDisplay?.label).toBe('09:00');

    hook.unmount();
  });

  test('SessionStateChanged to Paused flips the mission clock to paused too', async () => {
    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      missionTotalSeconds: 600,
      missionRemainingSeconds: 540,
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

    expect(hook.get().missionDisplay?.tone).toBe('running');

    act(() => {
      fireStateChanged({
        liveSessionId: 'sess-1',
        previousState: 'Active',
        currentState: 'Paused',
        changedAt: '2026-07-14T12:00:00Z',
      });
    });

    expect(hook.get().missionDisplay?.tone).toBe('paused');

    hook.unmount();
  });

  const SNAPSHOT_REVEAL = {
    substageSnapshotId: 'sub-2',
    playMode: 'TreasureHunt',
    revealUntil: '2026-06-04T10:00:10Z',
    isTerminal: false,
    emittedAt: '2026-06-04T10:00:00Z',
  };

  test('pairs the reveal with the snapshot observedAt and bumps the reveal version on success', async () => {
    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      activeRankingReveal: SNAPSHOT_REVEAL,
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

    // The reveal and the server time that orders it describe the same response.
    expect(hook.get().revealReconciliation).toEqual({
      reveal: SNAPSHOT_REVEAL,
      observedAt: BASE_SNAPSHOT.observedAt,
      version: 1,
    });

    hook.unmount();
  });

  test('a failed re-fetch does not bump the reveal reconciliation (no cached-reveal replay)', async () => {
    mockGetSnapshot
      .mockResolvedValueOnce({ ...BASE_SNAPSHOT, activeRankingReveal: SNAPSHOT_REVEAL })
      .mockRejectedValueOnce(new ApiError(0, 'network_error', 'Network request failed'));
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: true,
      reconnectNonce: 0,
      resyncNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    const afterSuccess = hook.get().revealReconciliation;
    expect(afterSuccess?.version).toBe(1);

    await hook.rerender({ resyncNonce: 1 });
    await act(async () => {
      await Promise.resolve();
    });

    // The failed fetch left the reconciliation untouched (same object, same version) so no stale reveal
    // is replayed downstream — but the generic snapshot version still advanced for the question reconcile.
    expect(hook.get().revealReconciliation).toBe(afterSuccess);
    expect(hook.get().error).not.toBeNull();

    hook.unmount();
  });

  test('SessionStateChanged for other session is ignored', async () => {
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
      fireStateChanged({
        liveSessionId: 'other-sess',
        previousState: 'Active',
        currentState: 'Paused',
        changedAt: '2026-07-14T12:00:00Z',
      });
    });

    expect(hook.get().sessionState).toBe('Active');
    expect(hook.get().timer?.isPaused).toBe(false);

    hook.unmount();
  });
});
