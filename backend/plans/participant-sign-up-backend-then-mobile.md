# Plan: Participant Sign-Up — Backend First, Then Mobile

**Status:** proposed
**Date:** 2026-06-03
**Primary services:** `backend/services/identity-access-service`, `frontend/` participant client
**Recommended branch:** `feature/participant-sign-up`

---

## Scope note

This plan is for **participant sign-up**, which is **not currently aligned** with the
active canon as written. The current backend PRD set explicitly rejects public
self-registration and assumes participants are already registered and then sign in.

Because of that, implementation should not start with code. It should start by
updating the owning product/architecture docs so the repo stops saying both:

- participants must already exist before mobile login
- participants should be able to sign up

---

## Recommended product shape

The safest version of "participants can sign up" in this codebase is:

- **invite-based participant sign-up**, not open public registration
- the participant receives an invite or activation token
- sign-up creates or activates a participant-scoped identity
- the resulting account is still constrained by team/session membership rules

This keeps the feature consistent with:

- the existing `Participant` role boundary
- the team-membership model from HU-05 / HU-07A
- the mobile-first participant access path from HU-06

If the team wants **open public sign-up** instead, that should be treated as a separate
decision because it changes trust, provisioning, abuse controls, and team-assignment flow.

---

## Dependencies and boundary

- **Owning backend context:** `identity-access-service`
- **Likely downstream consumer:** participant mobile client under `frontend/`
- **Do not start in `session-operations-service`**
  - sign-up is identity lifecycle
  - session admission still remains a later, separate concern

If the signup payload or identity state changes the session-entry contract, call that out
explicitly as a backend/mobile contract change.

---

## Phase 0 — Canon update and slice framing

**Goal**
- make the new scope explicit before code lands

**Work**
- add or update the owning PRD for participant sign-up
- update the relevant context docs that currently assume sign-in only
- record whether the feature is:
  - invite-based signup
  - operator-created invite + participant completes account
  - fully public signup
- define whether signup also assigns team membership, or only creates identity

**Artifacts to update before coding**
- `backend/docs/prd/`
- `backend/docs/umbral_user_stories.md`
- `backend/docs/hu07a-context.md` or successor context if signup affects entry flow
- `backend/docs/decisions/identity-access-service.md` if a design decision is needed

**Gate**
- one authoritative doc states the accepted product shape
- no remaining doc says public self-registration is out of scope if the team has now accepted it

---

## Backend Phase 1 — Domain (`identity-access-service`)

**Goal**
- model participant sign-up without leaking session/runtime concerns into Identity

**Work**
- introduce or extend the domain concept for participant onboarding
- model the signup token/invite lifecycle if invite-based
- define invariants for:
  - unique participant identity
  - token expiry / one-time use if invites are used
  - participant-only role creation
  - activation vs duplicate signup attempts
- add domain events for signup completion or invite redemption if needed

**Likely outputs**
- `Domain/Entities/`
- `Domain/ValueObjects/`
- `Domain/Events/`
- `Domain/Exceptions/`
- `Domain/Services/`

**Tests**
- successful participant signup
- expired invite rejected
- reused invite rejected
- duplicate email / identity rejected
- non-participant role escalation impossible from signup flow

**Gate**
- domain build passes
- invariants are covered by unit tests

---

## Backend Phase 2 — Application (`identity-access-service`)

**Goal**
- add explicit use cases for sign-up and invite issuance/redeeming

**Work**
- add commands/handlers/validators for the chosen flow
- recommended minimum set for invite-based signup:
  - `IssueParticipantSignupInvite`
  - `CompleteParticipantSignup`
  - `GetParticipantSignupContext`
- define DTOs for mobile consumption
- enforce authorization structurally:
  - operator/admin may issue invites
  - anonymous or invite-bearing participant may complete signup only through the allowed path
- keep team assignment separate unless Phase 0 explicitly chose combined signup+assignment

