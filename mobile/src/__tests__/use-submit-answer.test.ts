import React from 'react';
import { act, create } from 'react-test-renderer';
import { useSubmitAnswer, type UseSubmitAnswerResult } from '@/lib/realtime/use-submit-answer';
import { SubmitTriviaAnswerRejection } from '@/lib/api/sessions';

jest.mock('expo-haptics', () => ({
  impactAsync: jest.fn(),
  notificationAsync: jest.fn(),
  ImpactFeedbackStyle: { Light: 'light' },
  NotificationFeedbackType: { Success: 'success', Error: 'error' },
}));

jest.mock('@/lib/api/sessions', () => ({
  submitTriviaAnswer: jest.fn(),
  SubmitTriviaAnswerRejection: jest.requireActual('@/lib/api/sessions').SubmitTriviaAnswerRejection,
}));

const mockSubmit = jest.requireMock('@/lib/api/sessions').submitTriviaAnswer as jest.Mock;

const DEFAULT_PROPS = {
  liveSessionId: 'sess-1',
  teamId: 'team-1',
  triviaSubstageSnapshotId: 'substage-abc',
  questionSequenceOrder: 1,
};

// Container object (not a bare reassignable binding) so TestComponent captures the
// hook result without reassigning a variable declared outside the component.
const hookResult: { current: UseSubmitAnswerResult | null } = { current: null };

function renderHook(props = DEFAULT_PROPS) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(React.createElement(TestComponent, props));
  });
  return {
    get: () => hookResult.current!,
    rerender: (newProps: typeof DEFAULT_PROPS) => {
      act(() => {
        renderer!.update(React.createElement(TestComponent, newProps));
      });
    },
    unmount: () => act(() => renderer!.unmount()),
  };
}

function TestComponent(props: typeof DEFAULT_PROPS) {
  // Test harness: capture the hook's return so assertions can read it outside render.
  // eslint-disable-next-line react-hooks/immutability
  hookResult.current = useSubmitAnswer(props);
  return null;
}

