import React, { createContext, useContext, useEffect, useState } from 'react';
import {
  signInWithPassword,
  signOut as kcSignOut,
  deriveCredentials,
  KeycloakError,
} from './keycloak';
import { storeTokens, clearTokens, getRefreshToken } from './token-store';
import { getValidAccessToken, onSessionExpired } from './token-provider';
import {
  bootstrapAuthenticatedUser,
  getAuthenticatedProfile,
  type AuthenticatedActorProfileDto,
} from '@/lib/api/identity';
import { evaluateAccess, type RejectionReason } from './access-policy';
import { ApiError } from '@/lib/api/client';
import { clearReconnectContext } from '@/lib/realtime/reconnect-context';

export type AuthStatus =
  | 'idle'
  | 'authenticating'
  | 'authenticated'
  | 'rejected'
  | 'error';

type AuthState = {
  status: AuthStatus;
  profile: AuthenticatedActorProfileDto | null;
  rejectionReason: RejectionReason | null;
  errorMessage: string | null;
};

type AuthActions = {
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

export type AuthContextValue = AuthState & AuthActions;

const AuthContext = createContext<AuthContextValue | null>(null);

const INITIAL_STATE: AuthState = {
  // Start as 'authenticating' so routing shows a splash while the session
  // restore check runs on mount — transitions to 'idle' if no stored token.
  status: 'authenticating',
  profile: null,
  rejectionReason: null,
  errorMessage: null,
};

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>(INITIAL_STATE);

  useEffect(() => {
    restoreSession();
  }, []);

  // A refresh token Keycloak has rejected ends the session wherever it is noticed — a background API
  // call or a hub reconnect, not just startup. The provider has already cleared the store by now.
  useEffect(
    () =>
      onSessionExpired(() => {
        setState({ status: 'idle', profile: null, rejectionReason: null, errorMessage: null });
      }),
    [],
  );

  async function restoreSession(): Promise<void> {
    const token = await getValidAccessToken();
    if (!token) {
      setState((s) => ({ ...s, status: 'idle' }));
      return;
    }
    try {
      const profile = await getAuthenticatedProfile();
      const bootstrapResult = await bootstrapAuthenticatedUser(profile.displayName);
      const access = evaluateAccess(bootstrapResult);
      if (access.allowed) {
        setState({ status: 'authenticated', profile, rejectionReason: null, errorMessage: null });
      } else {
        await clearTokens();
        setState({ status: 'rejected', profile: null, rejectionReason: access.reason, errorMessage: null });
      }
    } catch (err) {
      // Only a rejected token ends the session: the client already refreshed and retried, so a 401
      // here means the credentials are genuinely gone. Any other failure — a flaky connection above
      // all — must keep the stored tokens so restoring succeeds once the network returns.
      if (err instanceof ApiError && err.status === 401) {
        await clearTokens();
        setState({ status: 'idle', profile: null, rejectionReason: null, errorMessage: null });
        return;
      }
      setState({
        status: 'error',
        profile: null,
        rejectionReason: null,
        errorMessage: 'Network error. Check your connection and try again.',
      });
    }
  }

  async function signIn(email: string, password: string): Promise<void> {
    setState((s) => ({ ...s, status: 'authenticating', errorMessage: null }));
    try {
      const tokens = await signInWithPassword(email, password);
      await storeTokens(tokens.accessToken, tokens.refreshToken);

      const { displayName } = deriveCredentials(tokens.idToken);
      const bootstrapResult = await bootstrapAuthenticatedUser(displayName);
      const access = evaluateAccess(bootstrapResult);

      if (!access.allowed) {
        await clearTokens();
        setState({ status: 'rejected', profile: null, rejectionReason: access.reason, errorMessage: null });
        return;
      }

      const profile = await getAuthenticatedProfile();
      setState({ status: 'authenticated', profile, rejectionReason: null, errorMessage: null });
    } catch (err) {
      await clearTokens();
      if (err instanceof KeycloakError) {
        const message =
          err.reason === 'network'
            ? 'Network error. Check your connection and try again.'
            : 'Wrong email or password.';
        setState({ status: 'error', profile: null, rejectionReason: null, errorMessage: message });
        return;
      }
      if (err instanceof ApiError && err.status === 0) {
        setState({
          status: 'error',
          profile: null,
          rejectionReason: null,
          errorMessage: 'Network error. Check your connection and try again.',
        });
        return;
      }
      setState({
        status: 'error',
        profile: null,
        rejectionReason: null,
        errorMessage: 'Something went wrong. Please try again.',
      });
    }
  }

  async function handleSignOut(): Promise<void> {
    const refreshToken = await getRefreshToken();
    if (refreshToken) {
      await kcSignOut(refreshToken);
    }
    await clearTokens();
    // Drop any persisted live context so a different user signing in on this
    // device never inherits a stale team/session to resume into.
    await clearReconnectContext();
    setState({ status: 'idle', profile: null, rejectionReason: null, errorMessage: null });
  }

  return (
    <AuthContext.Provider value={{ ...state, signIn, signOut: handleSignOut }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
