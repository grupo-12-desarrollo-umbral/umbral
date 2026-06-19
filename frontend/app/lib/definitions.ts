export type Role = 'Administrator' | 'Operator' | 'Participant'

export type SessionPayload = {
  externalIdentityId: string
  displayName: string
  email: string
  role: Role
  isActive: boolean
  expiresAt: Date
}

export type AuthenticatedActorProfileDto = {
  externalIdentityId: string
  displayName: string
  email: string
  role: string
  isActive: boolean
}

export type AuthenticateUserResultDto = {
  actor: {
    externalIdentityId: string
    displayName: string
    email: string
    role: string
    isActive: boolean
  }
  access: {
    capability: string
    isAllowed: boolean
    reason: string
  }
}

export type ProtectedAccessDecisionDto = {
  capability: string
  isAllowed: boolean
  reason: string
}

export type UserAccessCatalogItemDto = {
  id: number
  externalIdentityId: string
  displayName: string
  email: string
  role: string
  isActive: boolean
}

export type PagedResult<T> = {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export type TeamDto = {
  teamId: string
  displayName: string
  teamCode: string
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export type CreateTeamResultDto = {
  teamId: string
}

export type MissionSummaryDto = {
  id: number
  name: string
  description: string
  difficulty: string
  isActive: boolean
  activationState: string
  isSourceReady: boolean
}

export type MissionDto = {
  id: number
  name: string
  description: string
  difficulty: string
  maximumTimeMinutes: number
  isActive: boolean
  activationState: string
  isSourceReady: boolean
}

export type TeamMembershipDto = {
  teamMembershipId: string
  teamId: string
  userId: number       // database integer id of the assigned user
  email: string
  displayName: string
  assignedAt: string   // ISO 8601
}

export type TriviaOptionDto = {
  id: number
  optionText: string
  sequenceOrder: number
  isCorrect: boolean
}

export type TriviaQuestionDto = {
  id: number
  prompt: string
  sequenceOrder: number
  isActive: boolean
  options: TriviaOptionDto[]
  scoreValue: number | null
  timeLimitSeconds: number | null
  explanation: string | null
}

export type TriviaOptionRequest = {
  optionText: string
  sequenceOrder: number
  isCorrect: boolean
}

export type TriviaQuestionRequest = {
  prompt: string
  sequenceOrder: number
  scoreValue: number
  timeLimitSeconds: number
  explanation: string | null
  isActive: boolean
  options: TriviaOptionRequest[]
}

export type TriviaQuizSummaryDto = {
  id: number
  title: string
  description: string
  status: string   // "Draft" | "Published" | "Archived"
  isSourceReady: boolean
  sourceTriviaQuizId: number | null
  hasUsageHistory: boolean
  isDuplicate: boolean
}

export type TriviaQuizDto = {
  id: number
  title: string
  description: string
  status: string
  isSourceReady: boolean
  sourceTriviaQuizId: number | null
  hasUsageHistory: boolean
  isDuplicate: boolean
  questions: TriviaQuestionDto[]
}

export type CreateTriviaSessionRequest = {
  missionId: number
  sourceTriviaQuizId: number
  title: string
  maximumTimeMinutes: number
  scheduledAt: string // ISO 8601 UTC string
}

export type TriviaSessionCreatedDto = {
  liveSessionId: string // UUID
  sessionCode: string // e.g. "SES-A1B2C3D4E5F6"
  title: string
  sessionState: string // "Scheduled"
  scheduledAt: string // ISO 8601
  sourceTriviaQuizId: number
  questionCount: number
}

export type SessionLifecycleState =
  | 'Scheduled'
  | 'Preparing'
  | 'Active'
  | 'Paused'
  | 'Finished'
  | 'Cancelled'

export type TransitionSessionStateRequest = {
  targetState: SessionLifecycleState
  reason?: string
}

export type TransitionSessionStateResultDto = {
  liveSessionId: string
  previousState: string
  currentState: string
  transitionedAt: string
  timer?: SessionTimerSnapshotDto | null
}

export type SessionStateChangedNotificationDto = {
  liveSessionId: string
  previousState: string
  currentState: string
  changedAt: string
}

export type SessionAssignmentSummaryDto = {
  liveSessionId: string
  sessionCode: string
  title: string
  sessionState: SessionLifecycleState | string
  assignedOperatorUserId: number | null // null = unassigned
  scheduledAt: string
  lastTransitionedAt?: string | null
}

export type AssociatedSessionTeamDto = {
  runtimeTeamId: string
  referenceTeamId: string
  displayName: string
  teamCode: string
  joinStatus: string
}

export type SessionAssociatedTeamsDto = {
  liveSessionId: string
  teams: AssociatedSessionTeamDto[]
}

export type AssociateTeamToSessionResultDto = {
  liveSessionId: string
  runtimeTeamId: string
  referenceTeamId: string
  displayName: string
  teamCode: string
  sessionState: string
  associatedTeamCount: number
}

// Result of PATCH /api/sessions/{id}/operator-assignment (projection of AssignOperatorToSessionResultDto)
export type AssignSessionOperatorResultDto = {
  liveSessionId: string
  assignedOperatorUserId: number
}

// A candidate operator derived from the Identity actor-facts catalog (not a new backend type)
export type AssignableOperatorDto = {
  id: number
  displayName: string
  email: string
  role: string
}

// "Advancing" = timer counting down (session Active and not expired).
// "Frozen"    = timer not moving (session Paused, Scheduled, or Preparing).
// "Expired"   = remaining time reached zero.
export type SessionTimerStatus = 'Advancing' | 'Frozen' | 'Expired'

// Response of GET /api/sessions/{id}/timer (Operator) and
// GET /api/sessions/{id}/participants/timer (Participant).
export type SessionTimerSnapshotDto = {
  liveSessionId: string
  teamId: string | null
  sessionState: SessionLifecycleState | string
  totalSeconds: number
  remainingSeconds: number
  timerStatus: SessionTimerStatus
  isAdvancing: boolean
  isExpired: boolean
  observedAt: string
  advancingSince: string | null
  expiredAt: string | null
  activeQuestion: ActiveQuestionSnapshotDto | null
}

// SignalR "SessionTimerUpdated" hub event payload.
// Note: time units are milliseconds (long on the backend), not seconds.
export type SessionTimerUpdatedNotificationDto = {
  liveSessionId: string
  remainingMilliseconds: number
  isPaused: boolean
  emittedAt: string
  totalMilliseconds: number
  isExpired: boolean
  sessionState: SessionLifecycleState | string
}

// SignalR "QuestionActivated" hub event payload.
// Broadcast when the trivia orchestration activates a question server-side.
export type QuestionActivatedNotificationDto = {
  liveSessionId: string
  questionIndex: number // zero-based internal index
  sequenceOrder: number // one-based display number shown to operator
  prompt: string
  options: string[] // answer texts, order preserved — correct option NOT flagged
  timeLimitSeconds: number
  activatedAt: string // ISO 8601 UTC
}

export type ActiveQuestionSnapshotDto = QuestionActivatedNotificationDto & {
  remainingSeconds: number
}

// SignalR "QuestionClosed" hub event payload.
export type QuestionClosedNotificationDto = {
  liveSessionId: string
  questionIndex: number
  closedAt: string // ISO 8601 UTC
  wasExpiredByTimer: boolean
}

// Phases of the automated trivia round, derived from SignalR pushes only.
export type TriviaRoundPhase =
  | 'idle'
  | 'pregame'
  | 'question-active'
  | 'between-questions'

export class IdentityError extends Error {
  constructor(
    public code: 'deactivated' | 'unauthorized' | 'network' | 'unknown',
    message: string
  ) {
    super(message)
    this.name = 'IdentityError'
  }
}
