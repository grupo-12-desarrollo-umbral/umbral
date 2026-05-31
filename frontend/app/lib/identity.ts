import 'server-only'
import { IdentityError, type AuthenticateUserResultDto, type AuthenticatedActorProfileDto, type ProtectedAccessDecisionDto } from './definitions'

const API_GATEWAY_URL = process.env.API_GATEWAY_URL!

// Direct service URL for BFF-to-service calls (bypasses JWT gateway auth).
// The identity-access-service is exposed on port 5002 directly during local dev.
const IDENTITY_SERVICE_URL = 'http://localhost:5002'

export async function getCurrentUserProfile(
  externalIdentityId: string,
  role: string,
  email: string,
): Promise<AuthenticatedActorProfileDto> {
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/users/me`, {
    headers: {
      'X-User-Id': externalIdentityId,
      'X-User-Role': role,
      'X-User-Email': email,
    },
    cache: 'no-store',
  })

  if (!response.ok) {
    throw new IdentityError('unknown', `getCurrentUserProfile failed with status ${response.status}`)
  }

  return response.json()
}

export async function checkPlatformAccess(
  externalIdentityId: string,
  role: string,
  email: string,
): Promise<ProtectedAccessDecisionDto> {
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/permissions/authenticated-platform-access`, {
    headers: {
      'X-User-Id': externalIdentityId,
      'X-User-Role': role,
      'X-User-Email': email,
    },
  })

  if (response.status === 403) {
    throw new IdentityError('deactivated', 'Your account has been deactivated.')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `checkPlatformAccess failed with status ${response.status}`)
  }

  return response.json()
}

export async function bootstrapUser(
  accessToken: string,
  displayName: string
): Promise<AuthenticateUserResultDto> {
  const response = await fetch(`${API_GATEWAY_URL}/api/users/authenticated`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ displayName }),
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed. Invalid or missing token.')
  }

  if (response.status === 403) {
    throw new IdentityError('deactivated', 'Your account has been deactivated.')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `Bootstrap failed with status ${response.status}`)
  }

  return response.json()
}
