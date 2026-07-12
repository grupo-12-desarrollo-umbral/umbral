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

  it('maps a mission-not-eligible 409 to mission_not_eligible', async () => {
    const { createSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ type: 'mission-not-eligible-for-session', detail: 'Mission 1 is inactive.' }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      createSession({
        missionId: 1,
        title: 'Test',
        maximumTimeMinutes: 60,
        scheduledAt: '2026-12-01T10:00:00Z',
      }),
    ).rejects.toThrowError('mission_not_eligible')
  })

  it('maps an untyped 409 to mission_not_eligible (single source now)', async () => {
    const { createSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ detail: 'Conflict.' }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      createSession({
        missionId: 1,
        title: 'Test',
        maximumTimeMinutes: 60,
        scheduledAt: '2026-12-01T10:00:00Z',
      }),
    ).rejects.toThrowError('mission_not_eligible')
  })

  it('maps a mission-not-eligible 422 to mission_not_eligible', async () => {
    const { createSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ type: 'mission-not-eligible-for-session', detail: 'Mission 1 is not runtime-ready.' }),
        { status: 422, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      createSession({
        missionId: 1,
        title: 'Test',
        maximumTimeMinutes: 60,
        scheduledAt: '2026-12-01T10:00:00Z',
      }),
    ).rejects.toThrowError('mission_not_eligible')
  })

  it('maps a 404 to mission_not_found', async () => {
    const { createSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ detail: 'Resource not found.' }),
        { status: 404, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      createSession({
        missionId: 1,
        title: 'Test',
        maximumTimeMinutes: 60,
        scheduledAt: '2026-12-01T10:00:00Z',
      }),
    ).rejects.toThrowError('mission_not_found')
  })

  it('parses the operator session panel DTO on a 200', async () => {
    const { getOperatorSessionPanel } = await import('@/app/lib/sessions')
    const payload = {
      liveSessionId: 'session-1',
      state: 'Active',
      timer: { liveSessionId: 'session-1', teamId: null },
      teamProgress: [
        {
          teamId: 'team-a',
          teamCode: 'AAA',
          displayName: 'Alpha',
          score: 0,
          activeSubstage: {
            substageSnapshotId: 'sub-1',
            playMode: 'TreasureHunt',
            title: 'Hunt',
            totalActiveTargets: 3,
            resolvedTargets: 0,
            activeQuestionSequenceOrder: null,
            activeQuestionTimeLimitSeconds: null,
          },
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

    await expect(getOperatorSessionPanel('session-1')).resolves.toEqual(payload)

    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/operator-panel',
      expect.objectContaining({ cache: 'no-store', headers: expect.any(Headers) }),
    )
  })

  it('maps a 403 (non-owning operator) to an unauthorized identity error', async () => {
    const { getOperatorSessionPanel } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 403 }))

    await expect(getOperatorSessionPanel('session-1')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({
        name: 'IdentityError',
        code: 'unauthorized',
      }),
    )
  })

  it('maps a 401 to an unauthorized identity error', async () => {
    const { getOperatorSessionPanel } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 401 }))

    await expect(getOperatorSessionPanel('session-1')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )
  })

  it('maps a 404 to an unknown identity error (Session not found)', async () => {
    const { getOperatorSessionPanel } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 404 }))

    await expect(getOperatorSessionPanel('session-1')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({
        name: 'IdentityError',
        code: 'unknown',
        message: 'Session not found',
      }),
    )
  })

  it('maps another non-ok status to an unknown identity error', async () => {
    const { getOperatorSessionPanel } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 503 }))

    await expect(getOperatorSessionPanel('session-1')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unknown' }),
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
