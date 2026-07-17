import { beforeEach, describe, expect, it, vi } from 'vitest'

// Two failure modes under test. First: when the Keycloak session is gone, the user must land on
// login rather than on a page where every action throws `unauthorized`. Second: the access token
// outlives its 5-minute lifespan long before the refresh token does, and only the proxy can spend a
// refresh token — `cookies()` is read-only inside a Server Component — so the refresh has to happen
// here and reach both the response and the render.

const SESSION_SECRET = 'test-session-secret'

const { refreshAccessTokenMock } = vi.hoisted(() => ({ refreshAccessTokenMock: vi.fn() }))

vi.mock('@/app/lib/keycloak', async () => {
  const actual = await vi.importActual<typeof import('@/app/lib/keycloak')>('@/app/lib/keycloak')
  return { ...actual, refreshAccessToken: refreshAccessTokenMock }
})

async function mintSessionCookie(overrides: { isActive?: boolean } = {}): Promise<string> {
  const { encrypt } = await import('@/app/lib/session')
  return encrypt({
    externalIdentityId: 'user-1',
    displayName: 'Operator One',
    email: 'operator@example.com',
    role: 'Operator',
    isActive: overrides.isActive ?? true,
    expiresAt: new Date(Date.now() + 28_800_000),
  })
}

// Live by default: the access token is nowhere near expiry, so the proxy reads it and moves on
// without calling Keycloak. Overrides drive the refresh and dead-session branches.
async function mintKcSession(
  overrides: { accessExpiresAt?: number; refreshExpiresAt?: number } = {},
): Promise<string> {
  const { sealKeycloakTokens } = await import('@/app/lib/keycloak-tokens')
  const now = Date.now()
  return sealKeycloakTokens({
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    accessExpiresAt: overrides.accessExpiresAt ?? now + 300_000,
    refreshExpiresAt: overrides.refreshExpiresAt ?? now + 28_800_000,
  })
}

async function runProxy(
  path: string,
  cookies: Record<string, string>,
): Promise<{
  status: number
  location: string | null
  setCookie: string | null
  forwardedCookie: string | undefined
}> {
  const { NextRequest } = await import('next/server')
  const proxy = (await import('@/proxy')).default

  const request = new NextRequest(`http://localhost:3000${path}`)
  for (const [name, value] of Object.entries(cookies)) {
    request.cookies.set(name, value)
  }

  const response = await proxy(request)
  return {
    status: response.status,
    location: response.headers.get('location'),
    setCookie: response.headers.get('set-cookie'),
    // What the render will actually read on this same request.
    forwardedCookie: request.cookies.get('kc_session')?.value,
  }
}

