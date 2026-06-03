'use server'

import { verifySession } from '@/app/lib/dal'
import { listTriviaQuizzes } from '@/app/lib/trivias'
import { createTriviaSession as createTriviaSessionLib } from '@/app/lib/sessions'
import type {
  TriviaQuizSummaryDto,
  CreateTriviaSessionRequest,
  TriviaSessionCreatedDto,
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
