import React from 'react';
import { act, create, type ReactTestInstance } from 'react-test-renderer';
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

// The host node React Native's test renderer emits for a ScrollView. react-test-renderer types `type`
// against the DOM/SVG intrinsic union, which has no overlap with React Native's host names, so a direct
// `node.type === SCROLL_VIEW_HOST` is a compile error (TS2367) even though it matches at runtime.
// Comparing the widened string keeps the check on the value the renderer actually produces.
const SCROLL_VIEW_HOST = 'RCTScrollView';

function isScrollView(node: ReactTestInstance): boolean {
  return typeof node.type === 'string' && String(node.type) === SCROLL_VIEW_HOST;
}

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
    pregameSecondsLeft: null,
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

// Mixed-play-mode session: an opening Trivia substage (completed) followed by the active
// Treasure Hunt substage — the ordered substage progress the participant should now see (#171).
const TREASURE_HUNT_BOARD = {
  liveSessionId: 'sess-1',
  teamId: 'team-1',
  teamDisplayName: 'Lantern Foxes',
  teamCode: 'LF-01',
  currentScore: 240,
  timer: {} as never,
  activeSubstage: {
    substageSnapshotId: 'sub-2',
    playMode: 'TreasureHunt' as const,
    title: 'The Cartographer’s Vault',
    totalActiveTargets: 5,
    resolvedTargets: 2,
    activeQuestionSequenceOrder: null,
    activeQuestionTimeLimitSeconds: null,
  },
  substages: [
    {
      substageSnapshotId: 'sub-1',
      title: 'Opening Trivia',
      sequenceOrder: 0,
      playMode: 'Trivia' as const,
      status: 'Completed' as const,
    },
    {
      substageSnapshotId: 'sub-2',
      title: 'The Cartographer’s Vault',
      sequenceOrder: 1,
      playMode: 'TreasureHunt' as const,
      status: 'Active' as const,
    },
  ],
  visibleClues: [
    { targetSnapshotId: 't1', clueText: 'Follow the north colonnade.', targetName: 'Brass Astrolabe', operativeClueId: null },
  ],
  activeTargets: [
    { targetSnapshotId: 't1', name: 'Brass Astrolabe', sequenceOrder: 0, latitude: 40.4319, longitude: -3.6883 },
  ],
};

// Single-substage trivia session: only the active name, no ordered chips.
const TRIVIA_BOARD = {
  ...TREASURE_HUNT_BOARD,
  activeSubstage: {
    substageSnapshotId: 'sub-1',
    playMode: 'Trivia' as const,
    title: 'Opening Trivia',
    totalActiveTargets: 0,
    resolvedTargets: 0,
    activeQuestionSequenceOrder: null,
    activeQuestionTimeLimitSeconds: null,
  },
  substages: [
    {
      substageSnapshotId: 'sub-1',
      title: 'Opening Trivia',
      sequenceOrder: 0,
      playMode: 'Trivia' as const,
      status: 'Active' as const,
    },
  ],
};

describe('LiveTeamSpace play-mode branch', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    primeTimer();
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'none' } });
  });

  test('TreasureHunt board renders the board and not the trivia stage', () => {
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });

    const renderer = renderSpace();
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('TREASURE HUNT');
    expect(texts).toContain('The Cartographer’s Vault');
    expect(texts).toContain('240');
    // target progress (resolved / total), a count — not coordinates
    expect(texts.join('')).toContain('2 / 5 targets');
    // The Map tab now renders the real Leaflet map (#156): the board's activeTargets reach a WebView.
    expect(renderer.root.findByProps({ testID: 'target-map-webview' })).toBeTruthy();
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

  test('TreasureHunt gives the board the viewport — no enclosing ScrollView, no fixed height', () => {
    // The board is a full-screen surface (sticky header, flex body, pinned strip) whose tab bodies
    // are ScrollViews. Boxing it at a fixed height inside the screen's own ScrollView nested two
    // same-direction scrollers and left a long clue list barely scrollable, so this branch renders
    // the board as the root: the only ScrollViews below it are its own tab bodies.
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });
    const renderer = renderSpace();

    const root = renderer.toJSON() as { type?: string } | null;
    expect(root?.type).not.toBe(SCROLL_VIEW_HOST);

    const scrollViews = renderer.root.findAll(isScrollView);
    // Exactly one tab body is mounted at a time, and MAP (the default) has no scroller of its own.
    expect(scrollViews).toHaveLength(0);

    const heights = renderer.root
      .findAll((n) => typeof n.type === 'string')
      .flatMap((n) => [n.props?.style].flat())
      .filter((s): s is { height?: unknown } => !!s && typeof s === 'object')
      .map((s) => s.height);
    expect(heights).not.toContain(640);
  });

  test('TreasureHunt clue list is the single scroller and keeps the substage progress + leave action', () => {
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });
    const renderer = renderSpace();

    // Substage progress (#171) rides the board's sticky header now that the board owns the screen.
    expect(allText(renderer.toJSON())).toContain('The Cartographer’s Vault');
    expect(renderer.root.findAll((n) => n.props?.accessibilityLabel === 'Leave team space').length)
      .toBeGreaterThan(0);

    // Switching to CLUES mounts exactly one scroller: the clue list itself, with nothing above it.
    // The board's segmented control renders the first three role="button" pressables, in
    // map/clues/teams order (the strip's leave control comes after them).
    const buttons = renderer.root.findAll(
      (n) => n.props?.accessibilityRole === 'button' && typeof n.props?.onPress === 'function',
    );
    act(() => {
      (buttons[1].props.onPress as () => void)();
    });

    expect(renderer.root.findAll(isScrollView)).toHaveLength(1);
  });

  test('a retained board suppresses the error banner on a failed re-fetch', () => {
    // A failed re-fetch leaves the last-good board intact — render it, no banner.
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: 'network-error' });

    const texts = allText(renderSpace().toJSON());

    expect(texts).toContain('TREASURE HUNT');
    expect(texts.join(' ')).not.toContain("Couldn't reach the live board");
  });
});

