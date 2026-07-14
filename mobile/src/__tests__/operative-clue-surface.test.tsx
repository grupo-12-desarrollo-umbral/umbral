// HU-28 — the unified clue surface: one collapsed CLUES chip (durable store + dot) + arrival toast.
// Prop-driven off board.visibleClues; every clue kind (operative, substage-initial/mission, target)
// converges on the same toast + chip. The toast raises once per genuinely-new clue.
import React from 'react';
import { act, create } from 'react-test-renderer';
import {
  OperativeCluePortalHost,
  OperativeClueSurface,
} from '@/components/operative-clue-surface';
import type { VisibleClueDto } from '@/lib/realtime/team-board-types';

type TreeNode = {
  type?: unknown;
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

const OP = (id: string, text: string): VisibleClueDto => ({
  targetSnapshotId: null,
  targetName: null,
  clueText: text,
  operativeClueId: id,
});

const TARGET: VisibleClueDto = {
  targetSnapshotId: 't1',
  targetName: 'Brass Astrolabe',
  clueText: 'Follow the north colonnade.',
  operativeClueId: null,
};

// Mission (substage-initial / scheduled) clue: both ids null — must flow through the same surface.
const MISSION = (text: string): VisibleClueDto => ({
  targetSnapshotId: null,
  targetName: null,
  clueText: text,
  operativeClueId: null,
});

// Host node only (findAll returns both the composite and its rendered host).
function byTestId(renderer: ReturnType<typeof create>, id: string) {
  return renderer.root.findAll(
    (n) => typeof n.type === 'string' && n.props?.testID === id,
  );
}

function renderSurface(clues: VisibleClueDto[]) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(
      React.createElement(
        OperativeCluePortalHost,
        null,
        React.createElement(OperativeClueSurface, { visibleClues: clues }),
      ),
    );
  });
  return renderer!;
}

function update(renderer: ReturnType<typeof create>, clues: VisibleClueDto[]) {
  act(() => {
    renderer.update(
      React.createElement(
        OperativeCluePortalHost,
        null,
        React.createElement(OperativeClueSurface, { visibleClues: clues }),
      ),
    );
  });
}

// The chip's accessibilityLabel carries the "N new" unseen signal (the ember dot has no testID).
function chip(renderer: ReturnType<typeof create>) {
  return renderer.root.findAll(
    (n) =>
      typeof n.props?.accessibilityLabel === 'string' &&
      (n.props.accessibilityLabel as string).startsWith('Clues,') &&
      typeof n.props?.onPress === 'function',
  )[0];
}

function chipLabel(renderer: ReturnType<typeof create>): string {
  return chip(renderer).props.accessibilityLabel as string;
}

function pressChip(renderer: ReturnType<typeof create>) {
  act(() => {
    (chip(renderer).props.onPress as () => void)();
  });
}

function pressTestId(renderer: ReturnType<typeof create>, id: string) {
  const pressable = renderer.root.findAll(
    (n) => n.props?.testID === id && typeof n.props?.onPress === 'function',
  )[0];
  act(() => {
    (pressable.props.onPress as () => void)();
  });
}

