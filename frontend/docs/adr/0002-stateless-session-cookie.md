# Stateless encrypted session cookie, no server-side session store

After the Keycloak callback, the frontend stores `{ externalIdentityId, displayName, email, role, isActive }` in a `jose`-encrypted, `HttpOnly` cookie with a 7-day lifetime. There is no Redis, database table, or in-memory session store on the Next.js side. The cookie is the session. This is a deliberate choice for this sprint: the app has no persistent Next.js backend infrastructure today, adding a session store would require provisioning and wiring Redis or a Postgres table before a single login screen could ship, and the 7-day lifetime is short enough that the absence of server-side revocation is acceptable. Deactivation is enforced at the `proxy.ts` level on every request by checking `isActive` from the cookie payload; a deactivated user will be locked out as soon as their cookie expires or they attempt a new login.

## Consequences

If per-session server-side revocation is required before the cookie expires (e.g., an admin deactivates a user who is currently logged in), this decision must be revisited. The likely path is adding a short-lived Redis blocklist keyed on `externalIdentityId`. That is out of scope for HU-01.