function substageChips(renderer: ReturnType<typeof create>) {
  // Host nodes only (string type), matched by the chip's `Substage N:` label so the
  // container's "Substage progress" label and RN composite wrappers don't double-count.
  return renderer.root.findAll(
    (n) =>
      typeof n.type === 'string' &&
      typeof n.props?.accessibilityLabel === 'string' &&
      /^Substage \d+:/.test(n.props.accessibilityLabel as string),
  );
}

describe('LiveTeamSpace substage progress (#171)', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    primeTimer();
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'none' } });
  });

  test('renders the active substage name and an ordered chip per substage with play-mode labels', () => {
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });
    const renderer = renderSpace();
    const texts = allText(renderer.toJSON());

    // Active substage name at the top of the play UI.
    expect(texts).toContain('The Cartographer’s Vault');
    // Both substages render with their play-mode labels.
    expect(texts).toContain('Trivia');
    expect(texts).toContain('Treasure Hunt');
    // A chip per substage in order.
    expect(substageChips(renderer)).toHaveLength(2);
  });

  test('visually distinguishes the active substage from completed ones', () => {
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });
    const renderer = renderSpace();

    const selected = substageChips(renderer).filter(
      (n) => (n.props.accessibilityState as { selected?: boolean })?.selected === true,
    );
    expect(selected).toHaveLength(1);
    expect(selected[0].props.accessibilityLabel as string).toContain('active');
    expect(selected[0].props.accessibilityLabel as string).toContain('Treasure Hunt');
  });

  test('single-substage session renders the name but no chips', () => {
    mockUseTeamBoard.mockReturnValue({ board: TRIVIA_BOARD, isLoading: false, error: null });
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'waiting' } });
    const renderer = renderSpace();
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('Opening Trivia');
    expect(substageChips(renderer)).toHaveLength(0);
  });

  test('an advancement push whose active substage changed updates the top name and progress', () => {
    // Before: active on the opening Trivia substage. Mirrors an onTeamBoardUpdated push by swapping
    // the hook's returned board (which the live view re-reads on the next render).
    const BEFORE = {
      ...TREASURE_HUNT_BOARD,
      activeSubstage: { ...TRIVIA_BOARD.activeSubstage },
      substages: [
        { ...TREASURE_HUNT_BOARD.substages[0], status: 'Active' as const },
        { ...TREASURE_HUNT_BOARD.substages[1], status: 'Upcoming' as const },
      ],
    };
    mockUseTeamBoard.mockReturnValue({ board: BEFORE, isLoading: false, error: null });
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', isQuestionClosed: false, view: { kind: 'waiting' } });

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

    expect(allText(renderer!.toJSON())).toContain('Opening Trivia');

    // After advancement: the push moves the pointer to the Treasure Hunt substage.
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null });
    act(() => {
      renderer!.update(
        React.createElement(LiveTeamSpace, {
          outcome: OUTCOME,
          onLeave: jest.fn(),
          client: CLIENT,
          reconnectNonce: 0,
          referenceTeamId: 'team-1',
        }),
      );
    });

    const after = allText(renderer!.toJSON());
    expect(after).toContain('The Cartographer’s Vault');
    // The now-completed opening substage chip reflects the advancement.
    const completed = substageChips(renderer!).filter(
      (n) => (n.props.accessibilityLabel as string).includes('completed'),
    );
    expect(completed).toHaveLength(1);
  });
});
