'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listTriviaQuizzes,
  getTriviaQuizById,
  createTriviaQuiz as createTriviaQuizLib,
  updateTriviaQuiz as updateTriviaQuizLib,
} from '@/app/lib/trivias'
import { revalidatePath } from 'next/cache'
import type { TriviaQuizSummaryDto, TriviaQuizDto } from '@/app/lib/definitions'

export async function getTriviaQuizzes(): Promise<TriviaQuizSummaryDto[]> {
  await verifySession()
  return listTriviaQuizzes()
}

export async function getTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  await verifySession()
  return getTriviaQuizById(id)
}

export async function createTriviaQuiz(
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await createTriviaQuizLib(title, description)
  revalidatePath('/dashboard')
  return result
}

export async function updateTriviaQuiz(
  id: number,
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await updateTriviaQuizLib(id, title, description)
  revalidatePath('/dashboard')
  return result
}
