// HU-36A: the operator answered/not-answered board renders roster rows keyed by run-time team id,
// flips a row to data-answered="true" when the team is answered, and shows the empty / not-authorized
// states. Load-bearing assertion: NO option / correctness / points copy ever appears (no-leak invariant).
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/answeredMonitorPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { AnsweredMonitorPanel, type AnsweredTeamRow } from '@/app/dashboard/AnsweredMonitorPanel'

const rosterAllUnanswered: AnsweredTeamRow[] = [
  { runtimeTeamId: 'team-a', displayName: 'Alpha', teamCode: 'AAA', answered: false, answeredAt: null },
  { runtimeTeamId: 'team-b', displayName: 'Bravo', teamCode: 'BBB', answered: false, answeredAt: null },
]

function render(props: Partial<Parameters<typeof AnsweredMonitorPanel>[0]> = {}): string {
  return renderToStaticMarkup(
    createElement(AnsweredMonitorPanel, {
      activeQuestionOrder: 2,
      teams: rosterAllUnanswered,
      unauthorized: false,
      error: null,
      loading: false,
      ...props,
    }),
  )
}

describe('AnsweredMonitorPanel', () => {
  it('renders every roster team as not-answered-yet when none have answered', () => {
    const html = render()
    expect(html).toContain('data-testid="team-answer-status-team-a"')
    expect(html).toContain('data-testid="team-answer-status-team-b"')
    // both rows carry data-answered="false"
    expect(html.match(/data-answered="false"/g)?.length).toBeGreaterThanOrEqual(2)
    expect(html).not.toContain('data-answered="true"')
    expect(html).toContain('0 / 2 answered')
  })

  it('flips a row to data-answered="true" when that team is in the answered set', () => {
    const teams: AnsweredTeamRow[] = [
      { runtimeTeamId: 'team-a', displayName: 'Alpha', teamCode: 'AAA', answered: true, answeredAt: '2026-07-10T10:00:00Z' },
      { runtimeTeamId: 'team-b', displayName: 'Bravo', teamCode: 'BBB', answered: false, answeredAt: null },
    ]
    const html = render({ teams })
    expect(html).toMatch(/data-testid="team-answer-status-team-a"[^>]*data-answered="true"/)
    expect(html).toContain('1 / 2 answered')
  })

  it('shows the active question sequence order only (never a prompt)', () => {
    const html = render({ activeQuestionOrder: 5 })
    expect(html).toContain('data-testid="answered-monitor-active-question"')
    expect(html).toContain('Question 5')
  })

  it('renders the empty state when no trivia question is active', () => {
    const html = render({ activeQuestionOrder: null })
    expect(html).toContain('data-testid="answered-monitor-empty"')
    expect(html).not.toContain('team-answer-status-')
  })

  it('renders the empty state when no question is active, regardless of roster', () => {
    const html = render({ activeQuestionOrder: null, teams: [] })
    expect(html).toContain('data-testid="answered-monitor-empty"')
  })

  it('renders the waiting-for-roster state (not "no active question") when a question is active but the roster is empty', () => {
    const html = render({ activeQuestionOrder: 3, teams: [] })
    expect(html).toContain('data-testid="answered-monitor-no-roster"')
    expect(html).toContain('Question 3')
    expect(html).not.toContain('data-testid="answered-monitor-empty"')
  })

  it('renders the not-authorized state and no team data when unauthorized', () => {
    const html = render({ unauthorized: true, teams: rosterAllUnanswered })
    expect(html).toContain('data-testid="answered-monitor-unauthorized"')
    expect(html).not.toContain('team-answer-status-')
    expect(html).not.toContain('Alpha')
  })

  it('renders a retryable error state (not "not authorized") on a transient read failure', () => {
    const html = render({ error: 'boom', teams: rosterAllUnanswered })
    expect(html).toContain('data-testid="answered-monitor-error"')
    expect(html).not.toContain('data-testid="answered-monitor-unauthorized"')
    expect(html).not.toContain('team-answer-status-')
    expect(html).not.toContain('Alpha')
  })

  it('never exposes the chosen option, correctness, or points for any prop combination', () => {
    const answered: AnsweredTeamRow[] = rosterAllUnanswered.map((t) => ({
      ...t, answered: true, answeredAt: '2026-07-10T10:00:00Z',
    }))
    const variants = [
      render(),
      render({ teams: answered }),
      render({ activeQuestionOrder: null }),
      render({ unauthorized: true }),
      render({ error: 'boom' }),
      render({ activeQuestionOrder: 4, teams: [] }),
      render({ loading: true, teams: [], activeQuestionOrder: null }),
    ]
    const forbidden = /option|correct|incorrect|points|score/i
    for (const html of variants) {
      expect(html).not.toMatch(forbidden)
    }
  })
})
