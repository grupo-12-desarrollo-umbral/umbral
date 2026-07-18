import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/sessionHistoryPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { SessionHistoryPanel } from '@/app/dashboard/SessionHistoryPanel'
import type { SessionHistoryRowDto } from '@/app/lib/definitions'

const STATE_CHANGE_ID = '11111111-1111-1111-1111-111111111111'
const QUESTION_CLOSED_ID = '22222222-2222-2222-2222-222222222222'
const TEAM_ID = '33333333-3333-3333-3333-333333333333'
const ACTOR_ID = '44444444-4444-4444-4444-444444444444'

const stateChange: SessionHistoryRowDto = {
  sessionEventId: STATE_CHANGE_ID,
  eventType: 'SessionStateChanged',
  teamId: null,
  occurredAt: '2026-07-16T10:01:05.000Z',
  responsibleUserExternalId: ACTOR_ID,
  payloadSummary: 'Active→Paused: weather hold',
}

const questionClosed: SessionHistoryRowDto = {
  sessionEventId: QUESTION_CLOSED_ID,
  eventType: 'QuestionClosed',
  teamId: TEAM_ID,
  occurredAt: '2026-07-16T10:02:30.000Z',
  responsibleUserExternalId: null,
  payloadSummary: 'Question 3 closed',
}

const teamNames = { [TEAM_ID]: 'Gilded Owls' }

function render(props: Partial<Parameters<typeof SessionHistoryPanel>[0]> = {}): string {
  return renderToStaticMarkup(
    createElement(SessionHistoryPanel, {
      events: [stateChange, questionClosed],
      teamNames,
      unauthorized: false,
      error: null,
      loading: false,
      ...props,
    }),
  )
}

describe('SessionHistoryPanel', () => {
  it('renders a row per event with its type, summary and team', () => {
    const html = render()

    expect(html).toContain(`session-history-row-${STATE_CHANGE_ID}`)
    expect(html).toContain(`session-history-row-${QUESTION_CLOSED_ID}`)
    expect(html).toContain('Cambio de estado de la sesión')
    expect(html).toContain('Pregunta cerrada')
    expect(html).toContain('Gilded Owls')
  })

  it('renders the backend payload summary verbatim rather than remapping it', () => {
    // The summary is backend-authored; the panel must not reinterpret it into copy of its own.
    expect(render()).toContain('Active→Paused: weather hold')
  })

  it('labels a null teamId as a session-wide event, not an unknown team', () => {
    const html = render()

    expect(html).toContain('Toda la sesión')
    expect(html).not.toContain('Equipo desconocido')
  })

  it('names an unresolved teamId as unknown', () => {
    const html = render({ teamNames: {} })

    expect(html).toContain('Equipo desconocido')
  })

  it('preserves the server ordering instead of re-sorting', () => {
    // The repository orders by OccurredAt ascending; an audit trail reads oldest-first, so a panel
    // that re-sorted (as the evidence feed does) would misreport the sequence.
    const html = render()

    expect(html.indexOf(STATE_CHANGE_ID)).toBeLessThan(html.indexOf(QUESTION_CLOSED_ID))
  })

  it('distinguishes an empty history from a load in flight', () => {
    expect(render({ events: [] })).toContain('Aún no hay eventos registrados.')
    expect(render({ events: [], loading: true })).toContain('Cargando el historial…')
  })

  it('shows an authorization state without any rows', () => {
    const html = render({ unauthorized: true })

    expect(html).toContain('session-history-unauthorized')
    expect(html).not.toContain(`session-history-row-${STATE_CHANGE_ID}`)
  })

  it('reports a transient read failure as distinct from an authorization problem', () => {
    const html = render({ error: 'boom' })

    expect(html).toContain('session-history-error')
    expect(html).not.toContain('session-history-unauthorized')
    // The raw error must not leak into operator-facing copy.
    expect(html).not.toContain('boom')
  })

  it('promises an automatic refresh only where something refreshes it', () => {
    // Without onRetry the mount is one that self-heals (DashboardClient refetches on hub reconnect).
    const html = render({ error: 'boom' })

    expect(html).toContain('Se actualizará automáticamente.')
    expect(html).not.toContain('session-history-retry')
  })

  it('offers a retry instead of an empty promise where nothing refreshes it', () => {
    // A mount with no reconnect behind it would otherwise strand the operator in the error state.
    const html = render({ error: 'boom', onRetry: () => {} })

    expect(html).toContain('session-history-retry')
    expect(html).not.toContain('Se actualizará automáticamente.')
  })

  it('disables the retry while a read is already in flight', () => {
    const html = render({ error: 'boom', onRetry: () => {}, loading: true })

    expect(html).toContain('Reintentando…')
    expect(html).toContain('disabled')
  })
})
