import { createSessionsHubConnection } from '@/lib/realtime/sessions-hub';
import type { SessionTimerUpdatedNotificationDto } from '@/lib/realtime/timer-types';
import type { SessionStateChangedNotificationDto } from '@/lib/realtime/sessions-hub-types';

type Handler = (...args: unknown[]) => void;

const connectionHandlers: Record<string, Set<Handler>> = {};
const mockInvoke = jest.fn();
const mockConnectionStart = jest.fn<Promise<void>, []>();
const mockConnectionStop = jest.fn<Promise<void>, []>();
const mockOn = jest.fn((method: string, cb: Handler) => {
  if (!connectionHandlers[method]) connectionHandlers[method] = new Set();
  connectionHandlers[method].add(cb);
});
const mockOff = jest.fn((method: string, cb: Handler) => {
  connectionHandlers[method]?.delete(cb);
});

const mockConnection = {
  state: 'Disconnected',
  on: mockOn,
  off: mockOff,
  invoke: mockInvoke,
  start: mockConnectionStart,
  stop: mockConnectionStop,
  onreconnecting: jest.fn(),
  onreconnected: jest.fn(),
};

jest.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: jest.fn(() => ({
    withUrl: jest.fn().mockReturnThis(),
    withAutomaticReconnect: jest.fn().mockReturnThis(),
    withKeepAliveInterval: jest.fn().mockReturnThis(),
    withServerTimeout: jest.fn().mockReturnThis(),
    configureLogging: jest.fn().mockReturnThis(),
    build: jest.fn(() => mockConnection),
  })),
  HubConnectionState: { Disconnected: 'Disconnected' },
  HttpTransportType: { WebSockets: 1 },
  LogLevel: { Warning: 2 },
}));

jest.mock('@/lib/auth/token-store', () => ({
  getAccessToken: jest.fn(() => Promise.resolve('mock-token')),
}));

jest.mock('@/lib/host', () => ({
  hubBaseUrl: () => 'wss://test.hub',
}));

const payload: SessionTimerUpdatedNotificationDto = {
  liveSessionId: 'sess-1',
  remainingMilliseconds: 60_000,
  isPaused: false,
  emittedAt: '2026-06-04T10:00:00Z',
  totalMilliseconds: 300_000,
  isExpired: false,
  sessionState: 'Active',
};

function fire(method: string, ...args: unknown[]) {
  connectionHandlers[method]?.forEach(cb => cb(...args));
}

describe('onTimerUpdated', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    Object.keys(connectionHandlers).forEach(k => {
      delete connectionHandlers[k];
    });
  });

  test('handler receives SessionTimerUpdated payload', () => {
    const client = createSessionsHubConnection();
    const handler = jest.fn();

    client.onTimerUpdated(handler);
    fire('SessionTimerUpdated', payload);

    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith(payload);
  });

  test('multiple handlers each receive the event', () => {
    const client = createSessionsHubConnection();
    const h1 = jest.fn();
    const h2 = jest.fn();

    client.onTimerUpdated(h1);
    client.onTimerUpdated(h2);
    fire('SessionTimerUpdated', payload);

    expect(h1).toHaveBeenCalledWith(payload);
    expect(h2).toHaveBeenCalledWith(payload);
  });

  test('unsubscribe calls connection.off and stops delivery', () => {
    const client = createSessionsHubConnection();
    const handler = jest.fn();

    const unsub = client.onTimerUpdated(handler);
    unsub();

    expect(mockOff).toHaveBeenCalledWith('SessionTimerUpdated', handler);

    fire('SessionTimerUpdated', payload);
    expect(handler).not.toHaveBeenCalled();
  });

  test('unsubscribing one handler does not affect others', () => {
    const client = createSessionsHubConnection();
    const h1 = jest.fn();
    const h2 = jest.fn();

    const unsub1 = client.onTimerUpdated(h1);
    client.onTimerUpdated(h2);
    unsub1();

    fire('SessionTimerUpdated', payload);

    expect(h1).not.toHaveBeenCalled();
    expect(h2).toHaveBeenCalledWith(payload);
  });

  test('reconnect invocation is not affected by onTimerUpdated', async () => {
    const reconnectResult = { liveSessionId: 'sess-1', teamId: 'team-1' };
    mockInvoke.mockResolvedValueOnce(reconnectResult);

    const client = createSessionsHubConnection();
    client.onTimerUpdated(jest.fn());

    const result = await client.reconnect('sess-1', {
      teamId: 'team-1',
      displayName: 'Nova',
      token: null,
    });

    expect(mockInvoke).toHaveBeenCalledWith('ReconnectAsync', 'sess-1', {
      teamId: 'team-1',
      displayName: 'Nova',
      token: null,
    });
    expect(result).toEqual(reconnectResult);
  });
});

const statePayload: SessionStateChangedNotificationDto = {
  liveSessionId: 'sess-1',
  previousState: 'Preparing',
  currentState: 'Active',
  changedAt: '2026-06-04T10:00:00Z',
};

describe('onStateChanged', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    Object.keys(connectionHandlers).forEach(k => {
      delete connectionHandlers[k];
    });
  });

  test('handler receives SessionStateChanged payload', () => {
    const client = createSessionsHubConnection();
    const handler = jest.fn();

    client.onStateChanged(handler);
    fire('SessionStateChanged', statePayload);

    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith(statePayload);
  });

  test('unsubscribe removes the handler', () => {
    const client = createSessionsHubConnection();
    const handler = jest.fn();

    const unsub = client.onStateChanged(handler);
    unsub();

    expect(mockOff).toHaveBeenCalledWith('SessionStateChanged', handler);

    fire('SessionStateChanged', statePayload);
    expect(handler).not.toHaveBeenCalled();
  });

  test('onStateChanged and onTimerUpdated subscriptions are independent', () => {
    const client = createSessionsHubConnection();
    const stateHandler = jest.fn();
    const timerHandler = jest.fn();

    client.onStateChanged(stateHandler);
    client.onTimerUpdated(timerHandler);

    fire('SessionStateChanged', statePayload);
    expect(stateHandler).toHaveBeenCalledTimes(1);
    expect(timerHandler).not.toHaveBeenCalled();

    fire('SessionTimerUpdated', payload);
    expect(timerHandler).toHaveBeenCalledTimes(1);
    expect(stateHandler).toHaveBeenCalledTimes(1);
  });
});
