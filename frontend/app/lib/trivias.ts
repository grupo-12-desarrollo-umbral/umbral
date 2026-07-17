import 'server-only'
import { IdentityError, type TriviaQuizSummaryDto, type TriviaQuizDto, type TriviaQuestionRequest } from './definitions'
import { verifySession } from './dal'
import { API_GATEWAY_URL, getGatewayHeaders } from './gateway'

export async function listTriviaQuizzes(): Promise<TriviaQuizSummaryDto[]> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/trivias`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden.')
  if (!response.ok) throw new IdentityError('unknown', `listTriviaQuizzes failed with status ${response.status}`)
  return response.json()
}

export async function getTriviaQuizById(id: number): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/trivias/${id}`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (!response.ok) throw new IdentityError('unknown', `getTriviaQuizById failed with status ${response.status}`)
  return response.json()
}

export async function createTriviaQuiz(
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/trivias`, {
    method: 'POST',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ title, description, questions: [] }),
  })
  if (response.status === 400) throw new Error('invalid_fields')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (!response.ok) throw new IdentityError('unknown', `createTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function updateTriviaQuiz(
  id: number,
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/trivias/${id}`, {
    method: 'PUT',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ title, description, questions: [] }),
  })
  if (response.status === 400) throw new Error('invalid_fields')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_not_editable')
  if (!response.ok) throw new IdentityError('unknown', `updateTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function addTriviaQuestion(
  triviaQuizId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/trivias/${triviaQuizId}/questions`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(question),
    },
  )
  if (response.status === 400) throw new Error('invalid_question')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (!response.ok) throw new IdentityError('unknown', `addTriviaQuestion failed with status ${response.status}`)
  return response.json()
}

export async function updateTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/trivias/${triviaQuizId}/questions/${questionId}`,
    {
      method: 'PUT',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(question),
    },
  )
  if (response.status === 400) throw new Error('invalid_question')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (!response.ok) throw new IdentityError('unknown', `updateTriviaQuestion failed with status ${response.status}`)
  return response.json()
}

export async function removeTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/trivias/${triviaQuizId}/questions/${questionId}`,
    {
      method: 'DELETE',
      headers: await getGatewayHeaders(),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_not_editable')
  if (!response.ok) throw new IdentityError('unknown', `removeTriviaQuestion failed with status ${response.status}`)
  return response.json()
}

export async function publishTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/trivias/${id}/publish`,
    {
      method: 'POST',
      headers: await getGatewayHeaders(),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_publish_conflict')
  if (!response.ok) throw new IdentityError('unknown', `publishTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function archiveTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/trivias/${id}/archive`,
    {
      method: 'POST',
      headers: await getGatewayHeaders(),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_archive_conflict')
  if (!response.ok) throw new IdentityError('unknown', `archiveTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function duplicateTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/trivias/${id}/duplicate`,
    {
      method: 'POST',
      headers: await getGatewayHeaders(),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_duplicate_conflict')
  if (!response.ok) throw new IdentityError('unknown', `duplicateTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function retireTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/trivias/${id}/retire`,
    {
      method: 'POST',
      headers: await getGatewayHeaders(),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Operator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) {
    // Two distinct guards land here: the lifecycle state check, and ADR-0003's active-mission
    // reference check. Only `detail` distinguishes them, so carry it up rather than have the UI
    // guess at the cause.
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null
    throw new Error('trivia_retire_conflict', { cause: problem?.detail })
  }
  if (!response.ok) throw new IdentityError('unknown', `retireTriviaQuiz failed with status ${response.status}`)
  return response.json()
}
