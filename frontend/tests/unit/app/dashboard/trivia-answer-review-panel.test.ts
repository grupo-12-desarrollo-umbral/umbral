// HU-36B: the operator post-close answer review renders per-team rows with option,
// correctness badge, and points. Covers loading, unauthorized, error, empty, and roster states.
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/triviaAnswerReviewPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { TriviaAnswerReviewPanel, type AnswerReviewTeamRow } from '@/app/dashboard/TriviaAnswerReviewPanel'

const fixtureTeams: AnswerReviewTeamRow[] = [
  { teamId: 'team-a', displayName: 'Alpha', teamCode: 'AAA', selectedOptionSequenceOrder: 2, isCorrect: true, scoreValue: 100, answeredAt: '2026-07-10T10:00:00Z' },
  { teamId: 'team-b', displayName: 'Bravo', teamCode: 'BBB', selectedOptionSequenceOrder: 1, isCorrect: false, scoreValue: 0, answeredAt: '2026-07-10T10:00:01Z' },
  { teamId: 'team-c', displayName: 'Charlie', teamCode: 'CCC' },
]

function render(props: Partial<Parameters<typeof TriviaAnswerReviewPanel>[0]> = {}): string {
  return renderToStaticMarkup(
    createElement(TriviaAnswerReviewPanel, {
      questionSequenceOrder: 3,
      teams: fixtureTeams,
      unauthorized: false,
      error: null,
      loading: false,
      ...props,
    }),
  )
}

describe('TriviaAnswerReviewPanel', () => {
  it('renders every team as a row keyed by team id', () => {
    const html = render()
    expect(html).toContain('data-testid="trivia-answer-review-row-team-a"')
    expect(html).toContain('data-testid="trivia-answer-review-row-team-b"')
    expect(html).toContain('data-testid="trivia-answer-review-row-team-c"')
  })

  it('shows the correct badge for a correct answer', () => {
    const html = render()
    expect(html).toMatch(/data-testid="trivia-answer-review-correct-team-a"[^>]*>Correct</)
    expect(html).toContain('data-tone="success"')
  })

  it('shows the incorrect badge for a wrong answer', () => {
    const html = render()
    expect(html).toMatch(/data-testid="trivia-answer-review-correct-team-b"[^>]*>Incorrect</)
    expect(html).toContain('data-tone="critical"')
  })

  it('shows the no-answer badge when a team did not answer', () => {
    const html = render()
    expect(html).toMatch(/data-testid="trivia-answer-review-correct-team-c"[^>]*>No answer</)
    expect(html).toContain('data-tone="muted"')
  })

  it('displays the selected option sequence order for teams that answered', () => {
    const html = render()
    expect(html).toContain('Option 2')
    expect(html).toContain('Option 1')
  })

  it('displays an em-dash for teams without an answer', () => {
    const html = render()
    const rowCStart = html.indexOf('data-testid="trivia-answer-review-row-team-c"')
    const rowCEnd = html.indexOf('</li>', rowCStart)
    const rowCHtml = html.slice(rowCStart, rowCEnd)
    expect(rowCHtml).toContain('—')
  })

  it('displays points awarded for correct answers and zero for incorrect', () => {
    const html = render()
    expect(html).toContain('100 pts')
    expect(html).toContain('0 pts')
  })

  it('displays an em-dash for points when no answer was submitted', () => {
    const html = render()
    const rowCStart = html.indexOf('data-testid="trivia-answer-review-row-team-c"')
    const rowCEnd = html.indexOf('</li>', rowCStart)
    const rowCHtml = html.slice(rowCStart, rowCEnd)
    expect(rowCHtml).toContain('—')
  })

  it('renders the empty state when no question sequence order is provided', () => {
    const html = render({ questionSequenceOrder: null })
    expect(html).toContain('data-testid="trivia-answer-review-empty"')
    expect(html).not.toContain('trivia-answer-review-row-')
  })

  it('renders the loading state when no question is set and loading is true', () => {
    const html = render({ questionSequenceOrder: null, loading: true })
    expect(html).toContain('data-testid="trivia-answer-review-empty"')
    expect(html).toContain('Loading answer review')
  })

  it('renders the waiting-for-roster state when a question is set but teams are empty', () => {
    const html = render({ questionSequenceOrder: 2, teams: [] })
    expect(html).toContain('data-testid="trivia-answer-review-no-roster"')
    expect(html).toContain('Question 2')
    expect(html).not.toContain('data-testid="trivia-answer-review-empty"')
  })

  it('renders the unauthorized state and no team data when unauthorized', () => {
    const html = render({ unauthorized: true, teams: fixtureTeams })
    expect(html).toContain('data-testid="trivia-answer-review-unauthorized"')
    expect(html).not.toContain('trivia-answer-review-row-')
    expect(html).not.toContain('Alpha')
  })

  it('renders a retryable error state (not "not authorized") on a transient read failure', () => {
    const html = render({ error: 'boom', teams: fixtureTeams })
    expect(html).toContain('data-testid="trivia-answer-review-error"')
    expect(html).not.toContain('data-testid="trivia-answer-review-unauthorized"')
    expect(html).not.toContain('trivia-answer-review-row-')
    expect(html).not.toContain('Alpha')
  })

  it('shows the question sequence order in the header', () => {
    const html = render({ questionSequenceOrder: 7 })
    expect(html).toContain('data-testid="trivia-answer-review-question"')
    expect(html).toContain('Question 7')
  })
})
