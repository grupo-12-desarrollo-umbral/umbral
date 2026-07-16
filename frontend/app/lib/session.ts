import 'server-only'
import { SignJWT, jwtVerify } from 'jose'
import { cookies } from 'next/headers'
import { clearKeycloakTokens } from './keycloak-tokens'
import type { SessionPayload } from './definitions'

const secretKey = process.env.SESSION_SECRET
const encodedKey = new TextEncoder().encode(secretKey)

export async function encrypt(payload: SessionPayload): Promise<string> {
  return new SignJWT({
    externalIdentityId: payload.externalIdentityId,
    displayName: payload.displayName,
    email: payload.email,
    role: payload.role,
    isActive: payload.isActive,
    expiresAt: payload.expiresAt.toISOString(),
  })
    .setProtectedHeader({ alg: 'HS256' })
    .setIssuedAt()
    // Pin the JWT `exp` to the same instant as the cookie's Expires. A fixed lifetime here would
    // outlive the cookie for anyone who copies the value out, and — more importantly — would let a
    // `session` survive the Keycloak refresh token it was minted against.
    .setExpirationTime(Math.floor(payload.expiresAt.getTime() / 1000))
    .sign(encodedKey)
}

export async function decrypt(session: string | undefined): Promise<SessionPayload | null> {
  if (!session) return null

  try {
    const { payload } = await jwtVerify(session, encodedKey, {
      algorithms: ['HS256'],
    })

    return {
      externalIdentityId: payload.externalIdentityId as string,
      displayName: payload.displayName as string,
      email: payload.email as string,
      role: payload.role as SessionPayload['role'],
      isActive: payload.isActive as boolean,
      expiresAt: new Date(payload.expiresAt as string),
    }
  } catch {
    return null
  }
}

// `expiresAt` is caller-supplied rather than a fixed window because `session` must never outlive the
// `kc_session` it was minted alongside: once the Keycloak refresh token is dead no access token can
// be obtained, so a surviving `session` renders a signed-in shell in which every gateway call throws
// `unauthorized`. Callers pass the refresh token's expiry (`refreshExpiresIn`); re-issuing an
// existing session (a role/status change) must carry its current `expiresAt` through untouched, so
// the window is only ever set at login and never extended past Keycloak's own idle timeout.
export async function createSession(
  payload: Omit<SessionPayload, 'expiresAt'>,
  expiresAt: Date,
): Promise<void> {
  const session = await encrypt({ ...payload, expiresAt })
  const cookieStore = await cookies()

  cookieStore.set('session', session, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    expires: expiresAt,
    sameSite: 'lax',
    path: '/',
  })
}

export async function deleteSession(): Promise<void> {
  const cookieStore = await cookies()
  cookieStore.delete('session')
  await clearKeycloakTokens()
}
