import { test as base, Page } from '@playwright/test'
import { createKeycloakSession } from '@/tests/lib/keycloak-session-helper'
import { encryptForTest } from '@/tests/lib/session-helper'
import type { SessionPayload } from '@/app/lib/definitions'

export * from '@playwright/test'

export const test = base.extend<{
  operatorPage: Page
  adminPage: Page
  deactivatedPage: Page
  participantPage: Page
}>({
  operatorPage: async ({ browser }, runPageFixture) => {
    const ctx = await browser.newContext()
    // externalIdentityId = the Keycloak sub (UUID), not 'op-1': operator session-listing goes
    // gateway→JWT and identity-access keys the actor by sub. global-setup seeds op-1's row with
    // the same sub, so both the BFF-direct (X-User-Id) and gateway paths resolve to one row.
    const { cookie: keycloakSession, sub } = await createKeycloakSession('op-1', 'operator123')
    const payload: SessionPayload = {
      externalIdentityId: sub,
      displayName: 'Operator One',
      email: 'op-1@umbral.local',
      role: 'Operator',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    await ctx.addCookies([
      { name: 'session', value: session, url: 'http://localhost:3000' },
      { name: 'kc_session', value: keycloakSession, url: 'http://localhost:3000' },
    ])
    const page = await ctx.newPage()
    await runPageFixture(page)
    await ctx.close()
  },

  adminPage: async ({ browser }, runPageFixture) => {
    const ctx = await browser.newContext()
    // externalIdentityId = the Keycloak sub (UUID), not 'admin-1': Keycloak account provisioning
    // reconciles the identity-access row to the resolved sub the first time admin authenticates via
    // the gateway, dropping the literal-'admin-1' row global-setup seeded. global-setup's
    // seedAdminIdentity re-inserts the row keyed by the same sub, so the dashboard's BFF-direct access
    // check (X-User-Id) resolves it instead of 404ing and redirect-looping — same fix as op-1.
    const { cookie: keycloakSession, sub } = await createKeycloakSession('admin-1', 'admin123')
    const payload: SessionPayload = {
      externalIdentityId: sub,
      displayName: 'Administrator One',
      email: 'admin-1@umbral.local',
      role: 'Administrator',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    await ctx.addCookies([
      { name: 'session', value: session, url: 'http://localhost:3000' },
      { name: 'kc_session', value: keycloakSession, url: 'http://localhost:3000' },
    ])
    const page = await ctx.newPage()
    await runPageFixture(page)
    await ctx.close()
  },

  deactivatedPage: async ({ browser }, runPageFixture) => {
    const ctx = await browser.newContext()
    const payload: SessionPayload = {
      externalIdentityId: 'deactivated-1',
      displayName: 'Deactivated User',
      email: 'deactivated-1@umbral.local',
      role: 'Operator',
      isActive: false,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    await ctx.addCookies([{ name: 'session', value: session, url: 'http://localhost:3000' }])
    const page = await ctx.newPage()
    await runPageFixture(page)
    await ctx.close()
  },

  participantPage: async ({ browser }, runPageFixture) => {
    const ctx = await browser.newContext()
    // externalIdentityId = the Keycloak sub (UUID), not 'participant-1': global-setup's
    // seedParticipantIdentity re-inserts the identity-access row keyed by the resolved sub
    // (for the HU-36A gateway→JWT membership path), so the literal username no longer matches.
    // The dashboard's BFF-direct access check (X-User-Id) 404s on the literal and redirect-loops.
    const { cookie: keycloakSession, sub } = await createKeycloakSession('participant-1', 'participant123')
    const payload: SessionPayload = {
      externalIdentityId: sub,
      displayName: 'Participant One',
      email: 'participant-1@umbral.local',
      role: 'Participant',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    await ctx.addCookies([
      { name: 'session', value: session, url: 'http://localhost:3000' },
      { name: 'kc_session', value: keycloakSession, url: 'http://localhost:3000' },
    ])
    const page = await ctx.newPage()
    await runPageFixture(page)
    await ctx.close()
  },
})
