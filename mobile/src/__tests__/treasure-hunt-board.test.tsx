import React from 'react';
import { Text } from 'react-native';
import { act, create } from 'react-test-renderer';
import { TreasureHuntBoard } from '@/components/treasure-hunt-board';
import { OperativeCluePortalHost } from '@/components/operative-clue-surface';
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
  { targetSnapshotId: 't1', clueText: 'Follow the north colonnade.', targetName: 'Brass Astrolabe', operativeClueId: null },
];

const TARGETS = [
  { targetSnapshotId: 't1', name: 'Brass Astrolabe', sequenceOrder: 0, latitude: 40.4319, longitude: -3.6883 },
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
        activeTargets: TARGETS,
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
  test('renders score and the target-progress count from props', () => {
    const texts = allText(renderBoard().toJSON());

    expect(texts).toContain('TREASURE HUNT');
    // The active substage name is owned by the shared SubstageProgress component (#171).
    expect(texts).toContain('SCORE');
    expect(texts).toContain('240');
    // Map tab is the default; target progress is a count, not coordinates.
    expect(texts.join('')).toContain('2 / 5 targets');
    // No coordinate strings are rendered as chrome text — coordinates live inside the map WebView.
    expect(texts.join(' ')).not.toContain('°');
  });

  test('renders the real target map on the default MAP tab, fed by activeTargets', () => {
    // #156: the Map tab hosts a Leaflet WebView; the active targets and their coordinates reach the
    // injected HTML, replacing the old labelled stub.
    const renderer = renderBoard();
    const webview = renderer.root.findByProps({ testID: 'target-map-webview' });

    expect(webview).toBeTruthy();
    const html = (webview.props.source as { html: string }).html;
    expect(html).toContain('Brass Astrolabe');
    expect(html).toContain('40.4319');
    expect(html).toContain('-3.6883');
    expect(html).toContain('leaflet');
    // No stub label survives.
    expect(allText(renderer.toJSON())).not.toContain('MAP PREVIEW · STUB');
  });

  test('degrades to a clear empty state when there are no active targets', () => {
    // #156 AC: a target without coordinates (here, no active targets at all) shows an empty state and
    // never mounts a map WebView — no crash.
    const renderer = renderBoard({ activeTargets: [] });
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('NO MAP LOCATION YET');
    expect(
      renderer.root.findAll((n) => n.props?.testID === 'target-map-webview'),
    ).toHaveLength(0);
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
            { targetSnapshotId: 'clue-1', targetName: 'Brass Astrolabe', clueText: 'Follow the north colonnade.', operativeClueId: null },
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
            { targetSnapshotId: 'clue-1', targetName: 'Brass Astrolabe', clueText: 'Follow the north colonnade.', operativeClueId: null },
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

  test('renders a target-less clue with the unified CLUE label (null target)', () => {
    // HU-28: an operative clue carries null targetSnapshotId/targetName and a stable operativeClueId.
    // The card shows the unified "CLUE" label in place of a (missing) target name, plus its text.
    const renderer = renderBoard({
      visibleClues: [
        { targetSnapshotId: null, targetName: null, clueText: 'Look beneath the blue banner.', operativeClueId: 'op-1' },
      ],
    });
    switchTab(renderer, 'CLUES');
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('CLUE');
    expect(texts).not.toContain('OPERATIVE CLUE');
    expect(texts).toContain('Look beneath the blue banner.');
    // No target name is invented for a target-less clue.
    expect(texts).not.toContain('Brass Astrolabe');
  });

  test('renders a target clue and an operative clue together without a key collision', () => {
    // Mixed list: the null-safe key (targetSnapshotId ?? operativeClueId) keeps both cards distinct,
    // so React renders both without a duplicate-key warning.
    const warn = jest.spyOn(console, 'error').mockImplementation(() => {});
    const renderer = renderBoard({
      visibleClues: [
        { targetSnapshotId: 't1', targetName: 'Brass Astrolabe', clueText: 'Follow the north colonnade.', operativeClueId: null },
        { targetSnapshotId: null, targetName: null, clueText: 'Look beneath the blue banner.', operativeClueId: 'op-1' },
      ],
    });
    switchTab(renderer, 'CLUES');
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('Brass Astrolabe');
    expect(texts).toContain('CLUE');
    expect(texts).toContain('Follow the north colonnade.');
    expect(texts).toContain('Look beneath the blue banner.');
    expect(warn.mock.calls.some((args) => String(args[0]).includes('same key'))).toBe(false);
    warn.mockRestore();
  });

  test('renders two operative clues with identical text but distinct ids without collision', () => {
    // Same clueText, different operativeClueId → the key is the id, so no collision on identical text.
    const warn = jest.spyOn(console, 'error').mockImplementation(() => {});
    const renderer = renderBoard({
      visibleClues: [
        { targetSnapshotId: null, targetName: null, clueText: 'Regroup at the fountain.', operativeClueId: 'op-a' },
        { targetSnapshotId: null, targetName: null, clueText: 'Regroup at the fountain.', operativeClueId: 'op-b' },
      ],
    });
    switchTab(renderer, 'CLUES');
    const texts = allText(renderer.toJSON());

    // Both cards render the same text (two occurrences) and both unified CLUE labels.
    expect(texts.filter((t) => t === 'Regroup at the fountain.')).toHaveLength(2);
    expect(texts.filter((t) => t === 'CLUE')).toHaveLength(2);
    expect(warn.mock.calls.some((args) => String(args[0]).includes('same key'))).toBe(false);
    warn.mockRestore();
  });

  test('shows the new-clue indicator when an operative clue arrives while off the CLUES tab', () => {
    const renderer = renderBoard({ visibleClues: [] });
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
            { targetSnapshotId: null, targetName: null, clueText: 'Look beneath the blue banner.', operativeClueId: 'op-1' },
          ],
        }),
      );
    });

    // An operative clue arrived in a later render while still on MAP → indicator appears via clueKey.
    expect(dots()).toHaveLength(1);

    // Viewing the CLUES tab acknowledges it → indicator clears.
    switchTab(renderer, 'CLUES');
    expect(dots()).toHaveLength(0);
  });

  test('pins operative clues above the substage target clues on the CLUES tab', () => {
    // HU-28: the board projection appends operative clues after the target clues, which buries live
    // operator guidance below the mission's own always-visible clues (and off the bottom of the tab).
    const renderer = renderBoard({
      visibleClues: [
        { targetSnapshotId: 't1', targetName: 'Target 1', clueText: 'Find landmark #1.', operativeClueId: null },
        { targetSnapshotId: 't2', targetName: 'Target 2', clueText: 'Find landmark #2.', operativeClueId: null },
        { targetSnapshotId: null, targetName: null, clueText: 'Head right at the fountain.', operativeClueId: 'op-1' },
      ],
    });
    switchTab(renderer, 'CLUES');
    const texts = allText(renderer.toJSON()).map(String);

    expect(texts.indexOf('Head right at the fountain.')).toBeLessThan(texts.indexOf('Find landmark #1.'));
  });

  test('lists the operative group newest-released first, above the target clues', () => {
    // The projection orders each group newest-released first (`LiveSession.CollectVisibleClues` sorts
    // operative clues by CreatedAt descending); the board preserves that order within each group, so
    // the tab as a whole reads newest → oldest with the freshest operator clue at the very top.
    const renderer = renderBoard({
      visibleClues: [
        { targetSnapshotId: 't1', targetName: 'Target 1', clueText: 'Find landmark #1.', operativeClueId: null },
        { targetSnapshotId: null, targetName: null, clueText: 'Newest operative.', operativeClueId: 'op-3' },
        { targetSnapshotId: null, targetName: null, clueText: 'Middle operative.', operativeClueId: 'op-2' },
        { targetSnapshotId: null, targetName: null, clueText: 'Oldest operative.', operativeClueId: 'op-1' },
      ],
    });
    switchTab(renderer, 'CLUES');
    const texts = allText(renderer.toJSON()).map(String);

    const at = (t: string) => texts.indexOf(t);
    expect(at('Newest operative.')).toBeLessThan(at('Middle operative.'));
    expect(at('Middle operative.')).toBeLessThan(at('Oldest operative.'));
    expect(at('Oldest operative.')).toBeLessThan(at('Find landmark #1.'));
  });

  test('renders the header slot and keeps it on every tab', () => {
    // The board owns the whole viewport, so session chrome (substage progress #171, connection
    // banner) rides its sticky header and must survive tab switches.
    const renderer = renderBoard({
      headerSlot: React.createElement(Text, null, 'Now playing: The Vault'),
    });

    expect(allText(renderer.toJSON())).toContain('Now playing: The Vault');
    switchTab(renderer, 'CLUES');
    expect(allText(renderer.toJSON())).toContain('Now playing: The Vault');
    switchTab(renderer, 'TEAMS');
    expect(allText(renderer.toJSON())).toContain('Now playing: The Vault');
  });

  test('offers the leave action in the team strip only when onLeave is given', () => {
    expect(
      renderBoard().root.findAll((n) => n.props?.accessibilityLabel === 'Leave team space'),
    ).toHaveLength(0);

    const onLeave = jest.fn();
    const renderer = renderBoard({ onLeave });
    const leave = renderer.root.findByProps({ accessibilityLabel: 'Leave team space' });
    act(() => {
      (leave.props.onPress as () => void)();
    });

    expect(onLeave).toHaveBeenCalledTimes(1);
  });

  test('the leave action does not disturb the segmented tab order', () => {
    // switchTab indexes the role="button" pressables; the strip's leave control renders after the
    // tabs, so map/clues/teams stay at 0/1/2.
    const renderer = renderBoard({ onLeave: jest.fn() });
    switchTab(renderer, 'CLUES');

    expect(allText(renderer.toJSON())).toContain('Follow the north colonnade.');
  });

  test('no scan launcher renders without onScan', () => {
    const renderer = renderBoard();
    expect(
      renderer.root.findAllByProps({ testID: 'treasure-hunt-scan-button' }),
    ).toHaveLength(0);
  });

  test('the scan launcher invokes onScan', () => {
    const onScan = jest.fn();
    const renderer = renderBoard({ onScan });
    const scanButton = renderer.root.findAllByProps({ testID: 'treasure-hunt-scan-button' })[0];

    act(() => {
      (scanButton.props.onPress as () => void)();
    });

    expect(onScan).toHaveBeenCalledTimes(1);
  });
});

