import type { SessionLifecycleState } from '@/app/lib/definitions'

// Canonical session state machine mirrored from the backend. Kept as pure data (no React) so the
// "operator UI offers exactly the backend's allowed transitions" gate is unit-testable directly.

export type LifecycleAction = {
  label: string
  targetState: SessionLifecycleState
  description: string
  destructive?: boolean
  allowsReason?: boolean
}

export const lifecycleStates = new Set<SessionLifecycleState>([
  'Scheduled',
  'Preparing',
  'Active',
  'Paused',
  'Finished',
  'Cancelled',
])

// Allowed next actions per current state. Terminal states expose none.
export const lifecycleActions: Record<SessionLifecycleState, LifecycleAction[]> = {
  Scheduled: [
    { label: 'Prepare', targetState: 'Preparing', description: 'Open operator preparation for this session.' },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this scheduled session.', destructive: true, allowsReason: true },
  ],
  Preparing: [
    { label: 'Start', targetState: 'Active', description: 'Move teams into active answering.' },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this preparing session.', destructive: true, allowsReason: true },
  ],
  Active: [
    { label: 'Pause', targetState: 'Paused', description: 'Freeze the live session while preserving progress.' },
    { label: 'Finish', targetState: 'Finished', description: 'Terminally finish this live session.', destructive: true },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this live session.', destructive: true, allowsReason: true },
  ],
  Paused: [
    { label: 'Resume', targetState: 'Active', description: 'Return the paused session to active operation.' },
    { label: 'Finish', targetState: 'Finished', description: 'Terminally finish this paused session.', destructive: true },
    { label: 'Cancel', targetState: 'Cancelled', description: 'Terminally cancel this paused session.', destructive: true, allowsReason: true },
  ],
  Finished: [],
  Cancelled: [],
}

export function toLifecycleState(value: string): SessionLifecycleState | null {
  return lifecycleStates.has(value as SessionLifecycleState) ? (value as SessionLifecycleState) : null
}
