# ADR-0007: Application Database is the Source of Truth for User Roles

The Keycloak JWT was overwriting the user's role on every login (via `IdentityProvisioningPolicy.SynchronizeOrCreate`), making admin dashboard role changes invisible until a code change was deployed. We made the application database the role authority by removing the role-overwrite on login and instead syncing outward: the admin dashboard writes to the app DB, and the backend propagates the change to Keycloak via the Admin API. The frontend session cookie is refreshed on dashboard load via a server action so stale-role windows are at most one page navigation.

**Status:** Accepted

**Consequences:** Keycloak JWTs may lag behind the app DB by up to the token lifetime (typically 5-60 minutes). A `refreshSession` server action narrows this on the frontend but the gateway still routes based on the JWT claim. If tight role enforcement at the gateway is required, either the token lifetime must be shortened or the gateway must query the service on each request.
