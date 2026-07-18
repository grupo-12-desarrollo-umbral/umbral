# Users Service

`users-service` realizes the `Users` bounded context. It owns user, role, provider-session, and access-language concerns while delegating authentication to external identity infrastructure.

## Language

### Users

**Users**:
The supporting bounded context that owns user records and returns `Access Facts` for downstream decisions. It does not own the final admission decision for entering a live session.
_Avoid_: Identity, session runtime owner, join authority

**User**:
The aggregate root that represents an actor identity recognized by the platform and used for role assignment and access-policy evaluation.
_Avoid_: account record, auth principal

**Role**:
The authorization concept that classifies what kind of platform capabilities a `User` may access.
_Avoid_: permission set, profile type

**AccessToken**:
An infrastructure or identity-provider artifact used to support authentication and request propagation. It is not a standalone Umbral domain entity.
_Avoid_: domain token, business credential

**Access Facts**:
The identity-side facts returned after authentication, such as actor identity, role, token validity, and coarse access-policy results. `Access Facts` inform admission but do not decide it.
_Avoid_: final authorization, join decision, session approval

**RegisteredTeam**:
The Users-owned team catalog entry created by administrators or operators before a live session exists. It is reference data that may later be selected for session participation.
_Avoid_: live team state, session-local team, score holder

**RegisteredTeamMembership**:
The Users-owned authorization roster entry that states a `Participant` is explicitly pre-authorized to join a specific `RegisteredTeam`. It is an eligibility whitelist, not an attendance mandate: it says which teams a participant _may_ join, never that they _must_ join one. When memberships exist for a session's attached teams, the participant's pre-start choice is restricted to that authorized set; whether they must end up on a team is a `SessionOperations` decision, not a `Users` one.
_Avoid_: live-session roster, session participant, guaranteed admission, must-join mandate, forced assignment

**Post-Login Provisioning**:
The step that follows a successful Keycloak login where the client explicitly calls `users-service` to synchronize or create the application-side `User` record from the Keycloak-issued claims. This is what `AuthenticateUser` does — it is not a re-implementation of login, it is the application-side onboarding step that makes the actor known to the platform.
_Avoid_: re-authenticating with Keycloak, duplicating the login flow

## Boundary Rules

**Authentication**:
Authentication is externalized to `Keycloak`. The `api-gateway` validates every inbound Keycloak JWT and forwards identity as three trusted headers (`X-User-Id`, `X-User-Role`, `X-User-Email`); the original JWT is stripped. Individual services read these headers only — they do not validate tokens independently. After a successful Keycloak login, clients must complete `Post-Login Provisioning` before accessing protected capabilities.
_Avoid_: session admission, runtime ownership, per-service JWT validation, re-implementing login

**Access Validation**:
`Users` may validate actor identity, role, token status, coarse access-policy conditions for a requested target, and explicit pre-assignment through `RegisteredTeamMembership`. It does not own temporary pre-start open-team choice rules for a specific live session.
_Avoid_: final admission, join approval, session-scoped participation rules

**User Deactivation**:
The Users-owned access fact that a `User` is no longer allowed to participate on the platform. Downstream runtime contexts must treat it as immediately blocking further participation when they receive the deactivation fact.
_Avoid_: soft runtime warning, join-only restriction

**Team administration**:
`Users` treats registered-team administration and explicit participant pre-assignment as operator-facing backoffice capabilities. `Administrator` and `Operator` may register teams and manage `RegisteredTeamMembership`; session participation and join flows belong elsewhere.
_Avoid_: session team management, join-flow ownership

## Required Patterns

**Proxy**:
Role and policy-based access guards should behave as proxies in service and presentation layers, restricting protected capabilities before the underlying operation executes.
_Avoid_: leaking raw authorization conditionals into every use case or transport entry point

## Example Dialogue

Dev: "The participant authenticated successfully. Can Users admit them into Team Red?"

Domain expert: "No. Users can return the participant's identity, role, and token validity, but Session Operations decides whether Team Red may still be joined in this live session."
