// @vitest-environment jsdom
// Drives the trivia round reducer through its hook to assert substage tracking:
// ordinal starts at 1, advances only when a next substage exists, finalizes otherwise,
// and completes / resets as expected (HU-33A). Thin React.act harness — no testing-library.
import { afterEach, describe, expect, it } from 'vitest'
import { act, createElement, useEffect } from 'react'
import { createRoot } from 'react-dom/client'
import { useTriviaRoundState, type TriviaRoundState, type TriviaRoundHandlers } from '@/app/lib/realtime/use-trivia-round-state'

type Api = TriviaRoundState & TriviaRoundHandlers

// Renders the hook into a detached DOM node and surfaces the latest hook value.
function mountHook(): { get: () => Api; unmount: () => void } {
  const container = document.createElement('div')
  const root = createRoot(container)
  let latest: Api
  function Probe() {
    const api = useTriviaRoundState()
    useEffect(() => {
      latest = api
    })
    latest = api
    return null
  }
  act(() => {
    root.render(createElement(Probe))
  })
  return { get: () => latest, unmount: () => act(() => root.unmount()) }
}

const advance = (toSubstageId: string | null) => ({
  liveSessionId: 's1',
  fromSubstageId: 'a',
  fromPlayMode: 'Trivia' as const,
  toSubstageId,
})

let harness: ReturnType<typeof mountHook> | null = null
afterEach(() => {
  harness?.unmount()
  harness = null
})

describe('useTriviaRoundState — substage tracking', () => {
  it('starts at ordinal 1', () => {
    harness = mountHook()
    expect(harness.get().substageOrdinal).toBe(1)
    expect(harness.get().finalizing).toBe(false)
  })

  it('advances the ordinal and enters substage-advancing when a next substage exists', () => {
    harness = mountHook()
    act(() => harness!.get().handleSubstageAdvanced(advance('b')))
    expect(harness.get().substageOrdinal).toBe(2)
    expect(harness.get().phase).toBe('substage-advancing')
    expect(harness.get().finalizing).toBe(false)
  })

  it('keeps the ordinal and sets finalizing when there is no next substage', () => {
    harness = mountHook()
    act(() => harness!.get().handleSubstageAdvanced(advance('b'))) // now ordinal 2
    act(() => harness!.get().handleSubstageAdvanced(advance(null)))
    expect(harness.get().substageOrdinal).toBe(2)
    expect(harness.get().finalizing).toBe(true)
  })

  it('complete() moves to the complete phase and clears the active question', () => {
    harness = mountHook()
    act(() => harness!.get().complete())
    expect(harness.get().phase).toBe('complete')
    expect(harness.get().activeQuestion).toBeNull()
  })

  it('reset() restores ordinal 1 and clears finalizing', () => {
    harness = mountHook()
    act(() => harness!.get().handleSubstageAdvanced(advance('b')))
    act(() => harness!.get().handleSubstageAdvanced(advance(null)))
    act(() => harness!.get().reset())
    expect(harness.get().substageOrdinal).toBe(1)
    expect(harness.get().finalizing).toBe(false)
    expect(harness.get().phase).toBe('idle')
  })
})
