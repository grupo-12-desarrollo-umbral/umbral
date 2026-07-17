import 'server-only'
import { IdentityError } from './definitions'
import { KeycloakAuthError } from './keycloak'
import { getValidAccessToken } from './keycloak-tokens'

export const API_GATEWAY_URL = process.env.API_GATEWAY_URL!

// Every BFF-to-backend call authenticates with the session's Keycloak access token and lets the
// gateway mint the downstream X-User-* identity headers from the validated claims
// (ApiGateway.Transforms.TrustedHeadersTransform). Callers must not send those headers themselves:
// the gateway strips any inbound copy, so a hand-built one is dropped rather than honoured.
export async function getGatewayHeaders(headers?: HeadersInit): Promise<Headers> {
  try {
    const accessToken = await getValidAccessToken()
    const gatewayHeaders = new Headers(headers)
    gatewayHeaders.set('Authorization', `Bearer ${accessToken}`)
    return gatewayHeaders
  } catch (error) {
    if (
      error instanceof KeycloakAuthError ||
      (error instanceof Error && error.name === 'KeycloakAuthError')
    ) {
      throw new IdentityError('unauthorized', 'Authentication failed.')
    }

    throw error
  }
}
