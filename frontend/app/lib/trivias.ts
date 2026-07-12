import 'server-only'
import { IdentityError, type TriviaQuizSummaryDto, type TriviaQuizDto, type TriviaQuestionRequest } from './definitions'
import { verifySession } from './dal'

const MISSION_DESIGN_SERVICE_URL = 'http://localhost:5001'

function getIdentityHeaders(session: {
  externalIdentityId: string
  displayName: string
  email: string
  role: string
}) {
  return {
    'X-User-Id': session.externalIdentityId,
    'X-User-Role': session.role,
    'X-User-Email': session.email,
  }
}

export async function listTriviaQuizzes(): Promise<TriviaQuizSummaryDto[]> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias`, {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden.')
  if (!response.ok) throw new IdentityError('unknown', `listTriviaQuizzes failed with status ${response.status}`)
  return response.json()
}

export async function getTriviaQuizById(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}`, {
    headers: getIdentityHeaders(session),
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
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias`, {
    method: 'POST',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, description, questions: [] }),
  })
  if (response.status === 400) throw new Error('invalid_fields')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (!response.ok) throw new IdentityError('unknown', `createTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function updateTriviaQuiz(
  id: number,
  title: string,
  description: string,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}`, {
    method: 'PUT',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify({ title, description, questions: [] }),
  })
  if (response.status === 400) throw new Error('invalid_fields')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_not_editable')
  if (!response.ok) throw new IdentityError('unknown', `updateTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function addTriviaQuestion(
  triviaQuizId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${triviaQuizId}/questions`,
    {
      method: 'POST',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify(question),
    },
  )
  if (response.status === 400) throw new Error('invalid_question')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('question_sequence_conflict')
  if (!response.ok) throw new IdentityError('unknown', `addTriviaQuestion failed with status ${response.status}`)
  return response.json()
}

export async function updateTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
  question: TriviaQuestionRequest,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${triviaQuizId}/questions/${questionId}`,
    {
      method: 'PUT',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify(question),
    },
  )
  if (response.status === 400) throw new Error('invalid_question')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('question_sequence_conflict')
  if (!response.ok) throw new IdentityError('unknown', `updateTriviaQuestion failed with status ${response.status}`)
  return response.json()
}

export async function removeTriviaQuestion(
  triviaQuizId: number,
  questionId: number,
): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${triviaQuizId}/questions/${questionId}`,
    {
      method: 'DELETE',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_not_editable')
  if (!response.ok) throw new IdentityError('unknown', `removeTriviaQuestion failed with status ${response.status}`)
  return response.json()
}

export async function publishTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/publish`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_publish_conflict')
  if (!response.ok) throw new IdentityError('unknown', `publishTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function archiveTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/archive`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_archive_conflict')
  if (!response.ok) throw new IdentityError('unknown', `archiveTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function duplicateTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/duplicate`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_duplicate_conflict')
  if (!response.ok) throw new IdentityError('unknown', `duplicateTriviaQuiz failed with status ${response.status}`)
  return response.json()
}

export async function retireTriviaQuiz(id: number): Promise<TriviaQuizDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/trivias/${id}/retire`,
    {
      method: 'POST',
      headers: getIdentityHeaders(session),
    },
  )
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('trivia_not_found')
  if (response.status === 409) throw new Error('trivia_retire_conflict')
  if (!response.ok) throw new IdentityError('unknown', `retireTriviaQuiz failed with status ${response.status}`)
  return response.json()
}
