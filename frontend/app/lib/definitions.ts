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

export type AssociatedSessionTeamDto = {
  runtimeTeamId: string    // UUID — session-scoped identity
  referenceTeamId: string  // UUID — catalog identity (matches TeamDto.teamId)
  displayName: string
  teamCode: string
  joinStatus: string       // participant join state; display-only for now
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

export class IdentityError extends Error {
  constructor(
    public code: 'deactivated' | 'unauthorized' | 'network' | 'unknown',
    message: string
  ) {
    super(message)
    this.name = 'IdentityError'
  }
}
