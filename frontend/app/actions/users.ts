'use server'

import { verifySession } from '@/app/lib/dal'
import { listUsers, deactivateUserAccess } from '@/app/lib/users'
import { revalidatePath } from 'next/cache'
import type { PagedResult, UserAccessCatalogItemDto } from '@/app/lib/definitions'

export async function getUsersPage(
  page: number,
  pageSize = 20,
): Promise<PagedResult<UserAccessCatalogItemDto>> {
  const session = await verifySession()
  // Both Administrator and Operator may list users
  if (session.role !== 'Administrator' && session.role !== 'Operator') {
    throw new Error('Forbidden')
  }
  return listUsers(page, pageSize)
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
