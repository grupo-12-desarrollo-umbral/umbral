import 'server-only'
import { IdentityError, type PagedResult, type UserAccessCatalogItemDto, type AssignableOperatorDto, type InvitableRole, type InviteUserResultDto } from './definitions'
import { verifySession } from './dal'

// Direct service URL for BFF-to-service calls (bypasses JWT gateway auth)
const IDENTITY_SERVICE_URL = 'http://localhost:5002'

function getIdentityHeaders(session: {
  externalIdentityId: string
  displayName: string
  email: string
  role: string
}) {
  return {
    'X-User-Id': session.externalIdentityId,
    'X-User-Role': session.role,
    'X-User-Email': session.email,
  }
}

export async function listUsers(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<UserAccessCatalogItemDto>> {
  const session = await verifySession()
  const url = new URL(`${IDENTITY_SERVICE_URL}/api/users`)
  url.searchParams.set('page', String(page))
  url.searchParams.set('pageSize', String(pageSize))

  const response = await fetch(url.toString(), {
    headers: getIdentityHeaders(session),
    cache: 'no-store',
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed. Missing or invalid trusted headers.')
  }

  if (response.status === 403) {
    throw new IdentityError('deactivated', 'Your account has been deactivated.')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `listUsers failed with status ${response.status}`)
  }

  return response.json()
}

function buildLogicalUserKey(user: Pick<UserAccessCatalogItemDto, 'externalIdentityId' | 'email' | 'id'>): string {
  const externalIdentityKey = user.externalIdentityId.trim().toLowerCase()
  if (externalIdentityKey) return `external:${externalIdentityKey}`

  const emailKey = user.email.trim().toLowerCase()
  if (emailKey) return `email:${emailKey}`

  return `id:${user.id}`
}

function dedupeUsers(users: UserAccessCatalogItemDto[]): UserAccessCatalogItemDto[] {
  const uniqueUsers = new Map<string, UserAccessCatalogItemDto>()
  const seenExternalIdentityIds = new Set<string>()
  const seenEmails = new Set<string>()

  for (const user of users) {
    const externalIdentityKey = user.externalIdentityId.trim().toLowerCase()
    const emailKey = user.email.trim().toLowerCase()

    if (
      (externalIdentityKey && seenExternalIdentityIds.has(externalIdentityKey))
      || (emailKey && seenEmails.has(emailKey))
    ) {
      continue
    }

    const logicalKey = buildLogicalUserKey(user)
    if (externalIdentityKey) seenExternalIdentityIds.add(externalIdentityKey)
    if (emailKey) seenEmails.add(emailKey)

    if (!uniqueUsers.has(logicalKey)) {
      uniqueUsers.set(logicalKey, user)
    }
  }

  return [...uniqueUsers.values()]
}

export async function listDedupedUsers(
  page = 1,
  pageSize = 20,
): Promise<PagedResult<UserAccessCatalogItemDto>> {
  const allUsers: UserAccessCatalogItemDto[] = []
  let currentPage = 1

  for (;;) {
    const result = await listUsers(currentPage, 100)
    allUsers.push(...result.items)
    if (!result.hasNextPage) break
    currentPage += 1
  }

  const dedupedUsers = dedupeUsers(allUsers)
  const totalCount = dedupedUsers.length
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const safePage = Math.min(Math.max(page, 1), totalPages)
  const startIndex = (safePage - 1) * pageSize
  const items = dedupedUsers.slice(startIndex, startIndex + pageSize)

  return {
    items,
    totalCount,
    page: safePage,
    pageSize,
    totalPages,
    hasPreviousPage: safePage > 1,
    hasNextPage: safePage < totalPages,
  }
}

// Reads the RFC 7807 `detail` off a problem+json body so a backend validation message (e.g. a bad
// email address) can be surfaced verbatim. Falls back to null when the body is absent or unparseable.
async function readProblemDetail(response: Response): Promise<string | null> {
  try {
    const problem = (await response.json()) as { detail?: unknown }
    return typeof problem.detail === 'string' && problem.detail.trim() ? problem.detail : null
  } catch {
    return null
  }
}

export async function inviteUser(email: string, role: InvitableRole): Promise<InviteUserResultDto> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/users/invitations`, {
    method: 'POST',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ email, role }),
  })

  // 400 validation-failed: surface the backend's readable detail (invalid email, unknown role, …).
  if (response.status === 400) {
    throw new Error((await readProblemDetail(response)) ?? 'The invitation details are invalid.')
  }

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }

  // 409 conflict: an account already exists for this email.
  if (response.status === 409) {
    throw new Error((await readProblemDetail(response)) ?? 'A user with this email address already exists.')
  }

  // 422 unprocessable: role is not invitable (Participant). The form never offers it, but the guard
  // mirrors the backend contract in case a request is crafted directly.
  if (response.status === 422) {
    throw new Error(
      (await readProblemDetail(response)) ??
        'Participants self-register and cannot be invited. Only Operator and Administrator accounts can be invited.',
    )
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `inviteUser failed with status ${response.status}`)
  }

  return response.json()
}

export async function deactivateUserAccess(id: number): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/users/${id}/access`, {
    method: 'DELETE',
    headers: getIdentityHeaders(session),
  })

  if (response.status === 400) {
    throw new Error('already_deactivated')
  }

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed. Missing or invalid trusted headers.')
  }

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `deactivateUserAccess failed with status ${response.status}`)
  }
}

export async function assignUserRole(id: number, role: string): Promise<void> {
  const session = await verifySession()
  const response = await fetch(`${IDENTITY_SERVICE_URL}/api/users/${id}/role`, {
    method: 'PATCH',
    headers: {
      ...getIdentityHeaders(session),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ role }),
  })

  if (response.status === 400) {
    throw new Error('invalid_role')
  }

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed.')
  }

  if (response.status === 403) {
    throw new IdentityError('unauthorized', 'Forbidden. Administrator role required.')
  }

  if (response.status === 422) {
    throw new Error('target_deactivated')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `assignUserRole failed with status ${response.status}`)
  }
}

export async function listAssignableOperators(): Promise<AssignableOperatorDto[]> {
  const assignable = new Map<string, AssignableOperatorDto>()
  const result = await listDedupedUsers(1, Number.MAX_SAFE_INTEGER)

  // The selector should only show active operators from the logical user catalog.
  for (const u of result.items) {
    if (!u.isActive || u.role !== 'Operator') continue

    assignable.set(buildLogicalUserKey(u), {
      id: u.id,
      displayName: u.displayName,
      email: u.email,
      role: u.role,
    })
  }

  return [...assignable.values()].sort((left, right) =>
    left.displayName.localeCompare(right.displayName) || left.email.localeCompare(right.email),
  )
}
