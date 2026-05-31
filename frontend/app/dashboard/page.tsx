import { verifySession, enforceActivePlatformAccess } from '@/app/lib/dal'
import { redirect } from 'next/navigation'
import DashboardClient from './DashboardClient'
import type { Role } from '@/app/lib/definitions'

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

  return (
    <DashboardClient
      role={toDashboardRole(session.role)}
      displayName={session.displayName}
    />
  )
}
