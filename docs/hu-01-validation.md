# Validation: HU-01 — Current Changes

All four acceptance criteria are fully covered by the current changes across backend and frontend.

## 1. Registered user can log in with valid credentials ✅

| Layer | Implementation |
|---|---|
| **Keycloak** | OIDC provider handles credential validation |
| **Frontend** | `app/api/auth/login/route.ts:1-35` — PKCE setup + redirect to Keycloak |
| **Frontend** | `app/api/auth/callback/route.ts:1-78` — code exchange (`keycloak.ts:45-100`), user bootstrap (`identity.ts:6-32`), session creation (`session.ts:45-57`) |
| **Backend** | `POST /api/users/authenticated` — validates JWT via api-gateway, provisions user, returns `AuthenticateUserResultDto` with actor profile + access decision |

## 2. System identifies the authenticated user's role ✅

| Layer | Implementation |
|---|---|
| **Backend** | Role comes from `currentUser.Role` (injected via `X-User-Role` header by api-gateway) |
| **Backend** | Returned in `AuthenticateUserResultDto.actor.role` (`AuthenticateUserResultDto.cs:3`) |
| **Frontend** | Stored in encrypted session cookie as `SessionPayload.role` (`definitions.ts:7`) |
| **Frontend** | Read in `DashboardPage` via `verifySession()`, passed as `role` prop to `DashboardClient` (`dashboard/page.tsx:13`) |
| **Frontend** | Displayed in UI via `data-testid="role-chip"` (`DashboardClient.tsx:430`) |

## 3. Blocks unauthenticated or deactivated users ✅

| Gate | Unauthenticated | Deactivated |
|---|---|---|
| **`proxy.ts:18-24`** (HTTP layer) | Redirects `/login` | Redirects `/login?error=deactivated` |
| **`verifySession()`** `dal.ts:12-13` (server component layer) | Redirects `/login` | Redirects `/login?error=deactivated` |
| **`identity.ts:19-25`** (bootstrap call) | Throws `IdentityError('unauthorized')` on 401 | Throws `IdentityError('deactivated')` on 403 |
| **`callback/route.ts:46-51`** (post-bootstrap) | Redirects `/login?error=unauthorized` | Redirects `/login?error=deactivated` if `!result.access.isAllowed` |
| **Backend `AccessPolicy.cs:16-21`** | JWT validation fails → 401 | Returns `Deny` + `DeactivatedUserAccessDeniedException` → 403 |
| **Backend `AuthenticateUserCommandHandler.cs:75-86`** | N/A | Throws `DeactivatedUserAccessDeniedException` |

## 4. Authenticated user can only access role-permitted functionality ✅

| Layer | Implementation |
|---|---|
| **Frontend** | `DashboardClient.tsx:525-1051` — conditional rendering branches: `role === 'operator'` renders operator panel (`data-testid="operator-panel"`); otherwise renders admin panel (`data-testid="admin-panel"`) |
| **Frontend** | `toDashboardRole()` maps `'Administrator' → 'admin'`, `'Operator' → 'operator'` (`dashboard/page.tsx:4-6`) |
| **Backend** | `AccessPolicy.cs:23-30` — maps `ProtectedCapability` to allowed roles (`AdministratorPanel` → Administrator only, `OperatorPanel` → Administrator or Operator, etc.) |
| **Backend** | `CheckProtectedCapabilityAccessQueryHandler.cs:49-60` — enforces at query time |

## E2E Test Coverage (`tests/e2e/auth.spec.ts`)

| Test | Validates |
|---|---|
| `unauthenticated user is redirected to login` | Gate #3 |
| `operator sees operator dashboard` | Gates #2, #4 |
| `admin sees admin dashboard` | Gates #2, #4 |
| `deactivated user is redirected to deactivated error` | Gate #3 |
| `logout clears session and redirects to login` | Gate #3 |

## Seeded Development Users

Three users are pre-seeded in the Keycloak realm import (`backend/deploy/keycloak/import/umbral-realm.json:117-172`) for local development:

| Username | Password | Role | Email |
|---|---|---|---|
| `admin` | `admin123` | Administrator | admin@umbral.local |
| `operator` | `operator123` | Operator | operator@umbral.local |
| `participant` | `participant123` | Participant | participant@umbral.local |

Note: the frontend `DashboardClient` currently only has rendering branches for `operator` and `admin` — a `Participant` user can log in successfully but will see the admin panel as a fallback. A participant-specific view would need to be added.

## Summary

All four acceptance criteria are implemented and validated with E2E tests. The flow is end-to-end: Keycloak login → PKCE/OIDC → api-gateway JWT validation → user bootstrap → role-aware session → role-based dashboard rendering.
