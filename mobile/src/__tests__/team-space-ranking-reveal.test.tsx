// Guards the D-3 substage ranking reveal at the point it has to win: the branch in LiveTeamSpace.
// Drives the real `useRankingReveal` through a fake hub so the event, its server-measured window and
// the terminal/expiry paths are exercised as the screen sees them.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { LiveTeamSpace } from '@/app/(app)/team-space';
import { useActiveQuestion } from '@/lib/realtime/use-active-question';
import { useRanking } from '@/lib/realtime/use-ranking';
import { useSessionTimer } from '@/lib/realtime/use-session-timer';
import { useTeamBoard } from '@/lib/realtime/use-team-board';
import { useRankingReveal } from '@/lib/realtime/use-ranking-reveal';
import type {
  ActiveRankingRevealSnapshotDto,
  RevealSnapshotReconciliation,
  SubstageRankingRevealStartedNotificationDto,
} from '@/lib/realtime/ranking-reveal-types';
import type { SubstageAdvancedNotificationDto } from '@/lib/realtime/trivia-types';
import type { SessionStateChangedNotificationDto } from '@/lib/realtime/sessions-hub-types';
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
const REVEAL_MS = 10_000;

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

// The mixed-mode case the reveal exists for: a cleared treasure hunt whose full-screen board would
// otherwise swallow it.
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
    resolvedTargets: 5,
    activeQuestionSequenceOrder: null,
    activeQuestionTimeLimitSeconds: null,
  },
  substages: [
    {
      substageSnapshotId: 'sub-2',
      title: 'The Cartographer’s Vault',
      sequenceOrder: 0,
      playMode: 'TreasureHunt' as const,
      status: 'Active' as const,
    },
  ],
  visibleClues: [
    {
      targetSnapshotId: 't1',
      clueText: 'Follow the north colonnade.',
      targetName: 'Brass Astrolabe',
      operativeClueId: null,
    },
  ],
  activeTargets: [
    { targetSnapshotId: 't1', name: 'Brass Astrolabe', sequenceOrder: 0, latitude: 40.4319, longitude: -3.6883 },
  ],
};

const RANKING: RankingSnapshotDto = {
  liveSessionId: 'sess-1',
  generatedAt: '2026-07-11T10:05:00Z',
  calculationVersion: 1,
  rows: [
    { teamId: REFERENCE_TEAM_ID, teamDisplayName: 'Lantern Foxes', position: 1, totalScore: 240, resolutionTime: null },
    { teamId: 'team-2', teamDisplayName: 'Ember Owls', position: 2, totalScore: 180, resolutionTime: null },
  ],
};

function reveal(
  overrides: Partial<SubstageRankingRevealStartedNotificationDto> = {},
): SubstageRankingRevealStartedNotificationDto {
  return {
    liveSessionId: 'sess-1',
    substageSnapshotId: 'sub-2',
    playMode: 'TreasureHunt',
    emittedAt: '2026-07-11T10:05:00.000Z',
    // Window is read as `revealUntil - emittedAt`, so this is 10s regardless of the device clock.
    revealUntil: '2026-07-11T10:05:10.000Z',
    isTerminal: false,
    ...overrides,
  };
}

type Handler = (n: SubstageRankingRevealStartedNotificationDto) => void;
type AdvancedHandler = (n: SubstageAdvancedNotificationDto) => void;
type StateChangedHandler = (n: SessionStateChangedNotificationDto) => void;
let revealHandlers: Set<Handler>;
let advancedHandlers: Set<AdvancedHandler>;
let stateChangedHandlers: Set<StateChangedHandler>;

function makeClient() {
  revealHandlers = new Set();
  advancedHandlers = new Set();
  stateChangedHandlers = new Set();
  return {
    connection: {} as never,
    start: jest.fn(),
    stop: jest.fn(),
    reconnect: jest.fn(),
    onTimerUpdated: jest.fn(() => () => {}),
    onStateChanged(cb: StateChangedHandler) {
      stateChangedHandlers.add(cb);
      return () => stateChangedHandlers.delete(cb);
    },
    onQuestionActivated: jest.fn(() => () => {}),
    onQuestionClosed: jest.fn(() => () => {}),
    onSubstageAdvanced(cb: AdvancedHandler) {
      advancedHandlers.add(cb);
      return () => advancedHandlers.delete(cb);
    },
    onTeamBoardUpdated: jest.fn(() => () => {}),
    onSubstageRankingRevealStarted(cb: Handler) {
      revealHandlers.add(cb);
      return () => revealHandlers.delete(cb);
    },
  };
}

