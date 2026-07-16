// Guards the invariant that decides whether a timer snapshot may drive/seed the client trivia
// countdown: a paused question (remaining > 0) is live and freezable; the pre-game placeholder and a
// just-expired question (remaining 0, not advancing) are NOT — hydrating them froze the first
// question's countdown at 0 while the authoritative timer ticked (the Start-desync bug).
import { describe, expect, it } from 'vitest'
import { isNonLiveQuestionSnapshot, revealAnswerReviewSequenceOrder } from '@/app/dashboard/timer-snapshot'
import type { SessionTimerSnapshotDto } from '@/app/lib/definitions'

function snapshot(overrides: Partial<SessionTimerSnapshotDto> = {}): SessionTimerSnapshotDto {
  return {
    liveSessionId: 's1',
    teamId: null,
    sessionState: 'Active',
    totalSeconds: 30,
    remainingSeconds: 18,
    timerStatus: 'Advancing',
    isAdvancing: true,
    isExpired: false,
    observedAt: '2026-07-05T00:00:00Z',
    advancingSince: null,
    expiredAt: null,
    activeQuestion: {
      liveSessionId: 's1',
      questionIndex: 0,
      sequenceOrder: 1,
      prompt: 'Q',
      options: ['a', 'b'],
      timeLimitSeconds: 30,
      activatedAt: '2026-07-05T00:00:00Z',
      remainingSeconds: 18,
    },
    ...overrides,
  }
}

function withQuestionRemaining(seconds: number, rest: Partial<SessionTimerSnapshotDto> = {}) {
  const base = snapshot(rest)
  return { ...base, activeQuestion: { ...base.activeQuestion!, remainingSeconds: seconds } }
}

describe('isNonLiveQuestionSnapshot', () => {
  it('is true for the Start pre-game placeholder (expired, zero remaining, not advancing)', () => {
    expect(
      isNonLiveQuestionSnapshot(withQuestionRemaining(0, { isAdvancing: false, isExpired: true })),
    ).toBe(true)
  })

  it('is true for a just-expired question (zero remaining, not advancing)', () => {
    expect(
      isNonLiveQuestionSnapshot(withQuestionRemaining(0, { isAdvancing: false, isExpired: false })),
    ).toBe(true)
  })

  it('is false for a paused question that still has remaining time (must freeze, not skip)', () => {
    expect(
      isNonLiveQuestionSnapshot(
        withQuestionRemaining(24, { isAdvancing: false, timerStatus: 'Frozen', sessionState: 'Paused' }),
      ),
    ).toBe(false)
  })

  it('is false for a live advancing question', () => {
    expect(isNonLiveQuestionSnapshot(withQuestionRemaining(18, { isAdvancing: true }))).toBe(false)
  })

  it('is false when there is no active question', () => {
    expect(isNonLiveQuestionSnapshot(snapshot({ activeQuestion: null, isAdvancing: false }))).toBe(false)
  })
})

describe('revealAnswerReviewSequenceOrder', () => {
  it('returns the just-closed sequence order during the reveal window (no active question)', () => {
    expect(
      revealAnswerReviewSequenceOrder(
        snapshot({ activeQuestion: null, isAdvancing: false, awaitingRevealQuestionSequenceOrder: 3 }),
      ),
    ).toBe(3)
  })

  it('returns null when a question is active (a fresh activation must win over a stale reveal field)', () => {
    // Even if the field is somehow present, an active question means we are past the reveal window.
    expect(revealAnswerReviewSequenceOrder(snapshot({ awaitingRevealQuestionSequenceOrder: 3 }))).toBeNull()
  })

  it('returns null when no question is active and there is nothing awaiting reveal', () => {
    expect(
      revealAnswerReviewSequenceOrder(snapshot({ activeQuestion: null, awaitingRevealQuestionSequenceOrder: null })),
    ).toBeNull()
  })

  it('returns null when the field is absent (older backend / non-trivia snapshot)', () => {
    expect(revealAnswerReviewSequenceOrder(snapshot({ activeQuestion: null }))).toBeNull()
  })
})
