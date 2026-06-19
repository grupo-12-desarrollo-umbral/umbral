import 'server-only'
import {
  IdentityError,
  type CreateTriviaSessionRequest,
  type TriviaSessionCreatedDto,
  type SessionAssignmentSummaryDto,
  type SessionAssociatedTeamsDto,
  type AssociateTeamToSessionResultDto,
  type AssignSessionOperatorResultDto,
  type SessionLifecycleState,
  type TransitionSessionStateResultDto,
  type SessionTimerSnapshotDto,
} from './definitions'
import { verifySession } from './dal'
import { KeycloakAuthError } from './keycloak'
import { getValidAccessToken } from './keycloak-tokens'

const API_GATEWAY_URL = process.env.API_GATEWAY_URL!

async function getGatewayHeaders(headers?: HeadersInit): Promise<Headers> {
  try {
    const accessToken = await getValidAccessToken()
    const gatewayHeaders = new Headers(headers)
    gatewayHeaders.set('Authorization', `Bearer ${accessToken}`)
    return gatewayHeaders
  } catch (error) {
    if (
      error instanceof KeycloakAuthError ||
      (error instanceof Error && error.name === 'KeycloakAuthError')
    ) {
      throw new IdentityError('unauthorized', 'Authentication failed.')
    }

    throw error
  }
}

export async function createTriviaSession(
  req: CreateTriviaSessionRequest,
): Promise<TriviaSessionCreatedDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
    method: 'POST',
    headers: await getGatewayHeaders({
      'Content-Type': 'application/json',
    }),
    body: JSON.stringify(req),
  })

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator role required.')
  if (response.status === 404) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null
    if (problem?.detail?.includes('Mission')) throw new Error('mission_not_found')
    throw new Error('quiz_not_found')
  }
  if (response.status === 409) {
    const problem = (await response.json().catch(() => null)) as { type?: string } | null
    if (problem?.type === 'mission-not-eligible-for-session') throw new Error('mission_not_eligible')
    throw new Error('quiz_not_published')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createTriviaSession failed with status ${response.status}`)
  }

  return response.json() as Promise<TriviaSessionCreatedDto>
}

export async function listAssignableSessions(): Promise<SessionAssignmentSummaryDto[]> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (!response.ok) {
    throw new IdentityError('unknown', `listAssignableSessions failed with status ${response.status}`)
  }
  return response.json() as Promise<SessionAssignmentSummaryDto[]>
}

export async function listOperatorSessions(): Promise<SessionAssignmentSummaryDto[]> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator session listing unavailable.')
  if (!response.ok) {
    throw new IdentityError('unknown', `listOperatorSessions failed with status ${response.status}`)
  }
  return response.json() as Promise<SessionAssignmentSummaryDto[]>
}

export async function assignSessionOperator(
  liveSessionId: string,
  operatorUserId: number,
): Promise<AssignSessionOperatorResultDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/operator-assignment`,
    {
      method: 'PATCH',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ operatorUserId }),
    },
  )

  if (response.status === 400) throw new Error('invalid_input')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Administrator role required.')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 422) throw new Error('ineligible_operator')
  if (!response.ok) {
    throw new IdentityError('unknown', `assignSessionOperator failed with status ${response.status}`)
  }
  return response.json() as Promise<AssignSessionOperatorResultDto>
}

export async function transitionSessionState(
  liveSessionId: string,
  targetState: SessionLifecycleState,
  reason?: string,
): Promise<TransitionSessionStateResultDto> {
  await verifySession()
  const body = reason?.trim()
    ? { targetState, reason: reason.trim() }
    : { targetState }
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions/${liveSessionId}/state`, {
    method: 'PATCH',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(body),
  })

  if (response.status === 400) throw new Error('invalid_payload')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new Error('not_assigned_operator')
  if (response.status === 404) throw new Error('session_not_found')
  if (response.status === 409) {
    // The backend tags each transition conflict with a stable ProblemDetails `type` so the UI can
    // show a specific cause (no teams, no assigned operator) rather than one vague message.
    const problem = (await response.json().catch(() => null)) as { type?: string } | null
    switch (problem?.type) {
      case 'session-no-teams':
        throw new Error('no_teams')
      case 'session-operator-unassigned':
        throw new Error('session_unassigned')
      default:
        throw new Error('invalid_transition')
    }
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `transitionSessionState failed with status ${response.status}`)
  }

  return response.json() as Promise<TransitionSessionStateResultDto>
}

export async function getOperatorSessionTimerSnapshot(
  liveSessionId: string,
): Promise<SessionTimerSnapshotDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/sessions/${liveSessionId}/timer`,
    {
      headers: await getGatewayHeaders(),
      cache: 'no-store',
    },
  )

  if (response.status === 401) throw new IdentityError('unauthorized', 'Timer read: auth expired')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Timer read: not assigned operator')
  if (response.status === 404) throw new IdentityError('unknown', 'Session not found')
  if (!response.ok) throw new IdentityError('unknown', `Timer read failed with status ${response.status}`)

  return response.json() as Promise<SessionTimerSnapshotDto>
}

export async function getSessionAssociatedTeams(
  liveSessionId: string,
): Promise<SessionAssociatedTeamsDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions/${liveSessionId}/teams`, {
    headers: await getGatewayHeaders(),
    cache: 'no-store',
  })

  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator role required.')
  if (response.status === 404) throw new Error('session_not_found')
  if (!response.ok) {
    throw new IdentityError('unknown', `getSessionAssociatedTeams failed with status ${response.status}`)
  }

  return response.json() as Promise<SessionAssociatedTeamsDto>
}

export async function associateTeamToSession(
  liveSessionId: string,
  referenceTeamId: string,
): Promise<AssociateTeamToSessionResultDto> {
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/sessions/${liveSessionId}/teams`, {
    method: 'POST',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ referenceTeamId }),
  })

  if (response.status === 400) throw new Error('inactive_team')
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403) throw new IdentityError('unauthorized', 'Operator role required.')
  if (response.status === 404) throw new Error('not_found')
  if (response.status === 409) {
    const problem = (await response.json().catch(() => null)) as { detail?: string } | null
    const detail = problem?.detail?.toLowerCase() ?? ''
    if (detail.includes('already associated')) {
      throw new Error('duplicate_association')
    }
    if (detail.includes('scheduled')) {
      throw new Error('session_not_scheduled')
    }
    throw new Error('association_conflict')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `associateTeamToSession failed with status ${response.status}`)
  }

  return response.json() as Promise<AssociateTeamToSessionResultDto>
}
