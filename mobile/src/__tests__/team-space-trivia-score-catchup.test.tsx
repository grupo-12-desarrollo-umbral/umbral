// Guards the trivia header score against a missed `RankingChanged` push. A correct answer is scored
// asynchronously (submit → outbox → RabbitMQ → RecordScoreEntry → recalc → RankingChanged), so the
// header's live `score` only reflects the "+points" once the ranking snapshot refreshes. Unlike the
// treasure-hunt scan and penalties, the trivia flow had NO ranking catch-up — a missed push left the
// award invisible until the substage reveal force-refetched, reading as "the correct answer added no
// points." When a question closes into the reveal view, the screen must pull the ranking across the
// scoring window so the reveal shows the awarded points.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useRanking } from '@/lib/realtime/use-ranking';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';
import { useTeamBoard } from '@/lib/realtime/use-team-board';
import type { ActiveQuestionView } from '@/lib/realtime/active-question-types';

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
    teamId: 'team-1',
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

const QUESTION = {
  questionIndex: 0,
  sequenceOrder: 1,
  prompt: 'Which lantern is lit?',
  options: ['North', 'South'],
  timeLimitSeconds: 45,
  triviaSubstageSnapshotId: 'substage-abc',
};

const ACTIVE_VIEW: ActiveQuestionView = { kind: 'active', question: QUESTION };
const REVEAL_VIEW: ActiveQuestionView = {
  kind: 'reveal',
  question: QUESTION,
  correctOptionSequenceOrder: 1,
  explanation: 'The north lantern marks the vault.',
  teamResult: null,
};

function primeView(view: ActiveQuestionView) {
  mockUseActiveQuestion.mockReturnValue({
    sessionState: 'Active',
    isQuestionClosed: view.kind === 'reveal',
    view,
  });
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

describe('LiveTeamSpace trivia score catch-up', () => {
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
    mockUseTeamBoard.mockReturnValue({ board: null, isLoading: false, error: null });
  });

  afterEach(() => {
    act(() => {
      mounted?.unmount();
    });
    mounted = null;
    jest.clearAllTimers();
    jest.useRealTimers();
  });

  test('refetches the ranking when a question closes into the reveal view', () => {
    const refetch = jest.fn();
    mockUseRanking.mockReturnValue({ snapshot: null, isLoading: false, error: null, refetch });

    // A live question: no catch-up is due while it is still open (the header stays frozen on purpose).
    primeView(ACTIVE_VIEW);
    act(() => {
      mounted = create(element());
    });
    act(() => {
      jest.advanceTimersByTime(6000);
    });
    expect(refetch).not.toHaveBeenCalled();

    // The question closes: view flips to reveal, and the staggered REST catch-up pulls the ranking
    // across the scoring window so the reveal shows the awarded "+points".
    primeView(REVEAL_VIEW);
    act(() => {
      mounted!.update(element());
    });
    act(() => {
      jest.advanceTimersByTime(6000);
    });
    expect(refetch).toHaveBeenCalled();
  });

  test('does not re-arm the catch-up on re-renders that stay in the reveal view', () => {
    const refetch = jest.fn();
    mockUseRanking.mockReturnValue({ snapshot: null, isLoading: false, error: null, refetch });

    primeView(REVEAL_VIEW);
    act(() => {
      mounted = create(element());
    });
    act(() => {
      jest.advanceTimersByTime(6000);
    });
    const callsAfterFirstReveal = refetch.mock.calls.length;
    expect(callsAfterFirstReveal).toBeGreaterThan(0);

    // A re-render while still revealing the same question must not schedule a fresh round of refetches.
    act(() => {
      mounted!.update(element());
    });
    act(() => {
      jest.advanceTimersByTime(6000);
    });
    expect(refetch.mock.calls.length).toBe(callsAfterFirstReveal);
  });
});
