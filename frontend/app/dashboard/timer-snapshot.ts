import type { SessionTimerSnapshotDto } from '@/app/lib/definitions'

// A timer snapshot whose question has no remaining time and is not advancing is NOT a live countdown:
// it is either the pre-game placeholder emitted the instant a session goes Active (the first real
// question has not started yet) or a just-expired question that SignalR's close/advance events will
// replace. It must not seed or freeze the client trivia countdown (that would clobber the live one that
// QuestionActivated starts) nor overwrite a fresher live snapshot. A genuinely paused question — the
// case we DO want to freeze on — always carries remaining time, so this excludes only the dead states.
export function isNonLiveQuestionSnapshot(timer: SessionTimerSnapshotDto): boolean {
  return timer.activeQuestion != null && !timer.isAdvancing && timer.activeQuestion.remainingSeconds <= 0
}

// The just-closed trivia question's sequence order to fetch an answer review for (HU-36B AC4), or
// null when the snapshot is not in the reveal window. During the reveal window the backend reports no
// active question but carries the just-closed sequence order — so an operator who selects/reconnects
// into that window can populate the answer-review panel without a QuestionActivated push to key off.
// Guarded on there being no active question so a fresh activation always wins over a stale reveal.
export function revealAnswerReviewSequenceOrder(timer: SessionTimerSnapshotDto): number | null {
  if (timer.activeQuestion != null) return null
  return timer.awaitingRevealQuestionSequenceOrder ?? null
}
