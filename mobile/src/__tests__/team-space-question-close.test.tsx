// Integration drive for the HU-M3 close flow: real useSessionTimer + useActiveQuestion wired through
// LiveTeamSpace, a fake hub client for the pushes, and a mocked timer-snapshot fetch for the reconcile.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';

const mockGetSnapshot = jest.fn();
const mockSubmit = jest.fn();

jest.mock('@/lib/api/sessions', () => {
  const actual = jest.requireActual<typeof import('@/lib/api/sessions')>('@/lib/api/sessions');
  return {
    ...actual,
    getParticipantTimerSnapshot: (...args: unknown[]) => mockGetSnapshot(...args),
    submitTriviaAnswer: (...args: unknown[]) => mockSubmit(...args),
    // Keep the team board null (never-resolving) so this trivia close-flow drive is unaffected.
    getParticipantTeamBoard: () => new Promise(() => {}),
  };
});

jest.mock('expo-router', () => ({
  useLocalSearchParams: jest.fn(() => ({})),
  useRouter: jest.fn(() => ({ push: jest.fn(), replace: jest.fn() })),
}));

jest.mock('expo-haptics', () => ({
  impactAsync: jest.fn(),
  notificationAsync: jest.fn(),
  ImpactFeedbackStyle: { Light: 'light' },
  NotificationFeedbackType: { Success: 'success', Error: 'error' },
}));

jest.mock('@/lib/auth/use-auth', () => ({
  useAuth: jest.fn(() => ({ profile: { displayName: 'Nova' } })),
}));

// --- Fake hub client ---

type Handler = (n: any) => void;
let activatedHandlers: Set<Handler>;
let closedHandlers: Set<Handler>;

function makeClient() {
  activatedHandlers = new Set();
  closedHandlers = new Set();
  return {
    connection: {} as never,
    start: jest.fn(),
    stop: jest.fn(),
    reconnect: jest.fn(),
    onTimerUpdated: jest.fn(() => () => {}),
    onStateChanged: jest.fn(() => () => {}),
    onQuestionActivated(cb: Handler) {
      activatedHandlers.add(cb);
      return () => activatedHandlers.delete(cb);
    },
    onQuestionClosed(cb: Handler) {
      closedHandlers.add(cb);
      return () => closedHandlers.delete(cb);
    },
    onSubstageAdvanced: jest.fn(() => () => {}),
    onTeamBoardUpdated: jest.fn(() => () => {}),
  };
}

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

const BASE_SNAPSHOT = {
  liveSessionId: 'sess-1',
  teamId: 'team-1',
  sessionState: 'Active',
  totalSeconds: 300,
  remainingSeconds: 180,
  timerStatus: 'Running',
  isAdvancing: true,
  isExpired: false,
  observedAt: '2026-07-11T10:00:00Z',
  advancingSince: '2026-07-11T09:55:00Z',
  expiredAt: null,
  activeQuestion: null as unknown,
};

const ACTIVATED = {
  liveSessionId: 'sess-1',
  questionIndex: 2,
  sequenceOrder: 3,
  prompt: 'Which lantern is lit?',
  options: ['North', 'South', 'East'],
  timeLimitSeconds: 45,
  activatedAt: '2026-07-11T10:01:00Z',
  triviaSubstageSnapshotId: 'substage-abc',
};

const CLOSED_MATCH = {
  liveSessionId: 'sess-1',
  questionIndex: 2,
  closedAt: '2026-07-11T10:02:00Z',
  wasExpiredByTimer: true,
};

const NEXT_QUESTION_SNAPSHOT = {
  ...BASE_SNAPSHOT,
  activeQuestion: {
    liveSessionId: 'sess-1',
    questionIndex: 3,
    sequenceOrder: 4,
    prompt: 'Which key fits the archive lock?',
    options: ['Brass', 'Iron'],
    timeLimitSeconds: 45,
    remainingSeconds: 44,
    activatedAt: '2026-07-11T10:03:00Z',
    triviaSubstageSnapshotId: 'substage-abc',
  },
};

