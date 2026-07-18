// The penalty toast is driven by the explicit ScoringHub `PenaltyApplied` push (see `usePenaltyToast`),
// not inferred from a ranking score decrease. These tests mock that hook to assert the toast renders in
// the trivia surface, and that the header score freeze during a live question is independent of it.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { usePenaltyToast, type PenaltyToastData } from '@/lib/realtime/use-penalty-toast';
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
jest.mock('@/lib/realtime/use-penalty-toast', () => ({ usePenaltyToast: jest.fn() }));

const mockUseSessionTimer = useSessionTimer as jest.MockedFunction<typeof useSessionTimer>;
const mockUseActiveQuestion = useActiveQuestion as jest.MockedFunction<typeof useActiveQuestion>;
const mockUseTeamBoard = useTeamBoard as jest.MockedFunction<typeof useTeamBoard>;
const mockUseRanking = useRanking as jest.MockedFunction<typeof useRanking>;
const mockUsePenaltyToast = usePenaltyToast as jest.MockedFunction<typeof usePenaltyToast>;

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

function primePenalty(penalty: PenaltyToastData | null) {
  mockUsePenaltyToast.mockReturnValue({ penalty, clear: jest.fn() });
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
      missionDisplay: null,
      activeQuestion: null,
      revealReconciliation: null,
      sessionState: 'Active',
      pregameSecondsLeft: null,
      snapshotVersion: 1,
    });
    mockUseTeamBoard.mockReturnValue({ board: null, isLoading: false, error: null });
    primeActiveQuestion();
    primePenalty(null);
  });

  afterEach(() => {
    act(() => {
      mounted?.unmount();
    });
    mounted = null;
  });

  test('surfaces a penalty pushed for the own team, with the true magnitude and reason', () => {
    primeRanking(500);
    primePenalty({ id: 1, magnitude: 100, reason: 'Unsportsmanlike conduct' });
    const renderer = renderSpace();

    expect(allText(renderer.toJSON())).toContain('PENALIZACIÓN APLICADA');
    expect(flatText(renderer.toJSON())).toContain('La puntuación bajó 100 pts');
    expect(flatText(renderer.toJSON())).toContain('Unsportsmanlike conduct');
  });

  test('surfaces the penalty even when the resulting ranking score is unchanged (clamped)', () => {
    // A penalty against a team at zero leaves the ranking at zero — the old score-decrease heuristic
    // would show nothing. The explicit push still drives the toast.
    primeRanking(0);
    primePenalty({ id: 1, magnitude: 100, reason: 'Late arrival' });
    const renderer = renderSpace();

    expect(allText(renderer.toJSON())).toContain('PENALIZACIÓN APLICADA');
    expect(flatText(renderer.toJSON())).toContain('La puntuación bajó 100 pts');
  });

  test('shows no toast when no penalty has been pushed', () => {
    primeRanking(500);
    primePenalty(null);
    const renderer = renderSpace();

    expect(allText(renderer.toJSON())).not.toContain('PENALIZACIÓN APLICADA');
  });

  test('holds the header score steady during the question, independent of the penalty toast', () => {
    primeRanking(500);
    const renderer = renderSpace();

    // The ranking drops to 400 mid-question (a correct-answer award landed on the ledger — the freeze
    // hides it until the reveal, so a NEGATIVE-looking live move that isn't a penalty must not show).
    primeRanking(400);
    rerenderSpace(renderer);

    // The freeze is a header concern: the stage header keeps the pre-question score.
    const texts = allText(renderer.toJSON());
    expect(texts).toContain('500');
    expect(texts).not.toContain('400');
  });

  test('drops the header score immediately when a penalty is pushed mid-question', () => {
    // Header freezes at 500 on question open, with no penalty yet.
    primeRanking(500);
    const renderer = renderSpace();
    expect(allText(renderer.toJSON())).toContain('500');

    // The operator sends a -100 penalty: the toast appears AND the frozen header must drop at once,
    // without waiting for the question timer to end.
    primePenalty({ id: 1, magnitude: 100, reason: 'Unsportsmanlike conduct' });
    rerenderSpace(renderer);

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('400');
    expect(texts).not.toContain('500');
  });

  test('clamps the header score to zero when the penalty exceeds the current score', () => {
    // Team sits at 60; a -100 penalty must floor the header at 0, not go negative.
    primeRanking(60);
    const renderer = renderSpace();
    expect(allText(renderer.toJSON())).toContain('60');

    primePenalty({ id: 1, magnitude: 100, reason: 'Late arrival' });
    rerenderSpace(renderer);

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('0');
    expect(texts).not.toContain('60');
  });
});
