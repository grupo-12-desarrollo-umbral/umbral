import { describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

vi.mock('@/app/dashboard/evidenceSubmissionsPanel.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { EvidenceSubmissionsPanel } from '@/app/dashboard/EvidenceSubmissionsPanel'
import type { EvidenceTraceItemDto } from '@/app/lib/definitions'

const QR_ID = '11111111-1111-1111-1111-111111111111'
const TRIVIA_ID = '22222222-2222-2222-2222-222222222222'
const TEAM_ID = '33333333-3333-3333-3333-333333333333'

const qrScan: EvidenceTraceItemDto = {
  evidenceSubmissionId: QR_ID,
  teamId: TEAM_ID,
  activeSubstageId: 'substage-1',
  submissionType: 'TreasureHuntQrScan',
  originReference: 'target:9f1c',
  submittedAt: '2026-07-16T10:01:05.000Z',
  validationState: 'Pending',
  rejectionReason: null,
  resolvedAt: null,
}

const triviaAnswer: EvidenceTraceItemDto = {
  evidenceSubmissionId: TRIVIA_ID,
  teamId: TEAM_ID,
  activeSubstageId: 'substage-1',
  submissionType: 'TriviaAnswer',
  originReference: null,
  submittedAt: '2026-07-16T10:02:30.000Z',
  validationState: 'Accepted',
  rejectionReason: null,
  resolvedAt: '2026-07-16T10:02:31.000Z',
}

const teamNames = { [TEAM_ID]: 'Gilded Owls' }

function render(props: Partial<Parameters<typeof EvidenceSubmissionsPanel>[0]> = {}): string {
  return renderToStaticMarkup(
    createElement(EvidenceSubmissionsPanel, {
      items: [qrScan, triviaAnswer],
      teamNames,
      unauthorized: false,
      error: null,
      loading: false,
      live: true,
      ...props,
    }),
  )
}

describe('EvidenceSubmissionsPanel', () => {
  it('renders a row per submission with its team, state and origin', () => {
    const html = render()

    expect(html).toContain(`evidence-row-${QR_ID}`)
    expect(html).toContain(`evidence-row-${TRIVIA_ID}`)
    expect(html).toContain('Gilded Owls')
    expect(html).toContain('Pending')
    expect(html).toContain('Accepted')
    expect(html).toContain('target:9f1c')
  })

  // AC #5 names "evidencias/envíos" together, so a trivia answer — an evidence submission too — belongs
  // here, labelled by form rather than filtered out.
  it('shows both evidence forms, typed', () => {
    const html = render()

    expect(html).toContain('Escaneo QR')
    expect(html).toContain('Respuesta de trivia')
  })

  // The backend assigns no order, so the panel owns it: newest first, so the operator watches the head.
  it('orders newest first regardless of the order the rows merged in', () => {
    const html = render({ items: [qrScan, triviaAnswer] })
    const reversed = render({ items: [triviaAnswer, qrScan] })

    // The 10:02 trivia answer precedes the 10:01 QR scan in the markup either way.
    expect(html.indexOf(TRIVIA_ID)).toBeLessThan(html.indexOf(QR_ID))
    expect(reversed.indexOf(TRIVIA_ID)).toBeLessThan(reversed.indexOf(QR_ID))
  })

  it('renders the rejection reason as the display copy the backend sent', () => {
    const html = render({
      items: [
        {
          ...qrScan,
          validationState: 'Rejected',
          rejectionReason: 'Este objetivo ya fue resuelto por otro equipo.',
          resolvedAt: '2026-07-16T10:01:09.000Z',
        },
      ],
    })

    expect(html).toContain('Rejected')
    expect(html).toContain('Este objetivo ya fue resuelto por otro equipo.')
  })

  // qr:{scannedValue} echoes raw scanned input on an unmatched scan. It reaches only assignment-guarded
  // operators, but it is still untrusted text and must never become markup.
  it('escapes a scanned origin instead of rendering it as markup', () => {
    const html = render({
      items: [{ ...qrScan, originReference: 'qr:<img src=x onerror="alert(1)">' }],
    })

    expect(html).not.toContain('<img src=x')
    expect(html).toContain('&lt;img')
  })

  it('falls back to a placeholder when the team rollup has not loaded yet', () => {
    // The panel must not block on the operator-panel fetch that supplies the names.
    const html = render({ teamNames: {} })

    expect(html).toContain('Equipo desconocido')
    expect(html).toContain(`evidence-row-${QR_ID}`)
  })

  it('reports an empty trace and a loading trace distinctly', () => {
    expect(render({ items: [] })).toContain('Aún no hay envíos.')
    expect(render({ items: [], loading: true })).toContain('Cargando los envíos…')
  })

  it('shows the not-authorized state without any submission data', () => {
    const html = render({ unauthorized: true })

    expect(html).toContain('evidence-unauthorized')
    expect(html).not.toContain('Gilded Owls')
    expect(html).not.toContain('target:9f1c')
  })

  // A transient read failure is not an authorization problem — mirrors RankingPanel.
  it('shows a transient error distinctly from unauthorized', () => {
    const html = render({ error: 'boom' })

    expect(html).toContain('evidence-error')
    expect(html).not.toContain('evidence-unauthorized')
  })

  it('warns when the live channel is down, and stays quiet when it is up', () => {
    expect(render({ live: false })).toContain('evidence-live-paused')
    expect(render({ live: true })).not.toContain('evidence-live-paused')
  })
})