type TreeNode = { children?: (TreeNode | string)[] | null };

function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  return allText((node as TreeNode).children ?? []);
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
        referenceTeamId: REFERENCE_TEAM_ID,
      }),
    );
  });
  return renderer!;
}

function text(renderer: ReturnType<typeof create>): string {
  return allText(renderer.toJSON()).join(' ');
}

function primeSessionState(sessionState: string) {
  mockUseActiveQuestion.mockReturnValue({
    sessionState,
    isQuestionClosed: false,
    view: sessionState === 'Active' ? { kind: 'waiting' } : { kind: 'closed' },
  });
}

function fire(notification: SubstageRankingRevealStartedNotificationDto) {
  act(() => {
    revealHandlers.forEach(cb => cb(notification));
  });
}

function fireAdvanced(overrides: Partial<SubstageAdvancedNotificationDto> = {}) {
  const notification: SubstageAdvancedNotificationDto = {
    liveSessionId: 'sess-1',
    fromSubstageId: 'sub-2',
    fromPlayMode: 'TreasureHunt',
    toSubstageId: 'sub-3',
    advancedAt: '2026-07-11T10:05:10.000Z',
    ...overrides,
  };
  act(() => {
    advancedHandlers.forEach(cb => cb(notification));
  });
}

function fireStateChanged(currentState: string, overrides: Partial<SessionStateChangedNotificationDto> = {}) {
  const notification: SessionStateChangedNotificationDto = {
    liveSessionId: 'sess-1',
    previousState: 'Active',
    currentState,
    changedAt: '2026-07-11T10:05:05.000Z',
    ...overrides,
  };
  act(() => {
    stateChangedHandlers.forEach(cb => cb(notification));
  });
}

// A snapshot seed: mimics useSessionTimer surfacing a reveal from the reconnect snapshot fetch, paired
// with the response's observedAt so the reveal orders against live events by server time.
function primeSnapshotReveal(
  reveal: ActiveRankingRevealSnapshotDto | null,
  snapshotVersion = 1,
) {
  mockUseSessionTimer.mockReturnValue({
    display: '02:30',
    missionDisplay: null,
    activeQuestion: null,
    revealReconciliation: {
      reveal,
      observedAt: reveal?.emittedAt ?? '2026-07-11T10:05:00.000Z',
      version: snapshotVersion,
    },
    sessionState: 'Active',
    pregameSecondsLeft: null,
    snapshotVersion,
  } as never);
}

