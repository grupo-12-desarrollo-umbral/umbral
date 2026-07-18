// Guards the treasure-hunt header score against a missed `RankingChanged` push. When a TEAMMATE resolves
// a target, this device only learns of it through the reliable `TeamBoardUpdated` board push (the X/n
// numerator advances), never through its own scan flow — and the board's own score always projects 0. So
// the header score, which reads from the ranking snapshot, must be pulled across the scoring window
// whenever the resolved-target count grows, or it stays stuck on the pre-scan value until the reveal.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useRanking } from '@/lib/realtime/use-ranking';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';
import { useTeamBoard } from '@/lib/realtime/use-team-board';
import type { ParticipantTeamBoardDto } from '@/lib/realtime/team-board-types';

jest.mock('expo-router', () => ({
  useLocalSearchParams: jest.fn(() => ({})),
  useRouter: jest.fn(() => ({ push: jest.fn(), replace: jest.fn() })),
}));

jest.mock('expo-haptics', () => ({
  notificationAsync: jest.fn(),
  NotificationFeedbackType: { Success: 'success', Error: 'error' },
}));

jest.mock('@/lib/auth/use-auth', () => ({
  useAuth: jest.fn(() => ({ profile: { displayName: 'Nova' } })),
}));

jest.mock('@/lib/realtime/use-session-timer', () => ({ useSessionTimer: jest.fn() }));
jest.mock('@/lib/realtime/use-active-question', () => ({ useActiveQuestion: jest.fn() }));
jest.mock('@/lib/realtime/use-team-board', () => ({ useTeamBoard: jest.fn() }));
jest.mock('@/lib/realtime/use-ranking', () => ({ useRanking: jest.fn() }));

const mockUseSessionTimer = useSessionTimer as jest.MockedFunction<typeof useSessionTimer>;
const mockUseActiveQuestion = useActiveQuestion as jest.MockedFunction<typeof useActiveQuestion>;
const mockUseTeamBoard = useTeamBoard as jest.MockedFunction<typeof useTeamBoard>;
const mockUseRanking = useRanking as jest.MockedFunction<typeof useRanking>;

const REFERENCE_TEAM_ID = 'team-1';

const OUTCOME = {
  kind: 'reconnected' as const,
  result: {
    liveSessionId: 'sess-1',
    teamId: 'board-team-1',
    teamDisplayName: 'Lantern Foxes',
    sessionParticipantId: 'participant-1',
    participantDisplayName: 'Nova',
    sessionState: 'Active',
    isReconnect: false,
    joinedAt: '2026-07-11T10:00:00Z',
    lastSeenAt: '2026-07-11T10:00:00Z',
  },
};

const CLIENT = {
  connection: {} as never,
  start: jest.fn(),
  stop: jest.fn(),
  reconnect: jest.fn(),
  onTimerUpdated: jest.fn(),
  onStateChanged: jest.fn(() => () => {}),
  onQuestionActivated: jest.fn(),
  onQuestionClosed: jest.fn(),
  onSubstageAdvanced: jest.fn(() => () => {}),
  onTeamBoardUpdated: jest.fn(),
  onSubstageRankingRevealStarted: jest.fn(() => () => {}),
};

function treasureBoard(resolvedTargets: number): ParticipantTeamBoardDto {
  return {
    liveSessionId: 'sess-1',
    missionTitle: 'The Vault',
    teamId: 'board-team-1',
    teamDisplayName: 'Lantern Foxes',
    teamCode: 'LF',
    // Session-operations never awards score — always 0. The real score comes from the ranking snapshot.
    currentScore: 0,
    timer: {} as never,
    activeSubstage: {
      substageSnapshotId: 'substage-1',
      playMode: 'TreasureHunt',
      title: 'Find the relics',
      totalActiveTargets: 3,
      resolvedTargets,
      activeQuestionSequenceOrder: null,
      activeQuestionTimeLimitSeconds: null,
    },
    substages: [],
    visibleClues: [],
    activeTargets: [],
  };
}

let mounted: ReturnType<typeof create> | null = null;

function element() {
  return React.createElement(LiveTeamSpace, {
    outcome: OUTCOME,
    onLeave: jest.fn(),
    client: CLIENT,
    reconnectNonce: 0,
    referenceTeamId: REFERENCE_TEAM_ID,
  });
}

describe('LiveTeamSpace treasure-hunt score catch-up', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    mockUseSessionTimer.mockReturnValue({
      timer: null,
      isLoading: false,
      error: null,
      display: { label: '00:42', pct: 70, tone: 'running' },
      missionDisplay: null,
      activeQuestion: null,
      revealReconciliation: null,
      sessionState: 'Active',
      pregameSecondsLeft: null,
      snapshotVersion: 1,
    });
    // No trivia question — a parked treasure-hunt board.
    mockUseActiveQuestion.mockReturnValue({
      sessionState: 'Active',
      isQuestionClosed: false,
      view: { kind: 'none' },
    });
  });

  afterEach(() => {
    act(() => {
      mounted?.unmount();
    });
    mounted = null;
    jest.clearAllTimers();
    jest.useRealTimers();
  });

  test('refetches the ranking when a teammate advances the resolved-target count', () => {
    const refetch = jest.fn();
    mockUseRanking.mockReturnValue({ snapshot: null, isLoading: false, error: null, refetch });
    mockUseTeamBoard.mockReturnValue({ board: treasureBoard(1), isLoading: false, error: null });

    act(() => {
      mounted = create(element());
    });

    // Mount does not fire a catch-up (no growth relative to the mount baseline).
    act(() => {
      jest.advanceTimersByTime(6000);
    });
    expect(refetch).not.toHaveBeenCalled();

    // A teammate resolves a target: the board push lands with an advanced numerator (1 → 2), score still 0.
    mockUseTeamBoard.mockReturnValue({ board: treasureBoard(2), isLoading: false, error: null });
    act(() => {
      mounted!.update(element());
    });

    // The staggered REST catch-up pulls the ranking across the scoring window so the header updates.
    act(() => {
      jest.advanceTimersByTime(6000);
    });
    expect(refetch).toHaveBeenCalled();
  });

  test('does not refetch when the count resets on a substage boundary', () => {
    const refetch = jest.fn();
    mockUseRanking.mockReturnValue({ snapshot: null, isLoading: false, error: null, refetch });
    mockUseTeamBoard.mockReturnValue({ board: treasureBoard(3), isLoading: false, error: null });

    act(() => {
      mounted = create(element());
    });

    // The next substage begins: resolved-target count drops back to 0 — not a resolution, so no catch-up.
    mockUseTeamBoard.mockReturnValue({ board: treasureBoard(0), isLoading: false, error: null });
    act(() => {
      mounted!.update(element());
    });
    act(() => {
      jest.advanceTimersByTime(6000);
    });

    expect(refetch).not.toHaveBeenCalled();
  });
});
