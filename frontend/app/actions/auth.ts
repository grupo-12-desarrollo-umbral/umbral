'use server'

import { redirect } from 'next/navigation'
import { deleteSession } from '@/app/lib/session'

// The logout endpoint is a FRONT-CHANNEL redirect: the browser follows it, so it
// must use the public URL (NEXT_PUBLIC_KEYCLOAK_URL, e.g. http://localhost:8080),
// not the back-channel service name (KEYCLOAK_URL, e.g. http://keycloak:8080),
// which is only resolvable inside the Docker network. Falls back to KEYCLOAK_URL
// for local `next dev` where both match.
const PUBLIC_KEYCLOAK_URL = process.env.NEXT_PUBLIC_KEYCLOAK_URL ?? process.env.KEYCLOAK_URL
const KEYCLOAK_REALM = process.env.KEYCLOAK_REALM

export async function logout() {
  await deleteSession()

  const postLogoutUri = encodeURIComponent(`${process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3000'}/login`)
  const clientId = process.env.KEYCLOAK_CLIENT_ID
  const keycloakLogoutUrl =
    PUBLIC_KEYCLOAK_URL && KEYCLOAK_REALM && clientId
      ? `${PUBLIC_KEYCLOAK_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/logout?client_id=${encodeURIComponent(clientId)}&post_logout_redirect_uri=${postLogoutUri}`
      : null

  if (keycloakLogoutUrl) {
    redirect(keycloakLogoutUrl)
  }

  redirect('/login')
}
