# Handoff — e2e failures: FINAL VERDICT (validated end-to-end)

**Updated:** 2026-07-05 · **Branch:** `develop` · **Stack:** full backend up via docker

> This supersedes the prior "operator 500 → cascade" theory. Every claim below was reproduced
> directly (curl against gateway/services, live DB queries, git history, and a real Playwright run
> of `auth.spec.ts:8`). **The prior handoff correctly named *which* tests fail but misdiagnosed
> *why*.** The headline: the operator `/api/sessions` 500 is real but is **not** what fails the
> operator tests — those are a frontend/test-contract mismatch. The 500 is a separate, real backend
> bug. They must be fixed independently.

## TL;DR — what each failure actually is

| # | Test | Prior label | **Real cause** | **Fix lives in** |
|---|------|-------------|----------------|------------------|
| 1 | `auth.spec.ts:8` operator sees operator dashboard | operator 500 | **`operator-panel` testid not in default operator view** | frontend/test |
| 3 | `participants.spec.ts:201` HU-01 operator not regressed | operator 500 | same as #1 | frontend/test |
| 4 | `roles.spec.ts:76` HU-01 operator not regressed | operator 500 | same as #1 | frontend/test |
| 5 | `teams.spec.ts:188` HU-01 operator not regressed by teams | operator 500 | same as #1 | frontend/test |
| 7 | `teams.spec.ts:155` operator opens edit form | operator 500 | **`.nth(3)` team-row ordering** (data pollution) | test |
| 6 | `trivias.spec.ts:130` edit disabled for non-Draft | stale seed | **test references a quiz no longer seeded** | test |
| 2 | `missions.spec.ts:159` activate runtime-ready mission | unclassified | **shared mission polluted with junk substages** | seed / DB |

Plus an operator `GET /api/sessions` 500 that fixes none of the tests above and is an **e2e
provisioning bug, not a production backend bug** (see §E).

---

## A — The 4 "operator 500" failures are a frontend/test-contract mismatch (NOT the 500)

**Proven by running `auth.spec.ts:8`:** the operator dashboard *renders fine* —
`role-chip = 'operator'` passes. It fails on the very next line, `operator-panel` **not found in the
DOM**. The rendered DOM was `SessionsPanel`, showing the *caught* 500 as an error banner
("Assigned sessions could not be loaded through the gateway…") plus "You have no assigned sessions
yet." The 500 does not abort anything — the client `.catch()` handles it.

**Why `operator-panel` is absent.** Operator's `activeNav` defaults to `'sessions'`
(`DashboardClient.tsx:294`), so the main content renders `<SessionsPanel>` (`DashboardClient.tsx:762`),
whose testid is **`sessions-panel`**, not `operator-panel`. The three `operator-panel` sections
(`DashboardClient.tsx:775 / 819 / 1012`) only render when `activeNav !== 'sessions'`. So on the
default operator landing there is no `operator-panel` element — **fixing the 500 cannot make these
tests pass.** The tests assert an `operator-panel` contract from HU-01 (`dcdf2b7`) that the
hu-20/hu-21 refactor (`b43a60e`, 4 weeks ago) replaced with `SessionsPanel`; the tests were never
updated.

**Where to fix:** frontend/test. Decide the intended operator landing contract, then either
- update the 4 tests to assert `sessions-panel` (fastest, matches current UI), **or**
- re-introduce an `operator-panel` wrapper testid around the operator sessions view if that
  contract is meant to hold.

Affected tests: `auth.spec.ts:12`, `participants.spec.ts:201`, `roles.spec.ts:76`,
`teams.spec.ts:188` (all assert `[data-testid="operator-panel"]` visible after `/dashboard`).

---

## B — `teams.spec.ts:155` is a data-ordering assumption, not the 500

