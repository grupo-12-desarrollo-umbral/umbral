import { beforeEach, describe, expect, it, vi } from 'vitest'

// The failure mode under test: when the Keycloak session is gone, the user must land on login
// rather than on a page where every action throws `unauthorized`. `kc_session` is the signal —
// it expires with the refresh token and getValidAccessToken deletes it the moment a refresh is
// rejected — so a `session` without one is a login that can no longer reach the gateway.

const SESSION_SECRET = 'test-session-secret'

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

async function runProxy(
  path: string,
  cookies: Record<string, string>,
): Promise<{ status: number; location: string | null; setCookie: string | null }> {
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
  }
}

describe('proxy route guard — dead Keycloak session', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubEnv('NODE_ENV', 'test')
    process.env.SESSION_SECRET = SESSION_SECRET
  })

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

  it('lets a session through while kc_session is still present', async () => {
    const session = await mintSessionCookie()
    const { status, location } = await runProxy('/dashboard', {
      session,
      kc_session: 'sealed-opaque-value',
    })

    expect(status).toBe(200)
    expect(location).toBeNull()
  })

  it('keeps a user with a dead Keycloak session on /login instead of bouncing to /dashboard', async () => {
    const session = await mintSessionCookie()
    const { status, location } = await runProxy('/login', { session })

    // The signed-in bounce is gated on kc_session; otherwise /dashboard would only bounce back.
    expect(status).toBe(200)
    expect(location).toBeNull()
  })

  it('still bounces a fully signed-in user off /login to the dashboard', async () => {
    const session = await mintSessionCookie()
    const { location } = await runProxy('/login', { session, kc_session: 'sealed-opaque-value' })

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
    const { location } = await runProxy('/dashboard', {
      session,
      kc_session: 'sealed-opaque-value',
    })

    expect(location).toBe('http://localhost:3000/login?error=deactivated')
  })

  // The deactivated Playwright fixture (tests/fixtures/auth.ts) sets no kc_session, and
  // tests/e2e/auth.spec.ts asserts the deactivated chip — so the disabled-account reason has to win
  // over the missing-kc_session bounce, not merely coexist with it.
  it('reports a disabled account as deactivated even with no kc_session', async () => {
    const session = await mintSessionCookie({ isActive: false })
    const { location } = await runProxy('/dashboard', { session })

    expect(location).toBe('http://localhost:3000/login?error=deactivated')
  })

  it('sends a request with no cookies at all to plain login', async () => {
    const { location } = await runProxy('/dashboard', {})

    expect(location).toBe('http://localhost:3000/login')
  })
})
