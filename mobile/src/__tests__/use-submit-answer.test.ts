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

let hookResult: UseSubmitAnswerResult | null = null;

function renderHook(props = DEFAULT_PROPS) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(React.createElement(TestComponent, props));
  });
  return {
    get: () => hookResult!,
    rerender: (newProps: typeof DEFAULT_PROPS) => {
      act(() => {
        renderer!.update(React.createElement(TestComponent, newProps));
      });
    },
    unmount: () => act(() => renderer!.unmount()),
  };
}

function TestComponent(props: typeof DEFAULT_PROPS) {
  hookResult = useSubmitAnswer(props);
  return null;
}

describe('useSubmitAnswer', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    hookResult = null;
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
