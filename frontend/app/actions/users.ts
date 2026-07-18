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

// Discriminated result rather than a thrown error: a Server Action that throws has its message
// redacted in production ("An error occurred in the Server Components render…"), so the client can
// never show *why* the invite failed. Returning the reason as data keeps it intact across the
// server→client boundary. See app/dashboard/DashboardClient.tsx handleInvite for the consumer.
export type InviteUserActionResult =
  | { ok: true; data: InviteUserResultDto }
  | { ok: false; error: string }

export async function inviteUser(email: string, role: InvitableRole): Promise<InviteUserActionResult> {
  const session = await verifySession()
  // Re-check the role on the server rather than trusting the client-gated form.
  if (session.role !== 'Administrator') {
    return { ok: false, error: 'Acceso denegado. Se requiere rol de Administrador.' }
  }
  // Reject a role the invitation flow does not allow before the request leaves the BFF.
  if (!INVITABLE_ROLES.includes(role)) {
    return { ok: false, error: 'Solo se pueden invitar cuentas de Operador y Administrador.' }
  }
  try {
    const data = await inviteUserLib(email.trim(), role)
    revalidatePath('/dashboard') // invalidates any cached users data
    return { ok: true, data }
  } catch (err) {
    // The BFF throws IdentityError / Error with a human-readable (Spanish) message per status code.
    return {
      ok: false,
      error:
        err instanceof Error && err.message
          ? err.message
          : 'No se pudo enviar la invitación. Inténtalo de nuevo.',
    }
  }
}

export async function assignUserRole(id: number, role: string): Promise<void> {
  const session = await verifySession()
  if (session.role !== 'Administrator') {
    throw new Error('Forbidden')
  }
  await assignUserRoleLib(id, role)
  revalidatePath('/dashboard')
}
