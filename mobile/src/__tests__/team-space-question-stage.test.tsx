import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';
import { useTeamBoard } from '@/lib/realtime/use-team-board';

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

jest.mock('@/lib/realtime/use-session-timer', () => ({
  useSessionTimer: jest.fn(),
}));

jest.mock('@/lib/realtime/use-active-question', () => ({
  useActiveQuestion: jest.fn(),
}));

jest.mock('@/lib/realtime/use-team-board', () => ({
  useTeamBoard: jest.fn(() => ({ board: null, isLoading: false, error: null })),
}));

const mockUseSessionTimer = useSessionTimer as jest.MockedFunction<typeof useSessionTimer>;
const mockUseActiveQuestion = useActiveQuestion as jest.MockedFunction<typeof useActiveQuestion>;
const mockUseTeamBoard = useTeamBoard as jest.MockedFunction<typeof useTeamBoard>;

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

type TreeNode = {
  props?: Record<string, unknown>;
  children?: (TreeNode | string)[] | null;
};

function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  const n = node as TreeNode;
  return allText(n.children ?? []);
}

function renderSpace() {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(LiveTeamSpace, {
        outcome: OUTCOME,
        onLeave: jest.fn(),
        client: CLIENT,
        reconnectNonce: 0,
        referenceTeamId: 'team-1',
      }),
    );
  });
  return renderer!;
}

function primeTimer() {
  mockUseSessionTimer.mockReturnValue({
    timer: null,
    isLoading: false,
    error: null,
    display: { label: '00:42', pct: 70, tone: 'running' },
    activeQuestion: null,
    sessionState: 'Active',
    snapshotVersion: 1,
  });
}

describe('LiveTeamSpace question stage wiring', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    primeTimer();
    mockUseTeamBoard.mockReturnValue({ board: null, isLoading: false, error: null });
  });

  test('renders active question stage instead of standalone timer and join panel', () => {
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

    const texts = allText(renderSpace().toJSON());

    expect(texts).toContain('Which lantern is lit?');
    expect(texts).toContain('QUESTION 1');
    expect(texts).toContain('00:42');
    expect(texts).not.toContain('Running');
    expect(texts).not.toContain('Joined live session');
  });

  test('renders empty state beneath the stage header for non-active views', () => {
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'waiting' } });

    const texts = allText(renderSpace().toJSON());

    expect(texts).toContain('Active');
    expect(texts).toContain('SCORE');
    expect(texts.join(' ')).toContain('Waiting for the next question');
  });

  test('renders retained question while Paused instead of an empty state', () => {
    mockUseActiveQuestion.mockReturnValue({
      sessionState: 'Paused',
      isQuestionClosed: false,
      view: {
        kind: 'active',
        question: {
          questionIndex: 1,
          sequenceOrder: 2,
          prompt: 'Paused question stays visible',
          options: ['One', 'Two'],
          timeLimitSeconds: 30,
          triviaSubstageSnapshotId: 'substage-def',
        },
      },
    });

    const texts = allText(renderSpace().toJSON());

    expect(texts).toContain('Paused');
    expect(texts).toContain('Paused question stays visible');
    expect(texts.join(' ')).not.toContain('No active question yet');
  });

  test('threads a closed flag into the stage as the close affordance', () => {
    mockUseActiveQuestion.mockReturnValue({
      sessionState: 'Active',
      isQuestionClosed: true,
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

    const texts = allText(renderSpace().toJSON());

    expect(texts.join(' ')).toContain('Question closed — waiting for the next');
    expect(texts).not.toContain('Submit answer');
    expect(texts).not.toContain('Answer submitted');
  });

  test('opens teams sheet from the persistent footer', () => {
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'none' } });
    const renderer = renderSpace();

    expect(allText(renderer.toJSON()).join(' ')).toContain('YOUR TEAM · Lantern Foxes');
    const footerText = renderer.root.findByProps({ accessibilityLabel: 'Open all teams for Lantern Foxes' });

    act(() => {
      (footerText.props.onPress as () => void)();
    });

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('ALL TEAMS');
    expect(texts).toContain('Lantern Foxes');
    expect(texts).toContain('CLOSE');
  });
});

const TREASURE_HUNT_BOARD = {
  liveSessionId: 'sess-1',
  teamId: 'team-1',
  teamDisplayName: 'Lantern Foxes',
  teamCode: 'LF-01',
  currentScore: 240,
  timer: {} as never,
  activeSubstage: {
    substageSnapshotId: 'sub-1',
    playMode: 'TreasureHunt' as const,
    title: 'The Cartographer’s Vault',
    totalActiveTargets: 5,
    resolvedTargets: 2,
    activeQuestionSequenceOrder: null,
    activeQuestionTimeLimitSeconds: null,
  },
  visibleClues: [
    { targetSnapshotId: 't1', clueText: 'Follow the north colonnade.', targetName: 'Brass Astrolabe' },
  ],
};

const TRIVIA_BOARD = {
  ...TREASURE_HUNT_BOARD,
  activeSubstage: {
    ...TREASURE_HUNT_BOARD.activeSubstage,
    playMode: 'Trivia' as const,
    totalActiveTargets: 0,
    resolvedTargets: 0,
  },
};

describe('LiveTeamSpace play-mode branch', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    primeTimer();
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'none' } });
  });

  test('TreasureHunt board renders the board and not the trivia stage', () => {
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });

    const texts = allText(renderSpace().toJSON());

    expect(texts).toContain('TREASURE HUNT');
    expect(texts).toContain('The Cartographer’s Vault');
    expect(texts).toContain('240');
    // target progress (resolved / total), a count — not coordinates
    expect(texts.join('')).toContain('2 / 5 targets');
    // map stub + placeholder markers survive
    expect(texts).toContain('MAP PREVIEW · STUB');
    // trivia empty-state copy is NOT present
    expect(texts.join(' ')).not.toContain('Waiting for the next question');
  });

  test('timer display from useSessionTimer reaches the board', () => {
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });

    const texts = allText(renderSpace().toJSON());

    // primeTimer() sets label 00:42 — the board renders the shared SessionTimerBar with it.
    expect(texts).toContain('00:42');
  });

  test('Trivia board renders the trivia stage and not the board', () => {
    mockUseTeamBoard.mockReturnValue({ board: TRIVIA_BOARD, isLoading: false, error: null });
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'waiting' } });

    const texts = allText(renderSpace().toJSON());

    expect(texts.join(' ')).toContain('Waiting for the next question');
    expect(texts).not.toContain('TREASURE HUNT');
  });

  test('null board keeps the trivia surface (pre-load window)', () => {
    mockUseTeamBoard.mockReturnValue({ board: null, isLoading: false, error: null });
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'waiting' } });

    const texts = allText(renderSpace().toJSON());

    expect(texts.join(' ')).toContain('Waiting for the next question');
    expect(texts).not.toContain('TREASURE HUNT');
  });

  test('surfaces a failed board fetch instead of silently falling to trivia', () => {
    mockUseTeamBoard.mockReturnValue({ board: null, isLoading: false, error: 'forbidden' });
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'waiting' } });

    const texts = allText(renderSpace().toJSON());

    expect(texts.join(' ')).toContain("You don't have access to this team's board.");
  });

  test('a retained board suppresses the error banner on a failed re-fetch', () => {
    // A failed re-fetch leaves the last-good board intact — render it, no banner.
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: 'network-error' });

    const texts = allText(renderSpace().toJSON());

    expect(texts).toContain('TREASURE HUNT');
    expect(texts.join(' ')).not.toContain("Couldn't reach the live board");
  });
});
