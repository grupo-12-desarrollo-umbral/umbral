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

  // The route is incomplete (a complete one returned above), so we fall back to the persisted
  // session. Guard against a *partial* route that names a different session/team than the stored
  // one: honouring persisted then would silently reconnect the wrong session. Any session/team
  // field the route does supply must match persisted; empty fields (a bare resume) place no
  // constraint. (A previous all-fields check here was dead code — a fully-populated route can
  // never reach this branch — so a conflicting partial route slipped straight through.)
  if (seed.liveSessionId.length > 0 && seed.liveSessionId !== persisted.liveSessionId) {
    return null;
  }
  if (seed.teamId.length > 0 && seed.teamId !== persisted.teamId) {
    return null;
  }

  return persisted;
}
