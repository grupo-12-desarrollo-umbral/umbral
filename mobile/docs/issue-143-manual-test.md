# Issue #143 — Manual Test: participant self-registration from the mobile app

Goal: a new person taps **Create an account** on the mobile login screen, fills
Keycloak's **hosted** registration form, verifies their email (via Mailpit), then
**signs in** and lands in the participant app — arriving with exactly the
**Participant** realm role and no way to reach an operator surface.

You need the backend stack up (gateway `localhost:8000`, Keycloak `localhost:8080`,
Mailpit `localhost:8025`) and the mobile app on a phone/emulator pointed at that
gateway.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak + Mailpit — re-import the realm so the new
# registrationAllowed / default-role config is picked up
cd backend && docker compose down -v && docker compose up -d

# mobile app (separate terminal)
cd ../mobile && pnpm install && pnpm start        # open in Expo Go / emulator
```

> `down -v` wipes Keycloak's volume so `--import-realm` re-reads
> `umbral-realm.json`. Skip it and an already-imported realm keeps the old
> `registrationAllowed: false`.

## 2. What the realm now does (context)

`backend/deploy/keycloak/import/umbral-realm.json` turns on hosted registration and
pins the default role:

- `registrationAllowed: true` — the hosted login page shows a **Register** link.
- `registrationEmailAsUsername: true` — one identifier; the form has no username field.
- `verifyEmail: true` — a verification mail is sent; sign-in is blocked until verified.
- default role `default-roles-umbral` composes **exactly `Participant`** (plus the
  baseline `offline_access` / `uma_authorization` / account roles) — never `Operator`
  or `Administrator`.

## 3. Credentials

| Who              | Where                          | Login                      |
| ---------------- | ------------------------------ | -------------------------- |
| New participant  | mobile app (you create it)     | `you@example.com` / choose |
| Keycloak admin   | console `localhost:8080`       | `admin` / `admin`          |
| Mailpit inbox    | web UI `localhost:8025`        | (no login)                 |

## 4. Run it

### Step A — Mobile: open the hosted registration

1. Launch the app → the **login** screen.
2. Tap **New here? Create an account** → the system browser opens Keycloak's hosted
   **Register** form.
3. Confirm the form asks for **email, password, first name, last name** — and **no
   role field of any kind**.

### Step B — Register

1. Fill the form with a fresh email (e.g. `tester@umbral.local`) and submit.
2. Keycloak shows an **email-verification required** page and (after verifying)
   redirects to `http://localhost/` — a blank page on a phone is expected; you return
   to the app manually.

### Step C — Verify the email (Mailpit)

1. Open **`http://localhost:8025`** → the inbox has a **Verify email** message from
   `no-reply@umbral.local`.
2. Open it, click the **verification link** → Keycloak confirms "Your email address
   has been verified."

### Step D — Sign in on mobile

1. Back in the app's **login** screen, enter the **email + password** you just
   registered and tap **Sign in**.
2. You land in the participant app (the "you're in" team space) — the first sign-in
   provisioned a local user record via `POST /api/users/authenticated`.

That round-trip — create account → verify → sign in → provisioned as a Participant —
is issue #143 working. ✅

## 5. Security check (the point of the issue)

Prove the self-registered account is a Participant and nothing more.

**Keycloak console:** `localhost:8080` → admin/admin → realm **umbral** → **Users** →
your new user → **Role mapping**. Assigned role is `default-roles-umbral`; its
**effective** realm roles include `Participant` and **not** `Operator`/`Administrator`.

**API (optional):** the gateway must resolve this multi-role token to `Participant`.

```bash
# token for the new user (direct grant, umbral-mobile)
TOKEN=$(curl -s -X POST \
  "http://localhost:8080/realms/umbral/protocol/openid-connect/token" \
  -d grant_type=password -d client_id=umbral-mobile \
  -d username=tester@umbral.local -d password=<your-pw> \
  -d scope='openid profile email' | jq -r .access_token)

# provisioning through the gateway succeeds and reports role Participant
curl -s -X POST "http://localhost:8000/api/users/authenticated" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"displayName":"Tester"}' | jq '.actor.role'      # -> "Participant"
```

The token carries `realm_access.roles` in **no guaranteed order** (Participant sits
among the baseline roles); the gateway still forwards `X-User-Role: Participant`, so
provisioning never fails and no operator-gated route is reachable.

## 6. Quick extra checks (optional)

| Check                 | How                                                              | Expect                                                              |
| --------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------ |
| Blocked until verified| Try **Sign in** on mobile *before* clicking the Mailpit link     | Sign-in fails — the account isn't verified yet                     |
| No role selection     | Inspect the hosted Register form                                 | Email / password / first / last only — **no** role field           |
| Email = username      | In the Keycloak console, open the new user                       | `username` equals the email; no separate username was asked        |
| Duplicate email       | Register again with the **same** email                           | Keycloak rejects it ("email already exists")                       |

## 7. Known limitations (by design — not bugs)

- **Verification mail relies on the SMTP wiring from #141 (closed, already in this
  branch).** The realm points Keycloak's SMTP at Mailpit, so mail lands in
  `localhost:8025` locally — this was verified end-to-end (mail delivered; pre-verify
  sign-in refused with "Account is not fully set up"; post-verify sign-in succeeds as
  Participant). The one step that stays manual is clicking the emailed link: Keycloak 26
  shows an anti-scanner "Click here to proceed" page that requires a real browser, so it
  can't be driven headlessly. In an environment without a mail sink, verification can't
  complete.
- **Registration redirects to `http://localhost/`.** That URI matches the
  `umbral-mobile` client but has nothing served on a device — it's just the post-verify
  landing. You return to the app and sign in with the native form; the app does not
  consume a registration redirect.
- **Login stays direct-grant.** The app authenticates with its own email/password form
  (HU-06). Only the **registration** step uses Keycloak's hosted browser page.

## 8. Troubleshooting

- **No "Register" link / "Create an account" opens a page without registration.** The
  realm still has `registrationAllowed: false` — you skipped `docker compose down -v`,
  so the old realm was reused. Re-run step 1 with `down -v`.
- **"Account is not fully set up" on sign-in.** The profile is incomplete (missing
  first/last name) or the email is unverified — finish the hosted form and click the
  Mailpit link.
- **No verification mail in Mailpit.** Confirm Mailpit is up (`localhost:8025`) and
  Keycloak started after it; check `docker compose logs keycloak` for SMTP errors.
- **Mobile can't reach Keycloak/gateway.** It targets `localhost:8080` / `localhost:8000`;
  on a physical device use your machine's LAN IP, not `localhost` (see `mobile/README.md`).
- **Provisioning returns a role other than Participant.** Should not happen after this
  issue — if it does, the gateway's `TrustedHeadersTransform` role resolution regressed.

---

## Appendix — what changed

- `backend/deploy/keycloak/import/umbral-realm.json` — `registrationAllowed`,
  `registrationEmailAsUsername`, and a `default-roles-umbral` composite pinned to
  `Participant`.
- `mobile/src/lib/auth/keycloak.ts` / `mobile/src/app/(auth)/login.tsx` — the
  `buildRegistrationUrl()` helper and the **New here? Create an account** link.
- `backend/api-gateway/src/Transforms/TrustedHeadersTransform.cs` — resolves the one
  application role from a multi-role Keycloak token (was: first entry, order-dependent),
  so a self-registered participant is never mis-read as `offline_access`.
