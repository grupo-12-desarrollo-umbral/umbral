# PRD — HU-07A: Participant Membership Validation in Session

> Linear: DES-69 (this PRD) · related to DES-11 (HU-07A ticket) · team `umbral-equipo-12`
> Bounded context: `Identity` (`identity-access-service`)
> Branch: `feature/hu-07a-participant-membership-validation`
>
> Supersession note (2026-06-02): this branch now expands HU-07A to cover the
> participant session → team-lobby → team-space flow. The original "participant may
> only enter their own team" invariant is replaced here with **conditional
> self-select**: unassigned participants may self-select any team in the session and
> thereby self-assign; pre-assigned participants remain locked to their existing team.
> Identity also owns the minimal session-scoping surface for that lobby via
> `GET /api/sessions/{id}/teams`. `JoinToken` validation remains read-only and does
> not perform final admission.

## Problem Statement

When a live session is running, an authenticated participant must be able to enter the
session through a **session-scoped team lobby** instead of typing raw team GUIDs. Today
the platform can authenticate an actor (HU-01/HU-06), knows which team a participant was
assigned to (HU-04/HU-05), and can validate `(sessionId, teamId)` through an access
fact, but it cannot honestly answer the participant's first question: "which teams are in
this session, and which one may I join?" Without that discovery surface, the client is
forced into a GUID-pair harness rather than a shippable flow.

From the participant's perspective: "I logged in, I entered the session, and I should see
the teams for that session. If I was not pre-assigned, I should be able to choose one. If
I was already assigned, I should only be able to enter that team."

From the operator/administrator's perspective: "I need to hand each participant a
verifiable proof that they may enter a specific session-and-team, and I need the system to
reject anyone presenting a stale, foreign, or forged proof before any shared team state is
exposed."

## Solution

`Identity` owns a limited-scope **`JoinToken`** that proves a participant may enter a
specific `(LiveSession, Team)` pair through the approved join flow, plus two participant
access capabilities:

1. a **session-scoped team discovery** surface that lists the teams in a session and
   annotates each one for the caller as `mine`, `joinable`, or `locked`
2. a read-only **membership-validation** capability that confirms — for the
   authenticated participant only — that they are an active `Participant`, that they hold
   an active `TeamMembership` for the requested team, and (when presented) that their
   `JoinToken` is still valid

The capability returns **`Access Facts`** (an `AccessDecision`): identity, role, token
validity, and a coarse allow/deny for the requested team context. It deliberately does
**not** admit the participant to the live session or to the real-time hub — that final
admission decision belongs to `SessionOperations` (HU-07B). Identity produces the gate;
session runtime consumes it.

Concretely, three surfaces are added to `identity-access-service`:

1. **Issue a join token** — an administrator or operator mints a `JoinToken` for a
   `(liveSessionId, teamId)` pair and hands the one-time plaintext token to the
   participant's client.
2. **List teams in a session for the participant lobby** — the authenticated participant
   asks Identity for the teams associated with a `liveSessionId`; Identity returns the
   session-scoped team list plus the participant's `joinState` for each team.
3. **Validate participant membership access** — after selection or self-assignment, the
   authenticated participant asks Identity to confirm they may enter the chosen team's
   context; Identity answers with an `AccessDecision` without consuming the token.

These surfaces enforce authorization through a `Proxy` guard (the mandated pattern for this
context), never through ad-hoc role/ownership `if`-checks inside handlers or endpoints.

## User Stories