type TreeNode = { props?: Record<string, unknown>; children?: (TreeNode | string)[] | null };

function allText(node: unknown): string[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  const n = node as TreeNode;
  return allText(n.children ?? []);
}

function renderSpace(client: ReturnType<typeof makeClient>) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(LiveTeamSpace, {
        outcome: OUTCOME,
        onLeave: jest.fn(),
        client,
        reconnectNonce: 0,
        referenceTeamId: 'team-1',
      }),
    );
  });
  return renderer!;
}

async function flush() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

describe('LiveTeamSpace close flow (integration)', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  async function mountAndActivate(client: ReturnType<typeof makeClient>) {
    mockGetSnapshot.mockResolvedValueOnce({ ...BASE_SNAPSHOT, activeQuestion: null });
    const renderer = renderSpace(client);
    await flush(); // resolve the reconnect snapshot fetch

    act(() => {
      activatedHandlers.forEach(cb => cb(ACTIVATED));
    });
    expect(allText(renderer.toJSON())).toContain('Which lantern is lit?');
    return renderer;
  }

  test('close locks the question, re-fetches, and advances to the next question', async () => {
    const client = makeClient();
    const renderer = await mountAndActivate(client);

    mockGetSnapshot.mockResolvedValueOnce(NEXT_QUESTION_SNAPSHOT);

    // Close arrives for the displayed question: it locks (does not vanish) and shows the affordance.
    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_MATCH));
    });
    let texts = allText(renderer.toJSON());
    expect(texts).toContain('Which lantern is lit?');
    expect(texts.join(' ')).toContain('Question closed — waiting for the next');
    expect(texts).not.toContain('Submit answer');
    expect(mockGetSnapshot).toHaveBeenCalledTimes(2); // reconnect + close re-sync

    // Re-fetch resolves to a new question → advances and clears the lock.
    await flush();
    texts = allText(renderer.toJSON());
    expect(texts).toContain('Which key fits the archive lock?');
    expect(texts).not.toContain('Which lantern is lit?');
    expect(texts.join(' ')).not.toContain('Question closed — waiting for the next');
  });

  test('close with no next question on a live session reconciles to waiting', async () => {
    const client = makeClient();
    const renderer = await mountAndActivate(client);

    mockGetSnapshot.mockResolvedValueOnce({ ...BASE_SNAPSHOT, activeQuestion: null, sessionState: 'Active' });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_MATCH));
    });
    await flush();

    const texts = allText(renderer.toJSON());
    expect(texts.join(' ')).toContain('Waiting for the next question');
    expect(texts).not.toContain('Which lantern is lit?');
  });

  test('close into a terminal session state shows the session-closed panel', async () => {
    const client = makeClient();
    const renderer = await mountAndActivate(client);

    mockGetSnapshot.mockResolvedValueOnce({
      ...BASE_SNAPSHOT,
      activeQuestion: null,
      sessionState: 'Finished',
    });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_MATCH));
    });
    await flush();

    const texts = allText(renderer.toJSON());
    expect(texts.join(' ')).toContain('Session closed');
    expect(texts).not.toContain('Which lantern is lit?');
  });

  test('a stale close for a superseded question is ignored and does not re-fetch', async () => {
    const client = makeClient();
    const renderer = await mountAndActivate(client);

    act(() => {
      // Displayed question is index 2; a close for index 1 is stale.
      closedHandlers.forEach(cb => cb({ ...CLOSED_MATCH, questionIndex: 1 }));
    });

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('Which lantern is lit?');
    expect(texts.join(' ')).not.toContain('Question closed — waiting for the next');
    expect(mockGetSnapshot).toHaveBeenCalledTimes(1); // only the reconnect fetch; no re-sync
  });
});
