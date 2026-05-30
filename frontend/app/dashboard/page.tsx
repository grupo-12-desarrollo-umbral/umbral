import { verifySession } from '@/app/lib/dal'
import DashboardClient from './DashboardClient'

function toDashboardRole(role: 'Administrator' | 'Operator'): 'admin' | 'operator' {
  return role === 'Administrator' ? 'admin' : 'operator'
}

export default async function DashboardPage() {
  const session = await verifySession()

  return (
    <DashboardClient
      role={toDashboardRole(session.role)}
      displayName={session.displayName}
    />
  )
}
