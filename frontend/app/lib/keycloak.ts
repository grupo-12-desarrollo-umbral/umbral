import 'server-only'
import { randomBytes, createHash } from 'crypto'

const KEYCLOAK_URL = process.env.KEYCLOAK_URL
const KEYCLOAK_REALM = process.env.KEYCLOAK_REALM
const KEYCLOAK_CLIENT_ID = process.env.KEYCLOAK_CLIENT_ID
const KEYCLOAK_CLIENT_SECRET = process.env.KEYCLOAK_CLIENT_SECRET

const REDIRECT_URI = `${process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3000'}/api/auth/callback`

type JwtPayload = Record<string, unknown>

type KeycloakTokenResponse = {
  access_token?: string
  expires_in?: number
  refresh_expires_in?: number
  refresh_token?: string
  id_token?: string
  error?: string
  error_description?: string
}

export type KeycloakTokenSet = {
  accessToken: string
  refreshToken: string
  expiresIn: number
  refreshExpiresIn: number
}

export type KeycloakExchangeResult = KeycloakTokenSet & {
  idToken: string
  displayName: string
  email: string
}

export class KeycloakAuthError extends Error {
  readonly operation: 'exchange_code' | 'refresh_token'
  readonly status: number | null

  constructor(
    operation: 'exchange_code' | 'refresh_token',
    message: string,
    status: number | null = null,
  ) {
    super(message)
    this.name = 'KeycloakAuthError'
    this.operation = operation
    this.status = status
  }
}

function assertEnv(): void {
  const missing: string[] = []
  if (!KEYCLOAK_URL) missing.push('KEYCLOAK_URL')
  if (!KEYCLOAK_REALM) missing.push('KEYCLOAK_REALM')
  if (!KEYCLOAK_CLIENT_ID) missing.push('KEYCLOAK_CLIENT_ID')
  if (missing.length > 0) {
    throw new Error(`Missing environment variables: ${missing.join(', ')}. Ensure .env.local is configured.`)
  }
}

export function generateCodeVerifier(): string {
  return randomBytes(32).toString('base64url')
}

export function generateCodeChallenge(verifier: string): string {
  return createHash('sha256').update(verifier).digest('base64url')
}

export function buildAuthorizationUrl(state: string, codeChallenge: string): string {
  assertEnv()
  const url = new URL(
    `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/auth`
  )
  url.searchParams.set('client_id', KEYCLOAK_CLIENT_ID!)
  url.searchParams.set('redirect_uri', REDIRECT_URI)
  url.searchParams.set('response_type', 'code')
  url.searchParams.set('scope', 'openid profile email')
  url.searchParams.set('state', state)
  url.searchParams.set('code_challenge', codeChallenge)
  url.searchParams.set('code_challenge_method', 'S256')
  url.searchParams.set('prompt', 'login')
  return url.toString()
}

export function buildResetCredentialsUrl(
  keycloakUrl: string = KEYCLOAK_URL ?? '',
  realm: string = KEYCLOAK_REALM ?? '',
): string {
  if (!keycloakUrl || !realm) {
    throw new Error('Missing Keycloak URL or realm for reset-credentials flow.')
  }

  return `${keycloakUrl}/realms/${realm}/login-actions/reset-credentials`
}

export function toExpiresAtMs(expiresInSeconds: number, nowMs: number = Date.now()): number {
  return nowMs + expiresInSeconds * 1000
}

export function isExpiredOrNearExpiry(
  expiresAtMs: number,
  skewMs: number,
  nowMs: number = Date.now(),
): boolean {
  return expiresAtMs - skewMs <= nowMs
}

