import { beforeEach, describe, expect, it, vi } from 'vitest'

// The invariant under test: `session` must never outlive `kc_session`. Past the refresh token's
// expiry no access token can be minted, so a surviving `session` renders a signed-in shell in which
// every gateway call throws `unauthorized` — strictly worse than a clean bounce to login.

type CookieOptions = { expires?: Date }

const { cookieJar, cookieStore, getCurrentUserProfileMock, exchangeCodeMock, bootstrapUserMock } =
  vi.hoisted(() => {
    const cookieJar = new Map<string, { value: string; options?: CookieOptions }>()
    const cookieStore = {
      get: vi.fn((name: string) => {
        const entry = cookieJar.get(name)
        return entry ? { name, value: entry.value } : undefined
      }),
      set: vi.fn((name: string, value: string, options?: CookieOptions) => {
        cookieJar.set(name, { value, options })
      }),
      delete: vi.fn((name: string) => {
        cookieJar.delete(name)
      }),
    }

    return {
      cookieJar,
      cookieStore,
      getCurrentUserProfileMock: vi.fn(),
      exchangeCodeMock: vi.fn(),
      bootstrapUserMock: vi.fn(),
    }
  })

vi.mock('next/headers', () => ({
  cookies: vi.fn(async () => cookieStore),
}))

vi.mock('@/app/lib/identity', () => ({
  getCurrentUserProfile: getCurrentUserProfileMock,
  bootstrapUser: bootstrapUserMock,
}))

// Only the network call is stubbed; `toExpiresAtMs` stays real so the route's expiry maths is the
// thing under test rather than a restatement of it.
vi.mock('@/app/lib/keycloak', async () => {
  const actual = await vi.importActual<typeof import('@/app/lib/keycloak')>('@/app/lib/keycloak')
  return { ...actual, exchangeCode: exchangeCodeMock }
})

import { decodeJwt } from 'jose'

// Keycloak hands back `refresh_expires_in` = the realm's ssoSessionIdleTimeout (8h), which is the
// window `session` has to fit inside.
const REFRESH_EXPIRES_IN = 28_800
const ACCESS_EXPIRES_IN = 300

const ACTOR = {
  externalIdentityId: 'user-1',
  displayName: 'Operator One',
  email: 'operator@example.com',
  role: 'Operator' as const,
  isActive: true,
}

function cookieExpiry(name: string): Date {
  const entry = cookieJar.get(name)
  if (!entry?.options?.expires) {
    throw new Error(`cookie ${name} was set without an Expires`)
  }
  return entry.options.expires
}

async function loadModules() {
  const { toExpiresAtMs } = await import('@/app/lib/keycloak')
  const { createSession } = await import('@/app/lib/session')
  const { storeKeycloakTokens } = await import('@/app/lib/keycloak-tokens')
  return { toExpiresAtMs, createSession, storeKeycloakTokens }
}

// Mirrors what app/api/auth/callback/route.ts does on a successful code exchange.
async function signIn(nowMs: number, refreshExpiresIn = REFRESH_EXPIRES_IN) {
  const { toExpiresAtMs, createSession, storeKeycloakTokens } = await loadModules()

  await createSession(ACTOR, new Date(toExpiresAtMs(refreshExpiresIn, nowMs)))
  await storeKeycloakTokens(
    {
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      expiresIn: ACCESS_EXPIRES_IN,
      refreshExpiresIn,
    },
    nowMs,
  )
}

