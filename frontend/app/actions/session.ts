'use server'

import { createSession, decrypt } from '@/app/lib/session'
import { getCurrentUserProfile } from '@/app/lib/identity'
import { cookies } from 'next/headers'
import type { Role } from '@/app/lib/definitions'

export async function refreshSession(): Promise<{ role: string; isActive: boolean } | null> {
  const cookieStore = await cookies()
  const sessionCookie = cookieStore.get('session')?.value
  if (!sessionCookie) return null

  const session = await decrypt(sessionCookie)
  if (!session) return null

  const profile = await getCurrentUserProfile(
    session.externalIdentityId,
    session.role,
    session.email,
  )

  if (profile.role !== session.role || profile.isActive !== session.isActive) {
    await createSession({
      externalIdentityId: profile.externalIdentityId,
      displayName: profile.displayName,
      email: profile.email,
      role: profile.role as Role,
      isActive: profile.isActive,
    })
  }

  return { role: profile.role, isActive: profile.isActive }
}
