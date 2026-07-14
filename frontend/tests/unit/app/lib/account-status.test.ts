import { describe, expect, it } from 'vitest'

import { getAccountStatus, accountStatusLabel, accountStatusTone } from '@/app/lib/account-status'
import type { UserAccessCatalogItemDto } from '@/app/lib/definitions'

function createUser(overrides: Partial<UserAccessCatalogItemDto> = {}): UserAccessCatalogItemDto {
  return {
    id: 1,
    externalIdentityId: 'ext-1',
    displayName: 'Real Name',
    email: 'operator@example.com',
    role: 'Operator',
    isActive: true,
    ...overrides,
  }
}

describe('getAccountStatus', () => {
  it('classifies an active user whose display name equals its email as pending', () => {
    const user = createUser({ displayName: 'operator@example.com', email: 'operator@example.com' })
    expect(getAccountStatus(user)).toBe('pending')
  })

  it('classifies an active user with a distinct display name as active', () => {
    expect(getAccountStatus(createUser({ displayName: 'Real Name', email: 'operator@example.com' }))).toBe('active')
  })

  it('classifies an inactive user as deactivated even when display name equals email', () => {
    // Deactivation wins over the pending heuristic: an invited-then-deactivated account is deactivated.
    const user = createUser({ isActive: false, displayName: 'operator@example.com', email: 'operator@example.com' })
    expect(getAccountStatus(user)).toBe('deactivated')
  })

  it('normalizes case and surrounding whitespace when comparing display name to email', () => {
    const user = createUser({ displayName: '  Operator@Example.com ', email: 'operator@example.com' })
    expect(getAccountStatus(user)).toBe('pending')
  })

  it('classifies a synthetic invited row (empty externalIdentityId) as pending', () => {
    // Mirrors the shape an optimistic invite row would take from InviteUserResultDto {userId,email,role}.
    const user = createUser({
      id: 42,
      externalIdentityId: '',
      displayName: 'new.admin@example.com',
      email: 'new.admin@example.com',
      role: 'Administrator',
      isActive: true,
    })
    expect(getAccountStatus(user)).toBe('pending')
  })

  it('exposes a label and tone for every status', () => {
    for (const status of ['pending', 'active', 'deactivated'] as const) {
      expect(accountStatusLabel[status]).toBeTruthy()
      expect(accountStatusTone[status]).toBeTruthy()
    }
  })
})
