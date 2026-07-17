import { fetch } from 'expo/fetch';
import { keycloakBaseUrl } from '@/lib/host';

export type KeycloakTokens = {
  accessToken: string;
  refreshToken: string;
  idToken: string;
};

// The refresh grant returns no id_token, so refreshed tokens are deliberately narrower than
// `KeycloakTokens`. Keycloak rotates the refresh token on every grant: the new one must be
// persisted, or the next refresh replays a consumed token and is rejected as `invalid_grant`.
export type RefreshedTokens = {
  accessToken: string;
  refreshToken: string;
};

export type AuthCredentials = {
  displayName: string;
  email: string;
};

export class KeycloakError extends Error {
  constructor(
    // 'session-expired' is reserved for a refresh token Keycloak has rejected outright — the only
    // reason that may end a session. It must never be conflated with 'network'/'unknown', which are
    // transient and leave the stored session usable.
    public readonly reason:
      | 'wrong-credentials'
      | 'session-expired'
      | 'network'
      | 'unknown',
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

// Forgot-password does NOT use a Keycloak hosted page (ADR-0016 §1). It is a custom native form
// (app/(auth)/forgot-password.tsx) that posts to POST /api/users/forgot-password, which delegates to
// Keycloak's Admin API to email an UPDATE_PASSWORD action link. There is deliberately no
// buildResetCredentialsUrl here.

// Participant self-registration does NOT use a Keycloak hosted page (ADR-0016 §1). It is a custom
// native form (app/(auth)/register.tsx) that posts to POST /api/users/register — mirroring how login
// delegates only the credential exchange. There is deliberately no buildRegistrationUrl here.

export function parseJwt(token: string): Record<string, unknown> {
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
    (claims.name as string | undefined) ??
    (claims.given_name as string | undefined) ??
    (claims.preferred_username as string | undefined) ??
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

// `umbral-mobile` is a public client (no secret), so the refresh grant carries only client_id +
// refresh_token. Rejects with reason 'session-expired' only when Keycloak itself declares the token
// dead; every other failure stays transient so callers never sign a participant out over a blip.
export async function refreshTokens(refreshToken: string): Promise<RefreshedTokens> {
  const clientId = process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID;
  const body = new URLSearchParams({
    grant_type: 'refresh_token',
    client_id: clientId,
    refresh_token: refreshToken,
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
    // Only `invalid_grant` proves the refresh token is expired, revoked or already rotated. A 401
    // here means client authentication failed, not that the session ended — keep it transient.
    if (errorCode === 'invalid_grant') {
      throw new KeycloakError('session-expired', 'Session expired');
    }
    throw new KeycloakError('unknown', `Keycloak error: ${response.status}`);
  }

  const data = (await response.json()) as {
    access_token: string;
    refresh_token: string;
  };
  return {
    accessToken: data.access_token,
    refreshToken: data.refresh_token,
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
