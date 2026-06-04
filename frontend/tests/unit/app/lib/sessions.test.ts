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

import { IdentityError } from '@/app/lib/definitions'
import { KeycloakAuthError } from '@/app/lib/keycloak'

describe('session gateway auth', () => {
  beforeEach(() => {
    vi.resetModules()
    process.env.API_GATEWAY_URL = 'http://localhost:8000'
    verifySessionMock.mockReset()
    getValidAccessTokenMock.mockReset()
    verifySessionMock.mockResolvedValue({
      externalIdentityId: 'user-1',
      displayName: 'Operator One',
      email: 'operator@example.com',
      role: 'Operator',
      isActive: true,
      expiresAt: new Date('2026-06-04T00:00:00.000Z'),
    })
    global.fetch = vi.fn()
  })

  it('lists operator sessions through the gateway with a bearer token', async () => {
    const { listOperatorSessions } = await import('@/app/lib/sessions')
    const sessions = [
      {
        liveSessionId: 'session-1',
        sessionCode: 'SES-1',
        title: 'Night Session',
        sessionState: 'Scheduled',
        assignedOperatorUserId: 7,
        scheduledAt: '2026-06-04T00:00:00.000Z',
        lastTransitionedAt: null,
      },
    ]

    getValidAccessTokenMock.mockResolvedValue('fresh-access-token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(sessions), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await expect(listOperatorSessions()).resolves.toEqual(sessions)

    expect(verifySessionMock).toHaveBeenCalledOnce()
    expect(getValidAccessTokenMock).toHaveBeenCalledOnce()
    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions',
      expect.objectContaining({
        cache: 'no-store',
        headers: expect.any(Headers),
      }),
    )

    const [, init] = vi.mocked(global.fetch).mock.calls[0]!
    const headers = init?.headers
    expect(headers).toBeInstanceOf(Headers)
    expect((headers as Headers).get('Authorization')).toBe('Bearer fresh-access-token')
    expect((headers as Headers).get('X-User-Id')).toBeNull()
    expect((headers as Headers).get('X-User-Role')).toBeNull()
    expect((headers as Headers).get('X-User-Email')).toBeNull()
  })

  it('maps missing or invalid keycloak auth to an unauthorized identity error', async () => {
    const { listOperatorSessions } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockRejectedValue(
      new KeycloakAuthError('refresh_token', 'Missing kc_session'),
    )

    await expect(listOperatorSessions()).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({
        name: 'IdentityError',
        code: 'unauthorized',
        message: 'Authentication failed.',
      }),
    )

    expect(global.fetch).not.toHaveBeenCalled()
  })

  it('loads the associated teams for a session through the gateway', async () => {
    const { getSessionAssociatedTeams } = await import('@/app/lib/sessions')
    const payload = {
      liveSessionId: 'session-1',
      teams: [
        {
          runtimeTeamId: 'runtime-team-1',
          referenceTeamId: 'reference-team-1',
          displayName: 'Gilded Owls',
          teamCode: 'OWLS',
          joinStatus: 'Open',
        },
      ],
    }

    getValidAccessTokenMock.mockResolvedValue('fresh-access-token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(payload), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await expect(getSessionAssociatedTeams('session-1')).resolves.toEqual(payload)

    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/teams',
      expect.objectContaining({
        cache: 'no-store',
        headers: expect.any(Headers),
      }),
    )
  })

  it('maps a duplicate association conflict to a stable frontend error', async () => {
    const { associateTeamToSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('fresh-access-token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ detail: 'Team reference already associated with this session.' }),
        {
          status: 409,
          headers: { 'Content-Type': 'application/json' },
        },
      ),
    )

    await expect(
      associateTeamToSession('session-1', 'reference-team-1'),
    ).rejects.toThrowError('duplicate_association')

    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/teams',
      expect.objectContaining({
        method: 'POST',
        headers: expect.any(Headers),
        body: JSON.stringify({ referenceTeamId: 'reference-team-1' }),
      }),
    )
  })
})
