# HU-04 Context — Team Registration and Maintenance

> Paste this section into any agent session that needs context for HU-04.
> Last updated: 2026-05-31 | Branch: `feature/hu-04-team-registration`

## State

- DES-8 (HU-04): **In Progress**, labels: `Feature`, `ready-for-agent`, `svc:identity-access-service`
- DES-7 (HU-03): **In Progress**
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`
- Branch: `feature/hu-04-team-registration` (branch from `develop` after HU-03 merges, or from `feature/hu-03-role-permission-assignment` if not yet merged)

## What HU-01, HU-02, and HU-03 have already landed (reuse candidates for HU-04)

All of this is on `feature/hu-03-role-permission-assignment` (or `develop` once HU-03 merges).

**Domain layer**
- `User` aggregate root — `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; `Deactivate()` prevents protected access for inactive users
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `ProtectedCapability` enum + permission matrix (`AuthenticatedPlatformAccess`, `AdministratorPanel`, `OperatorPanel`, `ParticipantExperience`)
- `AccessPolicy` domain service — single authority for role-to-capability evaluation and inactive-user denial
- `IdentityProvisioningPolicy` — synchronizes or creates application-side `User` records from trusted claims
- `IdentityProviderSession` entity with revocation/expiry traceability
- Domain events already in play: `UserProvisioned`, `UserRoleAssigned`, `UserAccessDeactivated`, `AccessDecisionRecorded`, `IdentityProviderSessionStarted`, `IdentityProviderSessionEnded`

**Application layer**
- `AuthenticateUserCommand` / handler (post-login provisioning, deactivated-user rejection)
- `DeactivateUserCommand` / handler (Administrator-only)
- `AssignUserRoleCommand` / handler (Administrator-only, syncs role changes back to Keycloak)
- `GetUsersQuery` / handler (paginated catalog, Administrator or Operator)
- `GetAuthenticatedActorProfileQuery` + handler
- `CheckProtectedCapabilityAccessQuery` + handler
- `AuthorizationBehaviour`, `ValidationBehaviour`, `PerformanceBehaviour`, `UnhandledExceptionBehaviour`
- `ICurrentUser` / `GatewayRoleParser` (trusted-header identity)

**Infrastructure / API**
- EF Core `users` + `identity_provider_sessions` tables; repository support for user catalog and role/access lifecycle
- Endpoints already exposed: `POST /api/users/authenticated`, `GET /api/users/me`, `GET /api/users`, `DELETE /api/users/{id}/access`, `PATCH /api/users/{id}/role`, `GET /api/permissions/authenticated-platform-access`
- `ProblemDetailsExceptionHandler` already maps common validation/auth/not-found failures for the user and access flows

**Coverage:** 94.95% total line before HU-04/HU-05 expansion; threshold is 95% aggregate line coverage, so HU-04 API/integration work must close the gap rather than assume it is already passing.

## What HU-04 adds on top (per PRD DES-67)

| Concern | New work |
|---|---|
| Team registry | Introduce an Identity-side `Team` aggregate: `TeamId`, `DisplayName`, `TeamCode`, `IsActive`, `CreatedAt`, `UpdatedAt` |
| Team lifecycle | `Register(displayName, teamCode)`, `UpdateDetails(displayName, teamCode)`, `Deactivate()` with explicit invariant choice for already-inactive teams |
| Domain events | Add `TeamRegisteredEvent`, `TeamDetailsUpdatedEvent`, `TeamDeactivatedEvent` |
| Application use cases | `RegisterTeamCommand`, `UpdateTeamCommand`, `DeactivateTeamCommand`, `GetTeamsQuery`, `GetTeamByIdQuery` |
| Persistence | Add `teams` table, unique `TeamCode` index, `ITeamRepository`, and `AddTeams` migration |
| API endpoints | `POST /api/teams`, `GET /api/teams`, `GET /api/teams/{id}`, `PATCH /api/teams/{id}`, `DELETE /api/teams/{id}/status` |
| Authorization | `RegisterTeam` is Administrator/Operator; `UpdateTeam` and `DeactivateTeam` remain Administrator-only unless widened by a later decision; reads are Administrator/Operator |
| Historical traceability | Deactivation is soft: record remains queryable with `IsActive=false` |

## Touched surfaces

- `backend/` identity-access-service domain, application, infrastructure, and API layers
- API contract boundary: new `/api/teams` request/response/error shapes consumed by downstream clients
- Database schema surface: new `teams` table and `AddTeams` migration

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- The `Team` introduced in HU-04 is not the runtime `Team` owned by `session-operations-service`. In Identity it is team reference data only; never add runtime fields like score, join status, or progress.
- PRD DES-67 allows this only as an explicit bounded-context contract/projection. Treat HU-04 as controlled administrative ownership for registration data, not a second owner of live session state.
- `TeamCode` uniqueness is part of the application and database contract. Validate for duplicates in handlers and enforce it again with a unique index in persistence.
- Deactivation must preserve the row. Acceptance depends on re-fetching the team after `DELETE /api/teams/{id}/status` and observing `IsActive=false`.
- Coverage was already below threshold before HU-04 started. Phase X.4 must run the merged unit + integration suite and clear the aggregate ≥95% gate.
