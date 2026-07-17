import React from 'react';
import { act, create } from 'react-test-renderer';
import { useRanking, type UseRankingResult } from '@/lib/realtime/use-ranking';
import type { RankingSnapshotDto } from '@/lib/realtime/ranking-types';
import { ApiError } from '@/lib/api/client';

// --- Mocks ---

const mockGetRanking = jest.fn();

jest.mock('@/lib/api/sessions', () => {
  const actual = jest.requireActual<typeof import('@/lib/api/sessions')>('@/lib/api/sessions');
  return {
    ...actual,
    getRanking: (...args: unknown[]) => mockGetRanking(...args),
  };
});

// --- Fake scoring hub client (HU-25B Slice 3) ---

type RankingHandler = (s: RankingSnapshotDto) => void;

let rankingHandlers: Set<RankingHandler>;
let reconnectedHandlers: Set<() => void>;
let closedHandlers: Set<() => void>;

function makeScoringClient() {
  rankingHandlers = new Set();
  reconnectedHandlers = new Set();
  closedHandlers = new Set();
  return {
    connection: {} as never,
    start: jest.fn(),
    stop: jest.fn(),
    joinSessionGroup: jest.fn(),
    leaveSessionGroup: jest.fn(),
    onRankingChanged(cb: RankingHandler) {
      rankingHandlers.add(cb);
      return () => rankingHandlers.delete(cb);
    },
    onReconnected(cb: () => void) {
      reconnectedHandlers.add(cb);
      return () => reconnectedHandlers.delete(cb);
    },
    onClosed(cb: () => void) {
      closedHandlers.add(cb);
      return () => closedHandlers.delete(cb);
    },
  };
}

function fireRankingChanged(snapshot: RankingSnapshotDto) {
  rankingHandlers.forEach(cb => cb(snapshot));
}

function fireReconnected() {
  reconnectedHandlers.forEach(cb => cb());
}

function fireClosed() {
  closedHandlers.forEach(cb => cb());
}

// --- Hook harness ---

function renderHook(
  liveSessionId: string,
  teamId: string,
  token?: string | null,
  scoringClient?: ReturnType<typeof makeScoringClient> | null,
) {
  let result: UseRankingResult | null = null;
  let currentArgs = { liveSessionId, teamId, token, scoringClient };

  function Harness() {
    result = useRanking(
      currentArgs.liveSessionId,
      currentArgs.teamId,
      currentArgs.token,
      currentArgs.scoringClient,
    );
    return null;
  }

  let renderer: ReturnType<typeof create>;
  act(() => {
    renderer = create(React.createElement(Harness));
  });

  return {
    get: () => result!,
    rerender: async (patch: { liveSessionId?: string; teamId?: string; token?: string | null; scoringClient?: ReturnType<typeof makeScoringClient> | null }) => {
      currentArgs = { ...currentArgs, ...patch };
      await act(async () => {
        renderer.update(React.createElement(Harness));
      });
    },
    unmount: () => act(() => renderer.unmount()),
  };
}

// --- Fixtures ---

const BASE_SNAPSHOT: RankingSnapshotDto = {
  liveSessionId: 'sess-1',
  generatedAt: '2026-07-14T10:00:00Z',
  calculationVersion: 1,
  rows: [
    { teamId: 't1', teamDisplayName: 'Compass Rose', position: 1, totalScore: 580, resolutionTime: '00:35:12' },
    { teamId: 't2', teamDisplayName: 'Ember Foxes', position: 2, totalScore: 555, resolutionTime: '00:38:45' },
    { teamId: 't3', teamDisplayName: 'Lantern Bearers', position: 3, totalScore: 520, resolutionTime: '00:42:10' },
  ],
};

const EMPTY_SNAPSHOT: RankingSnapshotDto = {
  liveSessionId: 'sess-1',
  generatedAt: '0001-01-01T00:00:00',
  calculationVersion: 0,
  rows: [],
};

// --- Tests ---

