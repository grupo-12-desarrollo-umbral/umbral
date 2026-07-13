// @vitest-environment jsdom
// HU-26: the operator clue-release control renders the Active-only gate, the raw targetId input +
// team selector (All teams + one option per attached team), and maps each releaseClueAction outcome
// to its distinct success/error copy. Interactions run under jsdom + act; the server action is mocked
// so the test never pulls the server-only lib.
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
import type { SessionLifecycleState } from '@/app/lib/definitions'

const teams = [
  { teamId: 'team-a', displayName: 'Alpha' },
  { teamId: 'team-b', displayName: 'Bravo' },
]

function staticHtml(state: SessionLifecycleState) {
  return renderToStaticMarkup(
    createElement(OperatorClueReleasePanel, { liveSessionId: 's1', state, teams }),
  )
}

// Live render helpers for the interactive outcome tests.
let container: HTMLElement
let root: Root

async function mount() {
  container = document.createElement('div')
  document.body.appendChild(container)
  root = createRoot(container)
  await act(async () => {
    root.render(createElement(OperatorClueReleasePanel, { liveSessionId: 's1', state: 'Active', teams }))
  })
}

function setInput(value: string) {
  const input = container.querySelector<HTMLInputElement>('[data-testid="clue-release-target-input"]')!
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!
  setter.call(input, value)
  input.dispatchEvent(new Event('input', { bubbles: true }))
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
    expect(html).not.toContain('data-testid="clue-release-target-input"')
  })

  it('renders the input, the team selector (All teams + one option per team), and a submit when Active', () => {
    const html = staticHtml('Active')
    expect(html).toContain('data-testid="clue-release-target-input"')
    expect(html).toContain('data-testid="clue-release-team-select"')
    expect(html).toContain('data-testid="clue-release-submit"')
    expect(html).toContain('All teams')
    expect(html).toContain('Alpha')
    expect(html).toContain('Bravo')
  })

  it('disables submit while the targetId input is empty', () => {
    const html = staticHtml('Active')
    // The submit button element carries the disabled attribute in its initial (empty) render.
    const submitTag = html.slice(html.indexOf('data-testid="clue-release-submit"'))
    expect(submitTag.slice(0, submitTag.indexOf('>'))).toContain('disabled')
  })

  it('shows the success note with the released team count on { data }', async () => {
    releaseClueActionMock.mockResolvedValue({ data: { targetId: 'target-1', releasedTeamIds: ['team-a'] } })
    await mount()
    setInput('target-1')
    await submit()
    expect(releaseClueActionMock).toHaveBeenCalledWith('s1', { targetId: 'target-1', teamId: undefined })
    expect(q('clue-release-success')?.textContent).toContain('Released to 1 team.')
    expect(q('clue-release-error')).toBeNull()
  })

  it('shows the duplicate message on { duplicate }', async () => {
    releaseClueActionMock.mockResolvedValue({ duplicate: true })
    await mount()
    setInput('target-1')
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('already released to that team')
  })

  it('shows the not-releasable message on { notReleasable }', async () => {
    releaseClueActionMock.mockResolvedValue({ notReleasable: true })
    await mount()
    setInput('target-1')
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('no releasable hidden clue')
  })

  it('shows the not-active message on { notActive }', async () => {
    releaseClueActionMock.mockResolvedValue({ notActive: true })
    await mount()
    setInput('target-1')
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('must be Active')
  })

  it('shows the not-authorized message on { unauthorized }', async () => {
    releaseClueActionMock.mockResolvedValue({ unauthorized: true })
    await mount()
    setInput('target-1')
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('not authorized')
  })

  it('shows the transient error copy on { error }', async () => {
    releaseClueActionMock.mockResolvedValue({ error: 'Could not release the clue. Try again.' })
    await mount()
    setInput('target-1')
    await submit()
    expect(q('clue-release-error')?.textContent).toContain('Could not release the clue. Try again.')
  })
})
