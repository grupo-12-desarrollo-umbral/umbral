import { NextRequest, NextResponse } from 'next/server'
import { decrypt } from '@/app/lib/session'

// Signed-in users get bounced off these to the dashboard.
const AUTH_ROUTES = ['/login']
// Reachable without a session. Matched exactly, so '/' does not open up every route.
const PUBLIC_ROUTES = ['/']
const AUTH_CALLBACK = '/api/auth/callback'

export default async function proxy(req: NextRequest) {
  const path = req.nextUrl.pathname

  // Let the callback route through unconditionally
  if (path.startsWith(AUTH_CALLBACK)) return NextResponse.next()

  const isAuthRoute = AUTH_ROUTES.some((r) => path.startsWith(r))
  const isPublic = isAuthRoute || PUBLIC_ROUTES.includes(path)

  const cookie = req.cookies.get('session')?.value
  const session = cookie ? await decrypt(cookie) : null
  // `kc_session` expires with the Keycloak refresh token, and `getValidAccessToken` deletes it the
  // moment a refresh is rejected. So its absence beside a still-valid `session` means the Keycloak
  // session is gone for good — every gateway call would throw `unauthorized`. Presence is all we
  // check: the cookie is sealed and only the Node runtime holds the key to open it.
  const hasKeycloakSession = req.cookies.has('kc_session')

  if (!session && !isPublic) {
    return NextResponse.redirect(new URL('/login', req.nextUrl))
  }

  // Ahead of the kc_session check: deactivation is the more specific reason and holds regardless of
  // Keycloak, so a disabled account is told it is disabled rather than that auth merely failed.
  if (session && !session.isActive && !isPublic) {
    return NextResponse.redirect(new URL('/login?error=deactivated', req.nextUrl))
  }

  // Drop the orphaned `session` on the way out, otherwise it keeps re-triggering this same bounce.
  if (session && !hasKeycloakSession && !isPublic) {
    const response = NextResponse.redirect(new URL('/login?error=unauthorized', req.nextUrl))
    response.cookies.delete('session')
    return response
  }

  // Gated on `hasKeycloakSession` too: without it /dashboard would only bounce straight back here.
  if (session && session.isActive && isAuthRoute && hasKeycloakSession) {
    return NextResponse.redirect(new URL('/dashboard', req.nextUrl))
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico|.*\\.png$).*)'],
}
