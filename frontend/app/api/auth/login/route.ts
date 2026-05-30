import { NextResponse } from 'next/server'
import { buildAuthorizationUrl, generateCodeChallenge, generateCodeVerifier } from '@/app/lib/keycloak'

export async function GET() {
  try {
    const state = crypto.randomUUID()
    const codeVerifier = generateCodeVerifier()
    const codeChallenge = generateCodeChallenge(codeVerifier)
    const authUrl = buildAuthorizationUrl(state, codeChallenge)

    const response = NextResponse.redirect(authUrl)
    response.cookies.set('auth_state', state, {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      maxAge: 60 * 10, // 10 minutes
      sameSite: 'lax',
      path: '/',
    })
    response.cookies.set('auth_verifier', codeVerifier, {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      maxAge: 60 * 10, // 10 minutes
      sameSite: 'lax',
      path: '/',
    })

    return response
  } catch (error) {
    console.error('[auth/login] Failed to build Keycloak redirect:', error)
    return new Response(
      `Auth setup failed. Check server logs and ensure .env.local is configured. Error: ${error instanceof Error ? error.message : String(error)}`,
      { status: 500 }
    )
  }
}