The test clicks `[data-testid^="team-row-"].nth(3)` and never calls the operator session-listing
path (that only runs in the assignment modal, and is `try/catch`-guarded in `TeamsPanel.tsx:207-215`).
It needs ≥4 stable team rows; sibling tests deactivate teams (the test's own comment: *"rows may
shift so nth(4)"*). On a polluted DB `.nth(3)` is missing or a different team → failure.

**Where to fix:** test. Select the target team by name/code instead of positional `.nth(3)`, or
reseed teams before the teams spec.

---

## C — `trivias.spec.ts:130` stale seed (confirmed; commit re-attributed)

Test expects an **Archived** quiz **`Guitarristas mas queridos`** (`trivias.spec.ts:144,147`).
Live DB + `seed-all.sh` have no such quiz. What is seeded: `Capitales del mundo` (Archived, id 105)
and `Mejores guitarristas de la historia` (Published, id 102).

- The quiz was **removed in `b43a60e` (hu-21)** — *not* `3561a53` as previously stated
  (`3561a53` only consolidated already-guitarristas-free seeders).

**Where to fix:** test. Point `trivias.spec.ts:144/147` at `Capitales del mundo` (the actually-seeded
Archived quiz). Renaming the seed instead would be odd (geography copy under a guitar title).

---

## D — `missions.spec.ts:159` is shared-fixture pollution (now classified)

`E2E Activatable Mission` (mission id 18) is seeded Draft with one runtime-ready Trivia substage
(`global-setup.ts:161-203`, points at Published `Filosofos de Atenas` quiz 101). But the live mission
has **extra junk** from `mission-hierarchy.spec.ts` (which drives the same shared mission):

```
GET /api/missions/18/readiness → isReady:false, failures:
  "Treasure-hunt substage 'asdkjfds' in stage 'Stage 1' must have at least one active target."
  "Treasure-hunt substage 'asdkjfds' … must define a winner score."
  "Stage 'safdsf' must contain at least one substage."
```

Readiness fails → `activate-mission-btn` never enables → test times out at
`missions.spec.ts:171` (10s). Not cascade, not a backend defect.

**Where to fix:** seed / DB.
- Reseed clean before the run, **and**
- harden `global-setup.ts`'s already-exists ELSE branch (`:190-201`): today it re-points the Trivia
  substage but leaves any stray stages/substages the hierarchy tests added. Make it delete
  stages/substages that aren't the seeded one so a persistent DB can't stay poisoned.

---

## E — operator `GET /api/sessions` 500 is an e2e provisioning bug, NOT a production backend bug

**This is not a real backend defect.** In production the operator can list sessions fine — the 500
only appears in e2e because of how the test harness provisions identity. It fixes none of the tests
above (the client `.catch()`es it into an error banner). Verified: the moment the operator is
bootstrapped, `/api/sessions` returns `200 []`.

**Why it never happens in production:** a real operator logs in via OIDC →
`app/api/auth/callback/route.ts` calls `bootstrapUser` (`POST /api/users/authenticated`) →
identity-access creates/updates a row keyed by the real Keycloak `sub` (UUID) → `/users/me` resolves
→ `/api/sessions` returns 200. Both broken preconditions below are e2e-only.

**Reproduced (no browser):**

```bash
TOK=$(curl -s -X POST http://localhost:8080/realms/umbral/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=umbral-web&username=op-1&password=operator123" \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['access_token'])")
curl -s http://localhost:8000/api/sessions -H "Authorization: Bearer $TOK"
# => 500  {"detail":"Response status code does not indicate success: 404 (Not Found)."}
```

The 500 body is **not** empty — it exposes a downstream **404**. The prior "empty body / swallowed
exception / stale `dotnet watch` binary" notes were all wrong.

**Exact chain (traced through the code):**
1. Gateway validates the JWT and forwards `X-User-Id` = the Keycloak **`sub` (a UUID)**, e.g.
   `f7be84a6-…` — *not* the string `op-1`. (The prior "500 on gateway shape, 200 on X-User-* headers"
   was an artifact of testing with `X-User-Id: op-1`; the same header with the UUID value 500s.)
2. `ListAssignableSessionsQueryHandler` operator branch
   (`session-operations-service/.../ListAssignableSessions/ListAssignableSessionsQueryHandler.cs:47`)
   calls `AuthenticatedActorProfileAccessClient.GetCurrentAsync` →
   `GET identity-access:/api/users/me` forwarding that UUID
   (`.../Infrastructure/Identity/AuthenticatedActorProfileAccessClient.cs:22-26`).
3. identity-access has no user whose `ExternalIdentityId` = that UUID → **404** →
   `EnsureSuccessStatusCode()` throws → ProblemDetails 500.

**Why the mismatch exists (the actual root cause):**
- `global-setup.ts:81-92` seeds the identity-access `users` row with `ExternalIdentityId = 'op-1'`
  (the literal username).
- But Keycloak assigns a **fresh random UUID `sub`** for op-1 on every run — `ensureUser`
  (`global-setup.ts:296-320`) deletes+recreates the KC user because Keycloak **ignores** the
  payload's `id: 'op-1'` (`toUserPayload`, `:256`). Verified live: KC user `op-1` →
  `f7be84a6-…`, and `GET /admin/realms/umbral/users/op-1` → 404.
- The Playwright fixture (`tests/fixtures/auth.ts:14-32`) injects the session cookie **directly**,
  bypassing the OIDC bootstrap. Bootstrap (`/api/users/authenticated`) is what would create a
  UUID-keyed identity row — verified: calling it makes `/api/sessions` return `200 []` immediately
  (and creates a *duplicate* user: seeded id 15 `op-1` + bootstrapped id 303 `<uuid>`).
- Only session-listing carries a Bearer JWT (`getGatewayHeaders`, `sessions.ts:20-25` → UUID). Every
  other operator BFF call sends `X-User-Id: session.externalIdentityId` = `op-1` directly, which
  resolves — which is exactly why *only* session-listing 500s and the rest of the operator dashboard
  works. Admin session-listing works too: its branch never calls `/users/me`
  (`ListAssignableSessionsQueryHandler.cs:34-40`).

**Where to fix — this is an e2e-harness fix, not a backend change (pick one):**
- **Seed (recommended):** make identity-access `ExternalIdentityId` equal the **resolved Keycloak
  `sub`**. Seed identity users *after* `seedKeycloak()` using each user's resolved KC id (available
  from `ensureUser`), instead of the literal username. Removes the 500 *and* the id-15/id-303
  duplicate.
