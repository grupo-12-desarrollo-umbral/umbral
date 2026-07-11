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
};

const TIMER: TimerDisplay = { label: '00:42', pct: 70, tone: 'running' };

type TreeNode = {
  type?: string;
  props?: Record<string, unknown>;
  children?: (TreeNode | string)[] | null;
};

function render() {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(ActiveQuestionStage, {
        question: QUESTION,
        sessionState: 'Active',
        score: 120,
        timerDisplay: TIMER,
      }),
    );
  });
  return renderer!.toJSON() as TreeNode;
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
    const texts = allText(render());

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
    const texts = allText(render());

    expect(findAllByProp(render(), 'accessibilityRole', 'progressbar')).toHaveLength(1);
    expect(texts).not.toContain('Running');
    expect(texts).not.toContain('Paused');
    expect(texts).not.toContain('Expired');
    expect(texts).not.toContain('Unavailable');
  });

  test('renders prompt as a header and options as non-button text', () => {
    const tree = render();

    expect(findAllByProp(tree, 'accessibilityRole', 'header')).toHaveLength(1);
    expect(findAllByProp(tree, 'accessibilityRole', 'button')).toHaveLength(0);
    expect(findAllByProp(tree, 'accessibilityRole', 'text')).toHaveLength(QUESTION.options.length);
    expect(findAllWithProp(tree, 'onPress')).toHaveLength(0);
  });
});
