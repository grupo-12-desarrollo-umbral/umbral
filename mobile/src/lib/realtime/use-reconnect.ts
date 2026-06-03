import { useEffect, useState } from 'react';
import { useAuth } from '@/lib/auth/use-auth';
import {
  clearReconnectContext,
  saveReconnectContext,
} from './reconnect-context';
import {
  interpretHubError,
  toReconnectedOutcome,
  toUpdatedReconnectContext,
  type ReconnectOutcome,
} from './reconnect-policy';
import {
  createSessionsHubConnection,
  type SessionsHubClient,
} from './sessions-hub';
import type { ReconnectContext } from './sessions-hub-types';

export type ReconnectStatus =
  | 'idle'
  | 'connecting'
  | 'reconnecting'
  | 'reconnected'
  | 'denied'
  | 'error';

function statusForOutcome(outcome: ReconnectOutcome): ReconnectStatus {
  switch (outcome.kind) {
    case 'reconnected':
      return 'reconnected';
    case 'forbidden-late-join':
    case 'invalid-session-state':
    case 'lost-access':
    case 'unauthorized':
      return 'denied';
    case 'network-error':
    case 'error':
      return 'error';
  }
}

export function useReconnect() {
  const { signOut } = useAuth();
  const [status, setStatus] = useState<ReconnectStatus>('idle');
  const [outcome, setOutcome] = useState<ReconnectOutcome | null>(null);
  const [isHubReconnecting, setIsHubReconnecting] = useState(false);
  // Build the connection exactly once per hook instance. A lazy state
  // initializer (not a ref read during render) keeps this off the render path.
  const [client] = useState<SessionsHubClient>(createSessionsHubConnection);

  // Surface transient WS drops handled by `withAutomaticReconnect()` so the UI
  // can show a "Reconnecting…" banner instead of treating the drop as a denial.
  useEffect(() => {
    const connection = client.connection as {
      onreconnecting?: (callback: (error?: Error) => void) => void;
      onreconnected?: (callback: (connectionId?: string) => void) => void;
    };
    if (typeof connection.onreconnecting === 'function') {
      connection.onreconnecting(() => setIsHubReconnecting(true));
    }
    if (typeof connection.onreconnected === 'function') {
      connection.onreconnected(() => setIsHubReconnecting(false));
    }
  }, [client]);

  useEffect(() => {
    return () => {
      void client.stop();
    };
  }, [client]);

  async function reconnect(context: ReconnectContext): Promise<ReconnectOutcome> {
    setStatus('connecting');
    setOutcome(null);

    try {
      await client.start();
      setStatus('reconnecting');

      const result = await client.reconnect(context.liveSessionId, {
        teamId: context.teamId,
        displayName: context.displayName,
        teamCapacity: context.teamCapacity,
        token: context.token ?? null,
      });

      const nextOutcome = toReconnectedOutcome(result);
      await saveReconnectContext(toUpdatedReconnectContext(context, result));
      setOutcome(nextOutcome);
      setStatus('reconnected');
      return nextOutcome;
    } catch (error) {
      const nextOutcome = interpretHubError(error);

      if (nextOutcome.kind === 'unauthorized') {
        await clearReconnectContext();
        await signOut();
      }

      setOutcome(nextOutcome);
      setStatus(statusForOutcome(nextOutcome));
      return nextOutcome;
    }
  }

  async function stop(): Promise<void> {
    await client.stop();
  }

  function reset(): void {
    setStatus('idle');
    setOutcome(null);
  }

  return { status, outcome, reconnect, reset, stop, isHubReconnecting };
}
