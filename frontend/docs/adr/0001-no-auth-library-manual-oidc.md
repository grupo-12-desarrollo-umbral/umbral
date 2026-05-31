# Manual Keycloak OIDC flow instead of an auth library

The frontend drives Keycloak authentication with a manual OIDC authorization-code flow (redirect → `/api/auth/callback` route handler) and `jose` for session cookie signing, rather than using NextAuth/Auth.js or a Keycloak adapter library. The auth boundary is already well-defined: Keycloak issues the JWT, the api-gateway validates it and injects trusted headers, and the frontend only needs to redirect the user in and exchange the authorization code for an access token once. That is a small surface that does not justify pulling in a library that brings its own session model, database adapters, and provider abstractions on top of an already-opinionated stack. NextAuth in particular has a history of breaking changes on Next.js major upgrades; avoiding it keeps this boundary stable.

## Considered Options

- **NextAuth / Auth.js** — ruled out because it imposes its own session model (JWT or database), adds ~40 kB to the server bundle, and frequently lags Next.js major releases.
- **Keycloak JS adapter (`keycloak-js`)** — client-side only; incompatible with server-side session management and the `HttpOnly` cookie requirement.
- **Manual OIDC + `jose`** — chosen. Small surface, no transitive dependencies, edge-runtime-compatible.
