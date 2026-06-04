import { beforeEach, describe, expect, it, vi } from 'vitest'

const { cookieJar, cookieStore, decryptMock, getValidAccessTokenMock } = vi.hoisted(() => {
  const cookieJar = new Map<string, string>()
  const cookieStore = {
    get: vi.fn((name: string) => {
      const value = cookieJar.get(name)
      return value ? { name, value } : undefined
    }),
  }

  return {
    cookieJar,
    cookieStore,
    decryptMock: vi.fn(),
    getValidAccessTokenMock: vi.fn(),
  }
})

vi.mock('next/headers', () => ({
  cookies: vi.fn(async () => cookieStore),
}))

vi.mock('@/app/lib/session', () => ({
  decrypt: decryptMock,
}))

vi.mock('@/app/lib/keycloak-tokens', () => ({
  getValidAccessToken: getValidAccessTokenMock,
}))

import { KeycloakAuthError } from '@/app/lib/keycloak'
import { GET } from '@/app/api/realtime/hub-token/route'

describe('GET /api/realtime/hub-token', () => {
  beforeEach(() => {
    cookieJar.clear()
    cookieStore.get.mockClear()
    decryptMock.mockReset()
    getValidAccessTokenMock.mockReset()
  })

  it('returns 401 when the app session is missing', async () => {
    const response = await GET()

    expect(response.status).toBe(401)
    await expect(response.json()).resolves.toEqual({
      error: 'unauthorized',
      reason: 'missing_session',
    })
  })

  it('returns a fresh access token when session validation succeeds', async () => {
    cookieJar.set('session', 'signed-session')
    decryptMock.mockResolvedValue({
      externalIdentityId: 'operator-1',
      displayName: 'Operator One',
      email: 'operator@example.com',
      role: 'Operator',
      isActive: true,
      expiresAt: new Date('2026-06-04T00:00:00.000Z'),
    })
    getValidAccessTokenMock.mockResolvedValue('fresh-access-token')

    const response = await GET()

    expect(response.status).toBe(200)
    expect(response.headers.get('Cache-Control')).toBe('no-store')
    await expect(response.json()).resolves.toEqual({
      accessToken: 'fresh-access-token',
    })
  })

  it('returns 401 when token refresh fails', async () => {
    cookieJar.set('session', 'signed-session')
    decryptMock.mockResolvedValue({
      externalIdentityId: 'operator-1',
      displayName: 'Operator One',
      email: 'operator@example.com',
      role: 'Operator',
      isActive: true,
      expiresAt: new Date('2026-06-04T00:00:00.000Z'),
    })
    getValidAccessTokenMock.mockRejectedValue(
      new KeycloakAuthError('refresh_token', 'refresh failed', 401),
    )

    const response = await GET()

    expect(response.status).toBe(401)
    await expect(response.json()).resolves.toEqual({
      error: 'unauthorized',
      reason: 'keycloak_auth_failed',
    })
  })
})
