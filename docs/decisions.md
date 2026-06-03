
---

## [001] HU-01 End-to-End Auth — Frontend + Backend Integration
**Date:** 2026-05-30
**Phase:** X.5 — Frontend Auth Integration
**Commits:** 9a1d921, c1bcb1e (backend); untracked (frontend)
**HU tickets advanced:** HU-01

**What was built**
Full end-to-end authentication for HU-01: Keycloak OIDC login with PKCE, api-gateway JWT validation with trusted-header forwarding (`X-User-Id`, `X-User-Role`, `X-User-Email`), backend user bootstrap (`POST /api/users/authenticated`) with role persistence and deactivation enforcement, frontend login page, encrypted stateless session cookie, server-side route guard (`proxy.ts`), role-aware dashboard rendering (operator/admin), and 5 Playwright E2E tests covering all acceptance criteria.

On the backend side: api-gateway JWT bearer auth configured with dual-issuer support (localhost + docker network), `TrustedHeadersTransform` to strip the original token and inject identity headers, identity-access-service Dockerfile built and wired into docker-compose, and three seeded Keycloak users (admin, operator, participant) in the realm import.

On the frontend side: complete auth module (`app/lib/keycloak.ts`, `app/lib/identity.ts`, `app/lib/session.ts`, `app/lib/dal.ts`), route handlers (`/api/auth/login`, `/api/auth/callback`), login page with deactivated/unauthorized error chips, logout server action, proxy guard with optimistic session decryption, and single shared dashboard with role-based client component.

**Why this approach**
Manual OIDC (no NextAuth/Auth.js) keeps control over the exact flow and avoids library bloat for a single-IdP setup. The stateless encrypted cookie (jose-signed JWT) removes the need for Redis or a session DB — the cookie is the session. The proxy is optimistic-only (decrypts locally, never backends) for zero-latency guard checks. Role-aware rendering shares one app shell instead of separate admin/operator routes, keeping navigation and layout consistent per ADR-0004. The api-gateway is the sole JWT validator — downstream services consume trusted headers only, per DES-67 PRD constraints.

Key tradeoff: the `Participant` role is accepted by the backend but has no dedicated frontend rendering branch — it falls through to the admin panel. This was flagged but deferred since HU-01 scope is operator + admin.

**Deliberately skipped**
- Participant-specific dashboard view — not in HU-01 scope; backend supports it, frontend needs a dedicated branch
- User deactivation UI — no endpoint exposed yet; deactivation is DB-only for now
- Infrastructure-level E2E (full docker stack with real Keycloak) — tests use cookie injection to bypass OIDC
- AutoMapper in identity-access-service — DTOs still use manual mapping; deferred to future cleanup

**Next session needs to know**
Both builds (api-gateway, identity-access-service) pass clean. The frontend E2E suite passes with cookie injection. Full end-to-end validation with docker-compose + real Keycloak requires starting the stack and running through the sign-in flow manually. The `Participant` role is accepted by the backend but has no frontend rendering — if participant UX is needed, `DashboardClient.tsx` needs a new branch.

---

## [002] HU-07B Cross-Service Data Alignment — Identity + SessionOperations + Mobile
**Date:** 2026-06-03
**Phase:** 2.X — Mobile Participant Validation & Debug
**Commits:** uncommitted (seed script, migration, docs)
**HU tickets advanced:** HU-07B

**What was built**
- `backend/scripts/seed-dev-data.sh` — idempotent seed script that creates 6 aligned sessions (Scheduled/Preparing/Active/Paused/Finished/Cancelled) with matching GUIDs across `identity_access` and `session_operations` databases
- `20260603222547_AddTeamCapacityColumn` migration — adds the `team_capacity` column that was missing from `live_session_teams` (the original migration was edited after being applied)
- `docs/hu-07b-cross-service-data-alignment-fix.md` — comprehensive document explaining the three root causes, data flow, error mapping, and seed session table
- Identified and documented the `JoinPolicy` domain rule that only allows first join for `Scheduled`/`Preparing` sessions (all other states throw `LateJoinNotAllowedException`)

**Why this approach**
The original hu-07b feature commit created the `Capacity` property mapping and migration, but the migration was edited after being applied — the column never existed in the actual database. EF Core exploded when loading any `LiveSession` aggregate. A bash/psql seed script was chosen over an EF Core seed (which would require modifying entity configurations) or an init container (which fails because tables don't exist until `MigrateAsync()` runs). The script uses dynamic `gen_random_uuid()` for non-authoritative GUIDs and deterministic GUIDs for the cross-service shared IDs.

**Deliberately skipped**
- Pre-seeding participants in SessionOperations — the `JoinPolicy` intentionally blocks first join to Active sessions. This is the correct domain behaviour. The user validated it produces the expected "late join isn't allowed" copy on mobile.
- Cleaning up frontend test data sources (`global-setup.ts`, `seed-teams.sh`) — those seed independent teams without session associations and don't affect the mobile flow directly.
- Adding separate mobile copy for Finished/Cancelled vs Active/Paused — all non-Scheduled/Preparing states hit the same `LateJoinNotAllowedException`. Differentiating them would require new backend exception types and hub filter entries.

**Next session needs to know**
Run `./backend/scripts/seed-dev-data.sh` after `docker compose up -d`. All 6 SMOKE sessions are available for mobile validation. SMOKE3 (Scheduled) and SMOKE4 (Preparing) are the only ones where first join succeeds. The `team_capacity` migration will auto-apply on next container restart. The debrief doc at `docs/hu-07b-cross-service-data-alignment-fix.md` has full error mapping and data flow details.
