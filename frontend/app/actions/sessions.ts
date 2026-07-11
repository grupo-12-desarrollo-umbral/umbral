'use server'

import { verifySession } from '@/app/lib/dal'
import { listMissions } from '@/app/lib/missions'
import {
  createSession as createSessionLib,
  listAssignableSessions as listAssignableSessionsLib,
  listOperatorSessions as listOperatorSessionsLib,
  getSessionAssociatedTeams as getSessionAssociatedTeamsLib,
  associateTeamToSession as associateTeamToSessionLib,
  assignSessionOperator as assignSessionOperatorLib,
  transitionSessionState as transitionSessionStateLib,
  getOperatorSessionTimerSnapshot as getOperatorSessionTimerSnapshotLib,
  getOperatorTriviaAnsweredMonitor as getOperatorTriviaAnsweredMonitorLib,
} from '@/app/lib/sessions'
import { listAssignableOperators as listAssignableOperatorsLib } from '@/app/lib/users'
import { revalidatePath } from 'next/cache'
import type {
  MissionSummaryDto,
  CreateSessionRequest,
  SessionCreatedDto,
  SessionAssignmentSummaryDto,
  SessionAssociatedTeamsDto,
  AssociateTeamToSessionResultDto,
  AssignSessionOperatorResultDto,
  AssignableOperatorDto,
  SessionLifecycleState,
  TransitionSessionStateResultDto,
  SessionTimerSnapshotDto,
  TriviaAnsweredMonitorDto,
} from '@/app/lib/definitions'
import { IdentityError } from '@/app/lib/definitions'

export async function getActiveMissions(): Promise<MissionSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const all = await listMissions()
  return all.filter((m) => m.isActive)
}

export async function createSession(
  req: CreateSessionRequest,
): Promise<SessionCreatedDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await createSessionLib(req)
  revalidatePath('/dashboard')
  return result
}

export async function getAssignableOperators(): Promise<AssignableOperatorDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  return listAssignableOperatorsLib()
}

export async function listSessionsForAssignment(): Promise<SessionAssignmentSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  return listAssignableSessionsLib()
}

export async function listSessionsForOperator(): Promise<SessionAssignmentSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  return listOperatorSessionsLib()
}

export async function assignSessionOperator(
  liveSessionId: string,
  operatorUserId: number,
): Promise<AssignSessionOperatorResultDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await assignSessionOperatorLib(liveSessionId, operatorUserId)
  revalidatePath('/dashboard')
  return result
}

export async function transitionSessionState(
  liveSessionId: string,
  targetState: SessionLifecycleState,
  reason?: string,
): Promise<TransitionSessionStateResultDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await transitionSessionStateLib(liveSessionId, targetState, reason)
  revalidatePath('/dashboard')
  return result
}

export async function getSessionTimerSnapshotAction(
  liveSessionId: string,
): Promise<{ data: SessionTimerSnapshotDto } | { error: string }> {
  'use server'
  const session = await verifySession()
  if (session.role !== 'Operator') return { error: 'Forbidden' }
  try {
    const data = await getOperatorSessionTimerSnapshotLib(liveSessionId)
    return { data }
  } catch (error) {
    if (error instanceof IdentityError) return { error: error.message }
    return { error: 'Unexpected error fetching timer snapshot' }
  }
}

// HU-36A operator answered/not-answered board. Four outcomes the panel renders distinctly:
//   { data }              → the active question's per-team answered roster
//   { noActiveQuestion }  → 409: no trivia question is active right now (board's empty state)
//   { unauthorized }      → 403 non-owner / 401 auth expired / non-operator (board's not-authorized state)
//   { error }             → 404 / unexpected / transient backend failure (board's error state); never throws
// Genuine auth failures are kept separate from transient ones so a momentary 5xx/network blip does
// not tell a legitimately-assigned operator they are "not authorized".
export async function getTriviaAnsweredMonitorAction(
  liveSessionId: string,
): Promise<
  | { data: TriviaAnsweredMonitorDto }
  | { noActiveQuestion: true }
  | { unauthorized: true }
  | { error: string }
> {
  'use server'
  const session = await verifySession()
  if (session.role !== 'Operator') return { unauthorized: true }
  try {
    const data = await getOperatorTriviaAnsweredMonitorLib(liveSessionId)
    return { data }
  } catch (error) {
    if (error instanceof Error && error.message === 'no_active_question') {
      return { noActiveQuestion: true }
    }
    if (error instanceof IdentityError) {
      if (error.code === 'unauthorized') return { unauthorized: true }
      return { error: error.message }
    }
    return { error: 'Unexpected error fetching answered monitor' }
  }
}

export async function getSessionAssociatedTeams(
  liveSessionId: string,
): Promise<SessionAssociatedTeamsDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  return getSessionAssociatedTeamsLib(liveSessionId)
}

export async function associateTeamToSession(
  liveSessionId: string,
  referenceTeamId: string,
): Promise<AssociateTeamToSessionResultDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await associateTeamToSessionLib(liveSessionId, referenceTeamId)
  revalidatePath('/dashboard')
  return result
}
