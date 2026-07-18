// The trivia pre-game countdown renders a full-screen "GET READY" overlay: it seeds from the server
// tick, interpolates downward once per second so it never stalls, and resyncs to each new server tick.
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
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  test('renders the countdown numeral under a GET READY label', () => {
    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(React.createElement(SubstageCountdown, { secondsLeft: 5 }));
    });
    const texts = allText(renderer!.toJSON()).map(String);
    expect(texts).toContain('PREPÁRATE');
    expect(texts).toContain('5');
    act(() => renderer!.unmount());
  });

  test('exposes an assertive "Starting in N" accessibility label', () => {
    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(React.createElement(SubstageCountdown, { secondsLeft: 3 }));
    });
    const node = renderer!.root.findByProps({ testID: 'substage-countdown' });
    expect(node.props.accessibilityLabel).toBe('Comienza en 3');
    expect(node.props.accessibilityLiveRegion).toBe('assertive');
    act(() => renderer!.unmount());
  });

  test('interpolates downward one step per second between server ticks', () => {
    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(React.createElement(SubstageCountdown, { secondsLeft: 5 }));
    });
    expect(allText(renderer!.toJSON()).map(String)).toContain('5');

    act(() => jest.advanceTimersByTime(1000));
    expect(allText(renderer!.toJSON()).map(String)).toContain('4');

    act(() => jest.advanceTimersByTime(1000));
    expect(allText(renderer!.toJSON()).map(String)).toContain('3');
    act(() => renderer!.unmount());
  });

  test('a new server tick resyncs the displayed value', () => {
    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(React.createElement(SubstageCountdown, { secondsLeft: 5 }));
    });
    // Server sends the next number early — the display follows it rather than the local interpolation.
    act(() => {
      renderer!.update(React.createElement(SubstageCountdown, { secondsLeft: 2 }));
    });
    expect(allText(renderer!.toJSON()).map(String)).toContain('2');
    act(() => renderer!.unmount());
  });

  test('interpolation floors at 1 and waits to be unmounted', () => {
    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(React.createElement(SubstageCountdown, { secondsLeft: 1 }));
    });
    act(() => jest.advanceTimersByTime(3000));
    expect(allText(renderer!.toJSON()).map(String)).toContain('1');
    act(() => renderer!.unmount());
  });
});