describe('useSubmitAnswer', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    hookResult.current = null;
  });

  test('selecting an option stores it and clears a prior rejection', async () => {
    mockSubmit.mockRejectedValueOnce(
      new SubmitTriviaAnswerRejection('late-trivia-answer', 409, 'Too late'),
    );
    const hook = renderHook();

    act(() => hook.get().selectOption(0));
    expect(hook.get().selectedOptionSequenceOrder).toBe(0);

    await act(async () => {
      await hook.get().submit();
    });
    expect(hook.get().rejection).not.toBeNull();

    act(() => hook.get().selectOption(1));
    expect(hook.get().selectedOptionSequenceOrder).toBe(1);
    expect(hook.get().rejection).toBeNull();
  });

  test('submit with no selection is a no-op', async () => {
    const hook = renderHook();

    await act(async () => {
      await hook.get().submit();
    });

    expect(mockSubmit).not.toHaveBeenCalled();
  });

  test('submit sets isSubmitting then locks on 200', async () => {
    let resolveSubmit!: (value: unknown) => void;
    mockSubmit.mockReturnValueOnce(new Promise((resolve) => { resolveSubmit = resolve; }));
    const hook = renderHook();

    act(() => hook.get().selectOption(0));

    act(() => {
      hook.get().submit();
    });

    expect(hook.get().isSubmitting).toBe(true);

    await act(async () => {
      resolveSubmit({
        liveSessionId: 'sess-1',
        teamId: 'team-1',
        triviaSubstageSnapshotId: 'substage-abc',
        questionSequenceOrder: 1,
        answeredAt: '2026-07-11T10:00:00Z',
      });
    });

    expect(hook.get().isLocked).toBe(true);
    expect(hook.get().isSubmitting).toBe(false);
  });

  test('rejection populates with the mapped reason from SubmitTriviaAnswerRejection', async () => {
    mockSubmit.mockRejectedValueOnce(
      new SubmitTriviaAnswerRejection('duplicate-trivia-answer', 409, 'Already answered'),
    );
    const hook = renderHook();

    act(() => hook.get().selectOption(0));
    await act(async () => {
      await hook.get().submit();
    });

    expect(hook.get().rejection).toEqual({
      reasonCode: 'duplicate-trivia-answer',
      title: 'Already answered',
      message: 'Your team has already submitted an answer for this question.',
    });
  });

  test('isLocked prevents a second submit call', async () => {
    mockSubmit.mockResolvedValue({
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      triviaSubstageSnapshotId: 'substage-abc',
      questionSequenceOrder: 1,
      answeredAt: '2026-07-11T10:00:00Z',
    });
    const hook = renderHook();

    act(() => hook.get().selectOption(0));
    await act(async () => {
      await hook.get().submit();
    });

    expect(hook.get().isLocked).toBe(true);
    mockSubmit.mockClear();

    await act(async () => {
      await hook.get().submit();
    });

    expect(mockSubmit).not.toHaveBeenCalled();
  });

  test('a late response for the previous question does not corrupt the new question state', async () => {
    let resolveSubmit!: (value: unknown) => void;
    mockSubmit.mockReturnValueOnce(new Promise((resolve) => { resolveSubmit = resolve; }));
    const hook = renderHook();

    // Submit for question 1, then the operator advances to question 2 while it is still in flight.
    act(() => hook.get().selectOption(0));
    act(() => {
      hook.get().submit();
    });
    expect(hook.get().isSubmitting).toBe(true);

    hook.rerender({ ...DEFAULT_PROPS, questionSequenceOrder: 2 });
    // Question 2 starts clean: the reset effect cleared lock/submitting for the new question.
    expect(hook.get().isLocked).toBe(false);
    expect(hook.get().isSubmitting).toBe(false);

    // Question 1's request now resolves — it must NOT lock question 2 or touch its in-flight guard.
    await act(async () => {
      resolveSubmit({
        liveSessionId: 'sess-1',
        teamId: 'team-1',
        triviaSubstageSnapshotId: 'substage-abc',
        questionSequenceOrder: 1,
        answeredAt: '2026-07-11T10:00:00Z',
      });
    });

    expect(hook.get().isLocked).toBe(false);
    expect(hook.get().rejection).toBeNull();

    // The guard is intact for question 2: a fresh submit still fires.
    mockSubmit.mockResolvedValueOnce({
      liveSessionId: 'sess-1',
      teamId: 'team-1',
      triviaSubstageSnapshotId: 'substage-abc',
      questionSequenceOrder: 2,
      answeredAt: '2026-07-11T10:00:05Z',
    });
    act(() => hook.get().selectOption(1));
    await act(async () => {
      await hook.get().submit();
    });
    expect(mockSubmit).toHaveBeenCalledTimes(2);
    expect(hook.get().isLocked).toBe(true);
  });

  test('a late rejection for the previous question does not show its error on the new question', async () => {
    let rejectSubmit!: (reason: unknown) => void;
    mockSubmit.mockReturnValueOnce(new Promise((_, reject) => { rejectSubmit = reject; }));
    const hook = renderHook();

    act(() => hook.get().selectOption(0));
    act(() => {
      hook.get().submit();
    });

    hook.rerender({ ...DEFAULT_PROPS, questionSequenceOrder: 2 });

    await act(async () => {
      rejectSubmit(new SubmitTriviaAnswerRejection('late-trivia-answer', 409, 'Too late'));
      await Promise.resolve();
    });

    expect(hook.get().rejection).toBeNull();
  });

  test('network failure maps to unknown rejection', async () => {
    mockSubmit.mockRejectedValueOnce(
      new SubmitTriviaAnswerRejection('unknown', 0, 'Network request failed'),
    );
    const hook = renderHook();

    act(() => hook.get().selectOption(0));
    await act(async () => {
      await hook.get().submit();
    });

    expect(hook.get().rejection).toEqual({
      reasonCode: 'unknown',
      title: 'Connection issue',
      message: "Couldn't reach the server. Check your connection and try again.",
    });
  });
});
