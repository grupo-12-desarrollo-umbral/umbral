import { test as base, Page } from '@playwright/test'
import { createKeycloakSessionCookie } from '@/tests/lib/keycloak-session-helper'
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
    const payload: SessionPayload = {
      externalIdentityId: 'op-1',
      displayName: 'Operator One',
      email: 'op-1@umbral.local',
      role: 'Operator',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    const keycloakSession = await createKeycloakSessionCookie('op-1', 'operator123')
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
    const payload: SessionPayload = {
      externalIdentityId: 'admin-1',
      displayName: 'Administrator One',
      email: 'admin-1@umbral.local',
      role: 'Administrator',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    const keycloakSession = await createKeycloakSessionCookie('admin-1', 'admin123')
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
    const payload: SessionPayload = {
      externalIdentityId: 'participant-1',
      displayName: 'Participant One',
      email: 'participant-1@umbral.local',
      role: 'Participant',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    await ctx.addCookies([{ name: 'session', value: session, url: 'http://localhost:3000' }])
    const page = await ctx.newPage()
    await runPageFixture(page)
    await ctx.close()
  },
})
