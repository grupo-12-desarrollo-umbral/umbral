# Plan: operator team-association code routing + operator→mobile lobby sync

## Context

**Symptom:** On the operator dashboard, session SMOKE1 shows "No teams associated yet,"
but the mobile app (typing SMOKE1) shows "Soon Team" and "Soon2 Team."

**Root cause (diagnosed against the running Docker DB):** the two views read two
different databases, by design (per the bounded-context CONTEXT.md files):

- **Mobile lobby** → `identity-access-service` → `identity_access.session_team_associations`.
  `SessionTeamAssociation` is explicitly the Identity-owned index "used to scope
  participant lobby discovery." Mobile is reading correct data.
- **Operator "Associated teams"** → `session-operations-service` →
  `session_operations.live_session_teams`, filtered to rows with a non-null
  `reference_team_id` (`SessionTeamAssociationFacade.GetAssociatedTeamsAsync`,
  `services/session-operations-service/src/Application/Sessions/Facades/SessionTeamAssociationFacade.cs:62`).

Two concrete gaps:
1. **Seed bug:** `backend/scripts/seed-all.sh` inserts `live_session_teams` *without*
   `reference_team_id` (NULL), so the operator panel filters the seeded teams out.
   (`seed-dev-data.sh` was fixed to set it; `seed-all.sh` — the one actually run — was not.)
2. **No runtime sync:** when an operator associates a team via session-operations
   (`AssociateTeamToSession`), nothing tells identity-access to create the matching
   `SessionTeamAssociation`, so live associations never reach the mobile lobby. There is
   **no event bus wired** between services (RabbitMQ runs but is unused); cross-service
   calls today are synchronous HTTP (`TeamReferenceCatalogClient`).

**Intended outcome (user-approved decisions):**
- Operator team endpoints addressable by **session code**, *in addition to* the existing
  guid routes (keep guid working — the operator web frontend, not in this repo, still uses guids).
- Operator associate **propagates to identity-access via synchronous HTTP**, so the mobile
  lobby reflects it. Idempotent (already-associated = success).
- Fix the seed so seeded data agrees across operator and mobile.

## Gateway note (no change needed)

YARP (`backend/api-gateway/src/appsettings.json`): `identity-sessions` matches
`/api/sessions/{code:regex(^[A-Z0-9]+$)}/teams` → identity; `session-ops` catch-all
`/api/sessions/{**catch-all}` → session-operations. A bare `/api/sessions/SMOKE1/teams`
would be misrouted to identity, so the operator's code route uses a distinct prefix
`/api/sessions/by-code/{sessionCode}/teams`, which the catch-all already forwards to
session-operations. The session→identity sync call is service-to-service (bypasses gateway).

---

## Part A — Operator team endpoints addressable by code (session-operations)

Additive; guid routes untouched. Scope = the two team endpoints in question (associate POST,
get-associated GET). Same pattern can later extend to state/timer if wanted.

1. **Repository lookup by code.** Add `GetBySessionCodeAsync(string code, ct)` to
   `ILiveSessionRepository` (`src/Application/Common/Interfaces/ILiveSessionRepository.cs`)
   and `LiveSessionRepository` (`src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs`),
   mirroring `GetByIdAsync` but matching `SessionCode` (column is `UNIQUE`). Include `Teams`
   like `GetByIdAsync` does.
2. **Facade resolution by code.** In `SessionTeamAssociationFacade`, extract the post-load
   core of `AssociateAsync` / `GetAssociatedTeamsAsync` into private helpers that take a loaded
   `LiveSession`, then add `AssociateByCodeAsync` / `GetAssociatedTeamsByCodeAsync` that resolve
   via `GetBySessionCodeAsync` (throw `NotFoundException` when missing) and call the shared core.
   Update `ISessionTeamAssociationFacade`.
3. **Commands/queries + handlers.** Add `AssociateTeamToSessionByCodeCommand(string SessionCode,
   Guid ReferenceTeamId)` and `GetAssociatedTeamsForSessionByCodeQuery(string SessionCode)` with
   handlers delegating to the new facade methods, mirroring the existing guid pair under
   `src/Application/Sessions/Commands/AssociateTeamToSession` and `.../Queries/GetAssociatedTeamsForSession`.
   Add validators mirroring the existing ones (non-empty/normalized code).
4. **Routes.** In `src/Api/Endpoints/SessionsEndpoints.cs` add, alongside the guid routes:
   - `POST /api/sessions/by-code/{sessionCode}/teams` → `AssociateTeamByCodeAsync`
   - `GET  /api/sessions/by-code/{sessionCode}/teams` → `GetAssociatedTeamsByCodeAsync`
   Both `.RequireAuthorization(AuthorizationPolicies.Operator)`, reusing `AssociateTeamRequest`.