**Tests**
- authorized invite issuance
- unauthorized invite issuance denied
- valid signup completion
- invalid/expired invite denied
- duplicate completion denied
- validator coverage for all required fields

**Gate**
- clean build passes
- handler and validator tests pass
- authorization is structural, not scattered inline

---

## Backend Phase 3 — Infrastructure (`identity-access-service`)

**Goal**
- persist signup state and integrate with identity provider infrastructure

**Work**
- add EF mappings and migration for signup invite/onboarding state
- implement repositories
- wire the flow to Keycloak or the chosen identity provider path
- decide whether account creation is:
  - local persistence first, then Keycloak provisioning
  - Keycloak first, then local persistence
- make failures recoverable and idempotent where possible

**Tests**
- invite persistence round-trip
- duplicate constraints enforced at DB layer
- successful provisioning path
- partial-failure behavior is explicit and tested where feasible

**Gate**
- migration succeeds
- integration tests pass against real persistence
- provisioning path is not hand-waved

---

## Backend Phase 4 — API (`identity-access-service`)

**Goal**
- expose stable signup endpoints for the participant client

**Recommended contract surface**
- `POST /api/participant-signup/invites`
- `GET /api/participant-signup/{token}`
- `POST /api/participant-signup/complete`

**Work**
- add minimal API endpoints
- map domain/application exceptions to stable `ProblemDetails`
- document exact mobile-facing request/response shapes
- confirm whether any endpoint is anonymous, token-authenticated, or operator-authenticated

**API concerns to lock before mobile starts**
- invite token transport
- field validation messages
- duplicate-account behavior
- post-signup success shape
- whether the API also returns an immediate authenticated session or only account-creation success

**Tests**
- happy path endpoint test
- invalid token
- expired token
- duplicate signup
- forbidden invite issuance

**Gate**
- at least one end-to-end API path is green
- mobile contract is written down and verified

---

## Mobile Phase 1 — Client contract and screen states (`frontend/`)

**Goal**
- consume the backend contract without guessing

**Work**
- create typed client functions for signup endpoints
- define screen states for:
  - loading invite context
  - valid invite
  - expired invite
  - already used invite
  - signup success
  - signup failed
- confirm where this lives in the participant client structure under `frontend/`

**Gate**
- no hardcoded backend assumptions outside the verified contract

---

## Mobile Phase 2 — Participant sign-up UI flow (`frontend/`)

**Goal**
- let a participant complete sign-up from the mobile app

**Work**
- build the signup form
- collect only required fields
- submit against the new backend contract
- surface backend validation and token-state errors clearly
- route the participant to the existing login or post-signup entry flow

**Gate**
- successful signup is reachable from the app
- invalid and expired invite states are rendered explicitly

---

## Mobile Phase 3 — Handoff into existing login / session-entry flow (`frontend/`)

**Goal**
- connect signup to the already-existing participant auth and lobby flow

**Work**
- decide whether signup ends with:
  - redirect to login
  - silent authenticated session creation
  - forced app re-auth through Keycloak/OIDC
- verify compatibility with:
  - HU-06 participant login
  - HU-07A membership validation
  - later reconnect flow

**Gate**
- participant can sign up and then proceed into the existing allowed flow without dead ends

---

## Open decisions that must be resolved

1. Is this **invite-based sign-up** or **public self-registration**?
2. Does signup only create identity, or also attach the participant to a team?
3. Is the source of truth for credentials/local profile in Keycloak only, or split with local persistence?
4. Should signup end in an authenticated session, or redirect to the existing login flow?
5. Can operators pre-issue signup invites in bulk?

---

## Recommended implementation order

1. Phase 0 documentation decision
2. Backend Phase 1 Domain
3. Backend Phase 2 Application
4. Backend Phase 3 Infrastructure
5. Backend Phase 4 API
6. Mobile Phase 1 client contract
7. Mobile Phase 2 sign-up UI
8. Mobile Phase 3 handoff into login/session entry

This order keeps the mobile client from inventing a signup flow before the backend
contract and identity semantics are stable.
