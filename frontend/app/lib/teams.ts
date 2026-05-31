import 'server-only'
import { IdentityError, type PagedResult, type TeamDto, type CreateTeamResultDto, type TeamMembershipDto } from './definitions'
import { verifySession } from './dal'

const IDENTITY_SERVICE_URL = 'http://localhost:5002'

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

export async function listTeams(page = 1, pageSize = 20): Promise<PagedResult<TeamDto>> {
  const session = await verifySession()
  const url = new URL(`${IDENTITY_SERVICE_URL}/api/teams`)
  url.searchParams.set('page', String(page))
  url.searchParams.set('pageSize', String(pageSize))

  const response = await fetch(url.toString(), {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `listTeams failed with status ${response.status}`)
  }

  return response.json()
}

export async function getTeamById(id: string): Promise<TeamDto> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams/${id}`, {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `getTeamById failed with status ${response.status}`)
  }

  return response.json()
}

export async function createTeam(
  displayName: string,
  teamCode: string,
): Promise<CreateTeamResultDto> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams`, {
    method: 'POST',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ displayName, teamCode }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 409) {
    throw new Error('duplicate_team_code')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createTeam failed with status ${response.status}`)
  }

  return response.json()
}

export async function updateTeam(
  id: string,
  displayName: string,
  teamCode: string,
): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams/${id}`, {
    method: 'PATCH',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ displayName, teamCode }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (response.status === 409) {
    throw new Error('duplicate_team_code')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `updateTeam failed with status ${response.status}`)
  }
}

export async function deactivateTeam(id: string): Promise<TeamDto> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/teams/${id}/status`, {
    method: 'DELETE',
    headers: getIdentityHeaders(session),
  })

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 409 || response.status === 422) {
    throw new Error('already_inactive')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `deactivateTeam failed with status ${response.status}`)
  }

  return response.json()
}

export async function listTeamParticipants(teamId: string): Promise<TeamMembershipDto[]> {
  const session = await verifySession()
  const response = await fetch(
    `${IDENTITY_SERVICE_URL}/api/teams/${teamId}/participants`,
    {
      headers: getIdentityHeaders(session),
      cache: 'no-store',
    },
  )

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('team_not_found')
  }
  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `listTeamParticipants failed with status ${response.status}`,
    )
  }

  return response.json()
}

export async function assignParticipant(
  teamId: string,
  userId: number,
): Promise<void> {
  const session = await verifySession()
  const response = await fetch(
    `${IDENTITY_SERVICE_URL}/api/teams/${teamId}/participants`,
    {
      method: 'POST',
      headers: {
        ...getIdentityHeaders(session),
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ userId }),
    },
  )

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('not_found')
  }

  if (response.status === 409) {
    // Read ProblemDetails body to distinguish TeamNotActiveException from
    // ParticipantAlreadyAssignedToTeamException. Verify the exact field/value
    // against the live backend before finalising these match strings.
    let body: Record<string, unknown> = {}
    try {
      body = await response.json()
    } catch {
      // body unreadable — fall through to default 409 code
    }
    const detail = String(body.detail ?? '').toLowerCase()
    if (detail.includes('is not active') || detail.includes('not active')) {
      throw new Error('team_not_active')
    }
    throw new Error('participant_already_assigned')
  }

  if (response.status === 422) {
    throw new Error('user_not_participant_role')
  }

  if (!response.ok) {
    throw new IdentityError(
      'unknown',
      `assignParticipant failed with status ${response.status}`,
    )
  }

  // 201 body (membership id) is discarded — optimistic update handles UI state.
  // 204 with no body is also handled correctly here.
}
