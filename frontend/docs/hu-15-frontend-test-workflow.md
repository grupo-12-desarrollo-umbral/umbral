# HU-15 — Create a Session from an Active Mission: Frontend UI Test

Branch: `feature/hu-15-session-creation-from-mission`
App: `frontend/` (Next.js, runs on `http://localhost:3000`)
Backend: `docker compose up -d` in `backend/` (gateway on `http://localhost:8000`)

What changed: the admin **Create session** form now takes a **Mission only** — the Quiz
selector is gone. The session is built from the mission's runtime snapshot; the client sends
just `{ missionId, title, maximumTimeMinutes, scheduledAt }`.

---

## Prerequisites

1. **Backend up:**
   ```bash
   cd backend && docker compose up -d
   ```
2. **At least one active, runtime-ready mission** (so the Mission dropdown isn't empty).
   **Easiest:** run the e2e suite once — its `globalSetup`
   (`tests/setup/global-setup.ts`) seeds **"E2E Seed Mission"** (active + Ready) straight into the
   `mission_design` DB, and it persists:
   ```bash
   cd frontend && pnpm exec playwright test tests/e2e/sessions.spec.ts
   ```
   Alternatively, hand-author one via the backend smoke setup in
   `backend/docs/hu15-how-to-test.md` (Setup section). For the row-4 rejection test, deactivate
   a mission backend-side: `curl -X DELETE http://localhost:8000/api/missions/<id>` (with an admin
   bearer) — that flips it inactive so creation returns `409`.
3. **Frontend up:**
   ```bash
   cd frontend && pnpm install && pnpm dev
   ```

## Log in

- Open `http://localhost:3000` → you're redirected to Keycloak.
- Sign in as **admin / `admin123`** (Administrator). For the role-gating test, also have
  **operator / `operator123`** ready.

Keycloak users (realm `umbral`): `admin/admin123`, `operator/operator123`, `participant/participant123`.

## Reach the form

Dashboard left nav → **Assign operators** (this is the `sessions` panel; admins see it labeled
"Assign operators", operators see "My sessions"). Find the **Create session** card. You should see:
a **Mission** select, **Session title**, **Maximum time (minutes)**, **Scheduled at**, and a
**Create session** button. **There is no Quiz select.**

---

## Manual test matrix

Run as **admin** unless noted. Each row maps a UI action to the exact state the operator must see.

| # | Scenario | How to produce it | Expected UI |
|---|----------|-------------------|-------------|
| 1 | Happy path | Pick an active+ready mission, fill title / max time / scheduled-at, click **Create session** | Form clears; the new session appears in the list below in **Scheduled** state |
| 2 | Submit stays disabled until complete | Leave any field empty (no mission, or no title / time / date) | **Create session** is disabled; enabling only once all four are filled |
| 3 | No active missions | Test on a stack with no active mission | Mission select is empty + "No active missions available…" hint; submit disabled |
| 4 | Mission inactive **or** not runtime-ready | Select a mission that is deactivated or not ready (backend returns `409`) | Red banner: *"The selected mission is inactive or not runtime-ready … Activate the mission and ensure all stages, substages, and targets are configured."* Form stays intact |
| 5 | Mission not found | Select a mission, then delete it backend-side before submitting (`404`) | Banner: *"The selected mission was not found. Reload the page and try again."* |
| 6 | Created session is assignable | After row 1, the new Scheduled row shows **Assign operator** | Assignment modal opens for the just-created session |
| 7 | Operator cannot create | Log in as **operator**, open **My sessions** | Read-only panel — **no create form** |

Confirm while running the matrix:

- No "Quiz" / "Trivia" wording appears anywhere in the Create session card.
- Every rejection (rows 4–5) leaves the form filled and usable — never a blank/broken screen.
- The created session's state chip reads **Scheduled** (row 1).

---

## Automated coverage (optional)

```bash
cd frontend
pnpm build                                        # typecheck + build
pnpm exec vitest run tests/unit/app/lib/sessions.test.ts   # createSession mapping
pnpm exec playwright test tests/e2e/sessions.spec.ts        # UI flow (needs stack up)
```

> The e2e create-path tests need a real authenticated session (both the app `session` cookie and a
> valid sealed `kc_session` cookie). The Playwright fixture seeds matching Keycloak users and injects
> both cookies for admin/operator actors before running these rows.