describe('OperativeClueSurface', () => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => {
    act(() => {
      jest.runOnlyPendingTimers();
    });
    jest.useRealTimers();
  });

  test('renders a collapsed CLUES chip with the clue count', () => {
    const renderer = renderSurface([OP('op-1', 'Look beneath the blue banner.')]);
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('CLUES · 1');
    // Collapsed by default — the artifact body is not rendered yet.
    expect(texts).not.toContain('Look beneath the blue banner.');
    // No per-kind "OPERATIVE CLUE" wording anymore.
    expect(texts).not.toContain('OPERATIVE CLUE');
  });

  test('counts every clue kind in the one chip (operative + substage-initial)', () => {
    const renderer = renderSurface([MISSION('Initial guidance.'), OP('op-1', 'An operator push.')]);
    expect(allText(renderer.toJSON())).toContain('CLUES · 2');
  });

  test('renders nothing when there are no clues', () => {
    // The portal host wrapper still mounts, but the surface itself contributes no content.
    const renderer = renderSurface([]);
    expect(allText(renderer.toJSON())).toEqual([]);
  });

  test('expanding the chip reveals the parchment artifact (label + text)', () => {
    const renderer = renderSurface([OP('op-1', 'Look beneath the blue banner.')]);
    pressChip(renderer);
    const texts = allText(renderer.toJSON());

    // Target-less clues read the unified "CLUE" label — never "OPERATIVE"/"MISSION".
    expect(texts).toContain('CLUE');
    expect(texts).toContain('Look beneath the blue banner.');
    expect(texts).not.toContain('OPERATIVE CLUE');
    expect(texts).not.toContain('MISSION CLUE');
  });

  test('clues present at mount are already seen — no toast, no unseen dot', () => {
    const renderer = renderSurface([OP('op-1', 'Look beneath the blue banner.')]);
    expect(byTestId(renderer, 'operative-clue-toast')).toHaveLength(0);
    // No ", N new" suffix on the chip label.
    expect(chipLabel(renderer)).toBe('Clues, 1');
  });

  test('a newly-arrived operative clue flags the chip as unseen and raises the toast', () => {
    const renderer = renderSurface([]);
    // Empty at mount → the surface contributes no content.
    expect(allText(renderer.toJSON())).toEqual([]);

    update(renderer, [OP('op-1', 'Look beneath the blue banner.')]);

    // Chip shows the unseen signal…
    expect(chipLabel(renderer)).toBe('Clues, 1, 1 new');
    // …and the toast fires with the label + the (single) clue line.
    expect(byTestId(renderer, 'operative-clue-toast')).toHaveLength(1);
    const texts = allText(renderer.toJSON());
    expect(texts).toContain('NEW CLUE');
    expect(texts).toContain('Look beneath the blue banner.');
  });

  test('a substage-initial (mission) clue arriving raises the toast AND joins the chip', () => {
    // Item 2: initial clues flow through the SAME toast + dropdown + dot as operative clues.
    const renderer = renderSurface([]);
    update(renderer, [MISSION('The gate opens at dusk.')]);

    // Toast (a) …
    expect(byTestId(renderer, 'operative-clue-toast')).toHaveLength(1);
    expect(allText(renderer.toJSON())).toContain('The gate opens at dusk.');
    // Dot (b) — the chip flags it unseen …
    expect(chipLabel(renderer)).toBe('Clues, 1, 1 new');
    // Dropdown — expanding shows it, with no "MISSION CLUE" separate-surface wording.
    pressChip(renderer);
    const texts = allText(renderer.toJSON());
    expect(texts).toContain('The gate opens at dusk.');
    expect(texts).not.toContain('MISSION CLUE');
  });

  test('a scheduled/target clue arriving also raises the toast (item 4)', () => {
    const renderer = renderSurface([]);
    update(renderer, [TARGET]);
    expect(byTestId(renderer, 'operative-clue-toast')).toHaveLength(1);
    expect(allText(renderer.toJSON())).toContain('Follow the north colonnade.');
  });

  test('a re-projection that adds no clue raises no toast', () => {
    const renderer = renderSurface([OP('op-1', 'Look beneath the blue banner.')]);
    // Same clue set re-projected (e.g. a score-only update) → nothing new to toast.
    update(renderer, [OP('op-1', 'Look beneath the blue banner.')]);
    expect(byTestId(renderer, 'operative-clue-toast')).toHaveLength(0);
  });

  test('tapping the toast opens the list and reveals the artifact', () => {
    const renderer = renderSurface([]);
    update(renderer, [OP('op-1', 'Look beneath the blue banner.')]);
    expect(byTestId(renderer, 'operative-clue-toast')).toHaveLength(1);
    // Collapsed → artifact not shown yet.
    expect(allText(renderer.toJSON())).not.toContain('CLUE');

    pressTestId(renderer, 'operative-clue-toast');

    // The chip expands → the durable parchment artifact is now shown and the chip is acknowledged.
    const texts = allText(renderer.toJSON());
    expect(texts).toContain('CLUE');
    expect(texts).toContain('Look beneath the blue banner.');
    expect(chipLabel(renderer)).toBe('Clues, 1');
  });
});
