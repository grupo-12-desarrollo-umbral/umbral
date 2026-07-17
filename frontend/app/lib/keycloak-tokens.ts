import 'server-only'
import { createHash } from 'crypto'
import { EncryptJWT, jwtDecrypt } from 'jose'
import { cookies } from 'next/headers'
import {
  KeycloakAuthError,
  isExpiredOrNearExpiry,
  refreshAccessToken,
  toExpiresAtMs,
  type KeycloakTokenSet,
} from './keycloak'

export const KC_SESSION_COOKIE = 'kc_session'
const ACCESS_TOKEN_REFRESH_SKEW_MS = 60_000

type StoredKeycloakTokens = {
  accessToken: string
  refreshToken: string
  accessExpiresAt: number
  refreshExpiresAt: number
}

export type RefreshedKeycloakSession = {
  sealed: string
  refreshExpiresAt: number
}

export function kcSessionCookieOptions(refreshExpiresAt: number) {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax' as const,
    path: '/',
    expires: getCookieExpiry(refreshExpiresAt),
  }
}

// `cookies()` is read-only during a Server Component render: Next seals the store and every write
// throws ReadonlyRequestCookiesError. Proxy refreshes and persists kc_session ahead of the render
// (see proxy.ts), so a write arriving from an RSC render is already redundant — swallowing it keeps
// the page alive instead of 500ing. Route Handlers and Server Actions write for real.
function isReadonlyCookieStoreError(error: unknown): boolean {
  return error instanceof Error && error.message.includes('Cookies can only be modified')
}

function getSealSecret(): string {
  const explicitSecret = process.env.KC_TOKEN_SECRET
  if (explicitSecret) {
    return explicitSecret
  }

  if (process.env.NODE_ENV === 'production') {
    throw new Error('KC_TOKEN_SECRET is required in production')
  }

  const sessionSecret = process.env.SESSION_SECRET
  if (!sessionSecret) {
    throw new Error('SESSION_SECRET is required to derive the development kc_session seal secret')
  }

  return `umbral:kc-session:v1:${sessionSecret}`
}

function getEncodedSealKey(): Uint8Array {
  return createHash('sha256').update(getSealSecret()).digest()
}

function getCookieExpiry(refreshExpiresAt: number): Date {
  return new Date(refreshExpiresAt)
}

function mapTokenSetToStoredTokens(
  tokens: KeycloakTokenSet,
  nowMs: number = Date.now(),
): StoredKeycloakTokens {
  return {
    accessToken: tokens.accessToken,
    refreshToken: tokens.refreshToken,
    accessExpiresAt: toExpiresAtMs(tokens.expiresIn, nowMs),
    refreshExpiresAt: toExpiresAtMs(tokens.refreshExpiresIn, nowMs),
  }
}

function assertStoredTokensShape(payload: Record<string, unknown>): StoredKeycloakTokens {
  const accessToken = payload.accessToken
  const refreshToken = payload.refreshToken
  const accessExpiresAt = payload.accessExpiresAt
  const refreshExpiresAt = payload.refreshExpiresAt

  if (
    typeof accessToken !== 'string' ||
    typeof refreshToken !== 'string' ||
    typeof accessExpiresAt !== 'number' ||
    typeof refreshExpiresAt !== 'number'
  ) {
    throw new KeycloakAuthError('refresh_token', 'Invalid kc_session payload')
  }

  return {
    accessToken,
    refreshToken,
    accessExpiresAt,
    refreshExpiresAt,
  }
}

export async function sealKeycloakTokens(tokens: StoredKeycloakTokens): Promise<string> {
  return new EncryptJWT(tokens)
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(Math.floor(tokens.refreshExpiresAt / 1000))
    .encrypt(getEncodedSealKey())
}

export async function unsealKeycloakTokens(cookieValue: string): Promise<StoredKeycloakTokens> {
  const { payload } = await jwtDecrypt(cookieValue, getEncodedSealKey(), {
    keyManagementAlgorithms: ['dir'],
    contentEncryptionAlgorithms: ['A256GCM'],
  })

  return assertStoredTokensShape(payload)
}

