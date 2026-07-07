# ADR-0007: Application Database is the Source of Truth for User Roles

The Keycloak JWT was overwriting the user's role on every login (via `IdentityProvisioningPolicy.SynchronizeOrCreate`), making admin dashboard role changes invisible until a code change was deployed. We made the application database the role authority by removing the role-overwrite on login and instead syncing outward: the admin dashboard writes to the app DB, and the backend propagates the change to Keycloak via the Admin API. The frontend session cookie is refreshed on dashboard load via a server action so stale-role windows are at most one page navigation.

**Status:** Accepted

**Consequences:** Keycloak JWTs may lag behind the app DB by up to the token lifetime (typically 5-60 minutes). A `refreshSession` server action narrows this on the frontend but the gateway still routes based on the JWT claim. If tight role enforcement at the gateway is required, either the token lifetime must be shortened or the gateway must query the service on each request.

## Write reliability: Keycloak-first, no silent desync (2026-07-06)

The original write path was DB-then-Keycloak with the Keycloak call best-effort — exceptions were caught and logged, so a Keycloak outage left the app DB updated and Keycloak carrying the stale realm role indefinitely, with no visibility (issue #81). "App DB is the source of truth" was being used to justify committing a role change we knew had not propagated.

We reordered the write to **Keycloak-first, then commit the DB**, with a bounded in-process retry over the Keycloak Admin API call (transient failures — connection/5xx/timeout — retried; a missing realm role fails fast). On exhaustion the caller receives an `IdentityProviderRoleSyncException` mapped to **HTTP 503**.

**What this changes:**
- **Atomic from the caller's perspective.** If Keycloak can't be updated, `UpdateAsync` never runs — both stores keep the old role. The admin gets a deterministic, retryable 503 instead of a silent divergence.
- **"Source of truth" clarified.** The app DB remains authoritative for *reads* (login still never overwrites the role from the JWT). It is no longer allowed to move *ahead* of Keycloak on a write — outward propagation is now a precondition of committing, which strengthens the no-drift invariant rather than weakening it.
- **Trade-off:** a sustained Keycloak outage blocks role changes entirely. This is the correct failure mode for this ticket — a role change that cannot reach Keycloak is exactly the desync we are preventing.
- **Residual edge:** if Keycloak succeeds but the DB commit then fails, Keycloak is briefly ahead of the app DB. This is rare and self-heals: the app DB still holds the old role so the admin retries, and the sync reconciles the user's realm mappings idempotently on every call.

**Alternatives rejected:** an outbox/retry-queue (option a) and a saga with compensation (option c) both add durable infrastructure (a table, a background processor) to buy at-least-once delivery the current volume does not need — the synchronous 503 already gives the operator a deterministic, actionable signal. Revisit the outbox if role changes must succeed while Keycloak is down.
