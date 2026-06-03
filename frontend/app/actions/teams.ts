'use server'

import { verifySession } from '@/app/lib/dal'
import {
  listTeams,
  getTeamById,
  createTeam as createTeamLib,
  updateTeam as updateTeamLib,
  deactivateTeam as deactivateTeamLib,
  listTeamParticipants,
  assignParticipant as assignParticipantLib,
} from '@/app/lib/teams'
import { revalidatePath } from 'next/cache'
import type { PagedResult, TeamDto, CreateTeamResultDto, TeamMembershipDto } from '@/app/lib/definitions'

export async function getTeamsPage(
  page: number,
  pageSize = 20,
): Promise<PagedResult<TeamDto>> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return listTeams(page, pageSize)
}

export async function getTeam(id: string): Promise<TeamDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return getTeamById(id)
}

export async function createTeam(
  displayName: string,
  teamCode: string,
): Promise<CreateTeamResultDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  const result = await createTeamLib(displayName, teamCode)
  revalidatePath('/dashboard')
  return result
}

export async function updateTeam(
  id: string,
  displayName: string,
  teamCode: string,
): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  await updateTeamLib(id, displayName, teamCode)
  revalidatePath('/dashboard')
}

export async function deactivateTeam(id: string): Promise<TeamDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  const result = await deactivateTeamLib(id)
  revalidatePath('/dashboard')
  return result
}

export async function getTeamParticipants(
  teamId: string,
): Promise<TeamMembershipDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return listTeamParticipants(teamId)
}

export async function assignParticipantToTeam(
  teamId: string,
  userId: number,
): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  await assignParticipantLib(teamId, userId)
  revalidatePath('/dashboard')
}
