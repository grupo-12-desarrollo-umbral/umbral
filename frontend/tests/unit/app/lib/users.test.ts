// Guards the listUsers 403 contract: GET /api/users is gated by the backend's AdminOrOperator policy,
// so a 403 is a ROLE rejection. Deactivation never reaches here — verifySession and
// enforceActivePlatformAccess redirect first — and reporting it as such would misinform a live admin.
import { beforeEach, describe, expect, it, vi } from 'vitest'

const { verifySessionMock, getValidAccessTokenMock } = vi.hoisted(() => ({
  verifySessionMock: vi.fn(),
  getValidAccessTokenMock: vi.fn(),
}))

vi.mock('@/app/lib/dal', () => ({
  verifySession: verifySessionMock,
}))

vi.mock('@/app/lib/keycloak-tokens', () => ({
  getValidAccessToken: getValidAccessTokenMock,
}))

describe('listUsers', () => {
  beforeEach(() => {
    vi.resetModules()
    process.env.API_GATEWAY_URL = 'http://localhost:8000'
    verifySessionMock.mockReset()
    getValidAccessTokenMock.mockReset()
    verifySessionMock.mockResolvedValue({
      externalIdentityId: 'user-1',
      displayName: 'Admin One',
      email: 'admin@example.com',
      role: 'Administrator',
      isActive: true,
      expiresAt: new Date('2026-06-04T00:00:00.000Z'),
    })
    getValidAccessTokenMock.mockResolvedValue('token')
    global.fetch = vi.fn()
  })

  it('reports a 403 as a role rejection, not a deactivated account', async () => {
    const { listUsers } = await import('@/app/lib/users')
    vi.mocked(global.fetch).mockResolvedValue(new Response(null, { status: 403 }))

    const act = listUsers()

    await expect(act).rejects.toMatchObject({
      code: 'unauthorized',
      message: 'Forbidden. Administrator or operator role required.',
    })
    // 'deactivated' would redirect the caller to /login?error=deactivated and tell an
    // administrator whose only problem is role that their account was disabled.
    await act.catch((err: { code: string }) => {
      expect(err.code).not.toBe('deactivated')
    })
  })

  it('reports a 401 as an authentication failure', async () => {
    const { listUsers } = await import('@/app/lib/users')
    vi.mocked(global.fetch).mockResolvedValue(new Response(null, { status: 401 }))

    await expect(listUsers()).rejects.toMatchObject({ code: 'unauthorized' })
  })

  it('reports an unexpected status as unknown rather than an auth problem', async () => {
    const { listUsers } = await import('@/app/lib/users')
    vi.mocked(global.fetch).mockResolvedValue(new Response(null, { status: 500 }))

    await expect(listUsers()).rejects.toMatchObject({ code: 'unknown' })
  })

  it('returns the paged catalog on success', async () => {
    const { listUsers } = await import('@/app/lib/users')
    const page = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 1, hasNextPage: false }
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(page), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    )

    await expect(listUsers()).resolves.toEqual(page)
  })
})
