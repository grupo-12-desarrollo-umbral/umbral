# Issue #148 — Manual Test: administrator user management + invite operator UI

Goal: an **Administrator** opens the dashboard Users panel, invites a new
Operator/Administrator by email (no password set here — the invitee sets their
own), sees the invitation as a **pending** account, and can change an existing
user's role. The Users view is **Administrator-only** — operators cannot see the
**Users** nav item or reach the panel.

Everything is web-only: operator app on `localhost:3000`, gateway on
`localhost:8000`, Keycloak on `localhost:8080`, and Mailpit (mail catcher) on
`localhost:8025` — the invitation email lands there, it never leaves the machine.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak + Mailpit
cd backend && docker compose up -d

# operator web app
cd ../frontend && pnpm install && pnpm dev       # -> http://localhost:3000
```

No seed is needed — global-setup already seeds the admin/operator accounts below.

## 2. Credentials

| Who           | Where                | Login                    |
| ------------- | -------------------- | ------------------------ |
| Administrator | web `localhost:3000` | `admin-1` / `admin123`   |
| Operator      | web `localhost:3000` | `op-1` / `operator123`   |
| Mail catcher  | `localhost:8025`     | Mailpit web UI (no auth) |

## 3. Run it

### Step A — Administrator: open the Users panel

1. Sign in as **admin-1 / admin123**.
2. Click **Users** in the sidebar (`nav-users`). The panel (`users-panel`) shows
   the user list **and** an **Invite a user** form (`invite-user-form`).

### Step B — Invite a new operator

1. In the form, enter a **fresh email** (e.g. `new.op@umbral.local`) in
   `invite-email-input`, leave the role select (`invite-role-select`) on
   **Operator**, and click **Send invitation** (`invite-submit`).
2. Expect a success notice (`invite-notice`): _"Invitation sent to … pending
   invitation …"_. **No password field appears anywhere in the form.**
3. The list re-reads. The invitee appears with name **"Pending sign-in"** and a
   **Pending invitation** status chip (`user-status-<id>`, tone amber). On a
   large user base it may be on a later page — see _Known limitations_.
4. Open **Mailpit** (`localhost:8025`): the Keycloak invitation email
   ("Update Your Account" action link) is there — that link is where the invitee
   sets their own password.

### Step C — Verify pending → active transition (optional)

1. Open the Mailpit link, complete the account (set a password) as the invitee.
2. Back on the admin Users panel, click **Next/Previous** (or re-open the tab)
   to re-fetch. The account's chip flips from **Pending invitation** to
   **Active** once its display name is set at first sign-in.

### Step D — Change an existing user's role

1. On any active row, click **Change role** (`change-role-btn-<id>`), pick a new
   role in `role-select-<id>`, click **Save** (`save-role-btn-<id>`).
2. The row's Role cell updates immediately (optimistic), reflecting the
   `PATCH /api/users/{id}/role` result.

### Step E — Operator cannot reach the Users view

1. Sign out, sign in as **op-1 / operator123**.
2. There is **no Users item in the sidebar** (`nav-users` is absent) and **no
   users panel** (`users-panel`). The Users view is Administrator-only; operators
   cannot view or manage users.

## 4. Quick extra checks (optional)

| Check                | How                                                        | Expect                                                                    |
| -------------------- | --------------------------------------------------------- | ------------------------------------------------------------------------- |
| Only invitable roles | Open `invite-role-select`                                 | Exactly **Operator** and **Administrator** — no Participant               |
| Duplicate email      | Invite an email that already exists                       | Readable error in `invite-error`: "A user with this email already exists" |
| Invalid email        | Invite `not-an-email` and submit                          | Readable validation error in `invite-error` (from `problem+json`)         |
| Server-side guard    | As op-1, call the `inviteUser` action path                | Rejected — the action re-checks Administrator, not just the hidden nav item |

## 5. Known limitations (by design — not bugs)

- **Pending is derived, not a stored flag.** The catalog DTO has no status
  field; a "pending invitation" is inferred as an active account whose display
  name still equals its email (the backend uses the email as a placeholder until
  first sign-in). Robust for real invitations; tracked for a proper backend
  status field as a follow-up.
- **A new invite may not be on page 1.** After invite the panel re-fetches
  page 1, but the list is ordered by display name, so a fresh invitation (name =
  email) can sort onto a later page. The success notice says as much — page
  through to find it, or filter by the email you entered.
- **Invite requires Keycloak SMTP.** The account is created email-first: if the
  invitation email fails to send, the backend rolls the account back and the
  invite errors. The dev stack points Keycloak at Mailpit, so this works locally
  — but the mail must be reachable for the invite to succeed.

## 6. Troubleshooting

- **No invite form as admin.** Confirm you signed in as `admin-1` (role chip
  reads `admin`), not an operator. The form is gated to Administrators.
- **Invite fails with an error even for a fresh email.** Keycloak couldn't send
  the mail — check the `mailpit` container is up (`docker compose ps`) and the
  realm's `smtpServer` points at it (`host: mailpit`, `port: 1025`).
- **Invitee never becomes Active.** They haven't opened the Mailpit link / signed
  in yet; the account stays pending (name = email) until first sign-in.
- **Can't reach Keycloak/Mailpit.** They're on `localhost:8080` / `localhost:8025`
  on the same machine as the stack; use the host, not a container name.
