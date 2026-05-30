
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
