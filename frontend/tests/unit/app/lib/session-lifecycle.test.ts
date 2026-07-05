import { describe, expect, it } from 'vitest'
import { lifecycleActions, lifecycleStates, toLifecycleState } from '@/app/lib/session-lifecycle'

// The one source of truth for which transitions the operator UI offers. If this drifts from the
// backend state machine, the operator is shown an edge the API will reject (or hidden a valid one).
const CANONICAL: Record<string, string[]> = {
  Scheduled: ['Preparing', 'Cancelled'],
  Preparing: ['Active', 'Cancelled'],
  Active: ['Paused', 'Finished', 'Cancelled'],
  Paused: ['Active', 'Finished', 'Cancelled'],
  Finished: [],
  Cancelled: [],
}

describe('session lifecycle transition map', () => {
  it('offers exactly the canonical targets for every state', () => {
    for (const [state, targets] of Object.entries(CANONICAL)) {
      expect(lifecycleActions[state as keyof typeof lifecycleActions].map((a) => a.targetState)).toEqual(targets)
    }
  })

  it('exposes no state outside the canonical six', () => {
    expect([...lifecycleStates].sort()).toEqual(
      ['Active', 'Cancelled', 'Finished', 'Paused', 'Preparing', 'Scheduled'],
    )
    // no action targets a non-canonical state either
    for (const actions of Object.values(lifecycleActions)) {
      for (const a of actions) expect(lifecycleStates.has(a.targetState)).toBe(true)
    }
  })

  it('terminal states offer no actions', () => {
    expect(lifecycleActions.Finished).toEqual([])
    expect(lifecycleActions.Cancelled).toEqual([])
  })

  it('rejects non-canonical strings via toLifecycleState', () => {
    expect(toLifecycleState('Active')).toBe('Active')
    expect(toLifecycleState('live')).toBeNull()
    expect(toLifecycleState('draft')).toBeNull()
  })
})
