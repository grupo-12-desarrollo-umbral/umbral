import { NextRequest, NextResponse } from 'next/server'
import { decrypt } from '@/app/lib/session'

const PUBLIC_ROUTES = ['/login']
const AUTH_CALLBACK = '/api/auth/callback'

export default async function proxy(req: NextRequest) {
  const path = req.nextUrl.pathname

  // Let the callback route through unconditionally
  if (path.startsWith(AUTH_CALLBACK)) return NextResponse.next()

  const isPublic = PUBLIC_ROUTES.some((r) => path.startsWith(r))

  const cookie = req.cookies.get('session')?.value
  const session = cookie ? await decrypt(cookie) : null

  if (!session && !isPublic) {
    return NextResponse.redirect(new URL('/login', req.nextUrl))
  }

  if (session && !session.isActive && !isPublic) {
    return NextResponse.redirect(new URL('/login?error=deactivated', req.nextUrl))
  }

  if (session && session.isActive && isPublic) {
    return NextResponse.redirect(new URL('/dashboard', req.nextUrl))
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico|.*\\.png$).*)'],
}
