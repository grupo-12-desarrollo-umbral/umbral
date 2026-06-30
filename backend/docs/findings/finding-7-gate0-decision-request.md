# Decision request — Finding 7 (identity-access forwarder triplets), Gate 0

**To:** owners of ADR-0011 + ADR-0012 (Application-layer structure / pattern placement)
**From:** Phase-4 of `backend/plans/application-layer-overengineering-findings-6-9.md`
**Date:** 2026-06-30
**Status:** BLOCKING — Phase 4 must not start until this is answered.

## The decision (binary)

> For the 5 identity-access slices below, each shaped
> `*CommandHandler/*QueryHandler` (pure forwarder) → `I*Service` → `*AuthorizationProxy`
> (genuine guard) → `*Service` (real logic):
>
> **Does ADR-0011 §3(b) — "pure forwarding ceremony that adds no behavior (e.g. an
> `IService`/`IExecutor` indirection that only relays a call)" — describe the surviving
> `forwarder-handler + I*Service` seam, making it collapsible (Option A)?**
> **Or is the current handler→Proxy(`I*Service`)→Service the blessed ADR-0012 Proxy
> realization, to be kept (Option B)?**

Answer **A** or **B**. Nothing else in Phase 4 proceeds without it.

## Why this needs you and not just the plan

Finding 7 does **not** fill a gap — it asks you to **reverse a decision already on record**.
`HANDOFF.md` (Session 2026-06-29) states the team's position twice:

- *"the worker now implements the single subject interface; the `*AuthorizationProxy` wraps
  it as the real subject (textbook GoF Proxy)… **Handlers stay thin delegates to the Proxy
  (blessed by ADR-0011 §3 / ADR-0012).**"*
- *"Verdict: the three app layers are NOT over-engineered… every `*Service` under a proxy
  holds real domain logic (not a pure forwarder)."*

That same session already deleted the machine-checked ceremony (the `I*Executor` bottom of the
triplet) and **kept** the `I*Service` subject deliberately. Finding 7 re-opens exactly that call.
It also overlaps very recent commits on this branch (`18294e9`, `3a0e700` "collapse Executor into
single Proxy subject"). So this is a real disagreement between reasonable people reading the same
ADRs — it belongs with the ADR owners, not a refactor pass.

## The 5 slices (shape today)

DI: `Application/DependencyInjection.cs` registers each as
`AddScoped<*Service>()` + `AddScoped<I*Service>(proxy wrapping the concrete service via
ActivatorUtilities.CreateInstance)`; the thin handler injects `I*Service` and forwards.

| Slice | Forwarder handler | `I*Service` | Proxy (genuine guard — KEEP either way) | Service (real logic) |
|---|---|---|---|---|
| `JoinTokens/Commands/IssueJoinToken` | `IssueJoinTokenCommandHandler` | `IIssueJoinTokenService` | `JoinTokenIssuanceAuthorizationProxy` | `JoinTokenIssuanceService` |
| `JoinTokens/Queries/ValidateParticipantMembershipAccess` | `…QueryHandler` | `IValidateParticipantMembershipAccessService` | `ParticipantMembershipAccessAuthorizationProxy` | `ParticipantMembershipAccessValidationService` |
| `Sessions/Queries/GetSessionTeamsForParticipant` | `…QueryHandler` | `IGetSessionTeamsForParticipantService` | `ParticipantSessionTeamLobbyAuthorizationProxy` | `ParticipantSessionTeamLobbyService` |
| `Teams/Commands/JoinTeamAsParticipant` | `JoinTeamAsParticipantCommandHandler` | `IJoinTeamAsParticipantService` | `ParticipantTeamSelfJoinAuthorizationProxy` | `ParticipantTeamSelfJoinService` |
| `Users/Commands/AssignUserRole` | `AssignUserRoleCommandHandler` | `IUserRoleAssignmentService` | `UserRoleAssignmentAuthorizationProxy` | `UserRoleAssignmentService` |

All 5 Proxies are **matrix-mandated** (HU-03/07A/07B/19/20) and stay under both options. The
contested layer is the **forwarder handler + `I*Service`**, never the Proxy or the Service's logic.

Note: treat all 5 uniformly. The earlier `IUserRoleAssignmentService` "stays — mocked in tests"
carve-out is not a structural reason — it is the same forwarder shape, and the test would simply
mock the handler/decorator instead.

## The governing text (verbatim)

- **ADR-0011 §3:** *"A pattern instance may be removed only when both hold: (a) it is not named
  for that use case by the patterns matrix or a phase gate, and (b) it is pure forwarding ceremony
  that adds no behavior (e.g. an `IService`/`IExecutor` indirection that only relays a call)."*
- **ADR-0012, Proxy row:** *"Wraps the real subject and adds an access decision … before
  delegating. Ceremony: a `*Proxy` that only relays to a `*Handler`/`*Service` with no guard is the
  forwarding chain — collapse it."* (The condemned case is a **guardless** Proxy. These Proxies have
  guards — so the literal text does not condemn them; it is silent on the seam beneath a genuine guard.)
