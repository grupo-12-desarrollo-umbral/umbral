'use server'

import { verifySession } from '@/app/lib/dal'
import { listTriviaQuizzes } from '@/app/lib/trivias'
import {
  createTriviaSession as createTriviaSessionLib,
  listAssignableSessions as listAssignableSessionsLib,
  assignSessionOperator as assignSessionOperatorLib,
} from '@/app/lib/sessions'
import { listAssignableOperators as listAssignableOperatorsLib } from '@/app/lib/users'
import { revalidatePath } from 'next/cache'
import type {
  TriviaQuizSummaryDto,
  CreateTriviaSessionRequest,
  TriviaSessionCreatedDto,
  SessionAssignmentSummaryDto,
  AssignSessionOperatorResultDto,
  AssignableOperatorDto,
} from '@/app/lib/definitions'

export async function getPublishedTrivias(): Promise<TriviaQuizSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const all = await listTriviaQuizzes()
  return all.filter((q) => q.status === 'Published')
}

export async function createTriviaSession(
  req: CreateTriviaSessionRequest,
): Promise<TriviaSessionCreatedDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  return createTriviaSessionLib(req)
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
