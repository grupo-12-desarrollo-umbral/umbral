import type { UserAccessCatalogItemDto } from './definitions'

// Derived account status for a user in the admin user-management catalog. The catalog DTO has no
// explicit status field, so "pending" is inferred (see getAccountStatus).
export type AccountStatus = 'pending' | 'active' | 'deactivated'

// The user catalog has no explicit status field: an invited account is created active with its email
// standing in as a placeholder display name until the invitee's first sign-in overwrites it. So an
// active user whose display name still equals its email is an unaccepted invitation — surfaced as
// "pending" so an administrator can tell it apart from a working account. A deactivated account wins
// over the pending heuristic regardless of its display name.
export function getAccountStatus(user: UserAccessCatalogItemDto): AccountStatus {
  if (!user.isActive) return 'deactivated'
  if (user.displayName.trim().toLowerCase() === user.email.trim().toLowerCase()) return 'pending'
  return 'active'
}

export const accountStatusLabel: Record<AccountStatus, string> = {
  pending: 'Invitación pendiente',
  active: 'Activo',
  deactivated: 'Desactivado',
}

export const accountStatusTone: Record<AccountStatus, 'success' | 'warning' | 'critical'> = {
  pending: 'warning',
  active: 'success',
  deactivated: 'critical',
}
