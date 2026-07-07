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

**AccessToken**:
An infrastructure or identity-provider artifact used to support authentication and request propagation. It is not a standalone Umbral domain entity.
_Avoid_: domain token, business credential

**Access Facts**:
The identity-side facts returned after authentication, such as actor identity, role, token validity, and coarse access-policy results. `Access Facts` inform admission but do not decide it.
_Avoid_: final authorization, join decision, session approval

**Team**:
The Identity-side reference-data team catalog used for pre-session registration and membership facts. It is not the runtime team owned by `SessionOperations`.
_Avoid_: live team state, score holder, session-local aggregate

**SessionTeamAssociation**:
The minimal Identity-owned link between an opaque `LiveSessionId` and a reference-data `Team`, used to scope participant lobby discovery and enforce one active team membership per participant inside a session. It is an access-language index, not runtime session state.
_Avoid_: live team roster, runtime room membership, final session authority

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
`Identity` may validate actor identity, role, token status, coarse access-policy conditions for a requested target, and the session-scoped team facts needed to render a participant lobby (`mine` / `joinable` / `locked`). It may also enforce the "at most one active team membership per participant per session" rule. It still does not decide whether a specific live session may be joined right now; that decision belongs to `SessionOperations`.
_Avoid_: final admission, join approval

**Team administration**:
`Identity` treats team registration and participant-to-team assignment as operator-facing backoffice capabilities. `Administrator` and `Operator` may register teams and assign participants; broader lifecycle changes remain explicit per use case.
_Avoid_: assuming every team mutation is operator-enabled without a recorded decision

## Required Patterns

**Proxy**:
Role and policy-based access guards should behave as proxies in service and presentation layers, restricting protected capabilities before the underlying operation executes.
_Avoid_: leaking raw authorization conditionals into every use case or transport entry point

## Example Dialogue

Dev: "The participant authenticated successfully. Can Identity admit them into Team Red?"

Domain expert: "No. Identity can return the participant's identity, role, and token validity, but Session Operations decides whether Team Red may still be joined in this live session."
