'use server'

import { verifySession } from '@/app/lib/dal'
import {
  addMissionNode as addMissionNodeLib,
  updateMissionNode as updateMissionNodeLib,
  removeMissionNode as removeMissionNodeLib,
} from '@/app/lib/mission-structure'
import { revalidatePath } from 'next/cache'
import type {
  MissionDto,
  AddMissionNodeRequest,
  UpdateMissionNodeRequest,
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
