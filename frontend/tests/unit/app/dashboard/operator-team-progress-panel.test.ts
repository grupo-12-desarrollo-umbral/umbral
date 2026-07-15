// HU-24A: the operator live session panel renders the session-state readout plus the ordered
// per-team progress rollup (score + target progress / active-question order), and the
// unauthorized / error / no-data states. Progress is target-based, never clue-based.
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/operatorTeamProgressPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { OperatorTeamProgressPanel } from '@/app/dashboard/OperatorTeamProgressPanel'
import type { OperatorSessionPanelDto } from '@/app/lib/definitions'

const panel: OperatorSessionPanelDto = {
  liveSessionId: 's1',
  state: 'Active',
  timer: { liveSessionId: 's1', teamId: null } as OperatorSessionPanelDto['timer'],
  teamProgress: [
    {
      teamId: 'team-a',
      referenceTeamId: 'ref-team-a',
      teamCode: 'AAA',
      displayName: 'Alpha',
      score: 0,
      releasedClueCount: 0,
      activeSubstage: {
        substageSnapshotId: 'sub-1',
        playMode: 'TreasureHunt',
        title: 'Hunt',
        totalActiveTargets: 4,
        resolvedTargets: 0,
        activeQuestionSequenceOrder: null,
        activeQuestionTimeLimitSeconds: null,
      },
    },
    {
      teamId: 'team-b',
      referenceTeamId: 'ref-team-b',
      teamCode: 'BBB',
      displayName: 'Bravo',
      score: 15,
      releasedClueCount: 2,
      activeSubstage: {
        substageSnapshotId: 'sub-2',
        playMode: 'Trivia',
        title: 'Quiz',
        totalActiveTargets: 0,
        resolvedTargets: 0,
        activeQuestionSequenceOrder: 3,
        activeQuestionTimeLimitSeconds: 30,
      },
    },
  ],
}

function render(props: Partial<Parameters<typeof OperatorTeamProgressPanel>[0]> = {}): string {
  return renderToStaticMarkup(
    createElement(OperatorTeamProgressPanel, {
      panel,
      unauthorized: false,
      error: null,
      loading: false,
      ...props,
    }),
  )
}

describe('OperatorTeamProgressPanel', () => {
  it('renders the session-state readout and an ordered per-team rollup', () => {
    const html = render()
    expect(html).toContain('data-testid="panel-session-state"')
    expect(html).toContain('Active')
    expect(html).toContain('data-testid="team-progress-team-a"')
    expect(html).toContain('data-testid="team-progress-team-b"')
    // Alpha (treasure-hunt) precedes Bravo (backend teamCode order).
    expect(html.indexOf('team-progress-team-a')).toBeLessThan(html.indexOf('team-progress-team-b'))
  })

  it('shows the target progress for a treasure-hunt team (resolved/total)', () => {
    const html = render()
    expect(html).toContain('data-testid="team-progress-targets-team-a"')
    expect(html).toContain('0/4 targets')
  })

  it('shows the active-question order for a trivia team', () => {
    const html = render()
    expect(html).toContain('data-testid="team-progress-question-team-b"')
    expect(html).toContain('Question 3')
  })

  it('renders a zero score as "0 pts"', () => {
    const html = render()
    expect(html).toContain('data-testid="team-progress-score-team-a"')
    expect(html).toContain('0 pts')
    expect(html).toContain('15 pts')
  })

  it('shows a per-team released-clue tally only for teams with clues', () => {
    const html = render()
    // Bravo has 2 clues; Alpha has none, so no clue tally is rendered for it.
    expect(html).toContain('data-testid="team-progress-clues-team-b"')
    expect(html).toContain('2 clues')
    expect(html).not.toContain('data-testid="team-progress-clues-team-a"')
  })

  it('rolls up how many teams have clues released', () => {
    const html = render()
    expect(html).toContain('data-testid="panel-clue-rollup"')
    expect(html).toContain('Clues released to 1 team.')
  })

  it('omits the clue rollup when no team has released clues', () => {
    const html = render({
      panel: {
        ...panel,
        teamProgress: panel.teamProgress.map((team) => ({ ...team, releasedClueCount: 0 })),
      },
    })
    expect(html).not.toContain('data-testid="panel-clue-rollup"')
  })

  it('renders the not-authorized state and no team data when unauthorized', () => {
    const html = render({ unauthorized: true })
    expect(html).toContain('data-testid="panel-unauthorized"')
    expect(html).not.toContain('team-progress-team-a')
    expect(html).not.toContain('Alpha')
  })

  it('renders a retryable error state (not "not authorized") on a transient read failure', () => {
    const html = render({ error: 'boom' })
    expect(html).toContain('data-testid="panel-error"')
    expect(html).not.toContain('data-testid="panel-unauthorized"')
    expect(html).not.toContain('team-progress-team-a')
  })

  it('renders the no-data state when the panel is null', () => {
    const html = render({ panel: null })
    expect(html).toContain('data-testid="panel-no-teams"')
    expect(html).toContain('No progress data yet.')
  })

  it('renders a loading note when the panel is null and loading', () => {
    const html = render({ panel: null, loading: true })
    expect(html).toContain('data-testid="panel-no-teams"')
    expect(html).toContain('Loading session progress…')
  })

  it('renders the no-teams state when the panel has an empty roster', () => {
    const html = render({ panel: { ...panel, teamProgress: [] } })
    expect(html).toContain('data-testid="panel-no-teams"')
    expect(html).toContain('No teams associated yet.')
    expect(html).toContain('data-testid="panel-session-state"')
  })
})
