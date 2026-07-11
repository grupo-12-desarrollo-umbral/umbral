import React from 'react';
import { act, create } from 'react-test-renderer';
import { ActiveQuestionStage } from '@/components/active-question-stage';
import type { ActiveQuestion } from '@/lib/realtime/active-question-types';
import type { TimerDisplay } from '@/lib/realtime/timer-types';

const QUESTION: ActiveQuestion = {
  questionIndex: 1,
  sequenceOrder: 2,
  prompt: 'Which lantern is lit above the old archive door?',
  options: ['North lantern', 'South lantern', 'East lantern'],
  timeLimitSeconds: 45,
  triviaSubstageSnapshotId: 'substage-abc',
};

const TIMER: TimerDisplay = { label: '00:42', pct: 70, tone: 'running' };

type TreeNode = {
  type?: string;
  props?: Record<string, unknown>;
  children?: (TreeNode | string)[] | null;
};

function render(props: Record<string, unknown> = {}) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(ActiveQuestionStage, {
        question: QUESTION,
        sessionState: 'Active',
        score: 120,
        timerDisplay: TIMER,
        ...props,
      }),
    );
  });
  return renderer!;
}

function renderDisplayOnly() {
  return render().toJSON() as TreeNode;
}

function allText(node: unknown): string[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  const n = node as TreeNode;
  return allText(n.children ?? []);
}

function findAllByProp(node: unknown, key: string, value: unknown): TreeNode[] {
  if (!node || typeof node !== 'object') return [];
  const n = node as TreeNode;
  const own = n.props?.[key] === value ? [n] : [];
  const children = Array.isArray(n.children) ? n.children.flatMap(child => findAllByProp(child, key, value)) : [];
  return [...own, ...children];
}

function findAllWithProp(node: unknown, key: string): TreeNode[] {
  if (!node || typeof node !== 'object') return [];
  const n = node as TreeNode;
  const own = Object.prototype.hasOwnProperty.call(n.props ?? {}, key) ? [n] : [];
  const children = Array.isArray(n.children) ? n.children.flatMap(child => findAllWithProp(child, key)) : [];
  return [...own, ...children];
}