describe('useRanking', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('starts loading with null snapshot', async () => {
    // Keep the promise pending so loading stays true across the act boundary.
    mockGetRanking.mockReturnValueOnce(new Promise<RankingSnapshotDto>(() => {}));

    const hook = renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    expect(hook.get().isLoading).toBe(true);
    expect(hook.get().snapshot).toBeNull();
    expect(hook.get().error).toBeNull();

    hook.unmount();
  });

  test('fetches ranking snapshot on mount', async () => {
    mockGetRanking.mockResolvedValueOnce(BASE_SNAPSHOT);

    const hook = renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledWith('sess-1', 'team-a', undefined);
    expect(hook.get().snapshot?.rows).toHaveLength(3);
    expect(hook.get().snapshot?.rows[0].teamDisplayName).toBe('Compass Rose');
    expect(hook.get().isLoading).toBe(false);
    expect(hook.get().error).toBeNull();

    hook.unmount();
  });

  test('passes token to the API call', async () => {
    mockGetRanking.mockResolvedValueOnce(BASE_SNAPSHOT);

    const hook = renderHook('sess-1', 'team-a', 'tok-abc');

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledWith('sess-1', 'team-a', 'tok-abc');

    hook.unmount();
  });

  test('handles empty snapshot (no standings yet)', async () => {
    mockGetRanking.mockResolvedValueOnce(EMPTY_SNAPSHOT);

    const hook = renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    expect(hook.get().snapshot?.rows).toHaveLength(0);
    expect(hook.get().isLoading).toBe(false);
    expect(hook.get().error).toBeNull();

    hook.unmount();
  });

  test('sets error on fetch failure and keeps snapshot null', async () => {
    mockGetRanking.mockRejectedValueOnce(new ApiError(403, 'forbidden', 'nope'));

    const hook = renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    expect(hook.get().error).toBe('forbidden');
    expect(hook.get().snapshot).toBeNull();
    expect(hook.get().isLoading).toBe(false);

    hook.unmount();
  });

  test('refetch re-fetches the ranking', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);

    const hook = renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledTimes(1);

    act(() => {
      hook.get().refetch();
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledTimes(2);

    hook.unmount();
  });

  test('changing teamId triggers a re-fetch', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);

    const hook = renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledTimes(1);

    await hook.rerender({ teamId: 'team-b' });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledTimes(2);
    expect(mockGetRanking).toHaveBeenLastCalledWith('sess-1', 'team-b', undefined);

    hook.unmount();
  });

  test('changing liveSessionId triggers a re-fetch', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);

    const hook = renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledTimes(1);

    await hook.rerender({ liveSessionId: 'sess-2' });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledTimes(2);
    expect(mockGetRanking).toHaveBeenLastCalledWith('sess-2', 'team-a', undefined);

    hook.unmount();
  });

  // ── SignalR push tests (HU-25B Slice 3) ──────────────────────────────────

  test('subscribes to RankingChanged when scoringClient is provided', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);
    const scoringClient = makeScoringClient();

    const hook = renderHook('sess-1', 'team-a', undefined, scoringClient);

    await act(async () => {
      await Promise.resolve();
    });

    act(() => {
      fireRankingChanged({ ...BASE_SNAPSHOT, calculationVersion: 2, rows: BASE_SNAPSHOT.rows.slice(0, 1) });
    });

    expect(hook.get().snapshot?.calculationVersion).toBe(2);
    expect(hook.get().snapshot?.rows).toHaveLength(1);

    hook.unmount();
  });

  test('ignores a RankingChanged push for a different liveSessionId', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);
    const scoringClient = makeScoringClient();

    const hook = renderHook('sess-1', 'team-a', undefined, scoringClient);

    await act(async () => {
      await Promise.resolve();
    });

    act(() => {
      fireRankingChanged({ ...BASE_SNAPSHOT, liveSessionId: 'other-sess', calculationVersion: 2 });
    });

    expect(hook.get().snapshot?.calculationVersion).toBe(1);

    hook.unmount();
  });

  test('unsubscribes from RankingChanged on unmount', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);
    const scoringClient = makeScoringClient();

    const hook = renderHook('sess-1', 'team-a', undefined, scoringClient);

    await act(async () => {
      await Promise.resolve();
    });

    expect(rankingHandlers.size).toBe(1);

    hook.unmount();

    expect(rankingHandlers.size).toBe(0);
  });

  test('does not subscribe to RankingChanged when scoringClient is null', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);

    renderHook('sess-1', 'team-a');

    await act(async () => {
      await Promise.resolve();
    });

    // No handler should have been registered
    expect(rankingHandlers?.size ?? 0).toBe(0);
  });

  test('re-fetches the snapshot on transport reconnect (missed pushes)', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);
    const scoringClient = makeScoringClient();

    const hook = renderHook('sess-1', 'team-a', undefined, scoringClient);

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetRanking).toHaveBeenCalledTimes(1);

    await act(async () => {
      fireReconnected();
      await Promise.resolve();
    });

    // A reconnect re-syncs via REST so standings missed during the outage are pulled.
    expect(mockGetRanking).toHaveBeenCalledTimes(2);

    hook.unmount();
  });

  test('surfaces an error when the connection closes for good', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);
    const scoringClient = makeScoringClient();

    const hook = renderHook('sess-1', 'team-a', undefined, scoringClient);

    await act(async () => {
      await Promise.resolve();
    });

    expect(hook.get().error).toBeNull();

    act(() => {
      fireClosed();
    });

    expect(hook.get().error).toBe('network-error');

    hook.unmount();
  });

  test('unsubscribes reconnect/close handlers on unmount', async () => {
    mockGetRanking.mockResolvedValue(BASE_SNAPSHOT);
    const scoringClient = makeScoringClient();

    const hook = renderHook('sess-1', 'team-a', undefined, scoringClient);

    await act(async () => {
      await Promise.resolve();
    });

    expect(reconnectedHandlers.size).toBe(1);
    expect(closedHandlers.size).toBe(1);

    hook.unmount();

    expect(reconnectedHandlers.size).toBe(0);
    expect(closedHandlers.size).toBe(0);
  });
});
