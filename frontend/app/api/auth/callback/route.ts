import { NextRequest, NextResponse } from 'next/server'
import { exchangeCode, toExpiresAtMs } from '@/app/lib/keycloak'
import { bootstrapUser } from '@/app/lib/identity'
import { storeKeycloakTokens } from '@/app/lib/keycloak-tokens'
import { createSession, deleteSession } from '@/app/lib/session'

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url)
  const code = searchParams.get('code')
  const state = searchParams.get('state')
  const error = searchParams.get('error')

  if (error) {
    console.error('[auth/callback] Keycloak returned error:', error)
    const response = NextResponse.redirect(new URL('/login?error=unauthorized', request.url))
    response.cookies.delete('auth_state')
    response.cookies.delete('auth_verifier')
    return response
  }

  if (!code || !state) {
    return new Response('Missing code or state', { status: 400 })
  }

  // Validate state against short-lived cookie (CSRF protection)
  const stateCookie = request.cookies.get('auth_state')?.value
  if (!stateCookie || stateCookie !== state) {
    console.error('[auth/callback] State mismatch. Cookie:', stateCookie, 'Query:', state)
    return new Response('Invalid state parameter', { status: 400 })
  }

  // Retrieve PKCE verifier
  const codeVerifier = request.cookies.get('auth_verifier')?.value
  if (!codeVerifier) {
    console.error('[auth/callback] Missing auth_verifier cookie')
    return new Response('Missing PKCE verifier', { status: 400 })
  }

  try {
    console.log('[auth/callback] Exchanging code for tokens...')
    const { accessToken, refreshToken, expiresIn, refreshExpiresIn, displayName } = await exchangeCode(code, codeVerifier)
    console.log('[auth/callback] Tokens received. Bootstrapping user...')

    const result = await bootstrapUser(accessToken, displayName)
    console.log('[auth/callback] Bootstrap result:', JSON.stringify(result))

    if (!result.access.isAllowed) {
      await deleteSession()
      const response = NextResponse.redirect(new URL('/login?error=deactivated', request.url))
      response.cookies.delete('auth_state')
      response.cookies.delete('auth_verifier')
      return response
    }

    // Both cookies are cut from the same instant so `session` expires with the refresh token that
    // backs it, never after: past that point no access token can be minted and the app can only
    // fail. `toExpiresAtMs` is the same mapping `storeKeycloakTokens` applies to `kc_session`.
    const issuedAtMs = Date.now()
    await createSession(
      {
        externalIdentityId: result.actor.externalIdentityId,
        displayName: result.actor.displayName,
        email: result.actor.email,
        role: result.actor.role as 'Administrator' | 'Operator',
        isActive: result.actor.isActive,
      },
      new Date(toExpiresAtMs(refreshExpiresIn, issuedAtMs)),
    )
    await storeKeycloakTokens(
      {
        accessToken,
        refreshToken,
        expiresIn,
        refreshExpiresIn,
      },
      issuedAtMs,
    )

    const response = NextResponse.redirect(new URL('/dashboard', request.url))
    response.cookies.delete('auth_state')
    response.cookies.delete('auth_verifier')
    return response
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err)
    console.error('[auth/callback] Error during callback:', message)
    console.error('[auth/callback] Full error:', err)

    await deleteSession()
    const redirectUrl = message.includes('deactivated')
      ? new URL('/login?error=deactivated', request.url)
      : new URL('/login?error=unauthorized', request.url)
    const response = NextResponse.redirect(redirectUrl)
    response.cookies.delete('auth_state')
    response.cookies.delete('auth_verifier')
    return response
  }
}