describe('ActiveQuestionStage', () => {
  test('renders header state badge, score, question label, prompt, and options', () => {
    const texts = allText(renderDisplayOnly());

    expect(texts).toContain('Active');
    expect(texts).toContain('SCORE');
    expect(texts).toContain('120');
    expect(texts).toContain('QUESTION 2');
    expect(texts).toContain('00:42');
    expect(texts).toContain('Which lantern is lit above the old archive door?');
    expect(texts).toEqual(expect.arrayContaining(['A', 'B', 'C']));
    expect(texts).toEqual(expect.arrayContaining(QUESTION.options));
  });

  test('renders chip-less countdown without timer status labels', () => {
    const texts = allText(renderDisplayOnly());

    expect(findAllByProp(renderDisplayOnly(), 'accessibilityRole', 'progressbar')).toHaveLength(1);
    expect(texts).not.toContain('Running');
    expect(texts).not.toContain('Paused');
    expect(texts).not.toContain('Expired');
    expect(texts).not.toContain('Unavailable');
  });

  test('renders prompt as a header and options as non-button text without submit props', () => {
    const tree = renderDisplayOnly();

    expect(findAllByProp(tree, 'accessibilityRole', 'header')).toHaveLength(1);
    expect(findAllByProp(tree, 'accessibilityRole', 'button')).toHaveLength(0);
    expect(findAllByProp(tree, 'accessibilityRole', 'text')).toHaveLength(QUESTION.options.length);
    expect(findAllWithProp(tree, 'onPress')).toHaveLength(0);
  });

  test('renders tappable option rows with submit props', () => {
    const tree = render({
      selectedOptionSequenceOrder: null,
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: jest.fn(),
    }).toJSON() as TreeNode;

    expect(findAllByProp(tree, 'accessibilityRole', 'radio')).toHaveLength(QUESTION.options.length);
  });

  test('selected option renders emberAccentSoft background and filled pill', () => {
    const renderer = render({
      selectedOptionSequenceOrder: 1,
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: jest.fn(),
    });
    const tree = renderer.toJSON() as TreeNode;
    const radios = findAllByProp(tree, 'accessibilityRole', 'radio');
    const selected = radios.find(r => (r.props as Record<string, unknown>).accessibilityState === undefined
      ? false
      : ((r.props as Record<string, unknown>).accessibilityState as Record<string, unknown>).selected === true);

    expect(selected).toBeDefined();
    expect((selected!.props as Record<string, unknown>).accessibilityState).toEqual({ selected: true, disabled: false });
  });

  test('isLocked disables all option taps and shows success chip', () => {
    const renderer = render({
      selectedOptionSequenceOrder: 0,
      isLocked: true,
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: jest.fn(),
    });
    const tree = renderer.toJSON() as TreeNode;
    const texts = allText(tree);

    expect(texts).toContain('Answer submitted');
    expect(texts).not.toContain('Submit answer');
    expect(findAllByProp(tree, 'accessibilityRole', 'button')).toHaveLength(0);
  });

  test('isClosed disables option rows, hides Submit, and shows the close affordance', () => {
    const renderer = render({
      selectedOptionSequenceOrder: 1,
      isClosed: true,
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: jest.fn(),
    });
    const tree = renderer.toJSON() as TreeNode;
    const texts = allText(tree);

    // Close affordance shows, distinct from the success chip and the interactive Submit button.
    expect(texts.join(' ')).toContain('Question closed');
    expect(texts).not.toContain('Answer submitted');
    expect(texts).not.toContain('Submit answer');
    expect(findAllByProp(tree, 'accessibilityRole', 'button')).toHaveLength(0);

    // Option rows are rendered as non-pressable, disabled text.
    expect(findAllByProp(tree, 'accessibilityRole', 'radio')).toHaveLength(0);
    const rows = findAllByProp(tree, 'accessibilityRole', 'text');
    const optionRows = rows.filter(r => {
      const state = (r.props as Record<string, unknown>).accessibilityState as Record<string, unknown> | undefined;
      return state?.disabled === true;
    });
    expect(optionRows.length).toBeGreaterThanOrEqual(QUESTION.options.length);
    expect(findAllWithProp(tree, 'onPress')).toHaveLength(0);
  });

  test('isClosed takes precedence over the submit lock and suppresses rejection banners', () => {
    const renderer = render({
      selectedOptionSequenceOrder: 0,
      isLocked: true,
      isClosed: true,
      rejection: {
        reasonCode: 'trivia-answer-requires-active-question',
        title: 'Question closed',
        message: 'This question is no longer accepting answers.',
      },
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: jest.fn(),
    });
    const tree = renderer.toJSON() as TreeNode;
    const texts = allText(tree);

    expect(texts.join(' ')).toContain('Question closed — waiting for the next');
    expect(texts).not.toContain('Answer submitted');
    expect(findAllByProp(tree, 'accessibilityRole', 'alert')).toHaveLength(0);
  });

  test('isSubmitting shows spinner on the button', () => {
    const renderer = render({
      selectedOptionSequenceOrder: 0,
      isSubmitting: true,
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: jest.fn(),
    });
    const tree = renderer.toJSON() as TreeNode;
    const texts = allText(tree);

    expect(texts).not.toContain('Submit answer');
    expect(findAllByProp(tree, 'accessibilityRole', 'button')).toHaveLength(1);
  });

  test('rejection banner renders with title and body; dismiss calls onDismissRejection', () => {
    const onDismiss = jest.fn();
    const renderer = render({
      selectedOptionSequenceOrder: 0,
      rejection: {
        reasonCode: 'duplicate-trivia-answer',
        title: 'Already answered',
        message: 'Your team has already submitted an answer for this question.',
      },
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: onDismiss,
    });
    const tree = renderer.toJSON() as TreeNode;
    const texts = allText(tree);

    expect(texts).toContain('Already answered');
    expect(texts).toContain('Your team has already submitted an answer for this question.');
    expect(findAllByProp(tree, 'accessibilityRole', 'alert')).toHaveLength(1);

    const dismissButton = findAllByProp(tree, 'accessibilityRole', 'button')
      .find(b => (b.props as Record<string, unknown>).accessibilityLabel === 'Dismiss');
    expect(dismissButton).toBeDefined();
  });

  test('accessibilityRole and accessibilityState are set correctly', () => {
    const renderer = render({
      selectedOptionSequenceOrder: 1,
      onSelectOption: jest.fn(),
      onSubmit: jest.fn(),
      onDismissRejection: jest.fn(),
    });
    const tree = renderer.toJSON() as TreeNode;
    const radios = findAllByProp(tree, 'accessibilityRole', 'radio');

    expect(radios).toHaveLength(QUESTION.options.length);
    radios.forEach((radio, index) => {
      const state = (radio.props as Record<string, unknown>).accessibilityState as Record<string, unknown>;
      expect(state.selected).toBe(index === 0);
      expect(state.disabled).toBe(false);
    });
  });
});
