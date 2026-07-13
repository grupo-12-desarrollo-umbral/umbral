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
  test('renders score and the map-stub target progress from props', () => {
    const texts = allText(renderBoard().toJSON());

    expect(texts).toContain('TREASURE HUNT');
    // The active substage name is owned by the shared SubstageProgress component (#171).
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

  test('surfaces a newly-revealed clue when visibleClues grows (empty → first clue)', () => {
    // HU-26 reveal: the released clue arrives via a board push; the component is prop-driven, so a
    // re-render with the grown visibleClues replaces the empty state with the clue's name + text.
    const renderer = renderBoard({ visibleClues: [] });
    switchTab(renderer, 'CLUES');
    expect(allText(renderer.toJSON())).toContain('No clues yet.');

    act(() => {
      renderer.update(
        React.createElement(TreasureHuntBoard, {
          teamDisplayName: 'Lantern Foxes',
          currentScore: 240,
          timerDisplay: { label: '12:47', pct: 63, tone: 'running' },
          resolvedTargets: 2,
          totalActiveTargets: 5,
          visibleClues: [
            { targetSnapshotId: 'clue-1', targetName: 'Brass Astrolabe', clueText: 'Follow the north colonnade.' },
          ],
        }),
      );
    });

    const texts = allText(renderer.toJSON());
    expect(texts).not.toContain('No clues yet.');
    expect(texts).toContain('Brass Astrolabe');
    expect(texts).toContain('Follow the north colonnade.');
  });

  test('shows the new-clue indicator on growth while CLUES is unviewed, clears on view', () => {
    // HU-26 reveal affordance: an ember dot on the CLUES segment flags a clue that arrived after
    // mount; viewing the CLUES tab acknowledges it.
    const renderer = renderBoard({ visibleClues: [] });
    // Match the host node only: findAll returns both the composite View element and its rendered
    // host, so filter to the host (string type) to count one node per rendered dot.
    const dots = () =>
      renderer.root.findAll(
        (n) => typeof n.type === 'string' && n.props?.testID === 'treasure-hunt-clue-indicator',
      );

    // Default MAP tab, no clues at mount → no indicator.
    expect(dots()).toHaveLength(0);

    act(() => {
      renderer.update(
        React.createElement(TreasureHuntBoard, {
          teamDisplayName: 'Lantern Foxes',
          currentScore: 240,
          timerDisplay: { label: '12:47', pct: 63, tone: 'running' },
          resolvedTargets: 2,
          totalActiveTargets: 5,
          visibleClues: [
            { targetSnapshotId: 'clue-1', targetName: 'Brass Astrolabe', clueText: 'Follow the north colonnade.' },
          ],
        }),
      );
    });

    // A clue arrived in a later render while still on MAP → indicator appears.
    expect(dots()).toHaveLength(1);

    // Viewing the CLUES tab acknowledges it → indicator clears.
    switchTab(renderer, 'CLUES');
    expect(dots()).toHaveLength(0);
  });

  test('does not show the new-clue indicator when clues do not grow (score-only re-projection)', () => {
    const renderer = renderBoard({ visibleClues: CLUES });
    // Match the host node only: findAll returns both the composite View element and its rendered
    // host, so filter to the host (string type) to count one node per rendered dot.
    const dots = () =>
      renderer.root.findAll(
        (n) => typeof n.type === 'string' && n.props?.testID === 'treasure-hunt-clue-indicator',
      );

    expect(dots()).toHaveLength(0);

    act(() => {
      renderer.update(
        React.createElement(TreasureHuntBoard, {
          teamDisplayName: 'Lantern Foxes',
          currentScore: 999,
          timerDisplay: { label: '12:47', pct: 63, tone: 'running' },
          resolvedTargets: 2,
          totalActiveTargets: 5,
          visibleClues: CLUES,
        }),
      );
    });

    // Same clue ids, only the score changed → no new-clue signal.
    expect(dots()).toHaveLength(0);
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
