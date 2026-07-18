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

// Spanish display labels for the lifecycle states. The enum values themselves stay in English
// (they mirror the backend contract); this map is used wherever a state is shown to the operator.
export const lifecycleStateLabel: Record<SessionLifecycleState, string> = {
  Scheduled: 'Programada',
  Preparing: 'En preparación',
  Active: 'Activa',
  Paused: 'Pausada',
  Finished: 'Finalizada',
  Cancelled: 'Cancelada',
}

// Allowed next actions per current state. Terminal states expose none.
export const lifecycleActions: Record<SessionLifecycleState, LifecycleAction[]> = {
  Scheduled: [
    { label: 'Preparar', targetState: 'Preparing', description: 'Abre la preparación del operador para esta sesión.' },
    { label: 'Cancelar', targetState: 'Cancelled', description: 'Cancela definitivamente esta sesión programada.', destructive: true, allowsReason: true },
  ],
  Preparing: [
    { label: 'Iniciar', targetState: 'Active', description: 'Pasa los equipos a la fase activa de respuestas.' },
    { label: 'Cancelar', targetState: 'Cancelled', description: 'Cancela definitivamente esta sesión en preparación.', destructive: true, allowsReason: true },
  ],
  // Finished is deliberately absent from both: it is reached only via automatic SessionCompletion
  // (final-substage completion), never a manual Operator action — the backend now rejects a manual
  // Active/Paused -> Finished PATCH (CanTransitionTo excludes it).
  Active: [
    { label: 'Pausar', targetState: 'Paused', description: 'Congela la sesión en vivo conservando el progreso.' },
    { label: 'Cancelar', targetState: 'Cancelled', description: 'Cancela definitivamente esta sesión en vivo.', destructive: true, allowsReason: true },
  ],
  Paused: [
    { label: 'Reanudar', targetState: 'Active', description: 'Devuelve la sesión pausada a la operación activa.' },
    { label: 'Cancelar', targetState: 'Cancelled', description: 'Cancela definitivamente esta sesión pausada.', destructive: true, allowsReason: true },
  ],
  Finished: [],
  Cancelled: [],
}

export function toLifecycleState(value: string): SessionLifecycleState | null {
  return lifecycleStates.has(value as SessionLifecycleState) ? (value as SessionLifecycleState) : null
}

// True when `targetState` is still a transition the backend offers from `currentState`. Used to
// pre-empt a stale lifecycle button firing an already-obsolete transition — e.g. a "Reanudar" click
// racing a resume that another tab or a SignalR echo already applied, which the backend would reject
// as an Active->Active conflict. A no-op check keeps that doomed request (and its raw error) off screen.
export function isTransitionAllowed(
  currentState: SessionLifecycleState,
  targetState: SessionLifecycleState,
): boolean {
  return lifecycleActions[currentState].some((action) => action.targetState === targetState)
}