export async function storeKeycloakTokens(
  tokens: KeycloakTokenSet,
  nowMs: number = Date.now(),
): Promise<void> {
  const storedTokens = mapTokenSetToStoredTokens(tokens, nowMs)
  const sealedTokens = await sealKeycloakTokens(storedTokens)
  const cookieStore = await cookies()

  try {
    cookieStore.set(
      KC_SESSION_COOKIE,
      sealedTokens,
      kcSessionCookieOptions(storedTokens.refreshExpiresAt),
    )
  } catch (error) {
    if (!isReadonlyCookieStoreError(error)) throw error
  }
}

export async function clearKeycloakTokens(): Promise<void> {
  const cookieStore = await cookies()

  try {
    cookieStore.delete(KC_SESSION_COOKIE)
  } catch (error) {
    if (!isReadonlyCookieStoreError(error)) throw error
  }
}

// Cookie-store-free twin of getValidAccessToken's refresh half, so Proxy — which has no `cookies()`
// — can drive the same decision and hand the result to both the request and the response. Returns
// null when the access token is still good; throws KeycloakAuthError when the session is beyond
// saving, which is the caller's cue to bounce to /login.
export async function refreshSealedKeycloakSession(
  sealedTokens: string,
  nowMs: number = Date.now(),
): Promise<RefreshedKeycloakSession | null> {
  let storedTokens: StoredKeycloakTokens

  try {
    storedTokens = await unsealKeycloakTokens(sealedTokens)
  } catch {
    throw new KeycloakAuthError('refresh_token', 'Invalid kc_session')
  }

  if (!isExpiredOrNearExpiry(storedTokens.accessExpiresAt, ACCESS_TOKEN_REFRESH_SKEW_MS, nowMs)) {
    return null
  }

  if (isExpiredOrNearExpiry(storedTokens.refreshExpiresAt, 0, nowMs)) {
    throw new KeycloakAuthError('refresh_token', 'Refresh token expired')
  }

  let refreshedTokens: KeycloakTokenSet

  try {
    refreshedTokens = await refreshAccessToken(storedTokens.refreshToken)
  } catch (error) {
    if (error instanceof KeycloakAuthError) throw error
    throw new KeycloakAuthError('refresh_token', 'Keycloak token refresh failed')
  }

  const refreshed = mapTokenSetToStoredTokens(refreshedTokens, nowMs)

  return {
    sealed: await sealKeycloakTokens(refreshed),
    refreshExpiresAt: refreshed.refreshExpiresAt,
  }
}

// Depends on the realm keeping `revokeRefreshToken` off (see backend/deploy/keycloak/import/
// umbral-realm.json, where it is pinned to false explicitly). Parallel server requests can enter
// this concurrently and each redeem the *same* refresh token; unlimited reuse is what makes that
// safe. Turning rotation on would make every loser of that race hand Keycloak a spent token and get
// the whole session revoked — it would need single-flight coordination here first.
export async function getValidAccessToken(nowMs: number = Date.now()): Promise<string> {
  const cookieStore = await cookies()
  const sealedTokens = cookieStore.get(KC_SESSION_COOKIE)?.value

  if (!sealedTokens) {
    throw new KeycloakAuthError('refresh_token', 'Missing kc_session')
  }

  let storedTokens: StoredKeycloakTokens

  try {
    storedTokens = await unsealKeycloakTokens(sealedTokens)
  } catch {
    await clearKeycloakTokens()
    throw new KeycloakAuthError('refresh_token', 'Invalid kc_session')
  }

  if (!isExpiredOrNearExpiry(storedTokens.accessExpiresAt, ACCESS_TOKEN_REFRESH_SKEW_MS, nowMs)) {
    return storedTokens.accessToken
  }

  if (isExpiredOrNearExpiry(storedTokens.refreshExpiresAt, 0, nowMs)) {
    await clearKeycloakTokens()
    throw new KeycloakAuthError('refresh_token', 'Refresh token expired')
  }

  try {
    const refreshedTokens = await refreshAccessToken(storedTokens.refreshToken)
    await storeKeycloakTokens(refreshedTokens, nowMs)
    return refreshedTokens.accessToken
  } catch (error) {
    await clearKeycloakTokens()

    if (error instanceof KeycloakAuthError) {
      throw error
    }

    throw new KeycloakAuthError('refresh_token', 'Keycloak token refresh failed')
  }
}
