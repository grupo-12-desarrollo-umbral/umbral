'use server'

import { redirect } from 'next/navigation'
import { deleteSession } from '@/app/lib/session'

const KEYCLOAK_URL = process.env.KEYCLOAK_URL
const KEYCLOAK_REALM = process.env.KEYCLOAK_REALM

export async function logout() {
  await deleteSession()

  const postLogoutUri = encodeURIComponent(`${process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3000'}/login`)
  const clientId = process.env.KEYCLOAK_CLIENT_ID
  const keycloakLogoutUrl =
    KEYCLOAK_URL && KEYCLOAK_REALM && clientId
      ? `${KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/logout?client_id=${encodeURIComponent(clientId)}&post_logout_redirect_uri=${postLogoutUri}`
      : null

  if (keycloakLogoutUrl) {
    redirect(keycloakLogoutUrl)
  }

  redirect('/login')
}
