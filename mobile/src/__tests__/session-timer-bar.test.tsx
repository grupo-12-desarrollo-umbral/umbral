import React from 'react';
import { act, create } from 'react-test-renderer';
import { SessionTimerBar } from '@/components/session-timer-bar';
import { UNAVAILABLE_TIMER_DISPLAY, type TimerDisplay } from '@/lib/realtime/timer-types';

function render(display: TimerDisplay) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(React.createElement(SessionTimerBar, { display }));
  });
  return renderer!.toJSON();
}

function allText(node: unknown): string[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string') return [node];
  if (Array.isArray(node)) return (node as unknown[]).flatMap(allText);
  const n = node as { children?: unknown };
  return allText(n.children ?? []);
}

type TreeNode = {
  type?: string;
  props?: Record<string, unknown>;
  children?: TreeNode[] | null;
};

function findByProp(node: unknown, key: string, value: unknown): TreeNode | null {
  if (!node || typeof node !== 'object') return null;
  const n = node as TreeNode;
  if (n.props?.[key] === value) return n;
  if (Array.isArray(n.children)) {
    for (const child of n.children) {
      const found = findByProp(child, key, value);
      if (found) return found;
    }
  }
  return null;
}

describe('SessionTimerBar', () => {
  test('running tone shows time label and Running chip', () => {
    const texts = allText(render({ label: '05:00', pct: 50, tone: 'running' }));
    expect(texts).toContain('05:00');
    expect(texts).toContain('En curso');
  });

  test('paused tone shows Paused chip', () => {
    const texts = allText(render({ label: '03:30', pct: 30, tone: 'paused' }));
    expect(texts).toContain('03:30');
    expect(texts).toContain('En pausa');
  });

  test('expired tone shows 00:00 and Expired chip', () => {
    const texts = allText(render({ label: '00:00', pct: 0, tone: 'expired' }));
    expect(texts).toContain('00:00');
    expect(texts).toContain('Expirado');
  });

  test('unavailable shows --:-- and Unavailable chip', () => {
    const texts = allText(render(UNAVAILABLE_TIMER_DISPLAY));
    expect(texts).toContain('--:--');
    expect(texts).toContain('No disponible');
  });

  test('sets accessibilityRole=progressbar with correct value', () => {
    const tree = render({ label: '05:00', pct: 50, tone: 'running' });
    const bar = findByProp(tree, 'accessibilityRole', 'progressbar');
    expect(bar).not.toBeNull();
    expect(bar?.props?.accessibilityValue).toEqual({ min: 0, max: 100, now: 50 });
  });

  test('accessibilityLabel contains the time label', () => {
    const tree = render({ label: '02:30', pct: 25, tone: 'running' });
    const bar = findByProp(tree, 'accessibilityRole', 'progressbar');
    expect(bar?.props?.accessibilityLabel).toContain('02:30');
  });

  test('clamps pct above 100 to 100', () => {
    const tree = render({ label: '05:00', pct: 150, tone: 'running' });
    const json = JSON.stringify(tree);
    expect(json).toContain('"100%"');
    expect(json).not.toContain('"150%"');
  });

  test('clamps pct below 0 to 0', () => {
    const tree = render({ label: '05:00', pct: -5, tone: 'running' });
    const json = JSON.stringify(tree);
    expect(json).toContain('"0%"');
    expect(json).not.toContain('"-5%"');
  });

  test('running → paused → expired → unavailable renders distinct chip labels', () => {
    const tones: TimerDisplay['tone'][] = ['running', 'paused', 'expired', 'unavailable'];
    const labels = ['En curso', 'En pausa', 'Expirado', 'No disponible'];

    tones.forEach((tone, i) => {
      const texts = allText(render({ label: '02:00', pct: 40, tone }));
      expect(texts).toContain(labels[i]);
    });
  });
});