describe('LiveTeamSpace substage ranking reveal', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    mockUseSessionTimer.mockReturnValue({
      display: '02:30',
      missionDisplay: null,
      activeQuestion: null,
      revealReconciliation: null,
      sessionState: 'Active',
      pregameSecondsLeft: null,
      snapshotVersion: 0,
    } as never);
    mockUseTeamBoard.mockReturnValue({ board: TREASURE_HUNT_BOARD, isLoading: false, error: null } as never);
    mockUseRanking.mockReturnValue({ snapshot: RANKING, isLoading: false, error: null, refetch: jest.fn() });
    primeSessionState('Active');
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  test('a reveal push takes the screen from the treasure-hunt board', () => {
    const renderer = renderSpace(makeClient());
    expect(text(renderer)).toContain('ESCANEAR TARGET');

    fire(reveal());

    const shown = text(renderer);
    expect(shown).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(shown).toContain('Ember Owls');
    expect(shown).not.toContain('ESCANEAR TARGET');
  });

  test('the reveal holds past the local window until the authoritative advance arrives', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal());
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    // The backend is the sole authority: the local ten seconds elapsing is not a release. Waiting
    // through a worker-tick / delivery gap must never flash the play surface.
    act(() => {
      jest.advanceTimersByTime(REVEAL_MS * 2);
    });
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(text(renderer)).not.toContain('ESCANEAR TARGET');

    // Only the backend's SubstageAdvanced returns to gameplay.
    fireAdvanced();
    expect(text(renderer)).toContain('ESCANEAR TARGET');
    expect(text(renderer)).not.toContain('CLASIFICACIÓN DE LA RONDA');
  });

  test('an authoritative advance releases the reveal before its local window would', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal());
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    // The backend advanced (toSubstageId set) well before the 10s window measured from receipt.
    act(() => {
      jest.advanceTimersByTime(REVEAL_MS / 2);
    });
    fireAdvanced();

    expect(text(renderer)).toContain('ESCANEAR TARGET');
    expect(text(renderer)).not.toContain('CLASIFICACIÓN DE LA RONDA');
  });

  test('a stale advance off another substage leaves the reveal up', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal());
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    // A duplicate/stale advance whose fromSubstageId is not the revealed substage must not drop it.
    fireAdvanced({ fromSubstageId: 'sub-stale' });

    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(text(renderer)).not.toContain('ESCANEAR TARGET');
  });

  test('pausing past the local window keeps the ranking, and resume alone does not dismiss it', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal());
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    fireStateChanged('Paused', { previousState: 'Active' });
    act(() => {
      jest.advanceTimersByTime(REVEAL_MS * 3);
    });
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    // Resuming makes no reveal transition — the ranking closes only on the later backend advancement.
    fireStateChanged('Active', { previousState: 'Paused' });
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(text(renderer)).not.toContain('ESCANEAR TARGET');

    fireAdvanced();
    expect(text(renderer)).toContain('ESCANEAR TARGET');
  });

  test('a nonterminal reveal that finishes on the deadline retains the ranking as the final screen', () => {
    const renderer = renderSpace(makeClient());
    // Deadline finish (D-6): a nonterminal reveal, no SubstageAdvanced to release it — Finished lands.
    fire(reveal({ isTerminal: false }));
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    fireStateChanged('Finished', { previousState: 'Active' });
    act(() => {
      jest.advanceTimersByTime(REVEAL_MS * 3);
    });

    // The ranking stays up (the hook holds it); the play surface never returns.
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(text(renderer)).not.toContain('ESCANEAR TARGET');
  });

  test('a cancellation mid-reveal drops the ranking so the host notice shows through', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal());
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    fireStateChanged('Cancelled', { previousState: 'Active' });

    // The reveal is released; the cancellation surface (not the standings) owns the screen.
    expect(text(renderer)).not.toContain('CLASIFICACIÓN DE LA RONDA');
  });

  test('a reconnect snapshot seed restores the reveal with no start push', () => {
    // useSessionTimer surfaces an active reveal from the reconnect snapshot fetch; the hook seeds from
    // it without ever receiving a SubstageRankingRevealStarted push.
    primeSnapshotReveal({
      substageSnapshotId: 'sub-2',
      playMode: 'TreasureHunt',
      revealUntil: '2026-07-11T10:05:10.000Z',
      isTerminal: false,
      emittedAt: '2026-07-11T10:05:00.000Z',
    });

    const renderer = renderSpace(makeClient());

    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(text(renderer)).not.toContain('ESCANEAR TARGET');

    // Still authoritative: the later advance for that substage closes the restored reveal.
    fireAdvanced();
    expect(text(renderer)).toContain('ESCANEAR TARGET');
  });

  test('the terminal advance (null toSubstageId) does not release a terminal reveal', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal({ isTerminal: true }));
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    // The finish emits SubstageAdvanced with a null toSubstageId; the ranking must keep holding so
    // the play surface never flashes in the gap before Finished renders it.
    fireAdvanced({ toSubstageId: null });
    act(() => {
      jest.advanceTimersByTime(REVEAL_MS * 3);
    });

    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(text(renderer)).not.toContain('ESCANEAR TARGET');
  });

  test('an advance for another session leaves the reveal up', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal());
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    fireAdvanced({ liveSessionId: 'sess-other' });

    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');
    expect(text(renderer)).not.toContain('ESCANEAR TARGET');
  });

  test('a terminal reveal holds past the window so the finish never flashes the play surface', () => {
    const renderer = renderSpace(makeClient());
    fire(reveal({ isTerminal: true }));

    act(() => {
      jest.advanceTimersByTime(REVEAL_MS * 3);
    });
    expect(text(renderer)).toContain('CLASIFICACIÓN DE LA RONDA');

    // The mission finishes on this reveal: the same ranking stays up, now as the final one.
    primeSessionState('Finished');
    act(() => {
      renderer.update(
        React.createElement(LiveTeamSpace, {
          outcome: OUTCOME,
          onLeave: jest.fn(),
          client: makeClient(),
          reconnectNonce: 0,
          referenceTeamId: REFERENCE_TEAM_ID,
        }),
      );
    });
    expect(text(renderer)).toContain('MISIÓN COMPLETADA');
  });

  test('a finished session shows the final ranking with no reveal push at all (D-4 expiry)', () => {
    primeSessionState('Finished');
    const renderer = renderSpace(makeClient());

    const shown = text(renderer);
    expect(shown).toContain('MISIÓN COMPLETADA');
    expect(shown).toContain('Clasificación final');
    expect(shown).toContain('Lantern Foxes');
    expect(shown).not.toContain('ESCANEAR TARGET');
  });

  // Verified against the real backend: a session that expires before any team scores returns the
  // well-known empty snapshot, so the finished screen must not promise standings "once the round
  // begins" — the round is over.
  test('a finished session with an empty ranking says nobody scored, not that standings are coming', () => {
    mockUseRanking.mockReturnValue({
      snapshot: { ...RANKING, rows: [] },
      isLoading: false,
      error: null,
      refetch: jest.fn(),
    });
    primeSessionState('Finished');
    const renderer = renderSpace(makeClient());

    const shown = text(renderer);
    expect(shown).toContain('MISIÓN COMPLETADA');
    expect(shown).toContain('Ningún equipo puntuó antes de que terminara la misión.');
    expect(shown).not.toContain('La clasificación aparecerá cuando comience la ronda.');
  });

  test('a cancelled session keeps the host notice instead of dressing it up with standings', () => {
    primeSessionState('Cancelled');
    const renderer = renderSpace(makeClient());

    expect(text(renderer)).not.toContain('MISIÓN COMPLETADA');
  });

  test('a reveal for another session is ignored', () => {
    const renderer = renderSpace(makeClient());

    fire(reveal({ liveSessionId: 'sess-other' }));

    expect(text(renderer)).toContain('ESCANEAR TARGET');
    expect(text(renderer)).not.toContain('CLASIFICACIÓN DE LA RONDA');
  });
});

