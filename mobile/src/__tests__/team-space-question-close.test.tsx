// Integration drive for the HU-M3 close flow: real useSessionTimer + useActiveQuestion wired through
// LiveTeamSpace, a fake hub client for the pushes, and a mocked timer-snapshot fetch for the reconcile.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import type { TriviaTeamQuestionResultDto } from '@/lib/realtime/trivia-types';

const mockGetSnapshot = jest.fn();
const mockSubmit = jest.fn();
// Default resolved so the hook's reveal fetch never crashes when a test forgets to mock it.
const mockGetResult = jest.fn(
  (_liveSessionId: string, _sequenceOrder: number): Promise<TriviaTeamQuestionResultDto> =>
    Promise.resolve({
      selectedOptionSequenceOrder: null,
      isCorrect: null,
      scoreValue: 0,
      correctOptionSequenceOrder: 1,
      explanation: null,
    }),
);

jest.mock('@/lib/api/sessions', () => {
  const actual = jest.requireActual<typeof import('@/lib/api/sessions')>('@/lib/api/sessions');
  return {
    ...actual,
    getParticipantTimerSnapshot: (...args: unknown[]) => mockGetSnapshot.apply(null, args),
    submitTriviaAnswer: (...args: unknown[]) => mockSubmit.apply(null, args),
    getTriviaTeamQuestionResult: (...args: unknown[]) => mockGetResult.apply(null, args),
    // Keep the team board null (never-resolving) so this trivia close-flow drive is unaffected.
    getParticipantTeamBoard: () => new Promise(() => {}),
    // Same for the ranking: the terminal-state case renders it, and a real fetch here would resolve
    // into an error panel on its own schedule. Never-resolving keeps that surface at its empty state.
    getRanking: () => new Promise(() => {}),
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
let substageAdvancedHandlers: Set<Handler>;

function makeClient() {
  activatedHandlers = new Set();
  closedHandlers = new Set();
  substageAdvancedHandlers = new Set();
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
    onSubstageAdvanced(cb: Handler) {
      substageAdvancedHandlers.add(cb);
      return () => substageAdvancedHandlers.delete(cb);
    },
    onTeamBoardUpdated: jest.fn(() => () => {}),
    onSubstageRankingRevealStarted: jest.fn(() => () => {}),
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

// A substage-to-substage advance on a still-live session (non-null toSubstageId). Fired by the
// backend after the reveal window closes, when the just-closed question was the substage's last.
const SUBSTAGE_ADVANCED_LIVE = {
  liveSessionId: 'sess-1',
  fromSubstageId: 'substage-abc',
  fromPlayMode: 'Trivia',
  toSubstageId: 'substage-def',
  advancedAt: '2026-07-11T10:02:05Z',
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
    expect(texts.join(' ')).toContain('Pregunta cerrada — esperando la siguiente');
    expect(texts).not.toContain('Enviar respuesta');
    expect(mockGetSnapshot).toHaveBeenCalledTimes(2); // reconnect + close re-sync

    // Re-fetch resolves to a new question → advances and clears the lock.
    await flush();
    texts = allText(renderer.toJSON());
    expect(texts).toContain('Which key fits the archive lock?');
    expect(texts).not.toContain('Which lantern is lit?');
    expect(texts.join(' ')).not.toContain('Pregunta cerrada — esperando la siguiente');
  });

  test('close with no next question holds the reveal until the substage advances', async () => {
    const client = makeClient();
    const renderer = await mountAndActivate(client);

    // Reveal window (HU-35): the backend holds the next activation, so the close re-sync reports no
    // active question. The just-closed question must stay on screen, NOT drop to waiting.
    mockGetSnapshot.mockResolvedValueOnce({ ...BASE_SNAPSHOT, activeQuestion: null, sessionState: 'Active' });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_MATCH));
    });
    await flush();

    let texts = allText(renderer.toJSON());
    expect(texts).toContain('Which lantern is lit?');
    expect(texts.join(' ')).not.toContain('Esperando la siguiente pregunta');

    // Once the reveal window closes the substage advances (its last question just closed). That push
    // — not the close re-sync — drives the transition to the waiting state.
    act(() => {
      substageAdvancedHandlers.forEach(cb => cb(SUBSTAGE_ADVANCED_LIVE));
    });

    texts = allText(renderer.toJSON());
    expect(texts.join(' ')).toContain('Esperando la siguiente pregunta');
    expect(texts).not.toContain('Which lantern is lit?');
  });

  // A finished session lands on the final ranking rather than the session-closed panel: expiry (D-4)
  // sends no reveal event, so the terminal state alone has to carry the participant there.
  test('close into a terminal session state shows the final ranking', async () => {
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
    expect(texts.join(' ')).toContain('Clasificación final');
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
    expect(texts.join(' ')).not.toContain('Pregunta cerrada — esperando la siguiente');
    expect(mockGetSnapshot).toHaveBeenCalledTimes(1); // only the reconnect fetch; no re-sync
  });

  // ── HU-M4 reveal tests ─────────────────────────────────────────────────────

  const CLOSED_REVEAL = {
    ...CLOSED_MATCH,
    correctOptionSequenceOrder: 2, // South
    explanation: 'The south lantern is always lit first.',
  };

  const ECHO_SNAPSHOT = {
    ...BASE_SNAPSHOT,
    activeQuestion: {
      liveSessionId: 'sess-1',
      questionIndex: 2,
      sequenceOrder: 3,
      prompt: 'Which lantern is lit?',
      options: ['North', 'South', 'East'],
      timeLimitSeconds: 45,
      remainingSeconds: 0,
      activatedAt: '2026-07-11T10:01:00Z',
      triviaSubstageSnapshotId: 'substage-abc',
    },
  };

  async function mountAndActivateReveal(client: ReturnType<typeof makeClient>) {
    mockGetSnapshot.mockResolvedValueOnce({ ...BASE_SNAPSHOT, activeQuestion: null });
    const renderer = renderSpace(client);
    await flush();

    act(() => {
      activatedHandlers.forEach(cb => cb(ACTIVATED));
    });
    return renderer;
  }

  test('reveal highlights the correct option and shows the explanation', async () => {
    const client = makeClient();
    const renderer = await mountAndActivateReveal(client);

    mockGetSnapshot.mockResolvedValueOnce(ECHO_SNAPSHOT);
    mockGetResult.mockResolvedValueOnce({
      selectedOptionSequenceOrder: 2,
      isCorrect: true,
      scoreValue: 20,
      correctOptionSequenceOrder: 2,
      explanation: 'The south lantern is always lit first.',
    });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_REVEAL));
    });
    await flush();

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('South');
    expect(texts).toContain('CORRECTA');
    expect(texts).toContain('¿POR QUÉ?');
    expect(texts).toContain('The south lantern is always lit first.');
  });

  test('reveal omits the explanation card when explanation is null', async () => {
    const client = makeClient();
    const renderer = await mountAndActivateReveal(client);

    mockGetSnapshot.mockResolvedValueOnce(ECHO_SNAPSHOT);
    mockGetResult.mockResolvedValueOnce({
      selectedOptionSequenceOrder: 2,
      isCorrect: true,
      scoreValue: 20,
      correctOptionSequenceOrder: 2,
      explanation: null,
    });

    act(() => {
      closedHandlers.forEach(cb =>
        cb({ ...CLOSED_REVEAL, explanation: null }),
      );
    });
    await flush();

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('South');
    expect(texts).toContain('CORRECTA');
    expect(texts).not.toContain('¿POR QUÉ?');
  });

  test('reveal shows correct outcome chip and points when team answered correctly', async () => {
    const client = makeClient();
    const renderer = await mountAndActivateReveal(client);

    mockGetSnapshot.mockResolvedValueOnce(ECHO_SNAPSHOT);
    mockGetResult.mockResolvedValueOnce({
      selectedOptionSequenceOrder: 2,
      isCorrect: true,
      scoreValue: 20,
      correctOptionSequenceOrder: 2,
      explanation: null,
    });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_REVEAL));
    });
    await flush();

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('Correcta');
    expect(texts).toContain('+20');
  });

  test('reveal shows incorrect outcome chip when team answered wrong', async () => {
    const client = makeClient();
    const renderer = await mountAndActivateReveal(client);

    mockGetSnapshot.mockResolvedValueOnce(ECHO_SNAPSHOT);
    mockGetResult.mockResolvedValueOnce({
      selectedOptionSequenceOrder: 1, // North (wrong)
      isCorrect: false,
      scoreValue: 0,
      correctOptionSequenceOrder: 2,
      explanation: null,
    });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_REVEAL));
    });
    await flush();

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('Incorrecta');
    expect(texts).toContain('TU RESPUESTA');
    expect(texts).toContain('North');
  });

  test('reveal shows no-answer outcome chip when team never answered', async () => {
    const client = makeClient();
    const renderer = await mountAndActivateReveal(client);

    mockGetSnapshot.mockResolvedValueOnce(ECHO_SNAPSHOT);
    mockGetResult.mockResolvedValueOnce({
      selectedOptionSequenceOrder: null,
      isCorrect: null,
      scoreValue: 0,
      correctOptionSequenceOrder: 2,
      explanation: null,
    });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_REVEAL));
    });
    await flush();

    const texts = allText(renderer.toJSON());
    expect(texts).toContain('Sin respuesta');
    expect(texts).not.toContain('TU RESPUESTA');
  });

  test('controls stay locked during reveal and transition to next question still works', async () => {
    const client = makeClient();
    const renderer = await mountAndActivateReveal(client);

    mockGetSnapshot.mockResolvedValueOnce(ECHO_SNAPSHOT);
    mockGetResult.mockResolvedValueOnce({
      selectedOptionSequenceOrder: 2,
      isCorrect: true,
      scoreValue: 20,
      correctOptionSequenceOrder: 2,
      explanation: null,
    });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED_REVEAL));
    });
    await flush();

    // Controls are locked — no submit button.
    let texts = allText(renderer.toJSON());
    expect(texts).not.toContain('Enviar respuesta');
    expect(texts).toContain('South');
    expect(texts).toContain('CORRECTA');

    // Next question arrives via activate — clears reveal.
    mockGetSnapshot.mockResolvedValueOnce(NEXT_QUESTION_SNAPSHOT);
    act(() => {
      activatedHandlers.forEach(cb => cb(NEXT_QUESTION_SNAPSHOT.activeQuestion));
    });
    await flush();

    texts = allText(renderer.toJSON());
    expect(texts).toContain('Which key fits the archive lock?');
    expect(texts).not.toContain('South');
    expect(texts).not.toContain('CORRECTA');
  });
});