1. As an administrator, I want to mint a join token for a specific session-and-team pair, so that I can authorize a participant to enter exactly that context.
2. As an operator, I want to mint a join token the same way an administrator can, so that live-session staffing does not depend on an administrator being online.
3. As an administrator/operator, I want the minted token returned to me exactly once in plaintext, so that I can deliver it to the participant while the platform only ever stores its hash.
4. As an administrator/operator, I want a freshly issued token to default to a short lifetime when I do not specify one, so that an unused token cannot linger as a standing entry credential.
5. As an administrator/operator, I want to optionally set the token's lifetime, so that I can match it to how long the session admission window should stay open.
6. As an administrator/operator, I want the system to reject a non-positive or invalid expiry, so that I cannot accidentally mint a token that is already dead or never expires.
7. As an administrator/operator, I want each issued token to carry which session and team it authorizes, so that it can never be replayed against a different team or session.
8. As an administrator/operator, I want issuance to be refused if I am not an administrator or operator, so that participants cannot mint their own entry proofs.
9. As a participant, I want to ask the platform which teams belong to a session, so that my client can render a team lobby instead of asking me for a raw team GUID.
10. As a participant, I want the platform to confirm I am an active participant before granting team context, so that a deactivated account cannot slip into a live session.
11. As an unassigned participant, I want session teams marked as joinable, so that I can self-select one team without operator intervention.
12. As a pre-assigned participant, I want only my existing team marked as mine and all other teams marked locked, so that I cannot switch teams from the lobby.
13. As the mobile client, I want the lobby payload to include each team's `joinState`, so that I can disable locked teams without an extra round-trip.
14. As a participant, I want my presented join token validated against the requested session-and-team, so that a token issued for a different context is rejected.
15. As a participant, I want an expired join token to be rejected, so that stale credentials cannot be used after the admission window closes.
16. As a participant, I want a token that was already consumed or revoked to be rejected, so that a single-use proof cannot be replayed.
17. As a participant, I want validation to be read-only, so that merely checking my access does not burn my single-use token and break my actual join or later reconnection.
18. As a participant, I want a clear, structured error when access is denied, so that my client can tell me why (locked team, expired token, not a participant) rather than failing opaquely.
19. As the real-time channel owner (session-operations / mobile client), I want Identity's membership validation to be the authoritative gate before a participant is joined to their team's group, so that hub admission is grounded in a verified access fact.
20. As a security reviewer, I want join tokens stored only as deterministic hashes, so that a database leak does not expose usable entry credentials.
21. As a security reviewer, I want each token to carry high entropy, so that tokens cannot be guessed or brute-forced even though the hash is unsalted/deterministic.
22. As an auditor, I want a `JoinTokenIssued` event when a token is minted, so that there is a traceable record of who authorized which session-and-team and when.
23. As an auditor, I want a `JoinTokenConsumed` capability and event to exist on the domain model, so that the later admission slice can record single-use consumption without re-modeling the token.
24. As an operator, I want the platform to never claim it admitted a participant to a session, so that the boundary between identity access facts and session runtime ownership stays clear.
25. As a developer of `session-operations-service`, I want Identity to expose validation as a stable access fact (not a runtime admission), so that I can own the join/reconnection/sync rules in my context without Identity overreaching.
26. As a maintainer, I want authorization on the new lobby and validation surfaces enforced through a guard/proxy, so that the access rules are not duplicated as inline conditionals across handlers and endpoints.
27. As a maintainer, I want the new persistence to avoid leaking unwanted auditable columns into the `join_tokens` table, so that the schema reflects only the token's real attributes.

## Implementation Decisions

**Domain layer (`Identity`)**
- New minimal Identity-owned **`SessionTeamAssociation`** (or equivalent link-table shape) between opaque `LiveSessionId` and Identity reference-data `TeamId`, used only to scope lobby discovery and enforce the one-membership-per-session rule. It does **not** model runtime team state.
- New Identity-owned aggregate **`JoinToken`** with: `JoinTokenId`, `LiveSessionId`, `TeamId`, `TokenHash`, `IssuedAt`, `ExpiresAt`, `ConsumedAt`, `IssuedByUserId`, `Status`.
- `JoinTokenStatus` enum: `Active`, `Consumed`, `Expired`, `Revoked`.
- `LiveSessionId` and `TeamId` are **loose reference ids — no EF foreign key** (Identity does not own runtime `LiveSession`/`Team`), but with asymmetric semantics:
  - `TeamId` is the **Identity reference-data Team id** (HU-04) — the same id `TeamMembership` keys on. It is the only team identity Identity can validate membership against; the runtime team in session-ops is expected to correlate on this same origin id.
  - `LiveSessionId` is a fully **opaque correlation id** — stored and echoed, never looked up, FK'd, or range-checked.
- `JoinToken.Consume()` transitions `Active → Consumed`, sets `ConsumedAt`, and raises `JoinTokenConsumed`. Defined and unit-tested but **not** exercised by any HU-07A use case (single-use consumption belongs to admission / HU-07B).
- Timestamps (`IssuedAt`/`ExpiresAt`/`ConsumedAt`) are normalized to **microsecond precision at the domain boundary**, because PostgreSQL `timestamptz` (6 fractional digits) truncates .NET `DateTimeOffset` (7-digit ticks); without this, exact-equality comparisons between domain, event, and DB drift on the 7th digit.
- Domain service **`JoinTokenPolicy`** validates issuance, expiration, consumption, and replay constraints (a consumed/expired/revoked token can never grant access again). Registered as a stateless singleton.
- Domain events `JoinTokenIssued` (raised at issuance) and `JoinTokenConsumed` (raised by `Consume()`).
- Typed domain exceptions for each invariant breach (issuer required, team/session required, token-hash required, invalid expiration, expired, replay rejected).
- Reuses existing Identity domain: `User` (active + `Role.Participant`), the reference-data `Team`, the `TeamMembership` record (HU-05) as the authorization fact, and the `AccessDecision` value object / `AccessPolicy` posture.

