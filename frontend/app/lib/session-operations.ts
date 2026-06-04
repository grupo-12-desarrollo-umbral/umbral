import 'server-only'
import {
  IdentityError,
  type SessionAssociatedTeamsDto,
  type AssociateTeamToSessionResultDto,
} from './definitions'
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

export async function getSessionAssociatedTeams(
  liveSessionId: string,
): Promise<SessionAssociatedTeamsDto> {
  const session = await verifySession()
  const response = await fetch(
    `${SESSION_OPERATIONS_SERVICE_URL}/api/sessions/${liveSessionId}/teams`,
    {
      headers: getIdentityHeaders(session),
      cache: 'no-store',
    },
  )

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('session_not_found')
  }
  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `getSessionAssociatedTeams failed with status ${response.status}`,
    )
  }

  return response.json()
}

export async function associateTeamToSession(
  liveSessionId: string,
  referenceTeamId: string,
): Promise<AssociateTeamToSessionResultDto> {
  const session = await verifySession()
  const response = await fetch(
    `${SESSION_OPERATIONS_SERVICE_URL}/api/sessions/${liveSessionId}/teams`,
    {
      method: 'POST',
      headers: {
        ...getIdentityHeaders(session),
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ referenceTeamId }),
    },
  )

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (response.status === 400) {
    throw new Error('team_inactive')
  }
  if (response.status === 409) {
    throw new Error('duplicate_association')
  }
  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `associateTeamToSession failed with status ${response.status}`,
    )
  }

  return response.json()
}
