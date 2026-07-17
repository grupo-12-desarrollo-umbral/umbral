'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listTriviaQuizzes,
  getTriviaQuizById,
  createTriviaQuiz as createTriviaQuizLib,
  updateTriviaQuiz as updateTriviaQuizLib,
  addTriviaQuestion as addTriviaQuestionLib,
  updateTriviaQuestion as updateTriviaQuestionLib,
  removeTriviaQuestion as removeTriviaQuestionLib,
  publishTriviaQuiz as publishTriviaQuizLib,
  archiveTriviaQuiz as archiveTriviaQuizLib,
  duplicateTriviaQuiz as duplicateTriviaQuizLib,
  retireTriviaQuiz as retireTriviaQuizLib,
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
  if (session.role !== 'Operator') throw new Error('Forbidden')
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
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await updateTriviaQuizLib(id, title, description)
  revalidatePath('/dashboard')
  return result
}

export async function addTriviaQuestion(
  triviaQuizId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
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
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await updateTriviaQuestionLib(triviaQuizId, questionId, question)
  revalidatePath('/dashboard')
  return result
}

export async function removeTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await removeTriviaQuestionLib(triviaQuizId, questionId)
  revalidatePath('/dashboard')
  return result
}

export async function publishTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await publishTriviaQuizLib(id)
  revalidatePath('/dashboard')
  return result
}

export async function archiveTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await archiveTriviaQuizLib(id)
  revalidatePath('/dashboard')
  return result
}

export async function duplicateTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  if (session.role !== 'Operator') throw new Error('Forbidden')
  const result = await duplicateTriviaQuizLib(id)
  revalidatePath('/dashboard')
  return result
}

// Returns the conflict rather than throwing it: an Error thrown here crosses the Server Action
// boundary, and only `message`/`digest` survive that trip — `cause` is dropped, so the backend's
// curated 409 detail would never reach the UI. Next models expected errors as return values.
export type RetireTriviaQuizActionResult =
  | { data: TriviaQuizDto }
  | { error: string; detail?: string }

export async function retireTriviaQuiz(id: number): Promise<RetireTriviaQuizActionResult> {
  const session = await verifySession()
  if (session.role !== 'Operator') return { error: 'forbidden' }
  try {
    const data = await retireTriviaQuizLib(id)
    revalidatePath('/dashboard')
    return { data }
  } catch (error) {
    if (!(error instanceof Error)) return { error: 'unknown' }
    const detail = typeof error.cause === 'string' && error.cause.trim() ? error.cause : undefined
    return { error: error.message, detail }
  }
}
