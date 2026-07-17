import { NextRequest, NextResponse } from 'next/server'
import { decrypt } from '@/app/lib/session'
import {
  KC_SESSION_COOKIE,
  kcSessionCookieOptions,
  refreshSealedKeycloakSession,
  type RefreshedKeycloakSession,
} from '@/app/lib/keycloak-tokens'

// Signed-in users get bounced off these to the dashboard.
const AUTH_ROUTES = ['/login']
// Reachable without a session. Matched exactly, so '/' does not open up every route.
const PUBLIC_ROUTES = ['/']
const AUTH_CALLBACK = '/api/auth/callback'

function shedSession(response: NextResponse): NextResponse {
  response.cookies.delete('session')
  response.cookies.delete(KC_SESSION_COOKIE)
  return response
}

function applyRefreshed(response: NextResponse, refreshed: RefreshedKeycloakSession): NextResponse {
  response.cookies.set(
    KC_SESSION_COOKIE,
    refreshed.sealed,
    kcSessionCookieOptions(refreshed.refreshExpiresAt),
  )
  return response
}

export default async function proxy(req: NextRequest) {
  const path = req.nextUrl.pathname

  // Let the callback route through unconditionally
  if (path.startsWith(AUTH_CALLBACK)) return NextResponse.next()

  const isAuthRoute = AUTH_ROUTES.some((r) => path.startsWith(r))
  const isPublic = isAuthRoute || PUBLIC_ROUTES.includes(path)

  const cookie = req.cookies.get('session')?.value
  const session = await decrypt(cookie)
  const sealedTokens = req.cookies.get(KC_SESSION_COOKIE)?.value

  if (!session && !isPublic) {
    return NextResponse.redirect(new URL('/login', req.nextUrl))
  }

  // Ahead of the kc_session checks: deactivation is the more specific reason and holds regardless of
  // Keycloak, so a disabled account is told it is disabled rather than that auth merely failed.
  if (session && !session.isActive && !isPublic) {
    return NextResponse.redirect(new URL('/login?error=deactivated', req.nextUrl))
  }

  // Drop the orphaned `session` on the way out, otherwise it keeps re-triggering this same bounce.
  if (session && !sealedTokens && !isPublic) {
    return shedSession(NextResponse.redirect(new URL('/login?error=unauthorized', req.nextUrl)))
  }

  // Refresh here rather than inside the render. `cookies()` is read-only in a Server Component, so a
  // refresh driven from a page would throw ReadonlyRequestCookiesError instead of persisting, and
  // every request past the access token's 5-minute lifespan would 500. Refreshing here also makes
  // kc_session's *validity* — not merely its presence — the thing the guards below test: a page that
  // redirects to /login because the token is dead would otherwise be bounced straight back by the
  // signed-in check, looping until the cookies are cleared by hand.
  let refreshed: RefreshedKeycloakSession | null = null

  if (session && sealedTokens) {
    try {
      refreshed = await refreshSealedKeycloakSession(sealedTokens)
    } catch {
      // Beyond saving: no access token can be minted, so every gateway call would throw.
      if (!isPublic) {
        return shedSession(NextResponse.redirect(new URL('/login?error=unauthorized', req.nextUrl)))
      }
      // A public route is still reachable without a session — shed the dead cookies and carry on
      // rather than bouncing, so /login renders instead of redirecting to itself.
      return shedSession(NextResponse.next())
    }
  }

  // Gated on a live Keycloak session too: without one /dashboard would only bounce straight back.
  if (session && session.isActive && isAuthRoute && sealedTokens) {
    const response = NextResponse.redirect(new URL('/dashboard', req.nextUrl))
    return refreshed ? applyRefreshed(response, refreshed) : response
  }

  if (!refreshed) return NextResponse.next()

  // Hand the fresh cookie to the render on *this* request as well: `NextResponse.next({ request })`
  // forwards the mutated Cookie header, so the page reads the refreshed token instead of the stale
  // one it would otherwise pull from the original request and try (and fail) to refresh itself.
  req.cookies.set(KC_SESSION_COOKIE, refreshed.sealed)

  return applyRefreshed(
    NextResponse.next({ request: { headers: req.headers } }),
    refreshed,
  )
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico|.*\\.png$).*)'],
}
