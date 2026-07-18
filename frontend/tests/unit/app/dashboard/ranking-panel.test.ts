// HU-24B: the operator ranking panel renders the ledger standings in the backend's order, labelled by
// the backend's Position (RB-08), plus the unauthorized / error / empty states. It must never re-sort.
import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/rankingPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { RankingPanel } from '@/app/dashboard/RankingPanel'
import type { RankingSnapshotDto } from '@/app/lib/definitions'

const snapshot: RankingSnapshotDto = {
  liveSessionId: 's1',
  generatedAt: '2026-07-16T10:00:00Z',
  calculationVersion: 3,
  rows: [
    {
      teamId: 'ref-team-b',
      teamDisplayName: 'Bravo',
      position: 1,
      totalScore: 300,
      resolutionTime: '00:05:00',
    },
    {
      teamId: 'ref-team-a',
      teamDisplayName: 'Alpha',
      position: 2,
      totalScore: 100,
      resolutionTime: '00:07:30',
    },
  ],
}

function render(props: Partial<Parameters<typeof RankingPanel>[0]> = {}): string {
  return renderToStaticMarkup(
    createElement(RankingPanel, {
      snapshot,
      unauthorized: false,
      error: null,
      loading: false,
      live: true,
      ...props,
    }),
  )
}

describe('RankingPanel', () => {
  it('renders each team with its backend position and ledger score', () => {
    const html = render()

    expect(html).toContain('Bravo')
    expect(html).toContain('300 pts')
    expect(html).toContain('Alpha')
    expect(html).toContain('100 pts')
  })

  it('preserves the backend row order rather than re-sorting client-side (RB-08)', () => {
    const html = render()

    // Bravo (position 1) must precede Alpha (position 2) — the backend policy owns this ordering.
    expect(html.indexOf('Bravo')).toBeLessThan(html.indexOf('Alpha'))
  })

  it('renders a leading team the backend ranked first even when its score is lower', () => {
    // Guards against a client-side sort sneaking back in: the backend is authoritative, so a row it
    // marks position 1 leads even if the rendered scores would sort the other way.
    const html = render({
      snapshot: {
        ...snapshot,
        rows: [
          { teamId: 'ref-team-c', teamDisplayName: 'Charlie', position: 1, totalScore: 50, resolutionTime: '00:01:00' },
          { teamId: 'ref-team-d', teamDisplayName: 'Delta', position: 2, totalScore: 90, resolutionTime: '00:02:00' },
        ],
      },
    })

    expect(html.indexOf('Charlie')).toBeLessThan(html.indexOf('Delta'))
  })

  it('renders the resolution time as the visible RB-08 tiebreak, formatted for a human', () => {
    expect(render()).toContain('5m 00s')
  })

  it('never leaks a raw .NET TimeSpan into the standings', () => {
    // The original fixtures used clean "HH:mm:ss" values, so a day-carrying, tick-precise span reached
    // the DOM verbatim ("1.05:05:07.5499560") with every test still green.
    const html = render({
      snapshot: {
        ...snapshot,
        rows: [{ ...snapshot.rows[0], resolutionTime: '1.05:05:07.5499560' }],
      },
    })

    expect(html).not.toContain('1.05:05:07.5499560')
    expect(html).toContain('1d 05h 05m')
  })

  it('omits the resolution time when the backend sends none', () => {
    const html = render({
      snapshot: {
        ...snapshot,
        rows: [{ ...snapshot.rows[0], resolutionTime: null }],
      },
    })

    expect(html).toContain('Bravo')
    expect(html).not.toContain('ranking-time-')
  })

  it('renders the empty-standings state for the backend well-known empty snapshot', () => {
    const html = render({ snapshot: { ...snapshot, rows: [] } })

    expect(html).toContain('Aún no hay posiciones.')
  })

  it('renders the not-authorized state without any team data', () => {
    const html = render({ unauthorized: true })

    expect(html).toContain('No tienes autorización')
    expect(html).not.toContain('Bravo')
  })

  it('renders a transient read failure as retryable, never as not-authorized', () => {
    const html = render({ error: 'boom' })

    expect(html).toContain('Se actualizará automáticamente')
    expect(html).not.toContain('No tienes autorización')
  })

  it('renders the loading state before the first snapshot resolves', () => {
    expect(render({ snapshot: null, loading: true })).toContain('Cargando el ranking…')
  })

  it('flags standings as possibly stale when the live scoring channel is down', () => {
    // The whole point of the fix: with a snapshot present but the hub not delivering, the operator
    // must be told the rows can lag rather than trusting them as live.
    const html = render({ live: false })

    expect(html).toContain('ranking-live-paused')
    expect(html).toContain('Actualizaciones en vivo pausadas')
    // Still renders the last-known standings underneath the notice.
    expect(html).toContain('Bravo')
  })

  it('shows no stale-standings notice while the live channel is connected', () => {
    expect(render({ live: true })).not.toContain('ranking-live-paused')
  })
})