describe('session cookie lifetime invariant', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubEnv('NODE_ENV', 'test')
    process.env.SESSION_SECRET = 'test-session-secret'
    delete process.env.KC_TOKEN_SECRET
    cookieJar.clear()
    getCurrentUserProfileMock.mockReset()
  })

  it('cuts the session cookie to the refresh token expiry, not a fixed window', async () => {
    const nowMs = Date.now()
    await signIn(nowMs)

    expect(cookieExpiry('session').getTime()).toBe(nowMs + REFRESH_EXPIRES_IN * 1000)
    expect(cookieExpiry('session').getTime()).toBe(cookieExpiry('kc_session').getTime())
  })

  it('never sets a session cookie that outlives kc_session', async () => {
    const nowMs = Date.now()
    await signIn(nowMs)

    expect(cookieExpiry('session').getTime()).toBeLessThanOrEqual(
      cookieExpiry('kc_session').getTime(),
    )
  })

  it('pins the session JWT exp to the cookie expiry so the token cannot outlive it either', async () => {
    const nowMs = Date.now()
    await signIn(nowMs)

    const { exp } = decodeJwt(cookieJar.get('session')!.value)
    expect(exp).toBe(Math.floor(cookieExpiry('session').getTime() / 1000))
    // The bug this replaces: a hardcoded '7d' exp, wildly past the 8h refresh window.
    expect(exp! * 1000).toBeLessThanOrEqual(cookieExpiry('kc_session').getTime())
  })

  it('holds the invariant after a refresh slides kc_session forward', async () => {
    const nowMs = Date.now()
    await signIn(nowMs)
    const sessionExpiryAtLogin = cookieExpiry('session').getTime()

    // A later refresh re-seals kc_session with a fresh idle window; `session` stays pinned to login.
    const { storeKeycloakTokens } = await loadModules()
    const refreshedAtMs = nowMs + 3_600_000
    await storeKeycloakTokens(
      {
        accessToken: 'access-2',
        refreshToken: 'refresh-2',
        expiresIn: ACCESS_EXPIRES_IN,
        refreshExpiresIn: REFRESH_EXPIRES_IN,
      },
      refreshedAtMs,
    )

    expect(cookieExpiry('kc_session').getTime()).toBeGreaterThan(sessionExpiryAtLogin)
    expect(cookieExpiry('session').getTime()).toBe(sessionExpiryAtLogin)
    expect(cookieExpiry('session').getTime()).toBeLessThanOrEqual(
      cookieExpiry('kc_session').getTime(),
    )
  })

  // The login path is where the window is actually chosen, so drive the real route rather than a
  // local restatement of it: a callback that hands createSession the wrong expiry is the bug.
  it('cuts both cookies from the same refresh window on a real sign-in', async () => {
    process.env.KEYCLOAK_URL = 'http://localhost:8080'
    process.env.KEYCLOAK_REALM = 'umbral'
    process.env.KEYCLOAK_CLIENT_ID = 'umbral-web'

    exchangeCodeMock.mockResolvedValue({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      expiresIn: ACCESS_EXPIRES_IN,
      refreshExpiresIn: REFRESH_EXPIRES_IN,
      idToken: 'id-1',
      displayName: ACTOR.displayName,
      email: ACTOR.email,
    })
    bootstrapUserMock.mockResolvedValue({
      access: { isAllowed: true },
      actor: ACTOR,
    })

    const { NextRequest } = await import('next/server')
    const { GET } = await import('@/app/api/auth/callback/route')

    const request = new NextRequest('http://localhost:3000/api/auth/callback?code=c-1&state=s-1')
    request.cookies.set('auth_state', 's-1')
    request.cookies.set('auth_verifier', 'v-1')

    const nowMs = Date.now()
    const response = await GET(request)
    expect(response.headers.get('location')).toBe('http://localhost:3000/dashboard')

    const sessionExpiry = cookieExpiry('session').getTime()
    const kcExpiry = cookieExpiry('kc_session').getTime()

    expect(sessionExpiry).toBeLessThanOrEqual(kcExpiry)
    // Both cut from one `Date.now()`, so they land on the same instant (allowing for clock drift
    // across the awaits). A 7d window would miss this by six and a half days.
    expect(sessionExpiry).toBeGreaterThanOrEqual(nowMs + REFRESH_EXPIRES_IN * 1000 - 5_000)
    expect(sessionExpiry).toBeLessThanOrEqual(nowMs + REFRESH_EXPIRES_IN * 1000 + 5_000)
  })

  it('re-issues a role change without extending the session past kc_session', async () => {
    const nowMs = Date.now()
    await signIn(nowMs)
    const sessionExpiryAtLogin = cookieExpiry('session').getTime()

    getCurrentUserProfileMock.mockResolvedValue({ ...ACTOR, role: 'Administrator' })

    const { refreshSession } = await import('@/app/actions/session')
    await expect(refreshSession()).resolves.toEqual({ role: 'Administrator', isActive: true })

    // The cookie was re-minted (role changed) but the window must not restart.
    const { role } = decodeJwt(cookieJar.get('session')!.value)
    expect(role).toBe('Administrator')
    expect(cookieExpiry('session').getTime()).toBe(sessionExpiryAtLogin)
    expect(cookieExpiry('session').getTime()).toBeLessThanOrEqual(
      cookieExpiry('kc_session').getTime(),
    )
  })
})
