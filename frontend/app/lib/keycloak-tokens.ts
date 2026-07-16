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

const KC_SESSION_COOKIE = 'kc_session'
const ACCESS_TOKEN_REFRESH_SKEW_MS = 60_000

type StoredKeycloakTokens = {
  accessToken: string
  refreshToken: string
  accessExpiresAt: number
  refreshExpiresAt: number
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

  cookieStore.set(KC_SESSION_COOKIE, sealedTokens, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    path: '/',
    expires: getCookieExpiry(storedTokens.refreshExpiresAt),
  })
}

export async function clearKeycloakTokens(): Promise<void> {
  const cookieStore = await cookies()
  cookieStore.delete(KC_SESSION_COOKIE)
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
