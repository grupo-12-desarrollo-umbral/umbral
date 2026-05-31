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

export class IdentityError extends Error {
  constructor(
    public code: 'deactivated' | 'unauthorized' | 'network' | 'unknown',
    message: string
  ) {
    super(message)
    this.name = 'IdentityError'
  }
}
