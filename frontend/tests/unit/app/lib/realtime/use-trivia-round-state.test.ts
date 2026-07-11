// @vitest-environment jsdom
// Drives the trivia round reducer through its hook to assert substage tracking:
// ordinal starts at 1, advances only when a next substage exists, finalizes otherwise,
// and completes / resets as expected (HU-33A). Thin React.act harness — no testing-library.
import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, createElement, useEffect } from 'react'
import { createRoot } from 'react-dom/client'
import { useTriviaRoundState, type TriviaRoundState, type TriviaRoundHandlers } from '@/app/lib/realtime/use-trivia-round-state'
import type { ActiveQuestionSnapshotDto } from '@/app/lib/definitions'

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

const activeQuestion = (remainingSeconds: number): ActiveQuestionSnapshotDto => ({
  liveSessionId: 's1',
  questionIndex: 0,
  sequenceOrder: 1,
  prompt: '¿Quién fue el maestro de Platón?',
  options: ['Sócrates', 'Aristóteles'],
  timeLimitSeconds: 30,
  activatedAt: '2026-07-11T00:00:00Z',
  remainingSeconds,
})

let harness: ReturnType<typeof mountHook> | null = null
afterEach(() => {
  harness?.unmount()
  harness = null
  vi.useRealTimers()
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

// Regression: a paused session freezes the authoritative timer, so the client-side question
// countdown must hold its remainder rather than tick past the frozen snapshot. Re-hydrating a
// frozen snapshot (the operator's own pause, a poll, or a reconnect) must not restart a live tick.
describe('useTriviaRoundState — question countdown freeze on pause', () => {
  it('ticks down while advancing', () => {
    harness = mountHook()
    vi.useFakeTimers()
    act(() => harness!.get().hydrateActiveQuestion(activeQuestion(30), true))
    expect(harness.get().questionSecondsLeft).toBe(30)
    act(() => void vi.advanceTimersByTime(1000))
    expect(harness.get().questionSecondsLeft).toBe(29)
    act(() => void vi.advanceTimersByTime(2000))
    expect(harness.get().questionSecondsLeft).toBe(27)
  })

  it('holds the remainder when hydrated frozen (advancing=false)', () => {
    harness = mountHook()
    vi.useFakeTimers()
    act(() => harness!.get().hydrateActiveQuestion(activeQuestion(15), false))
    expect(harness.get().questionSecondsLeft).toBe(15)
    // Advancing wall-clock must not move a frozen question.
    act(() => void vi.advanceTimersByTime(5000))
    expect(harness.get().questionSecondsLeft).toBe(15)
    expect(harness.get().phase).toBe('question-active')
    expect(harness.get().activeQuestion).not.toBeNull()
  })

  it('stops ticking when a running question is re-hydrated frozen (the pause transition)', () => {
    harness = mountHook()
    vi.useFakeTimers()
    // Active question ticking...
    act(() => harness!.get().hydrateActiveQuestion(activeQuestion(30), true))
    act(() => void vi.advanceTimersByTime(3000))
    expect(harness.get().questionSecondsLeft).toBe(27)
    // ...operator pauses: the frozen snapshot re-hydrates at the frozen remainder and must freeze.
    act(() => harness!.get().hydrateActiveQuestion(activeQuestion(27), false))
    act(() => void vi.advanceTimersByTime(10_000))
    expect(harness.get().questionSecondsLeft).toBe(27)
  })

  it('resumes ticking when re-hydrated advancing again', () => {
    harness = mountHook()
    vi.useFakeTimers()
    act(() => harness!.get().hydrateActiveQuestion(activeQuestion(27), false))
    act(() => void vi.advanceTimersByTime(3000))
    expect(harness.get().questionSecondsLeft).toBe(27) // stayed frozen
    act(() => harness!.get().hydrateActiveQuestion(activeQuestion(27), true))
    act(() => void vi.advanceTimersByTime(1000))
    expect(harness.get().questionSecondsLeft).toBe(26) // ticking again
  })
})