// Deterministic race coverage for the ordered transition model in useRankingReveal, driven directly
// through the fake hub so snapshot/live orderings are exercised without timers or probabilistic delivery.
describe('useRankingReveal ordering', () => {
  const EARLY = '2026-07-11T10:05:00.000Z';
  const MID = '2026-07-11T10:05:05.000Z';
  const LATE = '2026-07-11T10:05:10.000Z';

  function reconciliation(
    reveal: ActiveRankingRevealSnapshotDto | null,
    observedAt: string,
    version: number,
  ): RevealSnapshotReconciliation {
    return { reveal, observedAt, version };
  }

  function revealSnap(
    overrides: Partial<ActiveRankingRevealSnapshotDto> = {},
  ): ActiveRankingRevealSnapshotDto {
    return {
      substageSnapshotId: 'sub-2',
      playMode: 'TreasureHunt',
      revealUntil: LATE,
      isTerminal: false,
      emittedAt: EARLY,
      ...overrides,
    };
  }

  function renderReveal(
    client: ReturnType<typeof makeClient>,
    initialReconciliation: RevealSnapshotReconciliation | null,
    liveSessionId = 'sess-1',
  ) {
    let result: { isRevealing: boolean } | null = null;
    let props = { client, liveSessionId, reconciliation: initialReconciliation };
    function Harness() {
      result = useRankingReveal(props as never);
      return null;
    }
    let renderer!: ReturnType<typeof create>;
    act(() => {
      renderer = create(React.createElement(Harness));
    });
    return {
      isRevealing: () => result!.isRevealing,
      rerender: (patch: Partial<typeof props>) => {
        props = { ...props, ...patch };
        act(() => {
          renderer.update(React.createElement(Harness));
        });
      },
    };
  }

  test('snapshot without a reveal, then a start push, then a late snapshot: reveal stays open', () => {
    const hook = renderReveal(makeClient(), reconciliation(null, EARLY, 1));
    expect(hook.isRevealing()).toBe(false);

    fire(reveal({ emittedAt: LATE }));
    expect(hook.isRevealing()).toBe(true);

    // The earlier read lands late (older observedAt, newer version) and must not close the live reveal.
    hook.rerender({ reconciliation: reconciliation(null, EARLY, 2) });
    expect(hook.isRevealing()).toBe(true);
  });

  test('snapshot with a reveal, a matching advance, then a late snapshot: reveal stays closed', () => {
    const hook = renderReveal(makeClient(), reconciliation(revealSnap(), EARLY, 1));
    expect(hook.isRevealing()).toBe(true);

    fireAdvanced({ advancedAt: LATE });
    expect(hook.isRevealing()).toBe(false);

    // A late reveal snapshot (older observedAt) must not reopen an already-advanced reveal.
    hook.rerender({ reconciliation: reconciliation(revealSnap(), EARLY, 2) });
    expect(hook.isRevealing()).toBe(false);
  });

  test('an advance arriving before its snapshot response blocks the late reopening snapshot', () => {
    // Race: the server read an active reveal, then advanced (LATE) while that snapshot response was
    // still in flight — so the advance lands with no reveal applied yet. It must still record a closing
    // baseline so the late, older (EARLY) snapshot carrying the now-stale reveal cannot reopen it.
    const hook = renderReveal(makeClient(), null);
    expect(hook.isRevealing()).toBe(false);

    fireAdvanced({ advancedAt: LATE });
    expect(hook.isRevealing()).toBe(false);

    hook.rerender({ reconciliation: reconciliation(revealSnap(), EARLY, 1) });
    expect(hook.isRevealing()).toBe(false);
  });

  test('a reveal snapshot followed by a newer advance closes the reveal', () => {
    const hook = renderReveal(makeClient(), reconciliation(revealSnap(), EARLY, 1));
    expect(hook.isRevealing()).toBe(true);

    fireAdvanced({ advancedAt: LATE });
    expect(hook.isRevealing()).toBe(false);
  });

  test('a newer null snapshot then an older start push: reveal stays closed', () => {
    const hook = renderReveal(makeClient(), reconciliation(null, LATE, 1));
    expect(hook.isRevealing()).toBe(false);

    fire(reveal({ emittedAt: EARLY }));
    expect(hook.isRevealing()).toBe(false);
  });

  test('a failed resync (unchanged version) does not reopen or reseed cached reveal state', () => {
    const hook = renderReveal(makeClient(), reconciliation(revealSnap(), EARLY, 1));
    fireAdvanced({ advancedAt: LATE });
    expect(hook.isRevealing()).toBe(false);

    // A failed re-fetch leaves useSessionTimer's reconciliation at the same version; re-applying it must
    // be a no-op — the cached reveal is not replayed.
    hook.rerender({ reconciliation: reconciliation(revealSnap(), EARLY, 1) });
    expect(hook.isRevealing()).toBe(false);
  });

  test('an equal-time start and advance resolve in favor of the advance', () => {
    const hook = renderReveal(makeClient(), reconciliation(null, EARLY, 1));

    fire(reveal({ emittedAt: MID }));
    expect(hook.isRevealing()).toBe(true);

    fireAdvanced({ advancedAt: MID });
    expect(hook.isRevealing()).toBe(false);
  });

  test('a stale advance for another substage or session leaves the reveal up', () => {
    const hook = renderReveal(makeClient(), reconciliation(revealSnap(), EARLY, 1));
    expect(hook.isRevealing()).toBe(true);

    fireAdvanced({ fromSubstageId: 'sub-stale', advancedAt: LATE });
    expect(hook.isRevealing()).toBe(true);

    fireAdvanced({ liveSessionId: 'sess-other', advancedAt: LATE });
    expect(hook.isRevealing()).toBe(true);
  });

  test('changing liveSessionId clears the previous session reconciliation state', () => {
    const hook = renderReveal(makeClient(), reconciliation(revealSnap(), EARLY, 1));
    expect(hook.isRevealing()).toBe(true);

    // The same (now stale) reconciliation must not leak into the next session.
    hook.rerender({ liveSessionId: 'sess-2' });
    expect(hook.isRevealing()).toBe(false);
  });
});
