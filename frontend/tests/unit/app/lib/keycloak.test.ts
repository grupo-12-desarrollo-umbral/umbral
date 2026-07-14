import { describe, expect, it } from 'vitest'
import {
  buildResetCredentialsUrl,
  isExpiredOrNearExpiry,
  toExpiresAtMs,
} from '@/app/lib/keycloak'

describe('keycloak expiry helpers', () => {
  it('converts expires_in seconds into an absolute timestamp', () => {
    expect(toExpiresAtMs(90, 1_000)).toBe(91_000)
  })

  it('treats tokens inside the skew window as near expiry', () => {
    expect(isExpiredOrNearExpiry(61_000, 2_000, 60_000)).toBe(true)
  })

  it('keeps tokens outside the skew window as fresh', () => {
    expect(isExpiredOrNearExpiry(70_000, 2_000, 60_000)).toBe(false)
  })

  it('builds the reset-credentials URL bound to the app client', () => {
    const url = new URL(buildResetCredentialsUrl('http://localhost:8080', 'umbral'))
    expect(url.origin + url.pathname).toBe(
      'http://localhost:8080/realms/umbral/login-actions/reset-credentials',
    )
    // client_id keeps Keycloak off the built-in account console after reset.
    expect(url.searchParams.get('client_id')).toBe('umbral-web')
    expect(url.searchParams.get('redirect_uri')).toBe('http://localhost:3000/login')
  })
})
