'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listTriviaQuizzes,
  getTriviaQuizById,
  createTriviaQuiz as createTriviaQuizLib,
  updateTriviaQuiz as updateTriviaQuizLib,
  addTriviaQuestion as addTriviaQuestionLib,
  updateTriviaQuestion as updateTriviaQuestionLib,
} from '@/app/lib/trivias'
import { revalidatePath } from 'next/cache'
import type { TriviaQuizSummaryDto, TriviaQuizDto, TriviaQuestionRequest } from '@/app/lib/definitions'

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

export async function addTriviaQuestion(
  triviaQuizId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await addTriviaQuestionLib(triviaQuizId, question)
  revalidatePath('/dashboard')
  return result
}

export async function updateTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await updateTriviaQuestionLib(triviaQuizId, questionId, question)
  revalidatePath('/dashboard')
  return result
}
