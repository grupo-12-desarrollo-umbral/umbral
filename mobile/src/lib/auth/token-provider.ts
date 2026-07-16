import { KeycloakError, parseJwt, refreshTokens } from './keycloak';
import { clearTokens, getAccessToken, getRefreshToken, storeTokens } from './token-store';

// Renew this long before `exp` so an access token cannot die between being attached and reaching the
// gateway. Mirrors the frontend's ACCESS_TOKEN_REFRESH_SKEW_MS.
const ACCESS_TOKEN_REFRESH_SKEW_MS = 60_000;

// Keycloak rotates the refresh token on every grant, so two concurrent refreshes would consume each
// other's token and leave the loser holding a dead one. Callers race constantly — API requests plus
// both hub handshakes all wake at once after a reconnect — so every refresh funnels through this one
// promise.
let inFlightRefresh: Promise<string> | null = null;

type SessionExpiredListener = () => void;

const sessionExpiredListeners = new Set<SessionExpiredListener>();

// Fires only when the refresh token itself is dead and the store has been cleared, letting auth state
// drop to signed-out. Transient failures never reach here.
export function onSessionExpired(listener: SessionExpiredListener): () => void {
  sessionExpiredListeners.add(listener);
  return () => {
    sessionExpiredListeners.delete(listener);
  };
}

function isExpiredOrNearExpiry(token: string, skewMs: number, nowMs: number): boolean {
  const { exp } = parseJwt(token);
  // An unreadable `exp` is treated as expired: one wasted refresh beats a request that is certain
  // to 401.
  if (typeof exp !== 'number') return true;
  return exp * 1000 - skewMs <= nowMs;
}

async function refreshOnce(): Promise<string> {
  const refreshToken = await getRefreshToken();
  // Nothing stored means there is no session to expire — throw before the listeners so a signed-out
  // cold start is not reported as an expiry.
  if (!refreshToken) {
    throw new KeycloakError('session-expired', 'No stored refresh token');
  }

  try {
    const tokens = await refreshTokens(refreshToken);
    await storeTokens(tokens.accessToken, tokens.refreshToken);
    return tokens.accessToken;
  } catch (err) {
    if (err instanceof KeycloakError && err.reason === 'session-expired') {
      await clearTokens();
      sessionExpiredListeners.forEach((listener) => listener());
    }
    throw err;
  }
}

function sharedRefresh(): Promise<string> {
  inFlightRefresh ??= refreshOnce().finally(() => {
    inFlightRefresh = null;
  });
  return inFlightRefresh;
}

// Forces a refresh regardless of the current token's age — for a caller the server has already
// answered with 401. Resolves null when the token could not be renewed.
export async function refreshAccessToken(): Promise<string | null> {
  try {
    return await sharedRefresh();
  } catch {
    return null;
  }
}

// The single entry point for anything needing a bearer token: renews proactively inside the skew
// window instead of waiting for a 401.
export async function getValidAccessToken(): Promise<string | null> {
  const token = await getAccessToken();
  const nowMs = Date.now();
  if (token && !isExpiredOrNearExpiry(token, ACCESS_TOKEN_REFRESH_SKEW_MS, nowMs)) {
    return token;
  }

  try {
    return await sharedRefresh();
  } catch (err) {
    // A transient failure inside the skew window leaves the current token stale but not yet dead —
    // prefer it over nothing so a brief outage does not interrupt a live game. A dead session has
    // already cleared the store and must not fall back.
    const sessionEnded = err instanceof KeycloakError && err.reason === 'session-expired';
    if (!sessionEnded && token && !isExpiredOrNearExpiry(token, 0, nowMs)) {
      return token;
    }
    return null;
  }
}