**Application layer (CQRS via MediatR)**
- Command **`IssueJoinToken`** — mints a `JoinToken` for a `(liveSessionId, teamId)` pair, restricted to administrators/operators, emits `JoinTokenIssued`, returns the plaintext token once (DTO carries the plaintext + identifiers).
- Query **`GetSessionTeamsForParticipant`** (name illustrative) — for the authenticated participant only: returns the teams associated with a `liveSessionId`, annotated per team with `joinState = mine | joinable | locked`.
- Query **`ValidateParticipantMembershipAccess`** — **read-only / non-consuming**. For the authenticated participant only: confirms active + `Participant` role, confirms an active `TeamMembership` for the target `teamId`, and (when a token is supplied) validates it via `JoinTokenPolicy`; returns an `AccessDecision` DTO. This query must **never** call `Consume()`.
- Authorization is enforced through **authorization proxies** (`Proxy` pattern, mandated for `Identity`), following the existing `UserRoleAssignmentAuthorizationProxy` (Application) precedent — the lobby's conditional-self-select / locked-team rules are proxy obligations, not inline conditionals.
- New repository contract `IJoinTokenRepository`; new token-service contract `IJoinTokenTokenService` (generation + hashing).
- Validators (FluentValidation) on both the command and the query, consistent with existing pipeline behaviours (validation/authorization/performance/unhandled-exception).

**Infrastructure layer**
- New `join_tokens` table via `AddJoinTokens` EF Core migration + `JoinTokenConfiguration`; unwanted `BaseAuditableEntity` audit columns are `Ignore`d so the schema reflects only the token's real attributes.
- `JoinTokenRepository` (EF Core) implements `IJoinTokenRepository`; lookups are by `TokenHash`.
- `JoinTokenTokenService`: a **256-bit CSPRNG** token paired with a **deterministic SHA-256 hash**. Hashing is deterministic (not bcrypt/Argon2) on purpose — validation recomputes `HashToken(supplied)` and looks up the row by hash, so a per-call random salt could never match. Brute-force resistance comes from the 256 bits of entropy in the token itself. HMAC-with-pepper is documented (ADR-0007) as the upgrade path if DB-leak resistance is later required (still deterministic; only adds secret config). Registered as a singleton.
- Migration ordering: `AddJoinTokens` is generated on top of `develop` with `AddTeamMemberships` already present; the generated migration is reviewed for leaked audit columns before commit.

**API layer (Minimal API)**
- `POST /api/join-tokens` — administrators/operators → `201`, returns the plaintext `token` exactly once. Applies a default 15-minute lifetime only when `expiresInSeconds` is omitted or non-positive; expiry semantics otherwise stay in the application/domain layers.
- `GET /api/sessions/{id}/teams` — authenticated participant → `200`, returns the session-scoped team lobby payload with `joinState` annotations.
- `POST /api/permissions/participant-membership-access` — authenticated participant → `200` with the `AccessDecision`.
- A custom **trusted-header authentication handler** + named authorization policies let Minimal API `RequireAuthorization(...)` run through the normal ASP.NET Core pipeline against the gateway-forwarded `X-User-Id`, `X-User-Role`, `X-User-Email` headers — **no per-service JWT validation** (the gateway remains the trust boundary).
- `ProblemDetailsExceptionHandler` extended to map join-token / membership failures to RFC-7807 responses (e.g. foreign team, expired/replayed token, non-participant) with appropriate status codes.

```
Issuance:   admin/operator ──POST /api/join-tokens──▶ JoinTokenIssuanceProxy ▶ IssueJoinToken ▶ JoinTokenIssued
                                                                              └─▶ 201 { token (once), joinTokenId, expiresAt }

Lobby:      participant ──GET /api/sessions/{id}/teams──▶ SessionTeamLobbyProxy
              ▶ GetSessionTeamsForParticipant
              └─▶ 200 [{ teamId, displayName, joinState }]

Validation: participant ──POST /api/permissions/participant-membership-access──▶ MembershipAccessProxy
              ▶ ValidateParticipantMembershipAccess (read-only)
                  · active + Role.Participant?
                  · active TeamMembership for teamId?
                  · JoinTokenPolicy.Validate(token, liveSessionId, teamId)?  (when token supplied)
              └─▶ 200 AccessDecision (allow/deny + reason)   — NO Consume(), NO hub admission
```
*(shape distilled from the implemented slice; not a literal contract)*

