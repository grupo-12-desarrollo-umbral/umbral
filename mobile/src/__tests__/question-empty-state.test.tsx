import React from 'react';
import { act, create } from 'react-test-renderer';
import { QuestionEmptyState } from '@/components/question-empty-state';

type TreeNode = {
  children?: (TreeNode | string)[] | null;
};

function render(props: React.ComponentProps<typeof QuestionEmptyState>) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(React.createElement(QuestionEmptyState, props));
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

describe('QuestionEmptyState', () => {
  test('renders waiting copy distinctly', () => {
    const text = allText(render({ kind: 'waiting', sessionState: 'Active' })).join(' ');

    expect(text).toContain('Esperando la siguiente pregunta');
    expect(text).toContain('El operador aún no ha activado la siguiente.');
  });

  test('renders no-active-question copy distinctly', () => {
    const text = allText(render({ kind: 'none', sessionState: 'Preparing' })).join(' ');

    expect(text).toContain('Aún no hay pregunta activa.');
    expect(text).toContain('el anfitrión iniciará la ronda');
  });

  test('renders Finished and Cancelled terminal variants', () => {
    const finished = allText(render({ kind: 'closed', sessionState: 'Finished' })).join(' ');
    const cancelled = allText(render({ kind: 'closed', sessionState: 'Cancelled' })).join(' ');

    expect(finished).toContain('Esta sesión ha terminado. Gracias por jugar.');
    expect(cancelled).toContain('Esta sesión fue cancelada por el anfitrión.');
    expect(finished).not.toBe(cancelled);
  });
});
