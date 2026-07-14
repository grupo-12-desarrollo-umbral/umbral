// @vitest-environment jsdom
// HU-26/HU-28: the operator clue-release control renders the Active-only gate, a clue dropdown fed by
// `releasableClues` (the active substage's still-releasable hidden clues) + a team selector (All teams
// + one option per attached team), a right-sized submit, and maps each releaseClueAction outcome to its
// distinct success/error copy. Interactions run under jsdom + act; the server action is mocked so the
// test never pulls the server-only lib.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

const { releaseClueActionMock } = vi.hoisted(() => ({ releaseClueActionMock: vi.fn() }))

vi.mock('@/app/actions/sessions', () => ({ releaseClueAction: releaseClueActionMock }))
vi.mock('@/app/dashboard/dashboard.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { act } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { OperatorClueReleasePanel } from '@/app/dashboard/OperatorClueReleasePanel'
import type { ReleasableClueDto, SessionLifecycleState } from '@/app/lib/definitions'

const teams = [
  { teamId: 'team-a', displayName: 'Alpha' },
  { teamId: 'team-b', displayName: 'Bravo' },
]

const releasableClues: ReleasableClueDto[] = [
  { targetId: 'target-1', targetName: 'Fountain', sequenceOrder: 1, clueText: 'Look beneath the fountain.' },
  { clueId: 'clue-2', sequenceOrder: 2, clueText: 'Behind the statue.' },
]

function staticHtml(state: SessionLifecycleState, clues: ReleasableClueDto[] = releasableClues) {
  return renderToStaticMarkup(
    createElement(OperatorClueReleasePanel, { liveSessionId: 's1', state, teams, releasableClues: clues }),
  )
}

// Live render helpers for the interactive outcome tests.
let container: HTMLElement
let root: Root

async function mount(clues: ReleasableClueDto[] = releasableClues) {
  container = document.createElement('div')
  document.body.appendChild(container)
  root = createRoot(container)
  await act(async () => {
    root.render(
      createElement(OperatorClueReleasePanel, {
        liveSessionId: 's1',
        state: 'Active',
        teams,
        releasableClues: clues,
      }),
    )
  })
}

function selectTarget(value: string) {
  const select = container.querySelector<HTMLSelectElement>('[data-testid="clue-release-target-select"]')!
  const setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')!.set!
  setter.call(select, value)
  select.dispatchEvent(new Event('change', { bubbles: true }))
}

async function submit() {
  const button = container.querySelector<HTMLButtonElement>('[data-testid="clue-release-submit"]')!
  await act(async () => {
    button.dispatchEvent(new MouseEvent('click', { bubbles: true }))
  })
  // Flush the async action transition's trailing setState.
  await act(async () => {})
}

const q = (testid: string) => container.querySelector(`[data-testid="${testid}"]`)
const submitDisabled = () =>
  container.querySelector<HTMLButtonElement>('[data-testid="clue-release-submit"]')!.disabled

describe('OperatorClueReleasePanel', () => {
  beforeEach(() => {
    releaseClueActionMock.mockReset()
  })

  afterEach(() => {
    if (root) act(() => root.unmount())
    container?.remove()
  })

  it('renders the inactive note and no submit when the session is not Active', () => {
    const html = staticHtml('Scheduled')
    expect(html).toContain('data-testid="clue-release-inactive"')
    expect(html).not.toContain('data-testid="clue-release-submit"')
    expect(html).not.toContain('data-testid="clue-release-target-select"')
  })

  it('renders the clue dropdown (one option per releasable clue), the team selector, and a submit when Active', () => {
    const html = staticHtml('Active')
    expect(html).toContain('data-testid="clue-release-target-select"')
    expect(html).toContain('data-testid="clue-release-team-select"')
    expect(html).toContain('data-testid="clue-release-submit"')
    expect(html).toContain('1. Fountain')
    expect(html).toContain('2. Pista 2')
    expect(html).toContain('All teams')
    expect(html).toContain('Alpha')
    expect(html).toContain('Bravo')
  })

  it('renders the empty note (no clue dropdown/submit) when there are no releasable clues', () => {
    const html = staticHtml('Active', [])
    expect(html).toContain('data-testid="clue-release-no-targets"')
    expect(html).not.toContain('data-testid="clue-release-target-select"')
    expect(html).not.toContain('data-testid="clue-release-submit"')
  })

  it('keeps submit disabled until a clue is chosen, then shows the selected clue preview', async () => {
    await mount()
    expect(submitDisabled()).toBe(true)
    expect(q('clue-release-preview')).toBeNull()

    selectTarget('target-1')
    await act(async () => {})
    expect(submitDisabled()).toBe(false)
    expect(q('clue-release-preview')?.textContent).toContain('Look beneath the fountain.')
  })

  it('shows the success note with the released team count on { data }', async () => {
    releaseClueActionMock.mockResolvedValue({ data: { targetId: 'target-1', releasedTeamIds: ['team-a', 'team-b'] } })
    await mount()
    selectTarget('target-1')
    await act(async () => {})
    await submit()
    expect(releaseClueActionMock).toHaveBeenCalledWith('s1', { targetId: 'target-1', clueId: undefined, teamId: undefined })
    expect(q('clue-release-success')?.textContent).toContain('Released to 2 teams.')
    expect(q('clue-release-error')).toBeNull()
  })

  it('shows the success note with the released team count for a trivia clue on { data }', async () => {
    releaseClueActionMock.mockResolvedValue({ data: { clueId: 'clue-2', releasedTeamIds: ['team-a'] } })
    await mount()
    selectTarget('clue-2')
    await act(async () => {})
    await submit()
    expect(releaseClueActionMock).toHaveBeenCalledWith('s1', { targetId: undefined, clueId: 'clue-2', teamId: undefined })
    expect(q('clue-release-success')?.textContent).toContain('Released to 1 team.')
    expect(q('clue-release-error')).toBeNull()
  })

  it('shows the duplicate message on { duplicate }', async () => {
    releaseClueActionMock.mockResolvedValue({ duplicate: true })
    await mount()
    selectTarget('target-1')
    await act(async () => {})
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('already released to that team')
  })

  it('shows the not-releasable message on { notReleasable }', async () => {
    releaseClueActionMock.mockResolvedValue({ notReleasable: true })
    await mount()
    selectTarget('target-1')
    await act(async () => {})
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('no releasable hidden clue')
  })

  it('shows the not-active message on { notActive }', async () => {
    releaseClueActionMock.mockResolvedValue({ notActive: true })
    await mount()
    selectTarget('target-1')
    await act(async () => {})
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('must be Active')
  })

  it('shows the not-authorized message on { unauthorized }', async () => {
    releaseClueActionMock.mockResolvedValue({ unauthorized: true })
    await mount()
    selectTarget('target-1')
    await act(async () => {})
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('not authorized')
  })

  it('shows the transient error copy on { error }', async () => {
    releaseClueActionMock.mockResolvedValue({ error: 'Could not release the clue. Try again.' })
    await mount()
    selectTarget('target-1')
    await act(async () => {})
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('Could not release the clue. Try again.')
  })
})
