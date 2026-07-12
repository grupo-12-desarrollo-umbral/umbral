import React from 'react';
import { act, create } from 'react-test-renderer';
import { useActiveQuestion, type UseActiveQuestionResult } from '@/lib/realtime/use-active-question';
import type { SessionStateChangedNotificationDto } from '@/lib/realtime/sessions-hub-types';
import type {
  ActiveQuestionSnapshotDto,
  QuestionActivatedNotificationDto,
  QuestionClosedNotificationDto,
  SubstageAdvancedNotificationDto,
} from '@/lib/realtime/trivia-types';

type QuestionActivatedHandler = (n: QuestionActivatedNotificationDto) => void;
type QuestionClosedHandler = (n: QuestionClosedNotificationDto) => void;
type SubstageAdvancedHandler = (n: SubstageAdvancedNotificationDto) => void;
type StateChangedHandler = (n: SessionStateChangedNotificationDto) => void;

let activatedHandlers: Set<QuestionActivatedHandler>;
let closedHandlers: Set<QuestionClosedHandler>;
let advancedHandlers: Set<SubstageAdvancedHandler>;
let stateHandlers: Set<StateChangedHandler>;

function makeClient() {
  activatedHandlers = new Set();
  closedHandlers = new Set();
  advancedHandlers = new Set();
  stateHandlers = new Set();

  return {
    connection: {} as never,
    start: jest.fn(),
    stop: jest.fn(),
    reconnect: jest.fn(),
    onTimerUpdated: jest.fn(),
    onQuestionActivated(cb: QuestionActivatedHandler) {
      activatedHandlers.add(cb);
      return () => activatedHandlers.delete(cb);
    },
    onQuestionClosed(cb: QuestionClosedHandler) {
      closedHandlers.add(cb);
      return () => closedHandlers.delete(cb);
    },
    onSubstageAdvanced(cb: SubstageAdvancedHandler) {
      advancedHandlers.add(cb);
      return () => advancedHandlers.delete(cb);
    },
    onStateChanged(cb: StateChangedHandler) {
      stateHandlers.add(cb);
      return () => stateHandlers.delete(cb);
    },
    onTeamBoardUpdated: jest.fn(),
  };
}

type HookProps = Parameters<typeof useActiveQuestion>[0];

function renderHook(initialProps: HookProps) {
  let result: UseActiveQuestionResult | null = null;
  let currentProps = { ...initialProps };

  function Harness() {
    result = useActiveQuestion(currentProps);
    return null;
  }

  let renderer: ReturnType<typeof create>;
  act(() => {
    renderer = create(React.createElement(Harness));
  });

  return {
    get: () => result!,
    rerender: (patch: Partial<HookProps>) => {
      currentProps = { ...currentProps, ...patch };
      act(() => {
        renderer.update(React.createElement(Harness));
      });
    },
    unmount: () => act(() => renderer.unmount()),
  };
}

const SNAPSHOT_QUESTION: ActiveQuestionSnapshotDto = {
  liveSessionId: 'sess-1',
  questionIndex: 1,
  sequenceOrder: 2,
  prompt: 'Where is the key hidden?',
  options: ['Library', 'Garden'],
  timeLimitSeconds: 60,
  remainingSeconds: 42,
  activatedAt: '2026-07-11T10:00:00Z',
  triviaSubstageSnapshotId: 'substage-abc',
};

const ACTIVATED: QuestionActivatedNotificationDto = {
  liveSessionId: 'sess-1',
  questionIndex: 2,
  sequenceOrder: 3,
  prompt: 'Which lantern is lit?',
  options: ['North', 'South', 'East'],
  timeLimitSeconds: 45,
  activatedAt: '2026-07-11T10:01:00Z',
  triviaSubstageSnapshotId: 'substage-abc',
};

const CLOSED: QuestionClosedNotificationDto = {
  liveSessionId: 'sess-1',
  questionIndex: 2,
  closedAt: '2026-07-11T10:02:00Z',
  wasExpiredByTimer: true,
};

const ADVANCED: SubstageAdvancedNotificationDto = {
  liveSessionId: 'sess-1',
  fromSubstageId: 'substage-1',
  fromPlayMode: 'Trivia',
  toSubstageId: 'substage-2',
  advancedAt: '2026-07-11T10:03:00Z',
};

const STATE_CHANGED: SessionStateChangedNotificationDto = {
  liveSessionId: 'sess-1',
  previousState: 'Active',
  currentState: 'Finished',
  changedAt: '2026-07-11T10:04:00Z',
};

function defaultProps(client = makeClient()): HookProps {
  return {
    client,
    liveSessionId: 'sess-1',
    isReconnected: true,
    reconnectNonce: 0,
    snapshotActiveQuestion: null,
    snapshotSessionState: 'Active',
  };
}

