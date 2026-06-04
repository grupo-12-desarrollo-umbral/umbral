'use server'

import { verifySession } from '@/app/lib/dal'
import {
  getSessionAssociatedTeams as getSessionAssociatedTeamsLib,
  associateTeamToSession as associateTeamToSessionLib,
} from '@/app/lib/session-operations'
import { revalidatePath } from 'next/cache'
import type {
  SessionAssociatedTeamsDto,
  AssociateTeamToSessionResultDto,
} from '@/app/lib/definitions'

export async function getSessionTeams(
  liveSessionId: string,
): Promise<SessionAssociatedTeamsDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return getSessionAssociatedTeamsLib(liveSessionId)
}

export async function associateTeamToSession(
  liveSessionId: string,
  referenceTeamId: string,
): Promise<AssociateTeamToSessionResultDto> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  const result = await associateTeamToSessionLib(liveSessionId, referenceTeamId)
  revalidatePath('/dashboard')
  return result
}