## Part B — Operator associate → identity-access lobby sync (synchronous HTTP)

**identity-access side — expose the already-existing domain capability.**
`LiveSessionReference.AssociateTeam(teamId)` already creates the `SessionTeamAssociation` and
guards duplicates (`src/Domain/Entities/LiveSessionReference.cs`); it is simply not exposed.

1. New `POST /api/sessions/{code}/teams` (role: Operator) in
   `services/identity-access-service/src/Api/Endpoints/SessionsEndpoints.cs`, body
   `{ liveSessionId, teamId }`. Maps to `AssociateTeamToSessionReferenceCommand(Guid LiveSessionId,
   string SessionCode, Guid TeamId)`.
2. Handler: `GetByIdAsync(liveSessionId)`; if null, `LiveSessionReference.Create(liveSessionId,
   sessionCode)` + `AddAsync`; else `reference.AssociateTeam(teamId)` + persist. Treat the
   duplicate case (`TeamAlreadyAssociatedWithSessionException`) as **idempotent success** (200).
3. Persistence: ensure `GetByIdAsync` **`.Include(_teamAssociations)`** so the in-memory dup
   guard works; add `UpdateAsync(LiveSessionReference, ct)` (SaveChanges) to
   `ILiveSessionReferenceRepository` + `LiveSessionReferenceRepository`. (Belt-and-suspenders:
   treat a DB unique-violation on `(live_session_id, team_id)` as idempotent success.)

**session-operations side — call it during associate.**
4. New typed HTTP client `ISessionTeamAssociationSyncClient` +
   `SessionTeamAssociationSyncClient` under `src/Infrastructure/Identity/`, modeled on
   `TeamReferenceCatalogClient` (same `ForwardTrustedHeaders` for `X-User-*`; base address =
   identity-access). Method `SyncAssociationAsync(liveSessionId, sessionCode, teamId, ct)` →
   `POST /api/sessions/{code}/teams`; 2xx and 409 both = success.
5. Register the typed client in `src/Infrastructure/DependencyInjection.cs` next to the existing
   identity client (reuse the same base-address config the catalog client uses).
6. Call it from `SessionTeamAssociationFacade` shared associate-core **after**
   `_liveSessionRepository.UpdateAsync(...)`, for both the guid and by-code paths. If the sync
   call hard-fails (non-2xx, non-409), surface the error so the operator can retry (idempotent).
   *Known limitation to note in PR:* write-time coupling, not a transactional outbox.

## Part C — Seed fix

In `backend/scripts/seed-all.sh`, both `live_session_teams` INSERT blocks: add the
`reference_team_id` column set to `'$TID'` and change `ON CONFLICT (id) DO NOTHING` →
`ON CONFLICT (id) DO UPDATE SET reference_team_id = EXCLUDED.reference_team_id` (mirror the
already-correct `seed-dev-data.sh`). Makes re-seeding repair existing NULL rows so seeded
teams show in the operator panel immediately.

## Tests

- session-operations unit: extend `SessionTeamAssociationFacadeTests` — by-code resolution and
  that associate invokes the sync client (add a fake sync client like `FakeTeamReferenceCatalogClient`).
- session-operations integration: extend `SessionTeamAssociationEndpointTests` for the
  `by-code` routes.
- identity-access: handler tests for `AssociateTeamToSessionReferenceCommand` (creates
  association, idempotent on duplicate, creates reference when absent); endpoint test for the
  new POST.

## Verification (end-to-end, against Docker stack)

1. `dotnet watch` containers hot-reload on save; if not, `docker compose up --build` the two services.
2. Re-run `./scripts/seed-all.sh`; confirm operator panel for SMOKE1 now lists Soon/Soon2
   (i.e. `session_operations.live_session_teams.reference_team_id` is no longer NULL).
3. Operator associate Bismarck via API (operator JWT):
   `POST /api/sessions/by-code/SMOKE1/teams { referenceTeamId: <Bismarck id> }`.
   Then verify in Docker DB:
   - `session_operations.live_session_teams` has a Bismarck row with `reference_team_id` set;
   - `identity_access.session_team_associations` has a matching row for SMOKE1.
4. Operator dashboard shows Bismarck under "Associated teams"; mobile lobby for SMOKE1 now
   lists Bismarck. Confirm guid route `GET /api/sessions/{guid}/teams` still works.
5. Run affected test suites for both services.
