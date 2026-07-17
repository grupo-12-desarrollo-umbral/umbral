// Guards the header score freeze across a view transition: entering a question must freeze the score as
// it stands at that moment, not a value left over from an earlier question.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useRanking } from '@/lib/realtime/use-ranking';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';
import { useTeamBoard } from '@/lib/realtime/use-team-board';
import type { RankingSnapshotDto } from '@/lib/realtime/ranking-types';

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

type TreeNode = { children?: (TreeNode | string)[] | null };

function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  return allText((node as TreeNode).children ?? []);
}

function snapshotWithScore(totalScore: number): RankingSnapshotDto {
  return {
    liveSessionId: 'sess-1',
    generatedAt: '2026-07-11T10:05:00Z',
    calculationVersion: 1,
    rows: [
      {
        teamId: REFERENCE_TEAM_ID,
        teamDisplayName: 'Lantern Foxes',
        position: 1,
        totalScore,
        resolutionTime: null,
      },
    ],
  };
}

function primeRanking(totalScore: number) {
  mockUseRanking.mockReturnValue({
    snapshot: snapshotWithScore(totalScore),
    isLoading: false,
    error: null,
    refetch: jest.fn(),
  });
}

function primeWaiting() {
  mockUseActiveQuestion.mockReturnValue({
    sessionState: 'Active',
    isQuestionClosed: false,
    view: { kind: 'waiting' },
  });
}

function primeActiveQuestion() {
  mockUseActiveQuestion.mockReturnValue({
    sessionState: 'Active',
    isQuestionClosed: false,
    view: {
      kind: 'active',
      question: {
        questionIndex: 0,
        sequenceOrder: 1,
        prompt: 'Which lantern is lit?',
        options: ['North', 'South'],
        timeLimitSeconds: 45,
        triviaSubstageSnapshotId: 'substage-abc',
      },
    },
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

function renderSpace() {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(element());
  });
  mounted = renderer!;
  return renderer!;
}

function rerenderSpace(renderer: ReturnType<typeof create>) {
  act(() => {
    renderer.update(element());
  });
}

describe('LiveTeamSpace header score freeze', () => {
  beforeEach(() => {
    jest.clearAllMocks();
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
  });

  // The score earned on an earlier question must be what the header freezes when the next one opens.
  // Mount at 120 (an earlier question's score), climb to 500 while waiting, then open a question.
  test('freezes the score as it stands when the question opens, not the mount-time score', () => {
    primeWaiting();
    primeRanking(120);
    const renderer = renderSpace();

    // The reveal awards points while the participant waits for the next question.
    primeRanking(500);
    rerenderSpace(renderer);
    expect(allText(renderer.toJSON())).toContain('500');

    // The next question opens: the header freezes, and must hold 500 — the score at this moment.
    primeActiveQuestion();
    rerenderSpace(renderer);

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('500');
    expect(texts).not.toContain('120');
  });
});
