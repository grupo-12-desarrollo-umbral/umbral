import 'server-only'
import { IdentityError, type AuthenticateUserResultDto } from './definitions'

const API_GATEWAY_URL = process.env.API_GATEWAY_URL!

export async function bootstrapUser(
  accessToken: string,
  displayName: string
): Promise<AuthenticateUserResultDto> {
  const response = await fetch(`${API_GATEWAY_URL}/api/users/authenticated`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ displayName }),
  })

  if (response.status === 401) {
    throw new IdentityError('unauthorized', 'Authentication failed. Invalid or missing token.')
  }

  if (response.status === 403) {
    throw new IdentityError('deactivated', 'Your account has been deactivated.')
  }

  if (!response.ok) {
    throw new IdentityError('unknown', `Bootstrap failed with status ${response.status}`)
  }

  return response.json()
}
