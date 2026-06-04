import { cookies } from 'next/headers'
import { NextResponse } from 'next/server'
import { KeycloakAuthError } from '@/app/lib/keycloak'
import { getValidAccessToken } from '@/app/lib/keycloak-tokens'
import { decrypt } from '@/app/lib/session'

export const dynamic = 'force-dynamic'

const NO_STORE_HEADERS = {
  'Cache-Control': 'no-store',
} as const

function unauthorized(reason: string): NextResponse {
  return NextResponse.json(
    { error: 'unauthorized', reason },
    {
      status: 401,
      headers: NO_STORE_HEADERS,
    },
  )
}

export async function GET(): Promise<NextResponse> {
  const cookieStore = await cookies()
  const sessionCookie = cookieStore.get('session')?.value

  if (!sessionCookie) {
    return unauthorized('missing_session')
  }

  const session = await decrypt(sessionCookie)
  if (!session) {
    return unauthorized('invalid_session')
  }

  if (!session.isActive) {
    return unauthorized('inactive_user')
  }

  try {
    const accessToken = await getValidAccessToken()
    return NextResponse.json(
      { accessToken },
      {
        status: 200,
        headers: NO_STORE_HEADERS,
      },
    )
  } catch (error) {
    if (error instanceof KeycloakAuthError) {
      const reason =
        error.message === 'Missing kc_session'
          ? 'missing_kc_session'
          : 'keycloak_auth_failed'

      return unauthorized(reason)
    }

    throw error
  }
}
