// Asserts the substage-aware render states: the active-question header carries the
// substage ordinal, and the substage-advancing / complete phases render their copy (HU-33A).
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/triviaRoundPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { TriviaRoundPanel } from '@/app/dashboard/TriviaRoundPanel'
import type { QuestionActivatedNotificationDto, TriviaRoundPhase } from '@/app/lib/definitions'

const activeQuestion: QuestionActivatedNotificationDto = {
  liveSessionId: 's1',
  questionIndex: 0,
  sequenceOrder: 3,
  prompt: 'Q',
  options: ['a', 'b'],
  timeLimitSeconds: 30,
  activatedAt: '2026-07-06T00:00:00Z',
}

function render(props: {
  phase: TriviaRoundPhase
  substageOrdinal?: number
  finalizing?: boolean
}): string {
  return renderToStaticMarkup(
    createElement(TriviaRoundPanel, {
      phase: props.phase,
      pregameSecondsLeft: null,
      activeQuestion: props.phase === 'question-active' ? activeQuestion : null,
      questionSecondsLeft: 12,
      substageOrdinal: props.substageOrdinal ?? 2,
      finalizing: props.finalizing ?? false,
    }),
  )
}

describe('TriviaRoundPanel — substage awareness', () => {
  it('shows the substage ordinal + question number in the active-question header', () => {
    expect(render({ phase: 'question-active' })).toContain('Substage 2 · Question')
  })

  it('renders the advancing copy when moving to the next substage', () => {
    const html = render({ phase: 'substage-advancing', finalizing: false })
    expect(html).toContain('data-phase="substage-advancing"')
    expect(html).toContain('Advancing to the next substage')
  })

  it('renders the finalizing copy when the final substage completes', () => {
    const html = render({ phase: 'substage-advancing', finalizing: true })
    expect(html).toContain('Final substage complete')
  })

  it('renders the completion state', () => {
    const html = render({ phase: 'complete' })
    expect(html).toContain('data-phase="complete"')
    expect(html).toContain('Session complete')
  })
})
