import 'server-only'
import { randomBytes, createHash } from 'crypto'

const KEYCLOAK_URL = process.env.KEYCLOAK_URL
const KEYCLOAK_REALM = process.env.KEYCLOAK_REALM
const KEYCLOAK_CLIENT_ID = process.env.KEYCLOAK_CLIENT_ID
const KEYCLOAK_CLIENT_SECRET = process.env.KEYCLOAK_CLIENT_SECRET

const REDIRECT_URI = `${process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3000'}/api/auth/callback`

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

export async function exchangeCode(
  code: string,
  codeVerifier: string
): Promise<{
  accessToken: string
  idToken: string
  displayName: string
  email: string
}> {
  assertEnv()
  const tokenUrl = `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`

  const body = new URLSearchParams()
  body.set('grant_type', 'authorization_code')
  body.set('client_id', KEYCLOAK_CLIENT_ID!)
  if (KEYCLOAK_CLIENT_SECRET) {
    body.set('client_secret', KEYCLOAK_CLIENT_SECRET)
  }
  body.set('code', code)
  body.set('redirect_uri', REDIRECT_URI)
  body.set('code_verifier', codeVerifier)

  const response = await fetch(tokenUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: body.toString(),
  })

  if (!response.ok) {
    throw new Error(`Keycloak token exchange failed: ${response.status}`)
  }

  const data = await response.json()

  if (!data.access_token || !data.id_token) {
    throw new Error('Keycloak response missing tokens')
  }

  // Parse id_token claims to extract display name
  const idTokenPayload = parseJwt(data.id_token)
  const displayName =
    (idTokenPayload.preferred_username as string) ??
    (idTokenPayload.name as string) ??
    (idTokenPayload.given_name as string) ??
    (idTokenPayload.email as string) ??
    'User'

  const email = (idTokenPayload.email as string) ?? ''

  return {
    accessToken: data.access_token,
    idToken: data.id_token,
    displayName,
    email,
  }
}

function parseJwt(token: string): Record<string, unknown> {
  try {
    const base64Url = token.split('.')[1]
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
