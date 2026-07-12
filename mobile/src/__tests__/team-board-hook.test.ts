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

// The hook prop is the identity-access *reference* team id (used for the REST fetch/guard);
// every board DTO carries the *per-session* team id (`Team.TeamId`). They must differ so the
// tests exercise the reference-vs-per-session contract the push filter used to get wrong.
const REFERENCE_TEAM_ID = 'ref-team-a0000000';
const PER_SESSION_TEAM_ID = 'session-team-d7f15865';

const BASE_BOARD: ParticipantTeamBoardDto = {
  liveSessionId: 'sess-1',
  teamId: PER_SESSION_TEAM_ID,
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
  substages: [
    { substageSnapshotId: 'sub-0', title: 'Opening Trivia', sequenceOrder: 0, playMode: 'Trivia', status: 'Completed' },
    { substageSnapshotId: 'sub-1', title: 'The Vault', sequenceOrder: 1, playMode: 'TreasureHunt', status: 'Active' },
  ],
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
      teamId: REFERENCE_TEAM_ID,
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
      teamId: REFERENCE_TEAM_ID,
      token: 'tok-abc',
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetTeamBoard).toHaveBeenCalledWith('sess-1', REFERENCE_TEAM_ID, 'tok-abc');
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
      teamId: REFERENCE_TEAM_ID,
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

  test('a push carrying an advanced substage sequence replaces the ordered substages', async () => {
    // Advancement rebroadcasts the whole board (#171): the prior substage flips to Completed and the
    // next becomes Active. The hook applies the push wholesale, so board.substages tracks it.
    mockGetTeamBoard.mockResolvedValueOnce(BASE_BOARD);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: REFERENCE_TEAM_ID,
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(hook.get().board?.substages.find(s => s.status === 'Active')?.substageSnapshotId).toBe('sub-1');

    act(() => {
      fireBoard({
        ...BASE_BOARD,
        activeSubstage: null,
        substages: [
          { substageSnapshotId: 'sub-0', title: 'Opening Trivia', sequenceOrder: 0, playMode: 'Trivia', status: 'Completed' },
          { substageSnapshotId: 'sub-1', title: 'The Vault', sequenceOrder: 1, playMode: 'TreasureHunt', status: 'Completed' },
        ],
      });
    });

    expect(hook.get().board?.substages.every(s => s.status === 'Completed')).toBe(true);

    hook.unmount();
  });

  test('ignores a push for a different liveSessionId', async () => {
    mockGetTeamBoard.mockResolvedValueOnce(BASE_BOARD);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: REFERENCE_TEAM_ID,
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

  test('applies a push whose teamId is the per-session id (differs from the reference id prop)', async () => {
    // Regression guard: the DTO carries the per-session team id, never the reference id the hook
    // is keyed on. A client-side teamId equality check would drop every push — it must not.
    mockGetTeamBoard.mockResolvedValueOnce(BASE_BOARD);
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: REFERENCE_TEAM_ID,
      isReconnected: true,
      reconnectNonce: 0,
    });

    await act(async () => {
      await Promise.resolve();
    });

    act(() => {
      fireBoard({ ...BASE_BOARD, teamId: PER_SESSION_TEAM_ID, currentScore: 999 });
    });

    expect(hook.get().board?.currentScore).toBe(999);

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
      teamId: REFERENCE_TEAM_ID,
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

  test('advancing sessionState (Preparing → Active) triggers a re-fetch', async () => {
    // The Preparing snapshot has no active substage; the Active re-fetch carries the treasure-hunt board.
    mockGetTeamBoard
      .mockResolvedValueOnce({ ...BASE_BOARD, activeSubstage: null })
      .mockResolvedValueOnce(BASE_BOARD);

    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: REFERENCE_TEAM_ID,
      isReconnected: true,
      reconnectNonce: 0,
      sessionState: 'Preparing',
    });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetTeamBoard).toHaveBeenCalledTimes(1);
    expect(hook.get().board?.activeSubstage).toBeNull();

    await hook.rerender({ sessionState: 'Active' });

    await act(async () => {
      await Promise.resolve();
    });

    expect(mockGetTeamBoard).toHaveBeenCalledTimes(2);
    expect(hook.get().board?.activeSubstage?.playMode).toBe('TreasureHunt');

    hook.unmount();
  });

  test('snapshot error sets the error token and leaves board null', async () => {
    mockGetTeamBoard.mockRejectedValueOnce(new ApiError(403, 'forbidden', 'nope'));
    const client = makeClient();

    const hook = renderHook({
      client,
      liveSessionId: 'sess-1',
      teamId: REFERENCE_TEAM_ID,
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