describe('proxy route guard', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubEnv('NODE_ENV', 'test')
    process.env.SESSION_SECRET = SESSION_SECRET
    delete process.env.KC_TOKEN_SECRET
    refreshAccessTokenMock.mockReset()
  })

  describe('dead Keycloak session', () => {
    it('sends a session with no kc_session to login with the unauthorized chip', async () => {
      const session = await mintSessionCookie()
      const { location } = await runProxy('/dashboard', { session })

      // `unauthorized` is the state app/login/LoginCard.tsx already renders.
      expect(location).toBe('http://localhost:3000/login?error=unauthorized')
    })

    it('clears the orphaned session cookie on the way out', async () => {
      const session = await mintSessionCookie()
      const { setCookie } = await runProxy('/dashboard', { session })

      // Left behind, it would re-trigger this same bounce on every navigation.
      expect(setCookie).toContain('session=')
      expect(setCookie).toMatch(/Expires=Thu, 01 Jan 1970|Max-Age=0/)
    })

    it('bounces an unopenable kc_session to login rather than passing it to the render', async () => {
      const session = await mintSessionCookie()
      const { location } = await runProxy('/dashboard', { session, kc_session: 'not-a-real-seal' })

      expect(location).toBe('http://localhost:3000/login?error=unauthorized')
    })

    // The page would redirect to /login on its own once the gateway rejected it; if the proxy still
    // read kc_session as merely present it would bounce that right back to /dashboard, forever.
    it('keeps a user with an expired refresh token on /login instead of bouncing to /dashboard', async () => {
      const session = await mintSessionCookie()
      const kc_session = await mintKcSession({
        accessExpiresAt: Date.now() - 5_000,
        refreshExpiresAt: Date.now() - 1_000,
      })
      const { status, location } = await runProxy('/login', { session, kc_session })

      expect(status).toBe(200)
      expect(location).toBeNull()
      expect(refreshAccessTokenMock).not.toHaveBeenCalled()
    })

    it('sheds the dead cookies when a public route is reached with one', async () => {
      const session = await mintSessionCookie()
      const kc_session = await mintKcSession({
        accessExpiresAt: Date.now() - 5_000,
        refreshExpiresAt: Date.now() - 1_000,
      })
      const { status, setCookie } = await runProxy('/', { session, kc_session })

      expect(status).toBe(200)
      expect(setCookie).toMatch(/Expires=Thu, 01 Jan 1970|Max-Age=0/)
    })

    it('sends a request with no cookies at all to plain login', async () => {
      const { location } = await runProxy('/dashboard', {})

      expect(location).toBe('http://localhost:3000/login')
    })
  })

  describe('access token refresh', () => {
    it('leaves a still-valid access token alone', async () => {
      const session = await mintSessionCookie()
      const kc_session = await mintKcSession()
      const { status, location, setCookie } = await runProxy('/dashboard', { session, kc_session })

      expect(status).toBe(200)
      expect(location).toBeNull()
      // No refresh, so nothing to re-seal — spending a refresh token per request would be waste.
      expect(refreshAccessTokenMock).not.toHaveBeenCalled()
      expect(setCookie).toBeNull()
    })

    // The regression this whole change exists for: past the 5-minute access token lifespan the
    // render used to drive this refresh and 500 on the read-only cookie store.
    it('refreshes an expired access token and persists the result', async () => {
      const session = await mintSessionCookie()
      const kc_session = await mintKcSession({ accessExpiresAt: Date.now() - 5_000 })
      refreshAccessTokenMock.mockResolvedValue({
        accessToken: 'fresh-access',
        refreshToken: 'refresh-2',
        expiresIn: 300,
        refreshExpiresIn: 28_800,
      })

      const { status, location, setCookie } = await runProxy('/dashboard', { session, kc_session })

      expect(status).toBe(200)
      expect(location).toBeNull()
      expect(refreshAccessTokenMock).toHaveBeenCalledWith('refresh-1')
      expect(setCookie).toContain('kc_session=')
      expect(setCookie).toContain('HttpOnly')
    })

    // Setting it only on the response would refresh a request too late to help the page that
    // triggered it — the render would still read the stale token and try to refresh it itself.
    it('forwards the refreshed cookie to the render on the same request', async () => {
      const session = await mintSessionCookie()
      const kc_session = await mintKcSession({ accessExpiresAt: Date.now() - 5_000 })
      refreshAccessTokenMock.mockResolvedValue({
        accessToken: 'fresh-access',
        refreshToken: 'refresh-2',
        expiresIn: 300,
        refreshExpiresIn: 28_800,
      })

      const { forwardedCookie } = await runProxy('/dashboard', { session, kc_session })
      expect(forwardedCookie).toBeDefined()
      expect(forwardedCookie).not.toBe(kc_session)

      const { unsealKeycloakTokens } = await import('@/app/lib/keycloak-tokens')
      await expect(unsealKeycloakTokens(forwardedCookie!)).resolves.toMatchObject({
        accessToken: 'fresh-access',
        refreshToken: 'refresh-2',
      })
    })

    it('bounces to login when Keycloak rejects the refresh token', async () => {
      const { KeycloakAuthError } = await import('@/app/lib/keycloak')
      const session = await mintSessionCookie()
      const kc_session = await mintKcSession({ accessExpiresAt: Date.now() - 5_000 })
      refreshAccessTokenMock.mockRejectedValue(
        new KeycloakAuthError('refresh_token', 'refresh failed', 401),
      )

      const { location, setCookie } = await runProxy('/dashboard', { session, kc_session })

      expect(location).toBe('http://localhost:3000/login?error=unauthorized')
      expect(setCookie).toMatch(/Expires=Thu, 01 Jan 1970|Max-Age=0/)
    })
  })

  describe('signed-in bounce', () => {
    it('still bounces a fully signed-in user off /login to the dashboard', async () => {
      const session = await mintSessionCookie()
      const kc_session = await mintKcSession()
      const { location } = await runProxy('/login', { session, kc_session })

      expect(location).toBe('http://localhost:3000/dashboard')
    })

    it('leaves the public landing route reachable with an orphaned session', async () => {
      const session = await mintSessionCookie()
      const { status, location } = await runProxy('/', { session })

      expect(status).toBe(200)
      expect(location).toBeNull()
    })

    it('prefers the deactivated redirect when the account is disabled', async () => {
      const session = await mintSessionCookie({ isActive: false })
      const kc_session = await mintKcSession()
      const { location } = await runProxy('/dashboard', { session, kc_session })

      expect(location).toBe('http://localhost:3000/login?error=deactivated')
    })

    // The deactivated Playwright fixture (tests/fixtures/auth.ts) sets no kc_session, and
    // tests/e2e/auth.spec.ts asserts the deactivated chip — so the disabled-account reason has to
    // win over the missing-kc_session bounce, not merely coexist with it.
    it('reports a disabled account as deactivated even with no kc_session', async () => {
      const session = await mintSessionCookie({ isActive: false })
      const { location } = await runProxy('/dashboard', { session })

      expect(location).toBe('http://localhost:3000/login?error=deactivated')
    })
  })
})
