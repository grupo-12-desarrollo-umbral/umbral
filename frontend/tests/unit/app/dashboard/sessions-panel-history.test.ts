// Pins where the session audit trail is reachable from: a concluded session is read-only and its live
// operation view never opens, so SessionsPanel is the only surface that can show its history.
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/dashboard.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))
vi.mock('@/app/dashboard/sessionHistoryPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))
vi.mock('@/app/actions/sessions', () => ({
  getSessionAssociatedTeams: vi.fn(),
  associateTeamToSession: vi.fn(),
  getSessionHistoryAction: vi.fn(),
}))
vi.mock('@/app/actions/teams', () => ({ getActiveTeams: vi.fn() }))

import { SessionsPanel } from '@/app/dashboard/SessionsPanel'
import type { SessionAssignmentSummaryDto } from '@/app/lib/definitions'

const FINISHED_ID = 'b1000000-0000-0000-0000-000000000001'
const ACTIVE_ID = '0a9656a9-bbfc-4931-91f2-d9a73775217f'

function session(
  liveSessionId: string,
  sessionState: string,
): SessionAssignmentSummaryDto {
  return {
    liveSessionId,
    sessionCode: 'S-1',
    title: 'Night Operation',
    sessionState,
    assignedOperatorUserId: 2,
    scheduledAt: '2026-07-17T06:00:00.000Z',
  }
}

const assignedSessions = [session(FINISHED_ID, 'Finished'), session(ACTIVE_ID, 'Active')]

function render(selectedSessionId: string | null): string {
  return renderToStaticMarkup(
    createElement(SessionsPanel, {
      assignedSessions,
      isLoadingAssignedSessions: false,
      assignedSessionsError: null,
      selectedSessionId,
      onSelectSession: vi.fn(),
      onOpenLiveOperation: vi.fn(),
    }),
  )
}

describe('SessionsPanel session history', () => {
  it('mounts the history panel for a concluded session', () => {
    const markup = render(FINISHED_ID)

    expect(markup).toContain('session-history-panel')
    expect(markup).toContain('concluded-session-readonly-note')
  })

  it('keeps the live operation view closed for a concluded session', () => {
    const markup = render(FINISHED_ID)

    expect(markup).not.toContain('Open live operation')
  })

  it('does not mount the history panel for a session still in play', () => {
    const markup = render(ACTIVE_ID)

    // The live view owns history while a session runs; two mounts would double-fetch and diverge.
    expect(markup).not.toContain('session-history-panel')
    expect(markup).toContain('Open live operation')
  })

  it('shows the loading state until the history read resolves', () => {
    const markup = render(FINISHED_ID)

    // Never the empty state on first paint: 'no events' is a claim the read has not yet supported.
    expect(markup).toContain('Loading history…')
    expect(markup).not.toContain('No events recorded yet.')
  })
})

// NOT covered here: that the concluded mount passes onRetry, and that the retry refetches. The error
// state only exists after the effect resolves, and effects do not run under renderToStaticMarkup —
// this repo has no client-side renderer (vitest runs environment: 'node'). Driven in a browser instead.
