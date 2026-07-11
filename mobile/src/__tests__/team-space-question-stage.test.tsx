import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';

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

const mockUseSessionTimer = useSessionTimer as jest.MockedFunction<typeof useSessionTimer>;
const mockUseActiveQuestion = useActiveQuestion as jest.MockedFunction<typeof useActiveQuestion>;

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
};

type TreeNode = {
  props?: Record<string, unknown>;
  children?: (TreeNode | string)[] | null;
};

function allText(node: unknown): string[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string') return [node];
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
  });
}

describe('LiveTeamSpace question stage wiring', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    primeTimer();
  });

  test('renders active question stage instead of standalone timer and join panel', () => {
    mockUseActiveQuestion.mockReturnValue({
      sessionState: 'Active',
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
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', view: { kind: 'waiting' } });

    const texts = allText(renderSpace().toJSON());

    expect(texts).toContain('Active');
    expect(texts).toContain('SCORE');
    expect(texts.join(' ')).toContain('Waiting for the next question');
  });

  test('renders retained question while Paused instead of an empty state', () => {
    mockUseActiveQuestion.mockReturnValue({
      sessionState: 'Paused',
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

  test('opens teams sheet from the persistent footer', () => {
    mockUseActiveQuestion.mockReturnValue({ sessionState: 'Active', view: { kind: 'none' } });
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