describe('useActiveQuestion', () => {
  test('seeds active view from snapshot question', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    expect(hook.get().view).toEqual({
      kind: 'active',
      question: {
        questionIndex: 1,
        sequenceOrder: 2,
        prompt: 'Where is the key hidden?',
        options: ['Library', 'Garden'],
        timeLimitSeconds: 60,
        triviaSubstageSnapshotId: 'substage-abc',
      },
    });
    expect(hook.get().sessionState).toBe('Active');

    hook.unmount();
  });

  test('seeds none without a snapshot question and closed for terminal snapshot state', () => {
    const hook = renderHook(defaultProps());
    expect(hook.get().view).toEqual({ kind: 'none' });

    hook.rerender({ reconnectNonce: 1, snapshotSessionState: 'Finished' });
    expect(hook.get().view).toEqual({ kind: 'closed' });
    expect(hook.get().sessionState).toBe('Finished');

    hook.unmount();
  });

  test('applies question lifecycle events filtered by liveSessionId', () => {
    const hook = renderHook(defaultProps());

    act(() => {
      activatedHandlers.forEach(cb => cb({ ...ACTIVATED, liveSessionId: 'other' }));
    });
    expect(hook.get().view).toEqual({ kind: 'none' });

    act(() => {
      activatedHandlers.forEach(cb => cb(ACTIVATED));
    });
    expect(hook.get().view).toEqual({
      kind: 'active',
      question: expect.objectContaining({ sequenceOrder: 3, prompt: 'Which lantern is lit?' }),
    });

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED));
    });
    // Close now locks the displayed question rather than blanking it straight to waiting.
    expect(hook.get().view.kind).toBe('active');
    expect(hook.get().isQuestionClosed).toBe(true);

    hook.unmount();
  });

  test('maps substage advance to waiting or closed', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    act(() => {
      advancedHandlers.forEach(cb => cb(ADVANCED));
    });
    expect(hook.get().view).toEqual({ kind: 'waiting' });

    act(() => {
      advancedHandlers.forEach(cb => cb({ ...ADVANCED, toSubstageId: null }));
    });
    expect(hook.get().view).toEqual({ kind: 'closed' });

    hook.unmount();
  });

  test('terminal state changes close the region while non-terminal state changes retain the question', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    act(() => {
      stateHandlers.forEach(cb => cb({ ...STATE_CHANGED, currentState: 'Paused' }));
    });
    expect(hook.get().view.kind).toBe('active');
    expect(hook.get().sessionState).toBe('Paused');

    act(() => {
      stateHandlers.forEach(cb => cb(STATE_CHANGED));
    });
    expect(hook.get().view).toEqual({ kind: 'closed' });
    expect(hook.get().sessionState).toBe('Finished');

    hook.unmount();
  });

  test('reconnectNonce re-seeds from the snapshot without a reconnecting region state', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    act(() => {
      // Matching close (SNAPSHOT_QUESTION is questionIndex 1) locks the question.
      closedHandlers.forEach(cb => cb({ ...CLOSED, questionIndex: 1 }));
    });
    expect(hook.get().view.kind).toBe('active');
    expect(hook.get().isQuestionClosed).toBe(true);

    hook.rerender({ reconnectNonce: 1, snapshotActiveQuestion: SNAPSHOT_QUESTION });

    expect(hook.get().view.kind).toBe('active');
    expect(hook.get().isQuestionClosed).toBe(false);
    expect(hook.get().view).not.toHaveProperty('kind', 'reconnecting');

    hook.unmount();
  });

  test('non-terminal snapshot state changes do not erase a live pushed question', () => {
    const hook = renderHook(defaultProps());

    act(() => {
      activatedHandlers.forEach(cb => cb(ACTIVATED));
    });
    expect(hook.get().view.kind).toBe('active');

    hook.rerender({ snapshotSessionState: 'Paused', snapshotActiveQuestion: null });

    expect(hook.get().sessionState).toBe('Paused');
    expect(hook.get().view).toEqual({
      kind: 'active',
      question: expect.objectContaining({ prompt: 'Which lantern is lit?' }),
    });

    hook.unmount();
  });

  test('terminal closed view is not overwritten by later lifecycle events', () => {
    const hook = renderHook({ ...defaultProps(), snapshotSessionState: 'Finished' });
    expect(hook.get().view).toEqual({ kind: 'closed' });

    act(() => {
      activatedHandlers.forEach(cb => cb(ACTIVATED));
      closedHandlers.forEach(cb => cb(CLOSED));
      advancedHandlers.forEach(cb => cb(ADVANCED));
    });

    expect(hook.get().view).toEqual({ kind: 'closed' });

    hook.unmount();
  });

  test('close or advance before any seen question remains the none state', () => {
    const hook = renderHook(defaultProps());

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED));
      advancedHandlers.forEach(cb => cb(ADVANCED));
    });

    expect(hook.get().view).toEqual({ kind: 'none' });

    hook.unmount();
  });

  test('close matching the displayed question locks it, flags closed, and requests a resync', () => {
    const requestResync = jest.fn();
    const hook = renderHook({ ...defaultProps(), requestResync });

    act(() => {
      activatedHandlers.forEach(cb => cb(ACTIVATED));
    });
    expect(hook.get().view.kind).toBe('active');

    act(() => {
      closedHandlers.forEach(cb => cb(CLOSED));
    });

    expect(hook.get().view).toEqual({
      kind: 'active',
      question: expect.objectContaining({ sequenceOrder: 3 }),
    });
    expect(hook.get().isQuestionClosed).toBe(true);
    expect(requestResync).toHaveBeenCalledTimes(1);

    hook.unmount();
  });

  test('a stale close for a superseded question index is ignored', () => {
    const requestResync = jest.fn();
    const hook = renderHook({ ...defaultProps(), requestResync });

    act(() => {
      activatedHandlers.forEach(cb => cb(ACTIVATED));
    });

    act(() => {
      // Close for question 1 arrives after question 2 is already displayed.
      closedHandlers.forEach(cb => cb({ ...CLOSED, questionIndex: 1 }));
    });

    expect(hook.get().view).toEqual({
      kind: 'active',
      question: expect.objectContaining({ sequenceOrder: 3 }),
    });
    expect(hook.get().isQuestionClosed).toBe(false);
    expect(requestResync).not.toHaveBeenCalled();

    hook.unmount();
  });

  test('a resync snapshot with a new question index advances and clears the closed flag', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    act(() => {
      closedHandlers.forEach(cb => cb({ ...CLOSED, questionIndex: 1 }));
    });
    expect(hook.get().isQuestionClosed).toBe(true);

    const NEXT_SNAPSHOT: ActiveQuestionSnapshotDto = {
      ...SNAPSHOT_QUESTION,
      questionIndex: 3,
      sequenceOrder: 4,
      prompt: 'The next question',
    };
    hook.rerender({ snapshotVersion: 1, snapshotActiveQuestion: NEXT_SNAPSHOT });

    expect(hook.get().view).toEqual({
      kind: 'active',
      question: expect.objectContaining({ questionIndex: 3, prompt: 'The next question' }),
    });
    expect(hook.get().isQuestionClosed).toBe(false);

    hook.unmount();
  });

  test('a resync with no active question on a live session resolves to waiting', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    act(() => {
      closedHandlers.forEach(cb => cb({ ...CLOSED, questionIndex: 1 }));
    });

    hook.rerender({ snapshotVersion: 1, snapshotActiveQuestion: null, snapshotSessionState: 'Active' });

    expect(hook.get().view).toEqual({ kind: 'waiting' });
    expect(hook.get().isQuestionClosed).toBe(false);

    hook.unmount();
  });

  test('a resync into a terminal session state resolves to closed', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    act(() => {
      closedHandlers.forEach(cb => cb({ ...CLOSED, questionIndex: 1 }));
    });

    hook.rerender({ snapshotVersion: 1, snapshotActiveQuestion: null, snapshotSessionState: 'Finished' });

    expect(hook.get().view).toEqual({ kind: 'closed' });
    expect(hook.get().sessionState).toBe('Finished');
    expect(hook.get().isQuestionClosed).toBe(false);

    hook.unmount();
  });

  test('a resync echoing the just-closed question index does not re-open it', () => {
    const hook = renderHook({ ...defaultProps(), snapshotActiveQuestion: SNAPSHOT_QUESTION });

    act(() => {
      closedHandlers.forEach(cb => cb({ ...CLOSED, questionIndex: 1 }));
    });
    expect(hook.get().isQuestionClosed).toBe(true);

    // Backend race: the snapshot still reports the same (just-closed) question.
    hook.rerender({ snapshotVersion: 1, snapshotActiveQuestion: SNAPSHOT_QUESTION });

    expect(hook.get().view).toEqual({ kind: 'waiting' });
    expect(hook.get().isQuestionClosed).toBe(false);

    hook.unmount();
  });

  test('unsubscribes all handlers on cleanup', () => {
    const hook = renderHook(defaultProps());
    expect(activatedHandlers.size).toBe(1);
    expect(closedHandlers.size).toBe(1);
    expect(advancedHandlers.size).toBe(1);
    expect(stateHandlers.size).toBe(1);

    hook.unmount();

    expect(activatedHandlers.size).toBe(0);
    expect(closedHandlers.size).toBe(0);
    expect(advancedHandlers.size).toBe(0);
    expect(stateHandlers.size).toBe(0);
  });
});