// The arrival toast is portalled out of the board, so these mount the board inside the host that
// LiveTeamSpace provides — without it the portal no-ops and no toast renders anywhere.
describe('TreasureHuntBoard operative-clue arrival toast', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });
  afterEach(() => {
    jest.clearAllTimers();
    jest.useRealTimers();
  });

  const TARGET: VisibleClueDto = {
    targetSnapshotId: 't1', targetName: 'Target 1', clueText: 'Find landmark #1.', operativeClueId: null,
  };
  const OPERATIVE: VisibleClueDto = {
    targetSnapshotId: null, targetName: null, clueText: 'Head right at the fountain.', operativeClueId: 'op-1',
  };

  function renderHosted(visibleClues: VisibleClueDto[]) {
    const element = (clues: VisibleClueDto[]) =>
      React.createElement(
        OperativeCluePortalHost,
        null,
        React.createElement(TreasureHuntBoard, {
          teamDisplayName: 'Lantern Foxes',
          currentScore: 240,
          timerDisplay: { label: '12:47', pct: 63, tone: 'running' },
          resolvedTargets: 2,
          totalActiveTargets: 5,
          visibleClues: clues,
        }),
      );

    let renderer: ReturnType<typeof create> | null = null;
    act(() => {
      renderer = create(element(visibleClues));
    });
    const push = (clues: VisibleClueDto[]) => act(() => renderer!.update(element(clues)));
    return { renderer: renderer!, push };
  }

  // Match the host node only: findAll returns the composite element and its rendered host.
  const toasts = (renderer: ReturnType<typeof create>) =>
    renderer.root.findAll(
      (n) => typeof n.type === 'string' && n.props?.testID === 'operative-clue-toast',
    );

  test('raises the toast when an operative clue arrives while off the CLUES tab', () => {
    const { renderer, push } = renderHosted([TARGET]);
    expect(toasts(renderer)).toHaveLength(0);

    push([TARGET, OPERATIVE]);

    expect(toasts(renderer)).toHaveLength(1);
    expect(allText(toasts(renderer)[0])).toContain('Head right at the fountain.');
  });

  test('raises the toast when a scheduled/target clue arrives too (item 4/5 convergence)', () => {
    // Pre-assigned/scheduled clues are target clues; a newly-released one must toast + dot, not dot only.
    const { renderer, push } = renderHosted([TARGET]);
    expect(toasts(renderer)).toHaveLength(0);

    const NEW_TARGET: VisibleClueDto = {
      targetSnapshotId: 't2', targetName: 'Target 2', clueText: 'Scan the second landmark.', operativeClueId: null,
    };
    push([TARGET, NEW_TARGET]);

    expect(toasts(renderer)).toHaveLength(1);
    expect(allText(toasts(renderer)[0])).toContain('Scan the second landmark.');
  });

  test('tapping the toast opens the CLUES tab', () => {
    const { renderer, push } = renderHosted([TARGET]);
    push([TARGET, OPERATIVE]);

    act(() => {
      (renderer.root.findByProps({ accessibilityLabel: 'Open clues' }).props.onPress as () => void)();
    });

    // The Clues tab is the durable home here, so the toast hands the reveal back to the board.
    expect(allText(renderer.toJSON())).toContain('Head right at the fountain.');
  });

  test('toasts the newest clue when several arrive at once', () => {
    // A batch (e.g. a reconnect delivering a backlog) raises one toast. The projection orders the
    // operative group newest-first, so the head of the batch is the clue worth signalling.
    const { renderer, push } = renderHosted([TARGET]);
    push([
      TARGET,
      { targetSnapshotId: null, targetName: null, clueText: 'Newest of the batch.', operativeClueId: 'op-2' },
      OPERATIVE,
    ]);

    expect(toasts(renderer)).toHaveLength(1);
    expect(allText(toasts(renderer)[0])).toContain('Newest of the batch.');
  });

  test('stays quiet when the clue arrives while the CLUES tab is already open', () => {
    const { renderer, push } = renderHosted([TARGET]);
    switchTab(renderer, 'CLUES');

    push([TARGET, OPERATIVE]);

    // The tab already shows it — no toast on top of its own list.
    expect(toasts(renderer)).toHaveLength(0);
    expect(allText(renderer.toJSON())).toContain('Head right at the fountain.');
  });
});
