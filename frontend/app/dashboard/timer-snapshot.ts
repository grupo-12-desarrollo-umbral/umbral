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
