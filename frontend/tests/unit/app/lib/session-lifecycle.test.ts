import { describe, expect, it } from 'vitest'
import { isTransitionAllowed, lifecycleActions, lifecycleStates, toLifecycleState } from '@/app/lib/session-lifecycle'

// The one source of truth for which transitions the operator UI offers. If this drifts from the
// backend state machine, the operator is shown an edge the API will reject (or hidden a valid one).
// Finished is deliberately absent from Active's and Paused's targets: it is reached only through
// automatic SessionCompletion (final-substage completion), never a manual Operator action — the
// backend rejects a manual Active/Paused -> Finished PATCH.
const CANONICAL: Record<string, string[]> = {
  Scheduled: ['Preparing', 'Cancelled'],
  Preparing: ['Active', 'Cancelled'],
  Active: ['Paused', 'Cancelled'],
  Paused: ['Active', 'Cancelled'],
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

  it('never offers a manual Finish action from Active or Paused', () => {
    expect(lifecycleActions.Active.some((a) => a.targetState === 'Finished')).toBe(false)
    expect(lifecycleActions.Paused.some((a) => a.targetState === 'Finished')).toBe(false)
  })
})

describe('isTransitionAllowed (stale double-transition guard)', () => {
  it('allows every canonical edge', () => {
    for (const [state, targets] of Object.entries(CANONICAL)) {
      for (const target of targets) {
        expect(isTransitionAllowed(state as never, target as never)).toBe(true)
      }
    }
  })

  it('rejects a same-state transition (the Active->Active resume race)', () => {
    // A stale "Reanudar" button firing Paused->Active after the session is already Active: the guard
    // sees the live state is Active, which offers no Active target, so the doomed request is suppressed.
    expect(isTransitionAllowed('Active', 'Active')).toBe(false)
    expect(isTransitionAllowed('Paused', 'Paused')).toBe(false)
  })

  it('rejects edges the current state does not offer', () => {
    expect(isTransitionAllowed('Active', 'Preparing')).toBe(false)
    expect(isTransitionAllowed('Finished', 'Active')).toBe(false)
    expect(isTransitionAllowed('Cancelled', 'Active')).toBe(false)
  })
})
