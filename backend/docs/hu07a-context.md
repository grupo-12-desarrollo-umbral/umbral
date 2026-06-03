# HU-07A Context — Participant Membership Validation in Session

> Paste this section into any agent session that needs context for HU-07A.
> Last updated: 2026-06-02 | Branch: `feature/hu-07a-participant-membership-validation`
>
> Phase-0 supersession note: HU-07A now expands on this branch to include the participant
> session → team-lobby → team-space flow. Treat any older "own-team-only" wording below as
> the original baseline, not the final branch scope. The current decision is:
> unassigned participants may self-select any team in the session and thereby self-assign;
> pre-assigned participants are locked to their existing team. Identity will add the
> minimal session-scoping surface via `GET /api/sessions/{id}/teams`.

## State

- DES-11 (HU-07A): labels expected `Feature`, `ready-for-agent`, `svc:identity-access-service` — **verify in Linear before driving** (no Linear MCP was available when this file was generated)
- DES-12 (HU-06, predecessor): **Done** (merged to `develop` via PR #13)
- DES-9 (HU-05), DES-8 (HU-04), DES-7 (HU-03), DES-6 (HU-02), DES-5 (HU-01): **Done**
- DES-67 (PRD): labels `ready-for-agent`, `svc:identity-access-service`
- Branch: `feature/hu-07a-participant-membership-validation`, base = **`develop`** (HU-06 is merged, so no predecessor branch chaining)

## Required design patterns

| Pattern | Owning phase | Why (from patterns matrix) | Concrete obligation |
|---|---|---|---|
| `Proxy` (mandated) | X.2 Application + X.4 API | "Membership validation also gates admission to the team's real-time hub/group." | Access is enforced through a guard — `AuthorizationBehaviour` / an authorization proxy / endpoint authorization policy — with **no ad-hoc role `if` checks** in handlers or endpoints. The conditional-self-select / locked-team rules are enforced through the guard, not an inline conditional. |

Transport note (NOT a design pattern, NOT a gate): the matrix tags HU-07A with **SignalR (DES-11)**, but per the PRD boundary rules `identity-access-service` does **not** build a SignalR hub. Identity issues/validates the `JoinToken` and returns access facts; `session-operations-service` + the mobile client own the real-time hub admission, reconnection, and sync. Do not scope a hub into this slice.

## What HU-01 through HU-06 have already landed (reuse candidates for HU-07A)

All of this is on `develop`.

**Domain layer**
- `User` aggregate root — `ExternalIdentityId`, `DisplayName`, `Email`, `Role`, `IsActive`; `Deactivate()`, `AssignRole()`
- `Role` enum: `Administrator`, `Operator`, `Participant`
- `ProtectedCapability` enum + matrix: `AuthenticatedPlatformAccess`, `AdministratorPanel`, `OperatorPanel`, `ParticipantExperience`
- `AccessPolicy` domain service — `EnsureCanAccess()` / `Evaluate()` → `AccessDecision`; single authority for the role/capability matrix
- `IdentityProvisioningPolicy`, `IdentityProviderSession` entity
- Identity-side reference-data `Team` aggregate (HU-04): `TeamId`, `DisplayName`, `TeamCode`, `IsActive`; `AssignParticipant(userId)` (HU-05)
- `TeamMembership` record (HU-05): `TeamMembershipId`, `TeamId`, `UserId`, `AssignedAt` — **the authorization fact HU-07A consumes**
- Domain events incl. `ParticipantAssignedToTeamEvent`, `AccessDecisionRecorded`, `UserProvisioned`, role/team events
- Domain exceptions incl. `UserNotParticipantRoleException`, `TeamNotActiveException`, `ParticipantAlreadyAssignedToTeamException`

**Application layer**
- `AuthenticateUserCommand`, `AssignUserRoleCommand`, `DeactivateUserCommand`
- `AssignParticipantToTeamCommand`, `GetTeamParticipantsQuery`, `GetTeams*`/`GetTeamById*`, `RegisterTeam`/`UpdateTeam`/`DeactivateTeam`
- `GetAuthenticatedActorProfileQuery`, `CheckProtectedCapabilityAccessQuery`, `GetUsersQuery`
- `UserRoleAssignmentAuthorizationProxy` (Application-layer Proxy precedent), `AuthorizationBehaviour`, `ValidationBehaviour`, `PerformanceBehaviour`, `UnhandledExceptionBehaviour`
- `ICurrentUser` / `GatewayRoleParser` (trusted-header identity), `[Authorize]` attribute
- `ITeamRepository`, `IUserRepository`

**Infrastructure / API**
- EF Core tables: `users`, `identity_provider_sessions`, `teams`, `team_memberships`; migrations `Init`, `AddTeams`, `AddTeamMemberships`
- `UserRepository`, `TeamRepository` (loads memberships)
- `AuthenticatedUserLoginProxy` in `Api/Services` (HU-06 — Api-layer Proxy precedent for the participant path)
- Endpoints: `/api/users/*`, `/api/permissions/authenticated-platform-access`, `/api/teams/*` incl. `POST/GET /api/teams/{id}/participants`
- `ProblemDetailsExceptionHandler` RFC-7807 mapping

**Coverage:** merged line coverage historically ~94.95% against the ADR-0005 gate (`backend/scripts/cover-gate.sh`). X.4 must keep the merged suite at or above the enforced threshold.

## What HU-07A adds on top (per PRD DES-67 §73-90, §217-228 and the DDD model)

| Concern | New work |
|---|---|
| `JoinToken` entity (Identity-owned) | New domain entity: `JoinTokenId`, `LiveSessionId`, `TeamId`, `TokenHash`, `IssuedAt`, `ExpiresAt`, `ConsumedAt`, `IssuedByUserId`, `Status` (`Active`/`Consumed`/`Expired`/`Revoked`). `LiveSessionId`/`TeamId` are **loose reference ids (no FK)** (see semantics in Known quirks). |
| `JoinTokenPolicy` domain service | Validates issuance, expiration, consumption, and replay constraints (a consumed/expired/revoked token cannot grant access again). |
| Domain events + `Consume()` | `JoinTokenIssued` (raised at issuance) and `JoinTokenConsumed` (raised by `Consume()`). `Consume()` is defined + tested but **not endpoint-wired in HU-07A** — single-use consumption happens at actual admission (session-ops / HU-07B), not at validation. |
| `IssueJoinToken` use case | Command — mint a `JoinToken` for a `(liveSessionId, teamId)` pair. Admin/Operator-only. Emits `JoinTokenIssued`. |
| Session-scoped lobby discovery | New Identity-owned session→team association + participant-facing `GET /api/sessions/{id}/teams`, returning lobby cards annotated as `mine` / `joinable` / `locked`. |
| `ValidateParticipantMembershipAccess` use case | Query (**read-only / non-consuming**) — for the **authenticated participant only**: confirm active + `Role.Participant`, confirm an active `TeamMembership` for the target `teamId`, and (when supplied) a valid `JoinToken`; return an `AccessDecision`. Realizes acceptance criteria 1 & 2 through the `Proxy` guard. |
| `IJoinTokenRepository` | New repository contract + EF implementation. |
| Persistence | New `join_tokens` table + `AddJoinTokens` migration. Guard against `BaseAuditableEntity` audit-column leak (`Ignore(...)` unwanted columns). |
| API endpoints | `POST /api/join-tokens` (Admin/Operator → 201, returns the plaintext `token` once); `POST /api/permissions/participant-membership-access` (Participant → 200 `AccessDecision`). |

## Touched surfaces

- `backend/` identity-access-service — domain, application, infrastructure, and API layers
- API contract boundary: new join-token issuance + membership-validation endpoints
- Database schema surface: new `join_tokens` table and `AddJoinTokens` migration
- `frontend/` (mobile-facing) participant join/membership-validation flow consuming the validated access fact — **not** the SignalR runtime itself

## Committed phases

| Commit | Phase | Description |
|---|---|---|
| — | — | No commits yet |

## Known quirks / gotchas

- **JoinToken references are loose ids — with specific semantics.** Neither becomes an EF foreign key (Identity does not own runtime `LiveSession`/`Team`). But they are not symmetric:
  - `TeamId` = the **Identity reference-data Team id** (HU-04, the same id `TeamMembership` keys on). This is deliberate: it is the *only* team identity Identity can validate membership against. The runtime `Team` in session-ops is expected to carry this same id as its origin so the contexts correlate (assumption flagged in the prompt rationale).
  - `LiveSessionId` = a fully **opaque correlation id**. Identity has no `LiveSession` concept, so it stores and echoes this value but **never validates it** against local data. Do not try to FK, look up, or range-check it.
- **Validation is read-only.** `ValidateParticipantMembershipAccess` must **not** call `Consume()` — burning a single-use token at validation would break the actual join and HU-07B reconnection. `Consume()`/`JoinTokenConsumed` are domain capabilities exercised by a later admission slice, tested here at unit/integration level only.
- **Identity is not the final admission authority.** `ValidateParticipantMembershipAccess` returns *access facts*; it does **not** admit a participant to a `LiveSession`. Do not overstate runtime guarantees (same posture as the HU-05 closing note).
- **No SignalR in this slice.** The real-time hub/group admission, reconnection, and multi-device sync are `session-operations-service` + mobile. This slice only produces the gate (JoinToken + membership fact). HU-07B (reconnection) and HU-08 (sync) build on it.
- **Proxy must be realized, not narrated.** Two precedents exist in this service: `UserRoleAssignmentAuthorizationProxy` (Application) and `AuthenticatedUserLoginProxy` (Api). The lobby's conditional-self-select and locked-team rules are Proxy obligations — enforce via guard, never an inline role/ownership `if` in the handler.
- **Migration ordering.** Do not run `dotnet ef migrations add AddJoinTokens` until the branch is on top of `develop` with `AddTeamMemberships` already present. Review the generated migration for leaked audit columns before commit.
- **Coverage gate is `cover-gate.sh` (ADR-0005), not `cover.sh`.** Pass every test project; the last one enforces the threshold. Never add `[ExcludeFromCodeCoverage]` to Domain/Application to pass.
