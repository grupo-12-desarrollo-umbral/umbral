'use server'

import { verifySession } from '@/app/lib/dal'
import {
  addMissionNode as addMissionNodeLib,
  updateMissionNode as updateMissionNodeLib,
  removeMissionNode as removeMissionNodeLib,
  assignSubstagePlayMode as assignSubstagePlayModeLib,
  addTarget as addTargetLib,
  updateTarget as updateTargetLib,
  removeTarget as removeTargetLib,
  associateClueWithTarget as associateClueWithTargetLib,
  unassociateClueFromTarget as unassociateClueFromTargetLib,
} from '@/app/lib/mission-structure'
import { revalidatePath } from 'next/cache'
import type {
  MissionDto,
  AddMissionNodeRequest,
  UpdateMissionNodeRequest,
  AssignPlayModeRequest,
  AddTargetRequest,
  UpdateTargetRequest,
} from '@/app/lib/definitions'

export async function addMissionNode(
  missionId: number,
  body: AddMissionNodeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await addMissionNodeLib(missionId, body)
  revalidatePath('/dashboard')
  return result
}

export async function updateMissionNode(
  missionId: number,
  nodeId: number,
  body: UpdateMissionNodeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await updateMissionNodeLib(missionId, nodeId, body)
  revalidatePath('/dashboard')
  return result
}

export async function removeMissionNode(missionId: number, nodeId: number): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await removeMissionNodeLib(missionId, nodeId)
  revalidatePath('/dashboard')
  return result
}

export async function assignSubstagePlayMode(
  missionId: number,
  stageId: number,
  substageId: number,
  body: AssignPlayModeRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await assignSubstagePlayModeLib(missionId, stageId, substageId, body)
  revalidatePath('/dashboard')
  return result
}

export async function addTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  body: AddTargetRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await addTargetLib(missionId, stageId, substageId, body)
  revalidatePath('/dashboard')
  return result
}

export async function updateTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
  body: UpdateTargetRequest,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await updateTargetLib(missionId, stageId, substageId, targetId, body)
  revalidatePath('/dashboard')
  return result
}

export async function removeTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await removeTargetLib(missionId, stageId, substageId, targetId)
  revalidatePath('/dashboard')
  return result
}

export async function associateClueWithTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
  clueId: number,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await associateClueWithTargetLib(missionId, stageId, substageId, targetId, clueId)
  revalidatePath('/dashboard')
  return result
}

export async function unassociateClueFromTarget(
  missionId: number,
  stageId: number,
  substageId: number,
  targetId: number,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') throw new Error('Forbidden')
  const result = await unassociateClueFromTargetLib(missionId, stageId, substageId, targetId)
  revalidatePath('/dashboard')
  return result
}
