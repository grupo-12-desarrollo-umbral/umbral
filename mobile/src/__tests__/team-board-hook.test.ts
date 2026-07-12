import React from 'react';
import { act, create } from 'react-test-renderer';
import { useTeamBoard, type UseTeamBoardResult } from '@/lib/realtime/use-team-board';
import type { ParticipantTeamBoardDto } from '@/lib/realtime/team-board-types';
import { ApiError } from '@/lib/api/client';

// --- Mocks ---

const mockGetTeamBoard = jest.fn();

jest.mock('@/lib/api/sessions', () => {
  const actual = jest.requireActual<typeof import('@/lib/api/sessions')>('@/lib/api/sessions');
  return {
    ...actual,
    getParticipantTeamBoard: (...args: unknown[]) => mockGetTeamBoard(...args),
  };
});

// --- Fake hub client ---

type BoardHandler = (b: ParticipantTeamBoardDto) => void;

let handlers: Set<BoardHandler>;

function makeClient() {
  handlers = new Set();
  return {
    connection: {} as never,
    start: jest.fn(),
    stop: jest.fn(),
    reconnect: jest.fn(),
    onTimerUpdated: jest.fn(),
    onStateChanged: jest.fn(),
    onQuestionActivated: jest.fn(),
    onQuestionClosed: jest.fn(),
    onSubstageAdvanced: jest.fn(),
    onTeamBoardUpdated(cb: BoardHandler) {
      handlers.add(cb);
      return () => handlers.delete(cb);
    },
  };
}

function fireBoard(b: ParticipantTeamBoardDto) {
  handlers.forEach(cb => cb(b));
}

// --- Hook harness ---

type HookProps = Parameters<typeof useTeamBoard>[0];

function renderHook(initialProps: HookProps) {
  let result: UseTeamBoardResult | null = null;
  let currentProps = { ...initialProps };

  function Harness() {
    result = useTeamBoard(currentProps);
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

const BASE_BOARD: ParticipantTeamBoardDto = {
  liveSessionId: 'sess-1',
  teamId: 'team-1',
  teamDisplayName: 'Lantern Foxes',
  teamCode: 'LF-01',
  currentScore: 0,
  timer: {} as never,
  activeSubstage: {
    substageSnapshotId: 'sub-1',
    playMode: 'TreasureHunt',
    title: 'The Vault',
    totalActiveTargets: 5,
    resolvedTargets: 1,
    activeQuestionSequenceOrder: null,
    activeQuestionTimeLimitSeconds: null,
  },
  visibleClues: [],
};

// --- Tests ---

describe('useTeamBoard', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('does not fetch before reconnection', () => {
    const client = makeClient();
    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      isReconnected: false,
      reconnectNonce: 0,
    });

    expect(hook.get().board).toBeNull();
    expect(mockGetTeamBoard).not.toHaveBeenCalled();

    hook.unmount();
  });

  test('seeds board from the REST snapshot on reconnection', async () => {
    mockGetTeamBoard.mockResolvedValueOnce(BASE_BOARD);
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

    expect(mockGetTeamBoard).toHaveBeenCalledWith('sess-1', 'team-1', 'tok-abc');
    expect(hook.get().board?.currentScore).toBe(0);
    expect(hook.get().board?.activeSubstage?.playMode).toBe('TreasureHunt');

    hook.unmount();
  });

  test('TeamBoardUpdated push replaces the board', async () => {
    mockGetTeamBoard.mockResolvedValueOnce(BASE_BOARD);
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
      fireBoard({ ...BASE_BOARD, currentScore: 320 });
    });

    expect(hook.get().board?.currentScore).toBe(320);

    hook.unmount();
  });

  test('ignores a push for a different liveSessionId', async () => {
    mockGetTeamBoard.mockResolvedValueOnce(BASE_BOARD);
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
      fireBoard({ ...BASE_BOARD, liveSessionId: 'other-sess', currentScore: 999 });
    });

    expect(hook.get().board?.currentScore).toBe(0);

    hook.unmount();
  });

  test('ignores a push for a different teamId', async () => {
    mockGetTeamBoard.mockResolvedValueOnce(BASE_BOARD);
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
      fireBoard({ ...BASE_BOARD, teamId: 'other-team', currentScore: 999 });
    });

    expect(hook.get().board?.currentScore).toBe(0);

    hook.unmount();
  });

  test('changing reconnectNonce triggers a re-fetch', async () => {
    mockGetTeamBoard
      .mockResolvedValueOnce(BASE_BOARD)
      .mockResolvedValueOnce({ ...BASE_BOARD, currentScore: 88 });

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

    expect(mockGetTeamBoard).toHaveBeenCalledTimes(1);

    await hook.rerender({ reconnectNonce: 1 });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetTeamBoard).toHaveBeenCalledTimes(2);
    expect(hook.get().board?.currentScore).toBe(88);

    hook.unmount();
  });

  test('snapshot error sets the error token and leaves board null', async () => {
    mockGetTeamBoard.mockRejectedValueOnce(new ApiError(403, 'forbidden', 'nope'));
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

    expect(hook.get().error).toBe('forbidden');
    expect(hook.get().board).toBeNull();

    hook.unmount();
  });
});
