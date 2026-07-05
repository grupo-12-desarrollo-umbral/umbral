import { createHash } from 'crypto'
import { EncryptJWT, decodeJwt } from 'jose'

type KeycloakTokenResponse = {
  access_token?: string
  expires_in?: number
  refresh_expires_in?: number
  refresh_token?: string
  error?: string
  error_description?: string
}

type StoredKeycloakTokens = {
  accessToken: string
  refreshToken: string
  accessExpiresAt: number
  refreshExpiresAt: number
}

const KEYCLOAK_URL = process.env.KEYCLOAK_URL ?? 'http://localhost:8080'
const KEYCLOAK_REALM = process.env.KEYCLOAK_REALM ?? 'umbral'
const KEYCLOAK_CLIENT_ID = process.env.KEYCLOAK_CLIENT_ID ?? 'umbral-web'
const KEYCLOAK_CLIENT_SECRET = process.env.KEYCLOAK_CLIENT_SECRET

function getSealSecret(): string {
  const explicitSecret = process.env.KC_TOKEN_SECRET
  if (explicitSecret) return explicitSecret

  const sessionSecret = process.env.SESSION_SECRET
  if (!sessionSecret) {
    throw new Error('SESSION_SECRET is required to seal the Playwright kc_session cookie')
  }

  return `umbral:kc-session:v1:${sessionSecret}`
}

function getEncodedSealKey(): Uint8Array {
  return createHash('sha256').update(getSealSecret()).digest()
}

function toExpiresAtMs(expiresInSeconds: number, nowMs: number): number {
  return nowMs + expiresInSeconds * 1000
}

async function requestKeycloakTokens(username: string, password: string): Promise<KeycloakTokenResponse> {
  const body = new URLSearchParams()
  body.set('grant_type', 'password')
  body.set('client_id', KEYCLOAK_CLIENT_ID)
  body.set('username', username)
  body.set('password', password)

  if (KEYCLOAK_CLIENT_SECRET) {
    body.set('client_secret', KEYCLOAK_CLIENT_SECRET)
  }

  const response = await fetch(
    `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    },
  )
  const data = (await response.json().catch(() => ({}))) as KeycloakTokenResponse

  if (!response.ok) {
    const reason = data.error_description ?? data.error ?? response.statusText
    throw new Error(`Keycloak password grant failed for ${username}: ${response.status} ${reason}`)
  }

  return data
}

function mapTokenResponseToStoredTokens(
  tokens: KeycloakTokenResponse,
  nowMs: number,
): StoredKeycloakTokens {
  if (
    typeof tokens.access_token !== 'string' ||
    typeof tokens.refresh_token !== 'string' ||
    typeof tokens.expires_in !== 'number' ||
    typeof tokens.refresh_expires_in !== 'number'
  ) {
    throw new Error('Keycloak token response was missing required token fields')
  }

  return {
    accessToken: tokens.access_token,
    refreshToken: tokens.refresh_token,
    accessExpiresAt: toExpiresAtMs(tokens.expires_in, nowMs),
    refreshExpiresAt: toExpiresAtMs(tokens.refresh_expires_in, nowMs),
  }
}

async function sealStoredTokens(tokens: StoredKeycloakTokens): Promise<string> {
  return new EncryptJWT(tokens)
    .setProtectedHeader({ alg: 'dir', enc: 'A256GCM' })
    .setIssuedAt()
    .setExpirationTime(Math.floor(tokens.refreshExpiresAt / 1000))
    .encrypt(getEncodedSealKey())
}

// Returns the sealed kc_session cookie plus the Keycloak `sub` (UUID) from the access token.
// The gateway forwards `sub` as X-User-Id on Bearer-JWT calls, so callers that must match a
// seeded identity-access row (operator session-listing) need it — the literal username won't do.
export async function createKeycloakSession(
  username: string,
  password: string,
): Promise<{ cookie: string; sub: string }> {
  const nowMs = Date.now()
  const tokens = await requestKeycloakTokens(username, password)
  const sub = decodeJwt(tokens.access_token ?? '').sub
  if (!sub) {
    throw new Error(`Keycloak access token for ${username} had no sub claim`)
  }
  const cookie = await sealStoredTokens(mapTokenResponseToStoredTokens(tokens, nowMs))
  return { cookie, sub }
}
