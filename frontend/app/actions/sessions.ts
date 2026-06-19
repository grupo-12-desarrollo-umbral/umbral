'use server'

import { verifySession } from '@/app/lib/dal'
import { listTriviaQuizzes } from '@/app/lib/trivias'
import { listMissions } from '@/app/lib/missions'
import {
  createTriviaSession as createTriviaSessionLib,
  listAssignableSessions as listAssignableSessionsLib,
  listOperatorSessions as listOperatorSessionsLib,
  getSessionAssociatedTeams as getSessionAssociatedTeamsLib,
  associateTeamToSession as associateTeamToSessionLib,
  assignSessionOperator as assignSessionOperatorLib,
  transitionSessionState as transitionSessionStateLib,
  getOperatorSessionTimerSnapshot as getOperatorSessionTimerSnapshotLib,
} from '@/app/lib/sessions'
import { listAssignableOperators as listAssignableOperatorsLib } from '@/app/lib/users'
import { revalidatePath } from 'next/cache'
import type {
  MissionSummaryDto,
  TriviaQuizSummaryDto,
  CreateTriviaSessionRequest,
  TriviaSessionCreatedDto,
  SessionAssignmentSummaryDto,
  SessionAssociatedTeamsDto,
  AssociateTeamToSessionResultDto,
  AssignSessionOperatorResultDto,
  AssignableOperatorDto,
  SessionLifecycleState,
  TransitionSessionStateResultDto,
  SessionTimerSnapshotDto,
} from '@/app/lib/definitions'
import { IdentityError } from '@/app/lib/definitions'

export async function getPublishedTrivias(): Promise<TriviaQuizSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const all = await listTriviaQuizzes()
  return all.filter((q) => q.status === 'Published')
}

export async function getActiveMissions(): Promise<MissionSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const all = await listMissions()
  return all.filter((m) => m.isActive)
}

export async function createTriviaSession(
  req: CreateTriviaSessionRequest,
): Promise<TriviaSessionCreatedDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await createTriviaSessionLib(req)
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
