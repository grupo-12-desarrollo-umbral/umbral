import { fetch } from 'expo/fetch';
import { keycloakBaseUrl } from '@/lib/host';

export type KeycloakTokens = {
  accessToken: string;
  refreshToken: string;
  idToken: string;
};

export type AuthCredentials = {
  displayName: string;
  email: string;
};

export class KeycloakError extends Error {
  constructor(
    public readonly reason: 'wrong-credentials' | 'network' | 'unknown',
    message: string,
  ) {
    super(message);
    this.name = 'KeycloakError';
  }
}

function tokenUrl(): string {
  const realm = process.env.EXPO_PUBLIC_KEYCLOAK_REALM;
  return `${keycloakBaseUrl()}/realms/${realm}/protocol/openid-connect/token`;
}

function logoutUrl(): string {
  const realm = process.env.EXPO_PUBLIC_KEYCLOAK_REALM;
  return `${keycloakBaseUrl()}/realms/${realm}/protocol/openid-connect/logout`;
}

// Bind the hosted reset-credentials flow to the app client + a redirect back to
// the app. Without client_id Keycloak defaults to the built-in `account` client
// and, after a successful reset, sends the user to the account console
// (`/realms/<realm>/account/`) — which this realm never configures, so it errors
// with "unexpected error". Mirrors buildRegistrationUrl's client_id + redirect_uri.
export function buildResetCredentialsUrl(
  baseUrl: string = keycloakBaseUrl(),
  realm: string = process.env.EXPO_PUBLIC_KEYCLOAK_REALM ?? '',
  clientId: string = process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID ?? 'umbral-mobile',
): string {
  if (!baseUrl || !realm) {
    throw new Error('Missing Keycloak URL or realm for reset-credentials flow');
  }

  const params = new URLSearchParams({
    client_id: clientId,
    redirect_uri: 'http://localhost/',
  });

  return `${baseUrl}/realms/${realm}/login-actions/reset-credentials?${params.toString()}`;
}

// Opens Keycloak's hosted self-registration form (realm registrationAllowed: true).
// The realm assigns the Participant default role; after email verification the user
// returns to the app and signs in with the native form, which provisions them via
// POST /api/users/authenticated. redirect_uri must match a umbral-mobile client
// redirect URI (http://localhost/*).
export function buildRegistrationUrl(
  baseUrl: string = keycloakBaseUrl(),
  realm: string = process.env.EXPO_PUBLIC_KEYCLOAK_REALM ?? '',
  clientId: string = process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID ?? '',
): string {
  if (!baseUrl || !realm || !clientId) {
    throw new Error('Missing Keycloak URL, realm, or client for registration flow');
  }

  const params = new URLSearchParams({
    client_id: clientId,
    response_type: 'code',
    scope: 'openid email',
    redirect_uri: 'http://localhost/',
  });

  return `${baseUrl}/realms/${realm}/protocol/openid-connect/registrations?${params.toString()}`;
}

function parseJwt(token: string): Record<string, unknown> {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join(''),
    );
    return JSON.parse(json);
  } catch {
    return {};
  }
}

export function deriveCredentials(idToken: string): AuthCredentials {
  const claims = parseJwt(idToken);
  const displayName =
    (claims.preferred_username as string | undefined) ??
    (claims.name as string | undefined) ??
    (claims.given_name as string | undefined) ??
    (claims.email as string | undefined) ??
    'User';
  return {
    displayName,
    email: (claims.email as string | undefined) ?? '',
  };
}

export async function signInWithPassword(
  email: string,
  password: string,
): Promise<KeycloakTokens> {
  const clientId = process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID;
  const body = new URLSearchParams({
    grant_type: 'password',
    client_id: clientId,
    username: email,
    password,
    scope: 'openid profile email',
  });

  let response: Response;
  try {
    response = await fetch(tokenUrl(), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    });
  } catch {
    throw new KeycloakError('network', 'Network request failed');
  }

  if (!response.ok) {
    let errorCode = 'unknown';
    try {
      const data = await response.json();
      errorCode = (data as { error?: string }).error ?? 'unknown';
    } catch {
      // ignore
    }
    if (errorCode === 'invalid_grant' || response.status === 401) {
      throw new KeycloakError('wrong-credentials', 'Wrong email or password');
    }
    throw new KeycloakError('unknown', `Keycloak error: ${response.status}`);
  }

  const data = (await response.json()) as {
    access_token: string;
    refresh_token: string;
    id_token: string;
  };
  return {
    accessToken: data.access_token,
    refreshToken: data.refresh_token,
    idToken: data.id_token,
  };
}

export async function signOut(refreshToken: string): Promise<void> {
  const clientId = process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID;
  const body = new URLSearchParams({
    client_id: clientId,
    refresh_token: refreshToken,
  });
  try {
    await fetch(logoutUrl(), {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: body.toString(),
    });
  } catch {
    // best-effort logout
  }
}
