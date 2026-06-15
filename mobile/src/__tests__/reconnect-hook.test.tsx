import React from 'react';
import { act, create } from 'react-test-renderer';
import { HttpError, TimeoutError } from '@microsoft/signalr';
import { useReconnect, type ReconnectStatus } from '@/lib/realtime/use-reconnect';
import type {
  ReconnectContext,
  ReconnectParticipantResultDto,
} from '@/lib/realtime/sessions-hub-types';

const mockSignOut = jest.fn<Promise<void>, []>(async () => {});
const mockSaveReconnectContext = jest.fn<Promise<void>, [ReconnectContext]>(
  async () => {},
);
const mockClearReconnectContext = jest.fn<Promise<void>, []>(async () => {});
const mockStart = jest.fn<Promise<void>, []>(async () => {});
const mockStop = jest.fn<Promise<void>, []>(async () => {});
const mockReconnectClient = jest.fn<
  Promise<ReconnectParticipantResultDto>,
  [string, { teamId: string; displayName: string; token?: string | null }]
>();
let reconnectingHandler: ((error?: Error) => void) | null = null;
let reconnectedHandler: ((connectionId?: string) => void | Promise<void>) | null =
  null;

jest.mock('@/lib/auth/use-auth', () => ({
  useAuth: () => ({ signOut: mockSignOut }),
}));

jest.mock('@/lib/realtime/reconnect-context', () => ({
  saveReconnectContext: (context: ReconnectContext) =>
    mockSaveReconnectContext(context),
  clearReconnectContext: () => mockClearReconnectContext(),
}));

jest.mock('@/lib/realtime/sessions-hub', () => ({
  createSessionsHubConnection: () => ({
    connection: {
      onreconnecting: (callback: (error?: Error) => void) => {
        reconnectingHandler = callback;
      },
      onreconnected: (
        callback: (connectionId?: string) => void | Promise<void>,
      ) => {
        reconnectedHandler = callback;
      },
    },
    start: mockStart,
    stop: mockStop,
    reconnect: mockReconnectClient,
  }),
}));

type HookSnapshot = {
  status: ReconnectStatus;
  outcome: ReturnType<typeof useReconnect>['outcome'];
  isHubReconnecting: ReturnType<typeof useReconnect>['isHubReconnecting'];
  reconnect: ReturnType<typeof useReconnect>['reconnect'];
  reset: ReturnType<typeof useReconnect>['reset'];
};

function createDeferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function renderUseReconnect(): {
  getSnapshot: () => HookSnapshot;
  unmount: () => void;
} {
  let snapshot: HookSnapshot | null = null;

  function Harness() {
    snapshot = useReconnect();
    return null;
  }

  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(<Harness />);
  });

  return {
    getSnapshot() {
      if (!snapshot) {
        throw new Error('Hook snapshot not ready.');
      }
      return snapshot;
    },
    unmount() {
      act(() => {
        renderer?.unmount();
      });
    },
  };
}

const context: ReconnectContext = {
  liveSessionId: 'session-1',
  teamId: 'team-1',
  displayName: 'Nova',
  token: null,
};

const result: ReconnectParticipantResultDto = {
  liveSessionId: 'session-1',
  teamId: 'runtime-team-1',
  teamDisplayName: 'Red',
  sessionParticipantId: 'participant-1',
  participantDisplayName: 'Nova',
  sessionState: 'Active',
  isReconnect: true,
  joinedAt: '2026-06-03T12:00:00.000Z',
  lastSeenAt: '2026-06-03T12:05:00.000Z',
};

