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

// --- HU-10A mission hierarchy (Phase 1) ---
export type MissionClueDto = {
  id: number
  sequenceOrder: number
  title: string
  text: string
  visibilityPolicy: string
}

export type MissionTargetDto = {
  id: number
  name: string
  qrCode: string
  sequenceOrder: number
  isActive: boolean
  clueId: number | null
  score: number
}

export type TriviaQuizSelectionDto = { triviaQuizId: number }

export type MissionSubstageDto = {
  id: number
  title: string
  sequenceOrder: number
  playMode: 'TreasureHunt' | 'Trivia' // JSON string enum
  triviaQuizSelection: TriviaQuizSelectionDto | null
  targets: MissionTargetDto[]
  clues: MissionClueDto[]
}

export type MissionStageDto = {
  id: number
  title: string
  sequenceOrder: number
  substages: MissionSubstageDto[]
}

export type MissionReadinessDto = {
  missionId: number
  activationState: string
  isReady: boolean
  failures: string[]
}

// --- HU-10A request payloads (added in P1, consumed 2.1–2.3) ---
export type AddMissionNodeRequest = {
  nodeType: 'Stage' | 'Substage' | 'Clue'
  title: string
  sequenceOrder: number
  stageId?: number // parent when nodeType === 'Substage'
  substageId?: number // parent when nodeType === 'Clue'
  playMode?: 'TreasureHunt' | 'Trivia'
  clueText?: string
  clueVisibilityPolicy?: string
}

export type UpdateMissionNodeRequest = {
  title: string
  sequenceOrder: number
  clueText?: string
  clueVisibilityPolicy?: string
}

export type AssignPlayModeRequest = { playMode: 'TreasureHunt' | 'Trivia' }

export type AddTargetRequest = {
  name: string
  qrCode: string
  sequenceOrder: number
  score: number
  isActive?: boolean
}

export type UpdateTargetRequest = {
  name: string
  qrCode: string
  sequenceOrder: number
  isActive: boolean
  score?: number
}

export type TriviaQuizSelectionRequest = { triviaQuizId: number }

export type MissionDto = {
  id: number
  name: string
  description: string
  difficulty: string
  maximumTimeMinutes: number
  isActive: boolean
  activationState: string
  isSourceReady: boolean
  stages: MissionStageDto[] // ← added in P1
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

export type CreateSessionRequest = {
  missionId: number
  title: string
  maximumTimeMinutes: number
  scheduledAt: string // ISO 8601 UTC string
}

export type SessionCreatedDto = {
  liveSessionId: string // UUID
  sessionCode: string // e.g. "SES-A1B2C3D4E5F6"
  title: string
  sessionState: string // "Scheduled" on create
  scheduledAt: string // ISO 8601
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

// Active-question timer status (the authoritative clock is the active trivia question window).
// "Advancing" = the active question timer is counting down (session Active, question open).
// "Frozen"    = the active question timer is held (session Paused, Scheduled, or Preparing).
// "Expired"   = the active question window reached zero.
// There is NO whole-session/mission countdown — a substage with no active question has no countdown.
export type SessionTimerStatus = 'Advancing' | 'Frozen' | 'Expired'

// Response of GET /api/sessions/{id}/timer (Operator) and
// GET /api/sessions/{id}/participants/timer (Participant).
// remainingSeconds/totalSeconds track the ACTIVE SUBSTAGE's timer — the active trivia question
// window — and are 0 when no question is active (treasure-hunt substage or between questions).
// activeQuestion is present ⇔ a trivia question is active.
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

// SignalR "SessionTimerUpdated" hub event payload — carries the active-substage
// (active trivia question) remaining window, not a whole-session countdown.
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

// SignalR "SubstageAdvanced" hub event payload (live-session:{id} group).
// Broadcast when orchestration moves from one substage to the next.
// toSubstageId absent/null ⇒ the FINAL substage just completed; the session will Finish next.
export type SubstageAdvancedNotificationDto = {
  liveSessionId: string
  fromSubstageId: string
  fromPlayMode: 'TreasureHunt' | 'Trivia' | string
  toSubstageId: string | null
}

// Phases of the automated trivia round, derived from SignalR pushes only.
export type TriviaRoundPhase =
  | 'idle'
  | 'pregame'
  | 'question-active'
  | 'between-questions'
  | 'substage-advancing'
  | 'complete'

export class IdentityError extends Error {
  constructor(
    public code: 'deactivated' | 'unauthorized' | 'network' | 'unknown',
    message: string
  ) {
    super(message)
    this.name = 'IdentityError'
  }
}
