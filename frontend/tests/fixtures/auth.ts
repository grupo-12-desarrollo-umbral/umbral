import { test as base, Page } from '@playwright/test'
import { encryptForTest } from '@/tests/lib/session-helper'
import type { SessionPayload } from '@/app/lib/definitions'

export * from '@playwright/test'

export const test = base.extend<{
  operatorPage: Page
  adminPage: Page
  deactivatedPage: Page
}>({
  operatorPage: async ({ browser }, runPageFixture) => {
    const ctx = await browser.newContext()
    const payload: SessionPayload = {
      externalIdentityId: 'op-1',
      displayName: 'Operator One',
      email: 'op@umbral.local',
      role: 'Operator',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    await ctx.addCookies([{ name: 'session', value: session, url: 'http://localhost:3000' }])
    const page = await ctx.newPage()
    await runPageFixture(page)
    await ctx.close()
  },

  adminPage: async ({ browser }, runPageFixture) => {
    const ctx = await browser.newContext()
    const payload: SessionPayload = {
      externalIdentityId: 'admin-1',
      displayName: 'Administrator One',
      email: 'admin@umbral.local',
      role: 'Administrator',
      isActive: true,
      expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    }
    const session = await encryptForTest(payload)
    await ctx.addCookies([{ name: 'session', value: session, url: 'http://localhost:3000' }])
    const page = await ctx.newPage()
    await runPageFixture(page)
    await ctx.close()
  },

  deactivatedPage: async ({ browser }, runPageFixture) => {
    const ctx = await browser.newContext()
    const payload: SessionPayload = {
      externalIdentityId: 'deactivated-1',
      displayName: 'Deactivated User',
      email: 'deactivated@umbral.local',
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
})
