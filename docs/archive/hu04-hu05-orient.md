# HU-04 / HU-05 Orientation

Source documents:
- `backend/services/identity-access-service/README.md`
- `backend/docs/prd/DES-67-primera-implementacion-de-identity-access-service-hu-01-a-hu-08.md`

## What already landed in HU-01 / HU-02 / HU-03

### HU-01

The current `identity-access-service` already includes the first authentication and provisioning slice:

- `User` aggregate with `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`
- `IdentityProviderSession` for provider-session traceability and revocation support
- `AuthenticateUserCommand` for post-login provisioning
- `IdentityProvisioningPolicy` to create or synchronize the application-side `User`
- trusted-header flow from `api-gateway` using `X-User-Id`, `X-User-Role`, and `X-User-Email`
- `AccessPolicy` and protected capability checks for coarse-grained access control

### HU-02

The access-management slice is already present:

- user catalog via `GetUsersQuery` and `GET /api/users`
- soft access deactivation via `DeactivateUserCommand` and `DELETE /api/users/{id}/access`
- deactivated users are denied protected access even if they still hold a valid token

### HU-03

The role and permission slice is already present:

- explicit `Role` model: `Administrator`, `Operator`, `Participant`
- `AccessPolicy` for role-to-capability evaluation
- admin role assignment via `AssignUserRoleCommand` and `PATCH /api/users/{id}/role`
- synchronization of assigned roles back to Keycloak

## What HU-04 adds per PRD

HU-04 adds CRUD-style administration for participating teams:

- create, query, edit, and deactivate teams
- keep a team identifier and basic operational data
- preserve historical traceability when a team is deactivated
- allow only active teams to be associated with new sessions

Important PRD boundary:

- `Identity` must not become the owner of `Team` or `TeamMember`
- if administrative team registration is needed here, it must happen through an explicit contract or controlled projection from `SessionOperations`, not duplicate ownership

## What HU-05 adds per PRD

HU-05 adds participant-to-team assignment and membership checks:

- associate registered participants to active teams
- persist the participant-team link
- query the members assigned to each team
- prevent participants from accessing a team context that does not belong to them

Important PRD boundary:

- membership validation can feed `Access Facts` and later `JoinToken`-based checks
- final runtime admission still belongs to `SessionOperations`

## Linear status

### DES-8

- Status: `In Progress`
- Labels: `svc:identity-access-service`, `Feature`, `ready-for-agent`

### DES-9

- Status: `Backlog`
- Labels: `svc:identity-access-service`, `Feature`
