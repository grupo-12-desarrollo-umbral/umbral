// Asserts the whole-mission clock (D-4): a running mission shows the countdown; a not-yet-seeded
// deadline (null mission fields, pre-start) shows the unavailable state; paused/expired map to chips.
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

// CSS-module class names are noise in a render test — stub to identity.
vi.mock('@/app/dashboard/operatorSessionTimerPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { MissionSessionTimerPanel } from '@/app/dashboard/OperatorSessionTimerPanel'
import type { SessionTimerSnapshotDto } from '@/app/lib/definitions'

function snapshot(overrides: Partial<SessionTimerSnapshotDto> = {}): SessionTimerSnapshotDto {
  return {
    liveSessionId: 's1',
    teamId: null,
    sessionState: 'Active',
    totalSeconds: 0,
    remainingSeconds: 0,
    timerStatus: 'Frozen',
    isAdvancing: false,
    isExpired: false,
    observedAt: '2026-07-05T00:00:00Z',
    advancingSince: null,
    expiredAt: null,
    activeQuestion: null,
    missionTotalSeconds: 1800,
    missionRemainingSeconds: 1471,
    ...overrides,
  }
}

function render(timer: SessionTimerSnapshotDto | null): string {
  return renderToStaticMarkup(
    createElement(MissionSessionTimerPanel, { timer, isLoading: false, error: null }),
  )
}

describe('MissionSessionTimerPanel', () => {
  it('renders the mission countdown even while no trivia question is active', () => {
    const html = render(snapshot())
    expect(html).toContain('data-testid="mission-timer-remaining"')
    expect(html).toContain('24:31') // 1471s → 24:31
    expect(html).toContain('>En curso<')
    expect(html).not.toContain('mission-timer-no-countdown')
  })

  it('renders the unavailable state before the mission deadline is seeded (null fields)', () => {
    const html = render(snapshot({ missionTotalSeconds: null, missionRemainingSeconds: null }))
    expect(html).toContain('data-testid="mission-timer-no-countdown"')
    expect(html).toContain('>No disponible<')
    expect(html).not.toContain('data-testid="mission-timer-remaining"')
  })

  it('shows the Paused chip when the session is paused', () => {
    const html = render(snapshot({ sessionState: 'Paused' }))
    expect(html).toContain('>Pausado<')
  })

  it('shows the Expired chip when the mission clock reaches zero', () => {
    const html = render(snapshot({ missionRemainingSeconds: 0 }))
    expect(html).toContain('>Expirado<')
  })
})
