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

  // Issue-3 fix: the generic invalid-transition 409 now carries the specific rejected from->to
  // edge (InvalidSessionStateTransitionException.PublicDetail) in ProblemDetails `detail`; the
  // gateway must surface it via Error.cause so the UI can render it instead of a vague message.
  it('maps a generic invalid-transition 409 to invalid_transition with the specific edge in cause', async () => {
    const { transitionSessionState } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({
          type: 'invalid-state-transition',
          title: 'Conflict.',
          detail: "Session cannot transition from 'Active' to 'Finished'.",
        }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    const act = transitionSessionState('session-1', 'Finished')

    await expect(act).rejects.toThrowError('invalid_transition')
    await act.catch((err: Error) => {
      expect(err.cause).toBe("Session cannot transition from 'Active' to 'Finished'.")
    })
  })

  it('maps a session-no-teams 409 to no_teams without a cause', async () => {
    const { transitionSessionState } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ type: 'session-no-teams', detail: 'This session has no teams.' }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(transitionSessionState('session-1', 'Active')).rejects.toThrowError('no_teams')
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

  // HU-36B operator post-close answer review: 200 parse, auth/status mapping, and 409 cause.
  it('parses the trivia answer review DTO on a 200', async () => {
    const { getTriviaAnswerReview } = await import('@/app/lib/sessions')
    const payload = {
      liveSessionId: 'session-1',
      questionSequenceOrder: 2,
      teams: [
        {
          teamId: 'team-a',
          teamCode: 'AAA',
          displayName: 'Alpha',
          selectedOptionSequenceOrder: 2,
          isCorrect: true,
          scoreValue: 100,
          answeredAt: '2026-07-10T10:00:00Z',
        },
        {
          teamId: 'team-b',
          teamCode: 'BBB',
          displayName: 'Bravo',
          selectedOptionSequenceOrder: 1,
          isCorrect: false,
          scoreValue: 0,
          answeredAt: '2026-07-10T10:00:01Z',
        },
        {
          teamId: 'team-c',
          teamCode: 'CCC',
          displayName: 'Charlie',
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

    await expect(getTriviaAnswerReview('session-1', 2)).resolves.toEqual(payload)

    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/trivia/questions/2/answer-review',
      expect.objectContaining({ cache: 'no-store', headers: expect.any(Headers) }),
    )
  })

  it('maps a 409 to question_not_closed', async () => {
    const { getTriviaAnswerReview } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 409 }))

    await expect(getTriviaAnswerReview('session-1', 2)).rejects.toThrowError('question_not_closed')
  })

  it('maps a 403 (non-owning operator) to an unauthorized identity error for answer review', async () => {
    const { getTriviaAnswerReview } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 403 }))

    await expect(getTriviaAnswerReview('session-1', 2)).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({
        name: 'IdentityError',
        code: 'unauthorized',
      }),
    )
  })

  it('maps a 401 to an unauthorized identity error for answer review', async () => {
    const { getTriviaAnswerReview } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 401 }))

    await expect(getTriviaAnswerReview('session-1', 2)).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )
  })

  it('maps a 404 to an unknown identity error (Session not found) for answer review', async () => {
    const { getTriviaAnswerReview } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 404 }))

    await expect(getTriviaAnswerReview('session-1', 2)).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({
        name: 'IdentityError',
        code: 'unknown',
        message: 'Session not found',
      }),
    )
  })

  it('maps a duplicate association conflict to a stable frontend error', async () => {
    const { associateTeamToSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('fresh-access-token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        // Discriminated by the ProblemDetails `type` slug, not the generic `detail` sentence.
        JSON.stringify({
          type: 'duplicate-team-association-in-session',
          detail: 'The request conflicts with the current state of the resource.',
        }),
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

  it('maps a non-scheduled session conflict to session_not_scheduled by type', async () => {
    const { associateTeamToSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('fresh-access-token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({
          type: 'team-association-requires-scheduled-session',
          detail: 'The request conflicts with the current state of the resource.',
        }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      associateTeamToSession('session-1', 'reference-team-1'),
    ).rejects.toThrowError('session_not_scheduled')
  })

  it('maps an unrecognized association 409 type to a generic conflict', async () => {
    const { associateTeamToSession } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('fresh-access-token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ type: 'something-else', detail: 'Conflict.' }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      associateTeamToSession('session-1', 'reference-team-1'),
    ).rejects.toThrowError('association_conflict')
  })

  // HU-26 operator clue release: 200 parse, auth/status mapping, and the three 409 detail causes.
  it('releases a target hidden clue to one team and returns the parsed result', async () => {
    const { releaseClue } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ targetId: 'target-1', releasedTeamIds: ['team-a'] }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      releaseClue('session-1', { targetId: 'target-1', teamId: 'team-a' }),
    ).resolves.toEqual({ targetId: 'target-1', releasedTeamIds: ['team-a'] })

    // Single-team body includes teamId.
    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/clues/release',
      expect.objectContaining({
        method: 'POST',
        headers: expect.any(Headers),
        body: JSON.stringify({ targetId: 'target-1', teamId: 'team-a' }),
      }),
    )
  })

  it('omits teamId from the body when releasing to all teams', async () => {
    const { releaseClue } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ targetId: 'target-1', releasedTeamIds: ['team-a', 'team-b'] }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await releaseClue('session-1', { targetId: 'target-1' })

    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/clues/release',
      expect.objectContaining({ body: JSON.stringify({ targetId: 'target-1' }) }),
    )
  })

  it('maps a 400 to invalid_input', async () => {
    const { releaseClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 400 }))
    await expect(releaseClue('session-1', { targetId: '' })).rejects.toThrowError('invalid_input')
  })

  it('maps 401 and 403 to an unauthorized identity error', async () => {
    const { releaseClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 401 }))
    await expect(releaseClue('session-1', { targetId: 't' })).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 403 }))
    await expect(releaseClue('session-1', { targetId: 't' })).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )
  })

  it('maps a 404 to session_not_found', async () => {
    const { releaseClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 404 }))
    await expect(releaseClue('session-1', { targetId: 't' })).rejects.toThrowError('session_not_found')
  })

  it('maps each 409 type cause to its distinct typed error', async () => {
    const { releaseClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    // The backend puts the discriminating slug in ProblemDetails `type`; `detail` is generic.
    const conflict = (type: string) =>
      new Response(
        JSON.stringify({ type, title: 'Conflict.', detail: 'The request conflicts with the current state of the resource.' }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      )

    vi.mocked(global.fetch).mockResolvedValueOnce(conflict('clue-already-released-to-team'))
    await expect(releaseClue('s', { targetId: 't', teamId: 'a' })).rejects.toThrowError('already_released')

    vi.mocked(global.fetch).mockResolvedValueOnce(conflict('clue-not-releasable'))
    await expect(releaseClue('s', { targetId: 't' })).rejects.toThrowError('not_releasable')

    vi.mocked(global.fetch).mockResolvedValueOnce(conflict('session-not-active-for-clue-release'))
    await expect(releaseClue('s', { targetId: 't' })).rejects.toThrowError('not_active')
  })

  it('maps an unrecognised 409 type to release_conflict', async () => {
    const { releaseClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify({ type: 'something-else', title: 'Conflict.', detail: 'Conflict.' }), {
        status: 409,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    await expect(releaseClue('s', { targetId: 't' })).rejects.toThrowError('release_conflict')
  })

  // HU-28 operator operative-clue authoring: 200 parse, auth/status mapping, POST body, single 409 detail cause.
  it('assigns an operative clue to one team and returns the parsed result', async () => {
    const { addOperativeClue } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({
          operativeClueIds: ['clue-1'],
          assignedTeamIds: ['team-a'],
          clueText: 'Look beneath the blue banner.',
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await expect(
      addOperativeClue('session-1', { clueText: 'Look beneath the blue banner.', teamIds: ['team-a'] }),
    ).resolves.toEqual({
      operativeClueIds: ['clue-1'],
      assignedTeamIds: ['team-a'],
      clueText: 'Look beneath the blue banner.',
    })

    // Single-team body sends the exact clueText + the one selected id.
    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/operative-clues',
      expect.objectContaining({
        method: 'POST',
        headers: expect.any(Headers),
        body: JSON.stringify({ clueText: 'Look beneath the blue banner.', teamIds: ['team-a'] }),
      }),
    )
  })

  it('sends every selected team id when assigning to several/all teams', async () => {
    const { addOperativeClue } = await import('@/app/lib/sessions')

    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({
          operativeClueIds: ['clue-1', 'clue-2'],
          assignedTeamIds: ['team-a', 'team-b'],
          clueText: 'Regroup at the fountain.',
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    await addOperativeClue('session-1', { clueText: 'Regroup at the fountain.', teamIds: ['team-a', 'team-b'] })

    // "All teams" is the full id list assembled by the caller — sent verbatim (no omit-for-all signal).
    expect(global.fetch).toHaveBeenCalledWith(
      'http://localhost:8000/api/sessions/session-1/operative-clues',
      expect.objectContaining({
        body: JSON.stringify({ clueText: 'Regroup at the fountain.', teamIds: ['team-a', 'team-b'] }),
      }),
    )
  })

  it('maps a 400 to invalid_input', async () => {
    const { addOperativeClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 400 }))
    await expect(
      addOperativeClue('session-1', { clueText: '', teamIds: ['team-a'] }),
    ).rejects.toThrowError('invalid_input')
  })

  it('maps 401 and 403 to an unauthorized identity error', async () => {
    const { addOperativeClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 401 }))
    await expect(
      addOperativeClue('session-1', { clueText: 't', teamIds: ['a'] }),
    ).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 403 }))
    await expect(
      addOperativeClue('session-1', { clueText: 't', teamIds: ['a'] }),
    ).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )
  })

  it('maps a 404 to session_not_found', async () => {
    const { addOperativeClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(new Response('', { status: 404 }))
    await expect(
      addOperativeClue('session-1', { clueText: 't', teamIds: ['a'] }),
    ).rejects.toThrowError('session_not_found')
  })

  it('maps the not-live 409 detail to not_live, and an unrecognised 409 to clue_conflict', async () => {
    const { addOperativeClue } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    // Single 409 cause here: the domain message lands in ProblemDetails `detail`.
    vi.mocked(global.fetch).mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          title: 'Conflict.',
          detail: "Adding an operative clue requires an Active or Paused live session. Current state is 'Scheduled'.",
        }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )
    await expect(
      addOperativeClue('s', { clueText: 't', teamIds: ['a'] }),
    ).rejects.toThrowError('not_live')

    vi.mocked(global.fetch).mockResolvedValueOnce(
      new Response(JSON.stringify({ title: 'Conflict.', detail: 'Some other conflict.' }), {
        status: 409,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    await expect(
      addOperativeClue('s', { clueText: 't', teamIds: ['a'] }),
    ).rejects.toThrowError('clue_conflict')
  })

  // HU-24B operator ranking snapshot — the REST fallback behind the RankingChanged push.
  it('fetches the operator ranking through the gateway with a bearer token', async () => {
    const { getOperatorRanking } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    const snapshot = {
      liveSessionId: 'session-1',
      generatedAt: '2026-07-16T10:00:00Z',
      calculationVersion: 3,
      rows: [
        { teamId: 'ref-a', teamDisplayName: 'Alpha', position: 1, totalScore: 300, resolutionTime: '00:05:00' },
      ],
    }
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(snapshot), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await expect(getOperatorRanking('session-1')).resolves.toEqual(snapshot)

    // The operator route is teamId-less: access is proven by session assignment, not team membership.
    const [url, init] = vi.mocked(global.fetch).mock.calls[0]
    expect(url).toBe('http://localhost:8000/api/sessions/session-1/ranking/operator')
    expect((init?.headers as Headers).get('Authorization')).toBe('Bearer token')
  })

  it('returns the empty ranking snapshot as data, not as an error', async () => {
    const { getOperatorRanking } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    // A live session with no score entries yet: the backend well-known empty snapshot.
    const empty = { liveSessionId: 's', generatedAt: '0001-01-01T00:00:00+00:00', calculationVersion: 0, rows: [] }
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(empty), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    )

    await expect(getOperatorRanking('s')).resolves.toEqual(empty)
  })

  it('maps ranking 401 and 403 to an unauthorized identity error', async () => {
    const { getOperatorRanking } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 401 }))
    await expect(getOperatorRanking('s')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )

    // 403 = an operator not assigned to this session; the scoring ownership Proxy denies.
    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 403 }))
    await expect(getOperatorRanking('s')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )
  })

  // HU-24B operator evidence trace — the REST fallback behind the EvidenceSubmission* pushes.
  it('fetches the operator evidence trace through the gateway with a bearer token', async () => {
    const { getOperatorEvidenceTrace } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    const trace = {
      liveSessionId: 'session-1',
      items: [
        {
          evidenceSubmissionId: 'sub-1',
          teamId: 'team-1',
          activeSubstageId: 'substage-1',
          submissionType: 'TreasureHuntQrScan',
          originReference: 'target:9f1c',
          submittedAt: '2026-07-16T10:01:05Z',
          validationState: 'Accepted',
          rejectionReason: null,
          resolvedAt: '2026-07-16T10:01:09Z',
        },
      ],
    }
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(trace), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await expect(getOperatorEvidenceTrace('session-1')).resolves.toEqual(trace)

    // No teamId filter: the assigned operator is entitled to the whole session's trace, and access is
    // proven by session assignment rather than team membership.
    const [url, init] = vi.mocked(global.fetch).mock.calls[0]
    expect(url).toBe('http://localhost:8000/api/sessions/session-1/evidence-submissions')
    expect((init?.headers as Headers).get('Authorization')).toBe('Bearer token')
  })

  it('returns an empty evidence trace as data, not as an error', async () => {
    const { getOperatorEvidenceTrace } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    // A live session where nobody has submitted anything yet.
    const empty = { liveSessionId: 's', items: [] }
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(empty), { status: 200, headers: { 'Content-Type': 'application/json' } }),
    )

    await expect(getOperatorEvidenceTrace('s')).resolves.toEqual(empty)
  })

  it('maps evidence trace 401 and 403 to an unauthorized identity error', async () => {
    const { getOperatorEvidenceTrace } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 401 }))
    await expect(getOperatorEvidenceTrace('s')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )

    // 403 = an operator not assigned to this session; the ownership Proxy denies.
    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 403 }))
    await expect(getOperatorEvidenceTrace('s')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
    )
  })

  // A transient 5xx must stay distinguishable from a 403, so the action can avoid telling a
  // legitimately assigned operator they are not authorized.
  it('maps an evidence trace 5xx to an unknown identity error, not unauthorized', async () => {
    const { getOperatorEvidenceTrace } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 503 }))
    await expect(getOperatorEvidenceTrace('s')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unknown' }),
    )
  })

  it('fetches session history through the gateway with a bearer token', async () => {
    const { getSessionHistory } = await import('@/app/lib/sessions')
    const history = {
      liveSessionId: 'session-1',
      events: [
        {
          sessionEventId: 'event-1',
          eventType: 'SessionStateChanged',
          teamId: null,
          occurredAt: '2026-07-17T10:42:00.000Z',
          responsibleUserExternalId: 'user-1',
          payloadSummary: "Preparing -> Active",
        },
      ],
    }
    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify(history), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await expect(getSessionHistory('session-1')).resolves.toEqual(history)

    const [url, init] = vi.mocked(global.fetch).mock.calls[0]
    expect(url).toBe('http://localhost:8000/api/sessions/session-1/history')
    expect((init?.headers as Headers).get('Authorization')).toBe('Bearer token')
  })

  it('narrows session history to a team via the teamId query parameter', async () => {
    const { getSessionHistory } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(JSON.stringify({ liveSessionId: 'session-1', events: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await getSessionHistory('session-1', 'team-7')

    const [url] = vi.mocked(global.fetch).mock.calls[0]
    expect(url).toBe('http://localhost:8000/api/sessions/session-1/history?teamId=team-7')
  })

  // 403 here is a role failure (the endpoint is Administrator OR Operator), not an assignment
  // failure — and a 5xx must stay distinguishable from it.
  it('maps session history 401/403 to unauthorized and 5xx to unknown', async () => {
    const { getSessionHistory } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    for (const status of [401, 403]) {
      vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status }))
      await expect(getSessionHistory('s')).rejects.toEqual(
        expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unauthorized' }),
      )
    }

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 503 }))
    await expect(getSessionHistory('s')).rejects.toEqual(
      expect.objectContaining<Partial<IdentityError>>({ name: 'IdentityError', code: 'unknown' }),
    )
  })

  it('maps a session history 404 to session_not_found', async () => {
    const { getSessionHistory } = await import('@/app/lib/sessions')
    getValidAccessTokenMock.mockResolvedValue('token')

    vi.mocked(global.fetch).mockResolvedValueOnce(new Response('', { status: 404 }))
    await expect(getSessionHistory('s')).rejects.toThrowError('session_not_found')
  })
})
