# HU-05 Context — Participant-to-Team Assignment

> Paste this section into any agent session that needs context for HU-05.
> Last updated: 2026-05-31 | Branch: `feature/hu-05-participant-team-assignment`

## State

- DES-9 (HU-05): **Backlog**, labels: `Feature`, `svc:identity-access-service`
- DES-8 (HU-04): **In Progress**
- DES-67 (PRD): **Backlog**, labels: `ready-for-agent`, `svc:identity-access-service`
- Branch: `feature/hu-05-participant-team-assignment` (branch from `feature/hu-04-team-registration`, not `develop`; HU-05 depends on HU-04's `Team` aggregate and repository contract)

## What HU-01, HU-02, HU-03, and HU-04 have already landed (reuse candidates for HU-05)

HU-05 assumes the user/access stack from HU-01 through HU-03 plus the team registry from HU-04.

**Domain layer**
- `User` aggregate root with `Role` and `IsActive`
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `AccessPolicy` and protected capability matrix for coarse-grained access checks
- `IdentityProviderSession` and identity provisioning model from HU-01
- Identity-side `Team` aggregate from HU-04: `TeamId`, `DisplayName`, `TeamCode`, `IsActive`, `CreatedAt`, `UpdatedAt`
- HU-04 team lifecycle events and invariants (`TeamRegisteredEvent`, `TeamDetailsUpdatedEvent`, `TeamDeactivatedEvent`)

**Application layer**
- Existing user/admin flows: authenticate, deactivate access, assign role, list users, inspect actor profile, check protected capability access
- HU-04 team administration flows: `RegisterTeamCommand`, `UpdateTeamCommand`, `DeactivateTeamCommand`, `GetTeamsQuery`, `GetTeamByIdQuery`
- `IUserRepository` already available for loading users by id
- `ITeamRepository` introduced by HU-04 and extended in HU-05 if membership eager-loading is required

**Infrastructure / API**
- Existing user/access endpoints from HU-01 through HU-03
- HU-04 team endpoints: `POST /api/teams`, `GET /api/teams`, `GET /api/teams/{id}`, `PATCH /api/teams/{id}`, `DELETE /api/teams/{id}/status`
- HU-04 persistence surface: `teams` table plus `AddTeams` migration must exist before HU-05 infrastructure work continues

**Coverage:** Aggregate line coverage was 94.95% before the team-management slices. HU-05 API work must verify the merged suite still clears the ≥95% gate after membership paths are added.

## What HU-05 adds on top (per PRD DES-67)

| Concern | New work |
|---|---|
| Team membership model | Add Identity-side `TeamMembership` record: `TeamMembershipId`, `TeamId`, `UserId`, `AssignedAt` |
| Membership invariant | `Team.AssignParticipant(userId)` only succeeds for active teams and rejects duplicate assignments |
| Domain events | Add `ParticipantAssignedToTeamEvent` |
| Domain exceptions | Add `TeamNotActiveException` and `ParticipantAlreadyAssignedToTeamException` |
| Application use case | `AssignParticipantToTeamCommand` — Administrator/Operator; verifies team exists, user exists, and `user.Role == Participant` before assigning |
| Membership queries | `GetTeamParticipantsQuery` returning `TeamMembershipDto` / participant projections for a team |
| Persistence | Add `team_memberships` table plus `AddTeamMemberships` migration and repository loading with memberships included |
| API endpoints | `POST /api/teams/{id}/participants`, `GET /api/teams/{id}/participants` |
| Authorization boundary | HU-05 records who belongs to which team, but final runtime admission still belongs to HU-07 + `session-operations-service` |

## Touched surfaces

- `backend/` identity-access-service domain, application, infrastructure, and API layers
- API contract boundary: new team-participant assignment and listing endpoints under `/api/teams/{id}/participants`
- Database schema surface: new `team_memberships` table and `AddTeamMemberships` migration

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- `TeamMembership` in HU-05 is not the runtime `TeamMember` concept from `session-operations-service`. It is an authorization fact only.
- Branching is constrained: start HU-05 from `feature/hu-04-team-registration`, not `develop`, because HU-05 reuses HU-04's `Team` aggregate and repository contract.
- Migration ordering is a hard rule. Do not run `dotnet ef migrations add AddTeamMemberships` until HU-04's `AddTeams` migration is committed, gated, and this branch is rebased on top of it.
- Role validation belongs at the application layer unless explicitly redesigned. The domain method receives a `userId`; handlers must ensure the loaded user has `Role.Participant` and throw `UserNotParticipantRoleException` otherwise.
- Deactivating a team must preserve existing memberships historically; assignment to an inactive team must fail with `TeamNotActiveException`.
- HU-05 does not by itself authorize live team-context access. It establishes membership facts that HU-07/JoinToken enforcement will consume later; do not overstate current runtime guarantees.
