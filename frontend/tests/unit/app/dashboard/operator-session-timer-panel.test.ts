// Asserts the two active-substage render states: an active trivia question shows the
// countdown; no active question shows the no-countdown state (HU-22 / OD-1).
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

// CSS-module class names are noise in a render test — stub to identity.
vi.mock('@/app/dashboard/operatorSessionTimerPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { OperatorSessionTimerPanel } from '@/app/dashboard/OperatorSessionTimerPanel'
import type { SessionTimerSnapshotDto } from '@/app/lib/definitions'

function snapshot(overrides: Partial<SessionTimerSnapshotDto> = {}): SessionTimerSnapshotDto {
  return {
    liveSessionId: 's1',
    teamId: null,
    sessionState: 'Active',
    totalSeconds: 30,
    remainingSeconds: 18,
    timerStatus: 'Advancing',
    isAdvancing: true,
    isExpired: false,
    observedAt: '2026-07-05T00:00:00Z',
    advancingSince: null,
    expiredAt: null,
    activeQuestion: {
      liveSessionId: 's1',
      questionIndex: 0,
      sequenceOrder: 1,
      prompt: 'Q',
      options: ['a', 'b'],
      timeLimitSeconds: 30,
      activatedAt: '2026-07-05T00:00:00Z',
      remainingSeconds: 18,
    },
    ...overrides,
  }
}

function render(timer: SessionTimerSnapshotDto | null): string {
  return renderToStaticMarkup(
    createElement(OperatorSessionTimerPanel, { timer, isLoading: false, error: null }),
  )
}

describe('OperatorSessionTimerPanel', () => {
  it('renders the active-question countdown when a question is active', () => {
    const html = render(snapshot())
    expect(html).toContain('data-testid="timer-remaining"')
    expect(html).toContain('00:18')
    expect(html).toContain('>Running<')
    expect(html).not.toContain('timer-no-countdown')
  })

  it('renders the no-countdown state when no question is active', () => {
    const html = render(snapshot({ activeQuestion: null, remainingSeconds: 0, totalSeconds: 0 }))
    expect(html).toContain('data-testid="timer-no-countdown"')
    expect(html).toContain('No active question')
    expect(html).toContain('>No question<')
    expect(html).not.toContain('data-testid="timer-remaining"')
  })
})