## Testing Decisions

- **Test external behavior, not implementation detail.** Assert on the `AccessDecision` returned, the HTTP status/Problem-Details body, the emitted domain events, and the persisted row — not on private fields or call sequencing.
- **Participant-lobby tests** must verify `mine` / `joinable` / `locked` rendering inputs from the session-teams query, including the pre-assigned lock case and the unassigned self-select case.
- **Domain unit tests** (`JoinTokenTests`, `JoinTokenStatusTests`, `JoinTokenPolicyTests`): issuance invariants, expiration, `Consume()` transition + `JoinTokenConsumed` event, and replay rejection (consumed/expired/revoked cannot grant access). Prior art: `UserTests`, `AccessPolicyTests`.
- **Application unit tests** (`IssueJoinTokenCommandHandlerTests`, `IssueJoinTokenCommandValidatorTests`, `ValidateParticipantMembershipAccessQueryHandlerTests`, `...QueryValidatorTests`): authorized issuance, participant-only validation, foreign-team denial, non-participant denial, expired/invalid-token denial, and that validation is non-consuming. Prior art: `AssignUserRoleCommandHandlerTests`, `AssignParticipantToTeamCommand` tests.
- **Infrastructure integration tests** (`JoinTokenRepositoryIntegrationTests`) against real PostgreSQL via Testcontainers: round-trip persistence, lookup by hash, deterministic-hash match, and consumed/expired status persistence. Prior art: `TeamRepositoryIntegrationTests`, `UserProvisioningRepositoryIntegrationTests`.
- **API integration tests** (`IdentityAccessApiEndpointsTests`) via `WebApplicationFactory`: authorized issuance returns `201` + one-time token, participant validation returns `200` `AccessDecision`, forbidden-role issuance is rejected, foreign-team validation is denied, and `join_tokens` is cleaned between API tests.
- **Coverage gate:** the merged suite must stay at or above the ADR-0005 threshold (`backend/scripts/cover-gate.sh`, historically ~95%). No `[ExcludeFromCodeCoverage]` on Domain/Application to pass the gate. Worth adding direct unit tests for `JoinTokenTokenService` (currently only covered indirectly through integration tests).

## Out of Scope

- **No SignalR/WebSocket hub in this slice.** Real-time hub/group admission, reconnection, and multi-device sync are owned by `session-operations-service` + the mobile client. This slice only produces the gate (a `JoinToken` + a membership access fact). HU-07B (reconnection) and HU-08 (sync) build on it.
- **No final session admission.** `ValidateParticipantMembershipAccess` returns access facts; it does not admit a participant to a `LiveSession`. The runtime join decision belongs to `SessionOperations`.
- **No join-token consumption endpoint.** `Consume()` / `JoinTokenConsumed` are defined and tested but not endpoint-wired here; single-use consumption happens at actual admission (session-ops / HU-07B) and must stay available for reconnection.
- **No runtime `LiveSession`/`Team` modeling in Identity.** `LiveSessionId` stays an opaque correlation id; the session-team association is a minimal access-language index, not live runtime state.
- **No Keycloak adapter or login re-implementation.** The gateway authenticates; the service consumes trusted headers only.
- **No real-time hub/reconnection rules in this PRD.** The lobby and validation flow live here; actual room admission and reconnection semantics remain in HU-07B / HU-08.

## Further Notes

- **Boundary posture (`CONTEXT.md`):** Identity returns `Access Facts` that *inform* admission but do not decide it. Keep all language and responses aligned — do not describe the validation result as a "join approval" or "session admission."
- **Mandated pattern:** `Proxy` is required for `Identity`. Two precedents exist in this service — `UserRoleAssignmentAuthorizationProxy` (Application) and `AuthenticatedUserLoginProxy` (Api). The conditional-self-select and locked-team rules are realized through guards, never narrated as inline `if` checks.
- **ADR-0007** records the token generation/hashing decision (256-bit CSPRNG + deterministic SHA-256, HMAC-with-pepper as the documented upgrade path).
- **Status of the work:** the original backend slice is implemented across phases X.1–X.4 on `feature/hu-07a-participant-membership-validation` (see decision-log entries [021]–[022]); the lobby supersession recorded on 2026-06-02 extends that baseline with the session-team discovery work described above.
- **Cross-context dependency:** the runtime `Team` in `session-operations-service` is assumed to carry the Identity reference-data `TeamId` as its origin so the two contexts correlate on membership validation — flag and confirm this assumption when HU-07B is scoped.
