import 'server-only'
import { IdentityError, type CreateTriviaSessionRequest, type TriviaSessionCreatedDto } from './definitions'
import { verifySession } from './dal'

const SESSION_OPERATIONS_SERVICE_URL = process.env.SESSION_OPERATIONS_SERVICE_URL!

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

export async function createTriviaSession(
  req: CreateTriviaSessionRequest,
): Promise<TriviaSessionCreatedDto> {
  const session = await verifySession()
  const response = await fetch(`${SESSION_OPERATIONS_SERVICE_URL}/api/sessions`, {
    method: 'POST',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(req),
  })

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator role required.')
  if (response.status === 404) throw new Error('quiz_not_found')
  if (response.status === 409) throw new Error('quiz_not_published')
  if (!response.ok) {
    throw new IdentityError('unknown', `createTriviaSession failed with status ${response.status}`)
  }

  return response.json() as Promise<TriviaSessionCreatedDto>
}
