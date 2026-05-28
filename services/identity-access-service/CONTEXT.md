# Identity Access Service

`identity-access-service` realizes the `Identity` bounded context. It owns identity, role, provider-session, and access-language concerns while delegating authentication to external identity infrastructure.

## Language

### Identity

**Identity**:
The supporting bounded context that authenticates actors and returns `Access Facts` for downstream decisions. It does not own the final admission decision for entering a live session.
_Avoid_: session runtime owner, join authority

**User**:
The aggregate root that represents an actor identity recognized by the platform and used for role assignment and access-policy evaluation.
_Avoid_: account record, auth principal

**Role**:
The authorization concept that classifies what kind of platform capabilities a `User` may access.
_Avoid_: permission set, profile type

**IdentityProviderSession**:
The persisted `Identity` concept that records the subset of external identity-provider session state Umbral must reason about for traceability, revocation, correlation, or policy enforcement. It is domain-relevant session language, not a purely infrastructural artifact.
_Avoid_: optional session model, infrastructure-only login state

**AccessToken**:
An infrastructure or identity-provider artifact used to support authentication and request propagation. It is not a standalone Umbral domain entity.
_Avoid_: domain token, business credential

**Access Facts**:
The identity-side facts returned after authentication, such as actor identity, role, token validity, and coarse access-policy results. `Access Facts` inform admission but do not decide it.
_Avoid_: final authorization, join decision, session approval

**JoinToken**:
The limited-scope token owned by `Identity` that proves a participant may enter a specific `LiveSession` and `Team` through the approved join flow, referencing them only as authorization targets. Participants must already hold a valid Keycloak JWT before a `JoinToken` can be consumed; the gateway validates the JWT first, and `identity-access-service` validates the `JoinToken` as a subsequent application-level guard.
_Avoid_: invite token, entry token, team join token, unauthenticated join

**Post-Login Provisioning**:
The step that follows a successful Keycloak login where the client explicitly calls `identity-access-service` to synchronize or create the application-side `User` record from the Keycloak-issued claims. This is what `AuthenticateUser` does — it is not a re-implementation of login, it is the application-side onboarding step that makes the actor known to the platform.
_Avoid_: re-authenticating with Keycloak, duplicating the login flow

## Boundary Rules

**Authentication**:
Authentication is externalized to `Keycloak`. The `api-gateway` validates every inbound Keycloak JWT and forwards identity as three trusted headers (`X-User-Id`, `X-User-Role`, `X-User-Email`); the original JWT is stripped. Individual services read these headers only — they do not validate tokens independently. After a successful Keycloak login, clients must complete `Post-Login Provisioning` before accessing protected capabilities.
_Avoid_: session admission, runtime ownership, per-service JWT validation, re-implementing login

**Access Validation**:
`Identity` may validate actor identity, role, token status, and coarse access-policy conditions for a requested target, but it does not decide whether a specific live session may be joined right now. This decision belongs to `SessionOperations`.
_Avoid_: final admission, join approval

## Example Dialogue

Dev: "The participant authenticated successfully. Can Identity admit them into Team Red?"

Domain expert: "No. Identity can return the participant's identity, role, and token validity, but Session Operations decides whether Team Red may still be joined in this live session."
