// Guards the retire-conflict contract: ADR-0003's active-mission reference check and the lifecycle
// state check both surface as 409, and only ProblemDetails `detail` tells them apart.
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

describe('retireTriviaQuiz', () => {
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
    getValidAccessTokenMock.mockResolvedValue('token')
    global.fetch = vi.fn()
  })

  it('carries the active-mission conflict detail on a 409', async () => {
    const { retireTriviaQuiz } = await import('@/app/lib/trivias')
    const detail =
      'This quiz is referenced by one or more active missions. Deactivate the mission or change its trivia selection first.'
    vi.mocked(global.fetch).mockResolvedValue(
      new Response(
        JSON.stringify({ type: 'trivia-quiz-referenced-by-active-mission', detail }),
        { status: 409, headers: { 'Content-Type': 'application/json' } },
      ),
    )

    const act = retireTriviaQuiz(1)

    await expect(act).rejects.toThrowError('trivia_retire_conflict')
    await act.catch((err: Error) => {
      expect(err.cause).toBe(detail)
    })
  })

  it('leaves cause undefined when the 409 body is unreadable', async () => {
    const { retireTriviaQuiz } = await import('@/app/lib/trivias')
    vi.mocked(global.fetch).mockResolvedValue(new Response('not json', { status: 409 }))

    const act = retireTriviaQuiz(1)

    await expect(act).rejects.toThrowError('trivia_retire_conflict')
    await act.catch((err: Error) => {
      expect(err.cause).toBeUndefined()
    })
  })
})
