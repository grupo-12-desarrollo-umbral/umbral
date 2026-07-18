import React from 'react';
import { act, create } from 'react-test-renderer';
import { TargetMap, type TargetMapTarget } from '@/components/target-map';

type TreeNode = { children?: (TreeNode | string | number)[] | null };

function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  return allText((node as TreeNode).children ?? []);
}

const TARGETS: TargetMapTarget[] = [
  { targetSnapshotId: 't1', name: 'Brass Astrolabe', latitude: 40.4319, longitude: -3.6883 },
];

function render(targets: readonly TargetMapTarget[]) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(React.createElement(TargetMap, { targets }));
  });
  return renderer!;
}

describe('TargetMap', () => {
  test('mounts a WebView carrying the target coordinates when targets are placeable', () => {
    const renderer = render(TARGETS);
    const webview = renderer.root.findByProps({ testID: 'target-map-webview' });
    const html = (webview.props.source as { html: string }).html;

    expect(html).toContain('40.4319');
    expect(html).toContain('Brass Astrolabe');
  });

  test('shows the empty state (no WebView) when the target list is empty', () => {
    const renderer = render([]);

    expect(allText(renderer.toJSON())).toContain('AÚN SIN UBICACIÓN EN EL MAPA');
    expect(renderer.root.findAll((n) => n.props?.testID === 'target-map-webview')).toHaveLength(0);
  });

  test('treats the 0,0 sentinel as unplaceable rather than pinning Null Island', () => {
    // A target the operator never placed reads back as 0,0 (the backend has no null coordinate). It
    // must fall to the empty state, not drop a pin in the Atlantic.
    const renderer = render([
      { targetSnapshotId: 't1', name: 'Unplaced', latitude: 0, longitude: 0 },
    ]);

    expect(allText(renderer.toJSON())).toContain('AÚN SIN UBICACIÓN EN EL MAPA');
    expect(renderer.root.findAll((n) => n.props?.testID === 'target-map-webview')).toHaveLength(0);
  });

  test('keeps placed targets when a sibling is unplaced, centring on the first placed one', () => {
    const renderer = render([
      { targetSnapshotId: 't1', name: 'Unplaced', latitude: 0, longitude: 0 },
      ...TARGETS,
    ]);
    const webview = renderer.root.findByProps({ testID: 'target-map-webview' });
    const html = (webview.props.source as { html: string }).html;

    expect(html).toContain('setView([40.4319, -3.6883]');
    expect(html).not.toContain('Unplaced');
  });

  test('treats a target with non-finite coordinates as unplaceable and degrades gracefully', () => {
    // Defensive: a malformed payload (NaN lat/lng) must not reach the map — it falls to the empty state.
    const renderer = render([
      { targetSnapshotId: 't1', name: 'Ghost', latitude: Number.NaN, longitude: 10 },
    ]);

    expect(allText(renderer.toJSON())).toContain('AÚN SIN UBICACIÓN EN EL MAPA');
    expect(renderer.root.findAll((n) => n.props?.testID === 'target-map-webview')).toHaveLength(0);
  });
});
