// Guards the penalty toast against the freeze that holds the header score steady during a live question:
// the toast must read the live score, or a mid-question penalty stays invisible until the reveal.
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
  onStateChanged: jest.fn(),
  onQuestionActivated: jest.fn(),
  onQuestionClosed: jest.fn(),
  onSubstageAdvanced: jest.fn(),
  onTeamBoardUpdated: jest.fn(),
};

type TreeNode = { children?: (TreeNode | string)[] | null };

function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  return allText((node as TreeNode).children ?? []);
}

// Text arrives split across segments ("Score dropped by ", "100", " pts"), so match on a flattened string.
function flatText(node: unknown): string {
  return allText(node).join(' ').replace(/\s+/g, ' ').trim();
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

// Tracked so afterEach can unmount: the toast holds a 4s auto-dismiss timeout whose cleanup only runs
// on unmount, and a surviving timer fires after Jest tears the environment down.
let mounted: ReturnType<typeof create> | null = null;

function renderSpace() {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(LiveTeamSpace, {
        outcome: OUTCOME,
        onLeave: jest.fn(),
        client: CLIENT,
        reconnectNonce: 0,
        referenceTeamId: REFERENCE_TEAM_ID,
      }),
    );
  });
  mounted = renderer!;
  return renderer!;
}

function rerenderSpace(renderer: ReturnType<typeof create>) {
  act(() => {
    renderer.update(
      React.createElement(LiveTeamSpace, {
        outcome: OUTCOME,
        onLeave: jest.fn(),
        client: CLIENT,
        reconnectNonce: 0,
        referenceTeamId: REFERENCE_TEAM_ID,
      }),
    );
  });
}

describe('LiveTeamSpace penalty toast', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUseSessionTimer.mockReturnValue({
      timer: null,
      isLoading: false,
      error: null,
      display: { label: '00:42', pct: 70, tone: 'running' },
      activeQuestion: null,
      sessionState: 'Active',
      pregameSecondsLeft: null,
      snapshotVersion: 1,
    });
    mockUseTeamBoard.mockReturnValue({ board: null, isLoading: false, error: null });
    primeActiveQuestion();
  });

  afterEach(() => {
    act(() => {
      mounted?.unmount();
    });
    mounted = null;
  });

  test('surfaces a penalty applied mid-question without waiting for the reveal', () => {
    primeRanking(500);
    const renderer = renderSpace();

    // The operator applies a penalty while the question is still on screen.
    primeRanking(400);
    rerenderSpace(renderer);

    expect(allText(renderer.toJSON())).toContain('PENALTY APPLIED');
    expect(flatText(renderer.toJSON())).toContain('Score dropped by 100 pts');
  });

  test('still surfaces a penalty that a correct answer nets back out', () => {
    primeRanking(500);
    const renderer = renderSpace();

    // Penalty (-100) lands, then the answer award (+100) returns the score to where it started.
    primeRanking(400);
    rerenderSpace(renderer);
    primeRanking(500);
    rerenderSpace(renderer);

    expect(allText(renderer.toJSON())).toContain('PENALTY APPLIED');
  });

  test('holds the header score steady during the question despite the penalty', () => {
    primeRanking(500);
    const renderer = renderSpace();

    primeRanking(400);
    rerenderSpace(renderer);

    // The freeze is a header concern and still applies: the stage header keeps the pre-question score.
    const texts = allText(renderer.toJSON());
    expect(texts).toContain('500');
    expect(texts).not.toContain('400');
  });
});
