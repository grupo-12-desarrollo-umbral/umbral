'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listMissions,
  getMissionById,
  createMission as createMissionLib,
  updateMission as updateMissionLib,
  deactivateMission as deactivateMissionLib,
} from '@/app/lib/missions'
import { revalidatePath } from 'next/cache'
import type { MissionSummaryDto, MissionDto } from '@/app/lib/definitions'

export async function getMissions(): Promise<MissionSummaryDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  return listMissions()
}

export async function getMission(id: number): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  return getMissionById(id)
}

export async function createMission(
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  const result = await createMissionLib(name, description, difficulty, maximumTimeMinutes)
  revalidatePath('/dashboard')
  return result
}

export async function updateMission(
  id: number,
  name: string,
  description: string,
  difficulty: string,
  maximumTimeMinutes: number,
): Promise<MissionDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  const result = await updateMissionLib(id, name, description, difficulty, maximumTimeMinutes)
  revalidatePath('/dashboard')
  return result
}

export async function deactivateMission(id: number): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await deactivateMissionLib(id)
  revalidatePath('/dashboard')
}