- **Enforcement gap:** `structure-guard.sh` only forbids `*Executor`. The `I*Service`/`*Service`
  seam is **not** machine-checked, so this is a manual-review call — which is why it lands here.

## The two readings

**Option A — Collapse (Finding 7 as written).** §3(b)'s "`IService` indirection that only relays a
call" describes the *interface seam* and the *forwarder handler*: the handler adds nothing, and the
`I*Service` exists only as the Proxy's decoration point. ADR-0012 says a Proxy "wraps the real
subject" — and a use case's real subject is its **handler**. So move the Service's logic into the
handler and let the Proxy implement `IRequestHandler<TCmd,TRes>` decorating that handler. End-state
per slice: **2 types** (handler-with-logic + Proxy-guard) instead of 3 types + 1 interface — and the
Proxy now genuinely wraps the real subject, matching ADR-0012 *more* closely than today.

**Option B — Keep (HANDOFF 2026-06-29 reading).** §3(b) fails because the `*Service` is **not** pure
forwarding — it holds real domain logic, so the *instance* is not removable ceremony. The
handler→Proxy(`I*Service`)→Service shape is the blessed ADR-0012 realization the team already signed
off. Finding 7 becomes a one-paragraph documentation note in
`backend/docs/findings/violations-overengineering.md`, and Phase 4 ships nothing.

## What each option costs

- **A:** ~−150 LOC; 5 mechanical-then-semantic commits (one per slice). **Risk:** MediatR resolves
  `IRequestHandler<,>` via assembly scan — the manual Proxy-as-decorator registration must win over
  the scanned handler (verify ordering or exclude the concrete handler from the scan). Must preserve
  the `ICurrentActor` memoization that already killed the double-fetch (2026-06-30). Gated by the
  full auth integration suite after each slice.
- **B:** no code; one note recorded. Zero risk. Accepts the 4-hop shape as intentional.

## Recommendation

From a **pure ADR-text** standpoint, Option A is defensible and arguably more aligned with
ADR-0012's "Proxy wraps the real subject." **But** the burden of A is to show the `I*Service` seam
is "an `IService` indirection that only relays a call" under §3(b) — and the team explicitly read it
the other way nine days ago and recorded the current shape as *blessed*. Absent a positive reason to
reverse that, the conservative reading of §3 ("when in doubt, keep a mandated-pattern instance")
points to **B**. We will not write a line of Phase 4 on inference — we need your explicit call.

## How to record the answer

- **If A:** note "Gate 0 → A, approved by <name> <date>" here; Phase 4 proceeds per the plan
  (one commit per slice, auth integration suite after each).
- **If B:** add the Option-B rationale to `backend/docs/findings/violations-overengineering.md`
  Finding 7 row and close Phase 4. This file can then be deleted.
