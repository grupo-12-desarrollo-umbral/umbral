# GH-144 — Manual Test: forgot-password entry points (web + mobile)

Goal: a user opens the hosted Keycloak forgot-password flow from the login
screen — **on the web dashboard and on the mobile app** — requests a reset mail,
and completes the reset using the Mailpit message.

GH-144 shipped both entry points (PR #212, commit `97c0d66`):

- **Web** — `frontend/app/login/LoginCard.tsx` renders a "Forgot your password?"
  link built by `buildResetCredentialsUrl()` in `frontend/app/lib/keycloak.ts`.
- **Mobile** — `mobile/src/app/(auth)/login.tsx` opens the same hosted page via
  `mobile/src/lib/auth/keycloak.ts`.

Both point at the same hosted Keycloak page, so the mail + reset half of the
test is identical; only the entry point differs. Run **Track A (web)** or
**Track B (mobile)** — or both to confirm parity.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak + Mailpit
cd backend && docker compose up -d

# web dashboard (http://localhost:3000)
cd ../frontend && pnpm install && pnpm dev

# mobile app (only needed for Track B)
cd ../mobile && pnpm install && pnpm start
```

## 2. Credentials

| Who  | Where               | Login                                         |
| ---- | ------------------- | --------------------------------------------- |
| User | web app / mobile app| `participant@umbral.local` / `participant123` |

Use any seeded account with a real email in `umbral-realm.json`. The default
participant seed is enough for this flow.

---

## Track A — Web dashboard

### Step A1 — Web: open the hosted reset flow

1. Open [http://localhost:3000/login](http://localhost:3000/login) and stop at
   the **login** screen.
2. Click **Forgot your password?** (below the "Sign in with Keycloak" button).
3. The browser should land on Keycloak's hosted reset page at:

```
http://localhost:8080/realms/umbral/login-actions/reset-credentials
```

Then continue with the shared **Step B / Step C** below.

---

## Track B — Mobile app

### Step B1 — Mobile: open the hosted reset flow

1. Open the app and stop at the **login** screen.
2. Tap **Forgot your password?**
3. The app should open Keycloak's hosted reset page at the same URL:

```
http://localhost:8080/realms/umbral/login-actions/reset-credentials
```

On a physical device, `localhost:8080` must resolve to your dev machine. If it
does not, set the Keycloak URL to a reachable LAN host.

Then continue with the shared **Step B / Step C** below.

---

## Step B (shared) — Keycloak: request the reset mail

1. Enter the seeded account email.
2. Submit the form once.
3. Open Mailpit at [http://localhost:8025](http://localhost:8025).
4. Verify a password-reset mail arrives for that address.

## Step C (shared) — Mailpit: follow the reset link

1. Open the reset email in Mailpit.
2. Click the Keycloak reset link.
3. Set a new password.
4. Return to the app (web login page or mobile app) and sign in with the new
   password.

That hosted reset round-trip — login entry point, Mailpit mail, new password,
successful sign-in — is GH-144 working. ✅ Do it once from the web login page and
once from the mobile login screen to confirm both entry points.

## 3. Quick extra checks (optional)

| Check                 | How                                    | Expect                                                  |
| --------------------- | -------------------------------------- | ------------------------------------------------------- |
| No account enumeration| Submit one existing and one fake email | Keycloak shows the same post-submit response both times |
| TTL sanity            | Wait more than 15 minutes before click | Reset link expires                                      |
| Retry path            | Re-open from web or mobile login       | Hosted reset page opens again                           |
| Web/mobile parity     | Run both tracks against the same seed  | Same hosted page, same reset outcome                    |

---

## Known limitations (by design — not bugs)

- Both the web dashboard and the mobile app only open the **hosted Keycloak
  page**. The reset form and success/error responses are owned by Keycloak, not
  by the Next.js dashboard or the Expo app.
- On a physical mobile device, `localhost:8080` must resolve to your dev
  machine. If it does not, set the Keycloak URL to a reachable LAN host.

## Troubleshooting

- **The "Forgot your password?" link is missing (web).** Confirm the dashboard
  is running the build that includes PR #212 and that
  `buildResetCredentialsUrl()` has `KEYCLOAK_URL` / `KEYCLOAK_REALM` set in
  `frontend/.env.local`.
- **Tapping the link does nothing (mobile).** The device cannot open the browser
  or the Keycloak URL is not reachable from that device.
- **No mail appears in Mailpit.** Check that `mailpit` and `keycloak` are up and
  that the realm SMTP config still points to `mailpit:1025`.
- **The reset link is expired.** The realm uses a 15-minute
  `actionTokenGeneratedByUserLifespan`; request a new mail and retry.
- **The new password works on web but not mobile (or vice versa).** Confirm both
  clients still target the same Keycloak realm and backend stack.
