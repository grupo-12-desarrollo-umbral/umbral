# Identity Access Service Alignment Findings

Date: 2026-07-06

## Question

Validate whether the current GitHub issues align with this intended responsibility split for `backend/services/identity-access-service/`:

- Keycloak is the identity provider.
- The service should delegate authentication and identity-provider concerns to Keycloak.
- The local database should keep only the minimum application-side user data and must not store passwords.
- When roles or activation state change, the change should be reflected in both Keycloak and the local database.

## Canon and code checked

- [backend/services/identity-access-service/README.md](/home/samu/Desktop/umbral/backend/services/identity-access-service/README.md:3)
- [backend/services/identity-access-service/CONTEXT.md](/home/samu/Desktop/umbral/backend/services/identity-access-service/CONTEXT.md:3)
- [backend/services/identity-access-service/src/Domain/Entities/User.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Domain/Entities/User.cs:8)
- [backend/services/identity-access-service/src/Application/Users/Commands/AssignUserRole/AssignUserRoleCommandHandler.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Application/Users/Commands/AssignUserRole/AssignUserRoleCommandHandler.cs:11)
- [backend/services/identity-access-service/src/Application/Users/Commands/DeactivateUser/DeactivateUserCommandHandler.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Application/Users/Commands/DeactivateUser/DeactivateUserCommandHandler.cs:10)
- [backend/services/identity-access-service/src/Infrastructure/Identity/Keycloak/KeycloakAdminService.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Infrastructure/Identity/Keycloak/KeycloakAdminService.cs:10)
- [backend/services/identity-access-service/src/Api/Controllers/TeamsController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/TeamsController.cs:18)
- [backend/services/identity-access-service/src/Api/Controllers/SessionsController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/SessionsController.cs:9)
- [backend/services/identity-access-service/src/Api/Controllers/JoinTokensController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/JoinTokensController.cs:8)
- [backend/services/identity-access-service/src/Api/Controllers/PermissionsController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/PermissionsController.cs:11)
- GitHub issues:
  - <https://github.com/grupo-12-desarrollo-umbral/umbral/issues/81>
  - <https://github.com/grupo-12-desarrollo-umbral/umbral/issues/82>
  - <https://github.com/grupo-12-desarrollo-umbral/umbral/issues/83>
  - <https://github.com/grupo-12-desarrollo-umbral/umbral/issues/84>

## Findings

### 1. The service already avoids password storage

The current `User` aggregate stores only:

- `ExternalIdentityId`
- `DisplayName`
- `Email`
- `Role`
- `IsActive`

See [User.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Domain/Entities/User.cs:19).

That is aligned with the goal of using Keycloak as the identity provider and not duplicating password storage in the service.

### 2. The current implementation does not reliably keep Keycloak and the local DB in sync

Role changes currently:

- update the local DB first
- then call Keycloak sync

See [AssignUserRoleCommandHandler.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Application/Users/Commands/AssignUserRole/AssignUserRoleCommandHandler.cs:31).

The Keycloak sync swallows failures and only logs them, which allows silent drift between the DB role and the Keycloak role:

- [KeycloakAdminService.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Infrastructure/Identity/Keycloak/KeycloakAdminService.cs:26)

Deactivation currently only updates local state and does not propagate to Keycloak:

- [DeactivateUserCommandHandler.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Application/Users/Commands/DeactivateUser/DeactivateUserCommandHandler.cs:26)

This is not aligned with the intended rule that role and active-state changes should update both stores.

### 3. The canon defines a broader responsibility than “thin Keycloak delegation”

The current service is documented and implemented as the `Identity` bounded context, not just a Keycloak adapter. In addition to user projection and access facts, it currently owns:

- team registration and maintenance
- participant-to-team assignment
- session-to-team association
- join-token issuance and validation
- participant membership access checks

References:

- [README.md](/home/samu/Desktop/umbral/backend/services/identity-access-service/README.md:9)
- [CONTEXT.md](/home/samu/Desktop/umbral/backend/services/identity-access-service/CONTEXT.md:33)
- [TeamsController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/TeamsController.cs:22)
- [SessionsController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/SessionsController.cs:13)
- [JoinTokensController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/JoinTokensController.cs:14)
- [PermissionsController.cs](/home/samu/Desktop/umbral/backend/services/identity-access-service/src/Api/Controllers/PermissionsController.cs:26)

If the intended target is “identity-access-service should mainly delegate to Keycloak and keep only minimal user data,” then the current service boundary is wider than that target.

## Issue alignment assessment

As checked on 2026-07-06, the repo has four open issues relevant to this service:

- `#81` role sync to Keycloak is fire-and-forget
- `#82` user deactivation is not propagated to Keycloak
- `#83` `IdentityProviderSession` lifecycle is modeled but not wired
- `#84` Keycloak options / design-time DB defaults mask misconfiguration

Assessment:

- `#81` is aligned with the intended architecture.
- `#82` is aligned with the intended architecture.
- `#84` is indirectly aligned because it makes Keycloak delegation failures loud instead of silent.
- `#83` is neutral. It is a scope decision about whether provider-session tracking belongs in this service.

## Missing tracker item

There is no GitHub issue that explicitly asks whether the service boundary itself should be narrowed.

If the intended future state is:

- Keycloak remains the source of authentication truth
- the local DB remains minimal
- identity-access-service should not keep growing domain ownership outside identity/access projection

then the missing tracker work is a boundary-realignment issue covering whether these responsibilities should stay here:

- teams
- participant membership
- session/team association
- join tokens

## Recommended next decision

Pick one of these explicitly:

1. Keep the current broader `Identity` bounded-context scope and fix only the Keycloak-sync reliability gaps (`#81`, `#82`, `#84`).
2. Narrow the service toward a thinner Keycloak-backed identity/profile service and open a new issue to realign or move the extra team/join responsibilities.

## 2026-07-06 realignment target (team membership)

`ParticipantSessionMembershipPolicy` (participant-team lock via `ParticipantLockedToAnotherSessionTeamException`), `Team.AssignParticipant` / `TeamMembership.Assign`, `SessionTeamAssociation`, and `JoinToken` in `identity-access-service` implement the **rejected mandate/lock** team model and live in the wrong context. They are the concrete move-and-rewrite targets for the `Users` ↔ `SessionOperations` split:

- Whitelist `RegisteredTeamMembership` (may-join, not must-join) stays in `Users`.
- Live team assignment, `Open Team Selection`, capacity, and the pre-start freeze move to `SessionOperations`.
- Note the naming collision: code `TeamMembership` is a live *assignment* (has `AssignedAt` + `ParticipantAssignedToTeamEvent`), while the new glossary `RegisteredTeamMembership` is a pre-authorization whitelist — same word, opposite meaning.

## Bottom line

The current GitHub issues are only partially aligned with the stated target.

- They do cover the most obvious Keycloak synchronization defects.
- They do not cover the larger scope mismatch between the current service boundary and the desired “delegate to Keycloak + keep minimal user state” interpretation.
