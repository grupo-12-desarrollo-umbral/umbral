import 'server-only'
import { IdentityError, type PagedResult, type UserAccessCatalogItemDto } from './definitions'
import { verifySession } from './dal'

// Direct service URL for BFF-to-service calls (bypasses JWT gateway auth)
const IDENTITY_SERVICE_URL = 'http://localhost:5002'

function getIdentityHeaders(session: {
  externalIdentityId: string
  displayName: string
  email: string
  role: string
}) {
  return {
    'X-User-Id': session.externalIdentityId,
    'X-User-Role': session.role,
    'X-User-Email': session.email,
  }
}

export async function listUsers(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<UserAccessCatalogItemDto>> {
  const session = await verifySession()
  const url = new URL(`${IDENTITY_SERVICE_URL}/api/users`)
  url.searchParams.set('page', String(page))
  url.searchParams.set('pageSize', String(pageSize))

  const response = await fetch(url.toString(), {
    headers: getIdentityHeaders(session),
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed. Missing or invalid trusted headers.')
  }

  if (response.status === 403) {
    throw new IdentityError('deactivated', 'Your account has been deactivated.')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `listUsers failed with status ${response.status}`)
  }

  return response.json()
}

export async function deactivateUserAccess(id: number): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/users/${id}/access`, {
    method: 'DELETE',
    headers: getIdentityHeaders(session),
  })

  if (response.status === 400) {
    throw new Error('already_deactivated')
  }

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed. Missing or invalid trusted headers.')
  }

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `deactivateUserAccess failed with status ${response.status}`)
  }
}
