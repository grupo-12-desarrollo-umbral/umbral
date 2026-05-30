import 'server-only'
import { cache } from 'react'
import { cookies } from 'next/headers'
import { redirect } from 'next/navigation'
import { decrypt } from './session'
import type { SessionPayload } from './definitions'

export const verifySession = cache(async (): Promise<SessionPayload> => {
  const cookie = (await cookies()).get('session')?.value
  const session = await decrypt(cookie)

  if (!session) redirect('/login')
  if (!session.isActive) redirect('/login?error=deactivated')

  return session
})

export async function getSessionUser(): Promise<Pick<SessionPayload, 'externalIdentityId' | 'displayName' | 'email' | 'role'>> {
  const session = await verifySession()
  return {
    externalIdentityId: session.externalIdentityId,
    displayName: session.displayName,
    email: session.email,
    role: session.role,
  }
}