describe('useReconnect', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    reconnectingHandler = null;
    reconnectedHandler = null;
  });

  test('runs the reconnect happy path and persists the refreshed context', async () => {
    mockStart.mockResolvedValueOnce(undefined);
    mockReconnectClient.mockResolvedValueOnce(result);

    const hook = renderUseReconnect();

    let outcome;
    await act(async () => {
      outcome = await hook.getSnapshot().reconnect(context);
    });

    expect(outcome).toEqual({ kind: 'reconnected', result });
    expect(mockStart).toHaveBeenCalledTimes(1);
    expect(mockReconnectClient).toHaveBeenCalledWith('session-1', {
      teamId: 'team-1',
      displayName: 'Nova',
      token: null,
    });
    expect(mockSaveReconnectContext).toHaveBeenCalledWith({
      ...context,
      lastSeenAt: '2026-06-03T12:05:00.000Z',
    });
    expect(hook.getSnapshot().status).toBe('reconnected');
    expect(hook.getSnapshot().outcome).toEqual({ kind: 'reconnected', result });

    hook.unmount();
  });

  test('moves through connecting then reconnecting before success resolves', async () => {
    const startDeferred = createDeferred<void>();
    const reconnectDeferred = createDeferred<ReconnectParticipantResultDto>();
    mockStart.mockImplementationOnce(() => startDeferred.promise);
    mockReconnectClient.mockImplementationOnce(() => reconnectDeferred.promise);

    const hook = renderUseReconnect();

    let pending: Promise<unknown>;
    await act(async () => {
      pending = hook.getSnapshot().reconnect(context);
    });

    expect(hook.getSnapshot().status).toBe('connecting');

    await act(async () => {
      startDeferred.resolve(undefined);
      await Promise.resolve();
    });

    expect(hook.getSnapshot().status).toBe('reconnecting');

    await act(async () => {
      reconnectDeferred.resolve(result);
      await pending;
    });

    expect(hook.getSnapshot().status).toBe('reconnected');
    hook.unmount();
  });

  test('maps late join denials to denied state', async () => {
    mockStart.mockResolvedValueOnce(undefined);
    mockReconnectClient.mockRejectedValueOnce(
      new Error(JSON.stringify({ code: 'LATE_JOIN_NOT_ALLOWED', message: 'late' })),
    );

    const hook = renderUseReconnect();

    let outcome;
    await act(async () => {
      outcome = await hook.getSnapshot().reconnect(context);
    });

    expect(outcome).toEqual({ kind: 'forbidden-late-join' });
    expect(hook.getSnapshot().status).toBe('denied');
    expect(hook.getSnapshot().outcome).toEqual({ kind: 'forbidden-late-join' });

    hook.unmount();
  });

  test('maps invalid session state denials', async () => {
    mockStart.mockResolvedValueOnce(undefined);
    mockReconnectClient.mockRejectedValueOnce(
      new Error(JSON.stringify({ code: 'TEAM_UNAVAILABLE', message: 'closed' })),
    );

    const hook = renderUseReconnect();

    await act(async () => {
      await hook.getSnapshot().reconnect(context);
    });

    expect(hook.getSnapshot().status).toBe('denied');
    expect(hook.getSnapshot().outcome).toEqual({ kind: 'invalid-session-state' });

    hook.unmount();
  });

  test('maps removed participants to lost access', async () => {
    mockStart.mockResolvedValueOnce(undefined);
    mockReconnectClient.mockRejectedValueOnce(
      new Error(JSON.stringify({ code: 'PARTICIPANT_REMOVED', message: 'removed' })),
    );

    const hook = renderUseReconnect();

    await act(async () => {
      await hook.getSnapshot().reconnect(context);
    });

    expect(hook.getSnapshot().status).toBe('denied');
    expect(hook.getSnapshot().outcome).toEqual({ kind: 'lost-access' });

    hook.unmount();
  });

  test('signs out and clears reconnect context on unauthorized', async () => {
    mockStart.mockRejectedValueOnce(new HttpError('Unauthorized', 401));

    const hook = renderUseReconnect();

    await act(async () => {
      await hook.getSnapshot().reconnect(context);
    });

    expect(mockClearReconnectContext).toHaveBeenCalledTimes(1);
    expect(mockSignOut).toHaveBeenCalledTimes(1);
    expect(hook.getSnapshot().status).toBe('denied');
    expect(hook.getSnapshot().outcome).toEqual({ kind: 'unauthorized' });

    hook.unmount();
  });

  test('maps connection failures to error state', async () => {
    mockStart.mockRejectedValueOnce(new TimeoutError());

    const hook = renderUseReconnect();

    await act(async () => {
      await hook.getSnapshot().reconnect(context);
    });

    expect(mockSignOut).not.toHaveBeenCalled();
    expect(hook.getSnapshot().status).toBe('error');
    expect(hook.getSnapshot().outcome).toEqual({ kind: 'network-error' });

    hook.unmount();
  });

  test('replays reconnect after SignalR restores the transport', async () => {
    mockStart.mockResolvedValue(undefined);
    mockReconnectClient
      .mockResolvedValueOnce(result)
      .mockResolvedValueOnce({
        ...result,
        lastSeenAt: '2026-06-03T12:06:00.000Z',
      });

    const hook = renderUseReconnect();

    await act(async () => {
      await hook.getSnapshot().reconnect(context);
    });

    expect(reconnectingHandler).not.toBeNull();
    expect(reconnectedHandler).not.toBeNull();

    act(() => {
      reconnectingHandler?.(new Error('transport dropped'));
    });

    expect(hook.getSnapshot().isHubReconnecting).toBe(true);
    expect(hook.getSnapshot().status).toBe('reconnected');

    await act(async () => {
      await reconnectedHandler?.('connection-2');
    });

    expect(mockReconnectClient).toHaveBeenNthCalledWith(2, 'session-1', {
      teamId: 'team-1',
      displayName: 'Nova',
      token: null,
    });
    expect(mockSaveReconnectContext).toHaveBeenLastCalledWith({
      ...context,
      lastSeenAt: '2026-06-03T12:06:00.000Z',
    });
    expect(hook.getSnapshot().isHubReconnecting).toBe(false);
    expect(hook.getSnapshot().status).toBe('reconnected');
    expect(hook.getSnapshot().outcome).toEqual({
      kind: 'reconnected',
      result: {
        ...result,
        lastSeenAt: '2026-06-03T12:06:00.000Z',
      },
    });

    hook.unmount();
  });

  test('stops the hub connection on unmount and reset returns to idle', async () => {
    mockStart.mockResolvedValueOnce(undefined);
    mockReconnectClient.mockResolvedValueOnce(result);

    const hook = renderUseReconnect();

    await act(async () => {
      await hook.getSnapshot().reconnect(context);
    });

    act(() => {
      hook.getSnapshot().reset();
    });

    expect(hook.getSnapshot().status).toBe('idle');
    expect(hook.getSnapshot().outcome).toBeNull();

    hook.unmount();

    expect(mockStop).toHaveBeenCalled();
  });
});
