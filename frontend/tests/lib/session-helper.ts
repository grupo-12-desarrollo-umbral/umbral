import { SignJWT } from 'jose'
import type { SessionPayload } from '@/app/lib/definitions'

const secretKey = process.env.SESSION_SECRET ?? 'test-secret-for-playwright-fixtures-only'
const encodedKey = new TextEncoder().encode(secretKey)

export async function encryptForTest(payload: SessionPayload): Promise<string> {
  return new SignJWT({
    externalIdentityId: payload.externalIdentityId,
    displayName: payload.displayName,
    email: payload.email,
    role: payload.role,
    isActive: payload.isActive,
    expiresAt: payload.expiresAt.toISOString(),
  })
    .setProtectedHeader({ alg: 'HS256' })
    .setIssuedAt()
    .setExpirationTime('7d')
    .sign(encodedKey)
}
