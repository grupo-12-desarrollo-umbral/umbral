'use server'

import { verifySession } from '@/app/lib/dal'
import { listDedupedUsers, listAssignableParticipants, deactivateUserAccess, assignUserRole as assignUserRoleLib, inviteUser as inviteUserLib } from '@/app/lib/users'
import { revalidatePath } from 'next/cache'
import type { AssignableParticipantDto, InvitableRole, InviteUserResultDto, PagedResult, UserAccessCatalogItemDto } from '@/app/lib/definitions'

export async function getUsersPage(
  page: number,
  pageSize = 20,
): Promise<PagedResult<UserAccessCatalogItemDto>> {
  const session = await verifySession()
  // Issue #148: the Users view is Administrator-only; re-check the role here rather
  // than trusting the client-side nav gate.
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  return listDedupedUsers(page, pageSize)
}

// Operators manage teams, so they may list the participants they can assign — but not the
// full users catalog, which stays Administrator-only above (#148). Mirrors the backend's
// AdminOrOperator policy on GET /api/users.
export async function getAssignableParticipants(): Promise<AssignableParticipantDto[]> {
  const session = await verifySession()
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return listAssignableParticipants()
}

export async function deactivateUser(id: number): Promise<void> {
  const session = await verifySession()
  // Only Administrator may deactivate
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await deactivateUserAccess(id)
  revalidatePath('/dashboard') // invalidates any cached users data
}

const INVITABLE_ROLES: readonly InvitableRole[] = ['Operator', 'Administrator']

export async function inviteUser(email: string, role: InvitableRole): Promise<InviteUserResultDto> {
  const session = await verifySession()
  // Re-check the role on the server rather than trusting the client-gated form.
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  // Reject a role the invitation flow does not allow before the request leaves the BFF.
  if (!INVITABLE_ROLES.includes(role)) {
    throw new Error('Only Operator and Administrator accounts can be invited.')
  }
  const result = await inviteUserLib(email.trim(), role)
  revalidatePath('/dashboard') // invalidates any cached users data
  return result
}

export async function assignUserRole(id: number, role: string): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await assignUserRoleLib(id, role)
  revalidatePath('/dashboard')
}
