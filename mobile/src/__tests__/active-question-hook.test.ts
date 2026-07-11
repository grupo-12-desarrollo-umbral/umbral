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
    expect(hook.get().view).toEqual({ kind: 'waiting' });

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
      closedHandlers.forEach(cb => cb(CLOSED));
    });
    expect(hook.get().view).toEqual({ kind: 'waiting' });

    hook.rerender({ reconnectNonce: 1, snapshotActiveQuestion: SNAPSHOT_QUESTION });

    expect(hook.get().view.kind).toBe('active');
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