- **Fixture:** have the operator fixture bootstrap (`POST /api/users/authenticated`) so the
  UUID-keyed row exists — but this leaves the duplicate-user split (sessions may be assigned to the
  seeded id 15 while the actor resolves to id 303), so the seed fix is cleaner.

**Optional backend hardening (NOT required; does not change any test):** `GetCurrentAsync` 500s when
`/users/me` 404s. It *could* map a 404 to a typed "actor not provisioned" result so an unprovisioned
operator gets an empty list instead of a 500. This is the only backend-side item, and it is a
robustness nit, not a bug fix — production never hits the 404.

---

## Uncommitted change to decide

`app/dashboard/page.tsx` — try/catch around `getCurrentUserProfile()` that redirects to `/login` on
`IdentityError`. **Confirmed correct-but-not-load-bearing:** that call uses
`session.externalIdentityId = 'op-1'` (BFF-direct to `:5002`), which resolves, so it never throws for
op-1 and the guard never fires in these flows. Keep it as small hardening or drop it — it fixes none
of the failures.

## Reproduce the full run

```bash
cd frontend
rm -rf test-results playwright-report
SESSION_SECRET=dev-session-secret-change-in-production \
API_GATEWAY_URL=http://localhost:8000 NEXT_PUBLIC_API_GATEWAY_URL=http://localhost:8000 \
  node_modules/.bin/playwright test --reporter=line
```
For a clean baseline run `backend/scripts/seed-all.sh` first (DELETEs+reseeds the trivia catalog).
`psql`: `/home/linuxbrew/.linuxbrew/opt/postgresql@17/bin/psql`.

## Not re-verified this session
- That the 5 former trivia "cascade" failures (`389/731/765/790/841`) now pass — plausible, but a
  full run wasn't repeated.

## Gotchas
- Sandbox blocks `docker`/`find`/`make` (`apply-seccomp … Permission denied`); run with sandbox off.
- `SessionOperatorPanel` (`data-testid="session-operator-panel"`, admin-only) ≠ the operator
  `operator-panel` sections — don't conflate.