export async function exchangeCode(
  code: string,
  codeVerifier: string
): Promise<KeycloakExchangeResult> {
  assertEnv()
  const body = new URLSearchParams()
  body.set('grant_type', 'authorization_code')
  appendClientCredentials(body)
  body.set('code', code)
  body.set('redirect_uri', REDIRECT_URI)
  body.set('code_verifier', codeVerifier)

  const data = await requestToken('exchange_code', body)

  const tokenSet = parseTokenSet(data, 'exchange_code')
  const idToken = requireString(data.id_token, 'id_token', 'exchange_code')

  // Parse id_token claims to extract display name. Prefer a human name over
  // preferred_username: for invited users the Keycloak username IS their email,
  // so preferring preferred_username would keep displayName == email forever.
  const idTokenPayload = parseJwt(idToken)
  // Treat empty/whitespace-only claims as absent (?? only guards null/undefined).
  const pickClaim = (value: unknown): string | undefined => {
    const text = typeof value === 'string' ? value.trim() : ''
    return text.length > 0 ? text : undefined
  }
  const givenName = pickClaim(idTokenPayload.given_name)
  const familyName = pickClaim(idTokenPayload.family_name)
  const fullFromParts = givenName
    ? [givenName, familyName].filter(Boolean).join(' ')
    : undefined
  const displayName =
    pickClaim(idTokenPayload.name) ??
    fullFromParts ??
    pickClaim(idTokenPayload.preferred_username) ??
    pickClaim(idTokenPayload.email) ??
    'User'

  const email = (idTokenPayload.email as string) ?? ''

  return {
    ...tokenSet,
    idToken,
    displayName,
    email,
  }
}

export async function refreshAccessToken(refreshToken: string): Promise<KeycloakTokenSet> {
  assertEnv()
  const body = new URLSearchParams()
  body.set('grant_type', 'refresh_token')
  appendClientCredentials(body)
  body.set('refresh_token', refreshToken)

  const data = await requestToken('refresh_token', body)
  return parseTokenSet(data, 'refresh_token')
}

function appendClientCredentials(body: URLSearchParams): void {
  body.set('client_id', KEYCLOAK_CLIENT_ID!)
  if (KEYCLOAK_CLIENT_SECRET) {
    body.set('client_secret', KEYCLOAK_CLIENT_SECRET)
  }
}

function getTokenUrl(): string {
  return `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`
}

async function requestToken(
  operation: 'exchange_code' | 'refresh_token',
  body: URLSearchParams,
): Promise<KeycloakTokenResponse> {
  const response = await fetch(getTokenUrl(), {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: body.toString(),
    cache: 'no-store',
  })

  const data = await parseTokenResponse(response)

  if (!response.ok) {
    const description = data.error_description ?? data.error ?? 'Request failed'
    throw new KeycloakAuthError(
      operation,
      `Keycloak ${operation === 'exchange_code' ? 'token exchange' : 'token refresh'} failed: ${response.status} ${description}`,
      response.status,
    )
  }

  return data
}

async function parseTokenResponse(response: Response): Promise<KeycloakTokenResponse> {
  try {
    return (await response.json()) as KeycloakTokenResponse
  } catch {
    return {}
  }
}

function parseTokenSet(
  data: KeycloakTokenResponse,
  operation: 'exchange_code' | 'refresh_token',
): KeycloakTokenSet {
  return {
    accessToken: requireString(data.access_token, 'access_token', operation),
    refreshToken: requireString(data.refresh_token, 'refresh_token', operation),
    expiresIn: requireNumber(data.expires_in, 'expires_in', operation),
    refreshExpiresIn: requireNumber(data.refresh_expires_in, 'refresh_expires_in', operation),
  }
}

function requireString(
  value: string | undefined,
  field: string,
  operation: 'exchange_code' | 'refresh_token',
): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new KeycloakAuthError(operation, `Keycloak response missing ${field}`)
  }

  return value
}

function requireNumber(
  value: number | undefined,
  field: string,
  operation: 'exchange_code' | 'refresh_token',
): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new KeycloakAuthError(operation, `Keycloak response missing ${field}`)
  }

  return value
}

function parseJwt(token: string): JwtPayload {
  try {
    const base64Url = token.split('.')[1]
    if (!base64Url) {
      return {}
    }
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    )
    return JSON.parse(jsonPayload)
  } catch {
    return {}
  }
}
