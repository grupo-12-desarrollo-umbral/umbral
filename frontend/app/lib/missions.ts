import 'server-only'
import { IdentityError, type MissionSummaryDto, type MissionDto } from './definitions'
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

export async function listMissions(): Promise<MissionSummaryDto[]> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions`, {
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
    throw new IdentityError('unknown', `listMissions failed with status ${response.status}`)
  }

  return response.json()
}

export async function getMissionById(id: number): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${id}`, {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden.')
  }
  if (response.status === 404) {
    throw new Error('mission_not_found')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `getMissionById failed with status ${response.status}`)
  }

  return response.json()
}

export async function createMission(
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions`, {
    method: 'POST',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ name, description, difficulty, maximumTimeMinutes }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `createMission failed with status ${response.status}`)
  }

  return response.json()
}

export async function updateMission(
  id: number,
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${id}`, {
    method: 'PUT',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ name, description, difficulty, maximumTimeMinutes }),
  })

  if (response.status === 400) {
    throw new Error('invalid_fields')
  }
  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('mission_not_found')
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `updateMission failed with status ${response.status}`)
  }

  return response.json()
}

export async function activateMission(id: number): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${id}/activate`, {
    method: 'POST',
    headers: getIdentityHeaders(session),
  })

  if (response.status === 400) {
    // Defensive: readiness failures now return 409 (not 400). Surface any ProblemDetails
    // `detail` verbatim should a validation 400 ever reach this endpoint.
    let detail = 'Mission could not be activated.'
    try {
      const problem = await response.json()
      if (typeof problem?.detail === 'string' && problem.detail.length > 0) {
        detail = problem.detail
      }
    } catch {
      /* keep fallback message */
    }
    throw new Error(detail)
  }
  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('mission_not_found')
  }
  if (response.status === 409) {
    // Activation conflicts — "mission already active" or "runtime plan not ready" — both arrive
    // as 409. `detail` carries the full message; the `type` slug only selects the fallback
    // wording for the rare case where the body can't be read.
    let problem: { type?: string; detail?: string } | null = null
    try {
      problem = await response.json()
    } catch {
      /* body unreadable — fall back on type/default below */
    }
    if (typeof problem?.detail === 'string' && problem.detail.length > 0) {
      throw new Error(problem.detail)
    }
    throw new Error(
      problem?.type === 'mission-not-ready-for-activation'
        ? 'Mission is not ready for activation.'
        : 'Mission is already active.',
    )
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `activateMission failed with status ${response.status}`)
  }

  return response.json()
}

export async function deactivateMission(id: number): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${id}`, {
    method: 'DELETE',
    headers: getIdentityHeaders(session),
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }
  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }
  if (response.status === 404) {
    throw new Error('mission_not_found')
  }
  if (response.status === 409) {
    // Backend now maps MissionAlreadyDeactivatedException → 409 (HU-09). Surface the
    // ProblemDetails `detail` verbatim instead of the old generic failure.
    let detail = 'Mission is already deactivated.'
    try {
      const problem = await response.json()
      if (typeof problem?.detail === 'string' && problem.detail.length > 0) detail = problem.detail
    } catch {
      /* keep fallback */
    }
    throw new Error(detail)
  }
  if (!response.ok) {
    throw new IdentityError('unknown', `deactivateMission failed with status ${response.status}`)
  }
}
