import 'server-only'
import {
  IdentityError,
  type MissionDto,
  type AddMissionNodeRequest,
  type UpdateMissionNodeRequest,
  type AssignPlayModeRequest,
  type AddTargetRequest,
  type UpdateTargetRequest,
} from './definitions'
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

// 400 (invalid fields) and 409 (containment / state conflict) both carry a ProblemDetails
// `detail` the UI surfaces verbatim — it never invents placement copy (Architecture Decision 5).
async function mapStructureError(response: Response, fnName: string): Promise<never> {
  if (response.status === 400 || response.status === 409) {
    let detail =
      response.status === 409
        ? 'The request conflicts with the mission state.'
        : 'Invalid request.'
    try {
      const problem = await response.json()
      if (typeof problem?.detail === 'string' && problem.detail.length > 0) detail = problem.detail
    } catch {
      /* keep fallback */
    }
    throw new Error(detail)
  }
  if (response.status === 401) throw new IdentityError('unauthorized', 'Authentication failed.')
  if (response.status === 403)
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  if (response.status === 404) throw new Error('mission_not_found')
  throw new IdentityError('unknown', `${fnName} failed with status ${response.status}`)
}

export async function addMissionNode(
  missionId: number,
  body: AddMissionNodeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${MISSION_DESIGN_SERVICE_URL}/api/missions/${missionId}/nodes`, {
    method: 'POST',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) await mapStructureError(response, 'addMissionNode')
  return response.json()
}

export async function updateMissionNode(
  missionId: number,
  nodeId: number,
  body: UpdateMissionNodeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/missions/${missionId}/nodes/${nodeId}`,
    {
      method: 'PUT',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    },
  )
  if (!response.ok) await mapStructureError(response, 'updateMissionNode')
  return response.json()
}

export async function removeMissionNode(missionId: number, nodeId: number): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${MISSION_DESIGN_SERVICE_URL}/api/missions/${missionId}/nodes/${nodeId}`,
    { method: 'DELETE', headers: getIdentityHeaders(session) },
  )
  if (!response.ok) await mapStructureError(response, 'removeMissionNode')
  return response.json() // DELETE returns the full MissionResponse
}

// --- 2.2: play-mode + treasure-hunt targets + clue association ---
// All these routes carry BOTH stageId and substageId in the path (verified against
// MissionsEndpoints.cs); the plan's `.../substages/{ssid}/...` is an abbreviation.

const substageBase = (missionId: number, stageId: number, substageId: number) =>
  `${MISSION_DESIGN_SERVICE_URL}/api/missions/${missionId}/stages/${stageId}/substages/${substageId}`

export async function assignSubstagePlayMode(
  missionId: number,
  stageId: number,
  substageId: number,
  body: AssignPlayModeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${substageBase(missionId, stageId, substageId)}/play-mode`, {
    method: 'PUT',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) await mapStructureError(response, 'assignSubstagePlayMode')
  return response.json()
}

export async function addTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  body: AddTargetRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(`${substageBase(missionId, stageId, substageId)}/targets`, {
    method: 'POST',
    headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) await mapStructureError(response, 'addTarget')
  return response.json()
}

export async function updateTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
  body: UpdateTargetRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}`,
    {
      method: 'PUT',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    },
  )
  if (!response.ok) await mapStructureError(response, 'updateTarget')
  return response.json()
}

export async function removeTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}`,
    { method: 'DELETE', headers: getIdentityHeaders(session) },
  )
  if (!response.ok) await mapStructureError(response, 'removeTarget')
  return response.json()
}

export async function associateClueWithTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
  clueId: number,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}/clue-association`,
    {
      method: 'POST',
      headers: { ...getIdentityHeaders(session), 'Content-Type': 'application/json' },
      body: JSON.stringify({ clueId }),
    },
  )
  if (!response.ok) await mapStructureError(response, 'associateClueWithTarget')
  return response.json()
}

export async function unassociateClueFromTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
): Promise<MissionDto> {
  const session = await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}/clue-association`,
    { method: 'DELETE', headers: getIdentityHeaders(session) },
  )
  if (!response.ok) await mapStructureError(response, 'unassociateClueFromTarget')
  return response.json()
}
