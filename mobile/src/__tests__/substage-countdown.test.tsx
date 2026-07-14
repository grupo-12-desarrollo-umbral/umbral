// The trivia pre-game countdown numeral renders its seconds + an assertive a11y label on mobile.
import React from 'react';
import { act, create } from 'react-test-renderer';
import { SubstageCountdown } from '@/components/substage-countdown';

type TreeNode = { props?: Record<string, unknown>; children?: (TreeNode | string | number)[] | null };

function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  const n = node as TreeNode;
  return allText(n.children ?? []);
}

describe('SubstageCountdown', () => {
  test('renders the countdown numeral under a GET READY label', () => {
    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(React.createElement(SubstageCountdown, { secondsLeft: 5 }));
    });
    const texts = allText(renderer!.toJSON()).map(String);
    expect(texts).toContain('GET READY');
    expect(texts).toContain('5');
  });

  test('exposes an assertive "Starting in N" accessibility label', () => {
    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(React.createElement(SubstageCountdown, { secondsLeft: 3 }));
    });
    const node = renderer!.root.findByProps({ testID: 'substage-countdown' });
    expect(node.props.accessibilityLabel).toBe('Starting in 3');
    expect(node.props.accessibilityLiveRegion).toBe('assertive');
  });
});
