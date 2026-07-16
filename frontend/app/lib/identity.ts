import 'server-only'
import { IdentityError, type AuthenticateUserResultDto, type AuthenticatedActorProfileDto, type ProtectedAccessDecisionDto } from './definitions'

const API_GATEWAY_URL = process.env.API_GATEWAY_URL!

// Direct service URL for BFF-to-service calls (bypasses JWT gateway auth).
// The identity-access-service is exposed on port 5002 directly during local dev.
const IDENTITY_SERVICE_URL = 'http://localhost:5002'

// Cap BFF-to-service calls so a down or unresponsive backend (e.g. running
// `pnpm dev` for the frontend alone) degrades to /login instead of hanging the
// RSC render forever. Network failures and timeouts are normalised to
// IdentityError('unknown') so the existing dal/page guards can redirect.
const IDENTITY_FETCH_TIMEOUT_MS = 5000

async function identityFetch(input: string, init?: RequestInit): Promise<Response> {
  try {
    return await fetch(input, { ...init, signal: AbortSignal.timeout(IDENTITY_FETCH_TIMEOUT_MS) })
  } catch (err) {
    if (err instanceof IdentityError) throw err
    const reason = err instanceof Error && err.name === 'TimeoutError' ? 'timed out' : 'is unreachable'
    throw new IdentityError('unknown', `Identity backend ${reason} (${input})`)
  }
}

export async function getCurrentUserProfile(
  externalIdentityId: string,
  role: string,
  email: string,
): Promise<AuthenticatedActorProfileDto> {
  const response = await identityFetch(`${IDENTITY_SERVICE_URL}/api/users/me`, {
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
  const response = await identityFetch(`${IDENTITY_SERVICE_URL}/api/permissions/authenticated-platform-access`, {
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
  const response = await identityFetch(`${API_GATEWAY_URL}/api/users/authenticated`, {
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
