import * as SecureStore from 'expo-secure-store';
import type { ReconnectContext } from './sessions-hub-types';

const RECONNECT_CONTEXT_KEY = 'umbral.reconnect_context';

function isReconnectContext(value: unknown): value is ReconnectContext {
  if (!value || typeof value !== 'object') return false;

  const candidate = value as Record<string, unknown>;
  const token = candidate.token;
  const lastSeenAt = candidate.lastSeenAt;

  return (
    typeof candidate.liveSessionId === 'string' &&
    candidate.liveSessionId.length > 0 &&
    typeof candidate.teamId === 'string' &&
    candidate.teamId.length > 0 &&
    typeof candidate.displayName === 'string' &&
    candidate.displayName.length > 0 &&
    (token === undefined || token === null || typeof token === 'string') &&
    (lastSeenAt === undefined || typeof lastSeenAt === 'string')
  );
}

export async function saveReconnectContext(
  context: ReconnectContext,
): Promise<void> {
  await SecureStore.setItemAsync(
    RECONNECT_CONTEXT_KEY,
    JSON.stringify(context),
  );
}

export async function loadReconnectContext(): Promise<ReconnectContext | null> {
  const raw = await SecureStore.getItemAsync(RECONNECT_CONTEXT_KEY);
  if (!raw) return null;

  try {
    const parsed: unknown = JSON.parse(raw);
    if (!isReconnectContext(parsed)) {
      await SecureStore.deleteItemAsync(RECONNECT_CONTEXT_KEY);
      return null;
    }
    return parsed;
  } catch {
    await SecureStore.deleteItemAsync(RECONNECT_CONTEXT_KEY);
    return null;
  }
}

export function clearReconnectContext(): Promise<void> {
  return SecureStore.deleteItemAsync(RECONNECT_CONTEXT_KEY);
}
