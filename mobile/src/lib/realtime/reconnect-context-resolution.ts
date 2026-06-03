import type { ReconnectContext } from './sessions-hub-types';

export type ReconnectRouteSeed = {
  liveSessionId: string;
  teamId: string;
  displayName: string;
};

export function buildReconnectContext(
  seed: ReconnectRouteSeed,
): ReconnectContext | null {
  if (
    !seed.liveSessionId ||
    !seed.teamId ||
    !seed.displayName
  ) {
    return null;
  }

  return {
    liveSessionId: seed.liveSessionId,
    teamId: seed.teamId,
    displayName: seed.displayName,
    token: null,
  };
}

export function resolveReconnectContext(
  seed: ReconnectRouteSeed,
  persisted: ReconnectContext | null,
): ReconnectContext | null {
  const fromRoute = buildReconnectContext(seed);
  if (fromRoute) return fromRoute;

  if (!persisted) return null;

  const hasRouteIdentity =
    seed.liveSessionId.length > 0 &&
    seed.teamId.length > 0 &&
    seed.displayName.length > 0;

  if (!hasRouteIdentity) return persisted;

  const matchesPersistedIdentity =
    persisted.liveSessionId === seed.liveSessionId &&
    persisted.teamId === seed.teamId &&
    persisted.displayName === seed.displayName;

  return matchesPersistedIdentity ? persisted : null;
}
