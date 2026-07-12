import React from 'react';
import { act, create } from 'react-test-renderer';
import { TreasureHuntBoard } from '@/components/treasure-hunt-board';
import type { VisibleClueDto } from '@/lib/realtime/team-board-types';

type TreeNode = {
  props?: Record<string, unknown>;
  children?: (TreeNode | string | number)[] | null;
};

function allText(node: unknown): (string | number)[] {
  if (node === null || node === undefined) return [];
  if (typeof node === 'string' || typeof node === 'number') return [node];
  if (Array.isArray(node)) return node.flatMap(allText);
  const n = node as TreeNode;
  return allText(n.children ?? []);
}

const CLUES: VisibleClueDto[] = [
  { targetSnapshotId: 't1', clueText: 'Follow the north colonnade.', targetName: 'Brass Astrolabe' },
];

function renderBoard(overrides?: Partial<React.ComponentProps<typeof TreasureHuntBoard>>) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(TreasureHuntBoard, {
        teamDisplayName: 'Lantern Foxes',
        currentScore: 240,
        substageTitle: 'The Cartographer’s Vault',
        timerDisplay: { label: '12:47', pct: 63, tone: 'running' },
        resolvedTargets: 2,
        totalActiveTargets: 5,
        visibleClues: CLUES,
        ...overrides,
      }),
    );
  });
  return renderer!;
}

const TAB_INDEX = { MAP: 0, CLUES: 1, TEAMS: 2 } as const;

function switchTab(renderer: ReturnType<typeof create>, tab: keyof typeof TAB_INDEX) {
  // The segmented control renders exactly three role="button" pressables in
  // map/clues/teams order; fire the onPress of the matching one.
  const pressables = renderer.root.findAll(
    (n) => n.props?.accessibilityRole === 'button' && typeof n.props?.onPress === 'function',
  );
  act(() => {
    (pressables[TAB_INDEX[tab]].props.onPress as () => void)();
  });
}

describe('TreasureHuntBoard', () => {
  test('renders score, substage title and the map-stub target progress from props', () => {
    const texts = allText(renderBoard().toJSON());

    expect(texts).toContain('TREASURE HUNT');
    expect(texts).toContain('The Cartographer’s Vault');
    expect(texts).toContain('SCORE');
    expect(texts).toContain('240');
    // Map tab is the default; target progress is a count, not coordinates.
    expect(texts).toContain('MAP PREVIEW · STUB');
    expect(texts.join('')).toContain('2 / 5 targets');
    // No coordinate strings from the old prototype.
    expect(texts.join(' ')).not.toContain('°');
  });

  test('exposes target progress via an accessibility label', () => {
    const renderer = renderBoard();
    const node = renderer.root.findByProps({ accessibilityLabel: 'Targets resolved 2 of 5' });
    expect(node).toBeTruthy();
  });

  test('renders visible clues as cards on the clues tab (no scope chip)', () => {
    const renderer = renderBoard();
    switchTab(renderer, 'CLUES');
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('Brass Astrolabe');
    expect(texts).toContain('Follow the north colonnade.');
    // VisibleClueDto has no scope field; no team/global chip is invented.
    expect(texts).not.toContain('TEAM');
    expect(texts).not.toContain('GLOBAL');
  });

  test('shows an empty panel when there are no clues', () => {
    const renderer = renderBoard({ visibleClues: [] });
    switchTab(renderer, 'CLUES');
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('No clues yet.');
  });

  test('marks other-team cards as placeholder on the teams tab', () => {
    const renderer = renderBoard();
    switchTab(renderer, 'TEAMS');
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('Lantern Foxes');
    expect(texts).toContain('PLACEHOLDER');
    expect(texts.join(' ')).toContain('Sample standings — not live yet');
  });

  test('renders the shared session timer label', () => {
    const texts = allText(renderBoard().toJSON());
    expect(texts).toContain('12:47');
  });
});
