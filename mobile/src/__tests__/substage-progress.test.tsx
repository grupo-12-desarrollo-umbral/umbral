import React from 'react';
import { act, create } from 'react-test-renderer';
import { SubstageProgress } from '@/components/substage-progress';
import type { ParticipantTeamBoardDto } from '@/lib/realtime/team-board-types';

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

function render(board: ParticipantTeamBoardDto) {
  let renderer: ReturnType<typeof create> | null = null;
  act(() => {
    renderer = create(React.createElement(SubstageProgress, { board }));
  });
  return renderer!;
}

function chips(renderer: ReturnType<typeof create>) {
  // Host nodes only, matched by the chip's `Substage N:` label so the container's
  // "Substage progress" label and RN composite wrappers don't double-count.
  return renderer.root.findAll(
    (n) =>
      typeof n.type === 'string' &&
      typeof n.props?.accessibilityLabel === 'string' &&
      /^Subetapa \d+:/.test(n.props.accessibilityLabel as string),
  );
}

const BASE: ParticipantTeamBoardDto = {
  liveSessionId: 'sess-1',
  missionTitle: 'City Quest',
  teamId: 'team-1',
  teamDisplayName: 'Lantern Foxes',
  teamCode: 'LF-01',
  currentScore: 0,
  timer: {} as never,
  activeSubstage: {
    substageSnapshotId: 'sub-2',
    playMode: 'TreasureHunt',
    title: 'The Vault',
    totalActiveTargets: 3,
    resolvedTargets: 0,
    activeQuestionSequenceOrder: null,
    activeQuestionTimeLimitSeconds: null,
  },
  substages: [
    { substageSnapshotId: 'sub-1', title: 'Opening Trivia', sequenceOrder: 0, playMode: 'Trivia', status: 'Completed' },
    { substageSnapshotId: 'sub-2', title: 'The Vault', sequenceOrder: 1, playMode: 'TreasureHunt', status: 'Active' },
  ],
  visibleClues: [],
  activeTargets: [],
};

describe('SubstageProgress', () => {
  test('multi-substage: shows the active name and a labelled, ordered chip per substage', () => {
    const renderer = render(BASE);
    const texts = allText(renderer.toJSON());

    expect(texts).toContain('The Vault');
    expect(texts).toContain('Trivia');
    expect(texts).toContain('Búsqueda del tesoro');
    expect(chips(renderer)).toHaveLength(2);

    const active = chips(renderer).filter(
      (n) => (n.props.accessibilityState as { selected?: boolean })?.selected === true,
    );
    expect(active).toHaveLength(1);
    expect(active[0].props.accessibilityLabel as string).toContain('active');
  });

  test('shows the mission title alongside the active substage', () => {
    const renderer = render(BASE);
    expect(allText(renderer.toJSON())).toContain('City Quest');
  });

  test('single-substage: shows only the name, no chips', () => {
    const single: ParticipantTeamBoardDto = {
      ...BASE,
      activeSubstage: { ...BASE.substages[0], totalActiveTargets: 0, resolvedTargets: 0, activeQuestionSequenceOrder: null, activeQuestionTimeLimitSeconds: null },
      substages: [{ substageSnapshotId: 'sub-1', title: 'Opening Trivia', sequenceOrder: 0, playMode: 'Trivia', status: 'Active' }],
    };
    const renderer = render(single);

    expect(allText(renderer.toJSON())).toContain('Opening Trivia');
    expect(chips(renderer)).toHaveLength(0);
  });

  test('falls back to the Active substage title when activeSubstage is absent', () => {
    const noActiveContext: ParticipantTeamBoardDto = { ...BASE, activeSubstage: null };
    const renderer = render(noActiveContext);

    expect(allText(renderer.toJSON())).toContain('The Vault');
  });
});
