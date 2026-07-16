import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const { cookieJar, cookieStore, refreshAccessTokenMock } = vi.hoisted(() => {
  const cookieJar = new Map<string, string>()
  const cookieStore = {
    get: vi.fn((name: string) => {
      const value = cookieJar.get(name)
      return value ? { name, value } : undefined
    }),
    set: vi.fn((name: string, value: string) => {
      cookieJar.set(name, value)
    }),
    delete: vi.fn((name: string) => {
      cookieJar.delete(name)
    }),
  }

  return {
    cookieJar,
    cookieStore,
    refreshAccessTokenMock: vi.fn(),
  }
})

vi.mock('next/headers', () => ({
  cookies: vi.fn(async () => cookieStore),
}))

vi.mock('@/app/lib/keycloak', async () => {
  const actual = await vi.importActual<typeof import('@/app/lib/keycloak')>('@/app/lib/keycloak')
  return {
    ...actual,
    refreshAccessToken: refreshAccessTokenMock,
  }
})

import { KeycloakAuthError } from '@/app/lib/keycloak'
import {
  getValidAccessToken,
  sealKeycloakTokens,
  unsealKeycloakTokens,
} from '@/app/lib/keycloak-tokens'

describe('keycloak token cookie lifecycle', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  beforeEach(() => {
    // NODE_ENV is a read-only typed property; stub it via vitest instead of assigning directly.
    vi.stubEnv('NODE_ENV', 'test')
    process.env.SESSION_SECRET = 'test-session-secret'
    delete process.env.KC_TOKEN_SECRET
    cookieJar.clear()
    cookieStore.get.mockClear()
    cookieStore.set.mockClear()
    cookieStore.delete.mockClear()
    refreshAccessTokenMock.mockReset()
  })

  it('round-trips sealed keycloak tokens', async () => {
    const nowMs = Date.now()
    const sealed = await sealKeycloakTokens({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessExpiresAt: nowMs + 111_000,
      refreshExpiresAt: nowMs + 222_000,
    })

    await expect(unsealKeycloakTokens(sealed)).resolves.toEqual({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessExpiresAt: nowMs + 111_000,
      refreshExpiresAt: nowMs + 222_000,
    })
  })

  it('refreshes an expired access token and persists refresh-token rotation', async () => {
    const nowMs = Date.now()
    const sealed = await sealKeycloakTokens({
      accessToken: 'stale-access',
      refreshToken: 'refresh-1',
      accessExpiresAt: nowMs - 5_000,
      refreshExpiresAt: nowMs + 300_000,
    })
    cookieJar.set('kc_session', sealed)

    refreshAccessTokenMock.mockResolvedValue({
      accessToken: 'fresh-access',
      refreshToken: 'refresh-2',
      expiresIn: 120,
      refreshExpiresIn: 900,
    })

    await expect(getValidAccessToken(nowMs)).resolves.toBe('fresh-access')
    expect(refreshAccessTokenMock).toHaveBeenCalledWith('refresh-1')
    expect(cookieStore.set).toHaveBeenCalledOnce()

    const refreshedCookie = cookieJar.get('kc_session')
    expect(refreshedCookie).toBeTruthy()

    const stored = await unsealKeycloakTokens(refreshedCookie!)
    expect(stored.accessToken).toBe('fresh-access')
    expect(stored.refreshToken).toBe('refresh-2')
    expect(stored.accessExpiresAt).toBe(nowMs + 120_000)
    expect(stored.refreshExpiresAt).toBe(nowMs + 900_000)
  })

  // `sealKeycloakTokens` sets the JWE's own `exp` to refreshExpiresAt, so an expired refresh token
  // makes the cookie undecryptable: jwtDecrypt throws ERR_JWT_EXPIRED before the explicit
  // refreshExpiresAt guard is ever consulted, and the message is 'Invalid kc_session'. The outcome
  // is what matters and it is identical either way — cookie cleared, KeycloakAuthError raised, no
  // pointless round trip — so this pins the observable contract rather than the branch taken.
  it('clears kc_session without calling Keycloak once the refresh token has expired', async () => {
    const nowMs = Date.now()
    const sealed = await sealKeycloakTokens({
      accessToken: 'stale-access',
      refreshToken: 'refresh-1',
      accessExpiresAt: nowMs - 5_000,
      refreshExpiresAt: nowMs - 1_000,
    })
    cookieJar.set('kc_session', sealed)

    await expect(getValidAccessToken(nowMs)).rejects.toBeInstanceOf(KeycloakAuthError)
    // A dead refresh token buys nothing but a round trip and a guaranteed rejection.
    expect(refreshAccessTokenMock).not.toHaveBeenCalled()
    // The clear is what the proxy reads: no kc_session beside a live `session` means the Keycloak
    // session is gone, so the next navigation lands on login instead of a shell that only errors.
    expect(cookieStore.delete).toHaveBeenCalledWith('kc_session')
    expect(cookieJar.has('kc_session')).toBe(false)
  })

  // The refreshExpiresAt guard is only reachable when the injected clock runs ahead of the sealed
  // `exp` — seal a still-valid cookie, then ask for a token from a moment past the refresh expiry.
  it('refuses a refresh token that expires before the caller-supplied instant', async () => {
    const nowMs = Date.now()
    const sealed = await sealKeycloakTokens({
      accessToken: 'stale-access',
      refreshToken: 'refresh-1',
      accessExpiresAt: nowMs - 5_000,
      refreshExpiresAt: nowMs + 30_000,
    })
    cookieJar.set('kc_session', sealed)

    await expect(getValidAccessToken(nowMs + 60_000)).rejects.toThrow('Refresh token expired')
    expect(refreshAccessTokenMock).not.toHaveBeenCalled()
    expect(cookieJar.has('kc_session')).toBe(false)
  })

  it('clears kc_session when refresh fails', async () => {
    const nowMs = Date.now()
    const sealed = await sealKeycloakTokens({
      accessToken: 'stale-access',
      refreshToken: 'refresh-1',
      accessExpiresAt: nowMs - 5_000,
      refreshExpiresAt: nowMs + 300_000,
    })
    cookieJar.set('kc_session', sealed)

    refreshAccessTokenMock.mockRejectedValue(
      new KeycloakAuthError('refresh_token', 'refresh failed', 401),
    )

    await expect(getValidAccessToken(nowMs)).rejects.toBeInstanceOf(KeycloakAuthError)
    expect(cookieStore.delete).toHaveBeenCalledWith('kc_session')
    expect(cookieJar.has('kc_session')).toBe(false)
  })
})
