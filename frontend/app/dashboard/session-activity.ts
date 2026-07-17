import type {
  EvidenceSubmissionRegisteredNotificationDto,
  EvidenceSubmissionResolvedNotificationDto,
  EvidenceSubmissionType,
  QuestionActivatedNotificationDto,
  QuestionClosedNotificationDto,
  SessionStateChangedNotificationDto,
  SubstageAdvancedNotificationDto,
  TeamAnsweredNotificationDto,
} from '@/app/lib/definitions'

// Operator live activity feed. A purely client-side projection of the SignalR pushes the operator
// already receives (state, question open/close, substage advance, team answered, evidence in/out) into a
// newest-first log. No REST snapshot backs it — a session switch resets it and it rebuilds from live
// events — so unlike the evidence trace there is nothing to merge: each event is one immutable line.
// Extracted from DashboardClient so the event→line mapping is directly testable.

const MAX_ENTRIES = 50

export type SessionActivityKind =
  | 'state'
  | 'questionActivated'
  | 'questionClosed'
  | 'substageAdvanced'
  | 'teamAnswered'
  | 'evidenceRegistered'
  | 'evidenceResolved'

export type SessionActivityEntry = {
  id: string // stable client id (monotonic within a session) — React key
  at: string // ISO 8601 event time; receipt time only for SubstageAdvanced, whose payload carries none
  kind: SessionActivityKind
  label: string // short badge text (fixed-width column): 'State', 'Question', 'Answer', …
  teamId: string | null // runtime team id when team-scoped; null for a whole-session event
  summary: string // human-readable description
}

export interface SessionActivityState {
  seq: number // monotonic counter → entry ids; survives the cap so ids never collide
  entries: SessionActivityEntry[] // newest first, capped at MAX_ENTRIES
}

export const emptySessionActivity: SessionActivityState = { seq: 0, entries: [] }

export type SessionActivityAction =
  | { type: 'reset' }
  | { type: 'state'; data: SessionStateChangedNotificationDto }
  | { type: 'questionActivated'; data: QuestionActivatedNotificationDto }
  | { type: 'questionClosed'; data: QuestionClosedNotificationDto }
  // SubstageAdvanced is the one push with no timestamp of its own; the caller stamps receipt time.
  | { type: 'substageAdvanced'; data: SubstageAdvancedNotificationDto; receivedAt: string }
  | { type: 'teamAnswered'; data: TeamAnsweredNotificationDto }
  | { type: 'evidenceRegistered'; data: EvidenceSubmissionRegisteredNotificationDto }
  | { type: 'evidenceResolved'; data: EvidenceSubmissionResolvedNotificationDto }

function submissionTypeLabel(type: EvidenceSubmissionType): string {
  switch (type) {
    case 'TreasureHuntQrScan':
      return 'QR scan'
    case 'TriviaAnswer':
      return 'trivia answer'
    default:
      // Forward-compat: a submission type the client doesn't know yet still reads sensibly.
      return 'evidence'
  }
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

function push(state: SessionActivityState, entry: Omit<SessionActivityEntry, 'id'>): SessionActivityState {
  const seq = state.seq + 1
  const entries = [{ id: `activity-${seq}`, ...entry }, ...state.entries].slice(0, MAX_ENTRIES)
  return { seq, entries }
}

export function sessionActivityReducer(
  state: SessionActivityState,
  action: SessionActivityAction,
): SessionActivityState {
  switch (action.type) {
    case 'reset':
      return emptySessionActivity
    case 'state': {
      const n = action.data
      // previousState is '' when a normalizer couldn't resolve it — then just report the new state.
      const summary = n.previousState ? `${n.previousState} → ${n.currentState}` : `Now ${n.currentState}`
      return push(state, { at: n.changedAt, kind: 'state', label: 'State', teamId: null, summary })
    }
    case 'questionActivated': {
      const n = action.data
      return push(state, {
        at: n.activatedAt,
        kind: 'questionActivated',
        label: 'Question',
        teamId: null,
        summary: `Question ${n.sequenceOrder} opened`,
      })
    }
    case 'questionClosed': {
      const n = action.data
      // QuestionClosed carries the zero-based questionIndex (no sequenceOrder); +1 for the display number.
      const summary = `Question ${n.questionIndex + 1} closed${n.wasExpiredByTimer ? ' (time expired)' : ''}`
      return push(state, { at: n.closedAt, kind: 'questionClosed', label: 'Question', teamId: null, summary })
    }
    case 'substageAdvanced': {
      const n = action.data
      const summary =
        n.toSubstageId === null
          ? `Final ${n.fromPlayMode} substage complete`
          : `Advanced from ${n.fromPlayMode} substage`
      return push(state, { at: action.receivedAt, kind: 'substageAdvanced', label: 'Substage', teamId: null, summary })
    }
    case 'teamAnswered': {
      const n = action.data
      return push(state, {
        at: n.answeredAt,
        kind: 'teamAnswered',
        label: 'Answer',
        teamId: n.teamId,
        summary: `Answered question ${n.questionSequenceOrder}`,
      })
    }
    case 'evidenceRegistered': {
      const n = action.data
      return push(state, {
        at: n.submittedAt,
        kind: 'evidenceRegistered',
        label: 'Evidence',
        teamId: n.teamId,
        summary: `Submitted ${submissionTypeLabel(n.submissionType)}`,
      })
    }
    case 'evidenceResolved': {
      const n = action.data
      const base = `${capitalize(submissionTypeLabel(n.submissionType))} ${n.validationState.toLowerCase()}`
      const summary =
        n.validationState === 'Rejected' && n.rejectionReason ? `${base}: ${n.rejectionReason}` : base
      return push(state, { at: n.resolvedAt, kind: 'evidenceResolved', label: 'Evidence', teamId: n.teamId, summary })
    }
  }
}
