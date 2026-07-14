// @vitest-environment jsdom
// HU-28: the operator operative-clue authoring control renders the Active-OR-Paused gate, the free-text
// clue textarea + an "Assign to" team dropdown (All teams + one option per attached team), keeps submit
// disabled until text is non-empty, and maps each addOperativeClueAction outcome to its distinct
// success/error copy. Interactions run under jsdom + act; the server action is mocked so the test never
// pulls the server-only lib.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'

const { addOperativeClueActionMock } = vi.hoisted(() => ({ addOperativeClueActionMock: vi.fn() }))

vi.mock('@/app/actions/sessions', () => ({ addOperativeClueAction: addOperativeClueActionMock }))
vi.mock('@/app/dashboard/dashboard.module.css', () => ({
  default: new Proxy({}, { get: (_t, key) => String(key) }),
}))

import { act } from 'react'
import { createRoot, type Root } from 'react-dom/client'
import { OperativeCluePanel } from '@/app/dashboard/OperativeCluePanel'
import type { SessionLifecycleState } from '@/app/lib/definitions'

const teams = [
  { teamId: 'team-a', displayName: 'Alpha' },
  { teamId: 'team-b', displayName: 'Bravo' },
]

function staticHtml(state: SessionLifecycleState) {
  return renderToStaticMarkup(
    createElement(OperativeCluePanel, { liveSessionId: 's1', state, teams }),
  )
}

// Live render helpers for the interactive outcome tests.
let container: HTMLElement
let root: Root

async function mount(state: SessionLifecycleState = 'Active') {
  container = document.createElement('div')
  document.body.appendChild(container)
  root = createRoot(container)
  await act(async () => {
    root.render(createElement(OperativeCluePanel, { liveSessionId: 's1', state, teams }))
  })
}

function setText(value: string) {
  const input = container.querySelector<HTMLTextAreaElement>('[data-testid="operative-clue-text-input"]')!
  const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')!.set!
  setter.call(input, value)
  input.dispatchEvent(new Event('input', { bubbles: true }))
}

function selectTeam(value: string) {
  const select = container.querySelector<HTMLSelectElement>('[data-testid="operative-clue-team-select"]')!
  const setter = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value')!.set!
  setter.call(select, value)
  select.dispatchEvent(new Event('change', { bubbles: true }))
}

async function submit() {
  const button = container.querySelector<HTMLButtonElement>('[data-testid="operative-clue-submit"]')!
  await act(async () => {
    button.dispatchEvent(new MouseEvent('click', { bubbles: true }))
  })
  // Flush the async action transition's trailing setState.
  await act(async () => {})
}

const q = (testid: string) => container.querySelector(`[data-testid="${testid}"]`)
const submitDisabled = () =>
  container.querySelector<HTMLButtonElement>('[data-testid="operative-clue-submit"]')!.disabled

describe('OperativeCluePanel', () => {
  beforeEach(() => {
    addOperativeClueActionMock.mockReset()
  })

  afterEach(() => {
    if (root) act(() => root.unmount())
    container?.remove()
  })

  it('renders the inactive note and no submit when the session is neither Active nor Paused', () => {
    const html = staticHtml('Scheduled')
    expect(html).toContain('data-testid="operative-clue-inactive"')
    expect(html).not.toContain('data-testid="operative-clue-submit"')
    expect(html).not.toContain('data-testid="operative-clue-text-input"')
  })

  it('renders the textarea, the "Assign to" dropdown (All teams + one option per team), and a submit when Active', () => {
    const html = staticHtml('Active')
    expect(html).toContain('data-testid="operative-clue-text-input"')
    expect(html).toContain('data-testid="operative-clue-team-select"')
    expect(html).toContain('data-testid="operative-clue-submit"')
    expect(html).toContain('All teams')
    expect(html).toContain('Alpha')
    expect(html).toContain('Bravo')
  })

  it('also renders the authoring control when Paused (live gate covers Active OR Paused)', () => {
    const html = staticHtml('Paused')
    expect(html).toContain('data-testid="operative-clue-text-input"')
    expect(html).toContain('data-testid="operative-clue-submit"')
    expect(html).not.toContain('data-testid="operative-clue-inactive"')
  })

  it('keeps submit disabled until the clue text is non-empty (team defaults to All teams)', async () => {
    await mount('Active')
    // Empty text → disabled even though "All teams" is the default selection.
    expect(submitDisabled()).toBe(true)

    // Non-empty text → enabled (All teams is a valid default target).
    setText('Look beneath the blue banner.')
    await act(async () => {})
    expect(submitDisabled()).toBe(false)

    // Clear the text again → disabled.
    setText('')
    await act(async () => {})
    expect(submitDisabled()).toBe(true)
  })

  it('assigns to the chosen single team on { data }', async () => {
    addOperativeClueActionMock.mockResolvedValue({
      data: { operativeClueIds: ['c1'], assignedTeamIds: ['team-a'], clueText: 'Look beneath the blue banner.' },
    })
    await mount('Active')
    setText('Look beneath the blue banner.')
    await act(async () => {})
    selectTeam('team-a')
    await act(async () => {})
    await submit()
    expect(addOperativeClueActionMock).toHaveBeenCalledWith('s1', {
      clueText: 'Look beneath the blue banner.',
      teamIds: ['team-a'],
    })
    expect(q('operative-clue-success')?.textContent).toContain('Assigned to 1 team.')
    expect(q('operative-clue-error')).toBeNull()
  })

  it('assigns to every team (the full id list) when "All teams" is selected', async () => {
    addOperativeClueActionMock.mockResolvedValue({
      data: { operativeClueIds: ['c1', 'c2'], assignedTeamIds: ['team-a', 'team-b'], clueText: 'Regroup.' },
    })
    await mount('Active')
    setText('Regroup.')
    await act(async () => {})
    // Leave the dropdown on its "All teams" default.
    await submit()
    expect(addOperativeClueActionMock).toHaveBeenCalledWith('s1', {
      clueText: 'Regroup.',
      teamIds: ['team-a', 'team-b'],
    })
    expect(q('operative-clue-success')?.textContent).toContain('Assigned to 2 teams.')
  })

  it('shows the not-live message on { notLive }', async () => {
    addOperativeClueActionMock.mockResolvedValue({ notLive: true })
    await mount('Active')
    setText('Clue')
    await act(async () => {})
    await submit()
    expect(q('operative-clue-error')?.textContent).toContain('must be Active or Paused')
  })

  it('shows the not-authorized message on { unauthorized }', async () => {
    addOperativeClueActionMock.mockResolvedValue({ unauthorized: true })
    await mount('Active')
    setText('Clue')
    await act(async () => {})
    await submit()
    expect(q('operative-clue-error')?.textContent).toContain('not authorized')
  })

  it('shows the transient error copy on { error }', async () => {
    addOperativeClueActionMock.mockResolvedValue({ error: 'Could not assign the operative clue. Try again.' })
    await mount('Active')
    setText('Clue')
    await act(async () => {})
    await submit()
    expect(q('operative-clue-error')?.textContent).toContain('Could not assign the operative clue. Try again.')
  })
})
