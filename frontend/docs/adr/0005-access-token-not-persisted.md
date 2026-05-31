# Keycloak access token is held in memory only during the callback handler

The Keycloak access token is used exactly once — in the `/api/auth/callback` route handler to call `POST /api/users/authenticated` via the api-gateway — and then discarded. It is never written to the session cookie, local storage, or any server-side store. The session cookie carries only the provisioned user profile (`role`, `isActive`, `displayName`, `email`, `externalIdentityId`). Persisting the access token would create a second, longer-lived copy of a credential that Keycloak already manages with its own lifecycle. If the token were stored in the cookie it would exceed the 4 KB cookie limit for longer JWTs, and if stored server-side it would require the session store ruled out in ADR-0002. Subsequent API calls from the frontend that require the user's identity will rely on the api-gateway re-authenticating via the trusted-header contract, not on the frontend re-presenting the original token.

## Consequences

Token refresh and silent re-authentication are out of scope for HU-01. If a use case arises where the frontend must make authenticated api-gateway calls directly from the browser (rather than through Next.js Server Actions or Route Handlers), this decision must be revisited.
