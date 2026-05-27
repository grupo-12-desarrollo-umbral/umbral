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
The limited-scope token owned by `Identity` that proves a participant may enter a specific `LiveSession` and `Team` through the approved join flow, referencing them only as authorization targets.
_Avoid_: invite token, entry token, team join token

## Boundary Rules

**Authentication**:
Authentication is externalized to `Keycloak` or another OIDC-compatible identity provider, but the `Identity` bounded context still owns the language and contracts Umbral uses around actor identity and access.
_Avoid_: session admission, runtime ownership

**Access Validation**:
`Identity` may validate actor identity, role, token status, and coarse access-policy conditions for a requested target, but it does not decide whether a specific live session may be joined right now. This decision belongs to `SessionOperations`.
_Avoid_: final admission, join approval

## Example Dialogue

Dev: "The participant authenticated successfully. Can Identity admit them into Team Red?"

Domain expert: "No. Identity can return the participant's identity, role, and token validity, but Session Operations decides whether Team Red may still be joined in this live session."
