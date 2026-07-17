import 'server-only'
import {
  IdentityError,
  type MissionDto,
  type MissionReadinessDto,
  type AddMissionNodeRequest,
  type UpdateMissionNodeRequest,
  type AssignPlayModeRequest,
  type AddTargetRequest,
  type UpdateTargetRequest,
} from './definitions'
import { verifySession } from './dal'
import { API_GATEWAY_URL, getGatewayHeaders } from './gateway'

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
  await verifySession()
  const response = await fetch(`${API_GATEWAY_URL}/api/missions/${missionId}/nodes`, {
    method: 'POST',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
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
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/missions/${missionId}/nodes/${nodeId}`,
    {
      method: 'PUT',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(body),
    },
  )
  if (!response.ok) await mapStructureError(response, 'updateMissionNode')
  return response.json()
}

export async function removeMissionNode(missionId: number, nodeId: number): Promise<MissionDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/missions/${missionId}/nodes/${nodeId}`,
    { method: 'DELETE', headers: await getGatewayHeaders() },
  )
  if (!response.ok) await mapStructureError(response, 'removeMissionNode')
  return response.json() // DELETE returns the full MissionResponse
}

// --- 2.2: play-mode + treasure-hunt targets + clue association ---
// All these routes carry BOTH stageId and substageId in the path (verified against
// MissionsEndpoints.cs); the plan's `.../substages/{ssid}/...` is an abbreviation.

const substageBase = (missionId: number, stageId: number, substageId: number) =>
  `${API_GATEWAY_URL}/api/missions/${missionId}/stages/${stageId}/substages/${substageId}`

export async function assignSubstagePlayMode(
  missionId: number,
  stageId: number,
  substageId: number,
  body: AssignPlayModeRequest,
): Promise<MissionDto> {
  await verifySession()
  const response = await fetch(`${substageBase(missionId, stageId, substageId)}/play-mode`, {
    method: 'PUT',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
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
  await verifySession()
  const response = await fetch(`${substageBase(missionId, stageId, substageId)}/targets`, {
    method: 'POST',
    headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
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
  await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}`,
    {
      method: 'PUT',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
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
  await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}`,
    { method: 'DELETE', headers: await getGatewayHeaders() },
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
  await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}/clue-association`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
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
  await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/targets/${targetId}/clue-association`,
    { method: 'DELETE', headers: await getGatewayHeaders() },
  )
  if (!response.ok) await mapStructureError(response, 'unassociateClueFromTarget')
  return response.json()
}

// --- 2.3: trivia-quiz selection + readiness ---
// POST sets a selection on a substage with none; PUT replaces an existing one. Both
// handlers are identical server-side (validate published + SelectTriviaQuiz), so the
// verb is dispatched purely on whether a selection already exists.

export async function setTriviaQuizSelection(
  missionId: number,
  stageId: number,
  substageId: number,
  triviaQuizId: number,
): Promise<MissionDto> {
  await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/trivia-quiz-selection`,
    {
      method: 'POST',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ triviaQuizId }),
    },
  )
  if (!response.ok) await mapStructureError(response, 'setTriviaQuizSelection')
  return response.json()
}

export async function updateTriviaQuizSelection(
  missionId: number,
  stageId: number,
  substageId: number,
  triviaQuizId: number,
): Promise<MissionDto> {
  await verifySession()
  const response = await fetch(
    `${substageBase(missionId, stageId, substageId)}/trivia-quiz-selection`,
    {
      method: 'PUT',
      headers: await getGatewayHeaders({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ triviaQuizId }),
    },
  )
  if (!response.ok) await mapStructureError(response, 'updateTriviaQuizSelection')
  return response.json()
}

export async function getMissionReadiness(missionId: number): Promise<MissionReadinessDto> {
  await verifySession()
  const response = await fetch(
    `${API_GATEWAY_URL}/api/missions/${missionId}/readiness`,
    { headers: await getGatewayHeaders(), cache: 'no-store' },
  )
  if (!response.ok) await mapStructureError(response, 'getMissionReadiness')
  return response.json()
}
