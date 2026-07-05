import { verifySession, enforceActivePlatformAccess } from '@/app/lib/dal'
import { getCurrentUserProfile } from '@/app/lib/identity'
import { redirect } from 'next/navigation'
import DashboardClient from './DashboardClient'
import { IdentityError, type Role } from '@/app/lib/definitions'

function toDashboardRole(role: Role): 'admin' | 'operator' | 'participant' {
  switch (role) {
    case 'Administrator': return 'admin'
    case 'Operator':      return 'operator'
    case 'Participant':   return 'participant'
    default:
      redirect('/login')
  }
}

export default async function DashboardPage() {
  const session = await verifySession()
  await enforceActivePlatformAccess()

  // Guard the profile fetch like enforceActivePlatformAccess does: a BFF IdentityError
  // thrown here aborts the RSC stream, which under Next 16 dev surfaces as an
  // uncaughtException (ECONNRESET) that destabilises the server and cascade-fails every
  // subsequent e2e test. Degrade to /login instead of throwing.
  let profile
  try {
    profile = await getCurrentUserProfile(
      session.externalIdentityId,
      session.role,
      session.email,
    )
  } catch (err) {
    if (err instanceof IdentityError) {
      if (err.code === 'deactivated') redirect('/login?error=deactivated')
      redirect('/login')
    }
    throw err
  }
  const role = toDashboardRole(profile.role as Role)

  return (
    <DashboardClient
      role={role}
      displayName={profile.displayName}
    />
  )
}
