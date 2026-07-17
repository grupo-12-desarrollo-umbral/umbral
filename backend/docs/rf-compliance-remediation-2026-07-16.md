# RF compliance remediation — 2026-07-16

Fix specs for the gaps found auditing the codebase against the functional requirements (RF-01…RF-18) in
`docs/proyecto_requisitos_documento_clave.md` §8, canonicalised in `docs/academic-requirements-canon.md`.
Each item is a self-contained spec: root cause, verified evidence, the change, and the test that proves it.

**Status (2026-07-17): the audit was read-only, but the fixes are no longer.** F6/F7/F8 are implemented;
F1/F2/F3/F5 were verified present in the working tree (F2 arrives via in-flight gateway work that happens to
add the `trivia-design` route). F4 is DES-100's and stays unactioned here. **All of it is uncommitted on
`ci/api-gateway-build-test`**, interleaved with several unrelated changes — so "is F<n> done?" is not
answerable from `git log`. Only F8's spec has been amended against what was learned implementing it
(Corrections #4); F1–F5 are still as-audited and unrevised.

Audit outcome: **14 of 18 RFs met.** Gaps in RF-03, RF-07, RF-15, RF-16.

> **Revised after review.** A second pass corrected two material errors in the first draft of this doc. Both
> are recorded in "Corrections" below rather than quietly edited out, because both were the kind of mistake
> that changes what you'd do first. Line references throughout have been re-verified against the working tree.
>
> **Revised again during implementation — 2026-07-17.** Implementing F6–F8 falsified one of this doc's *own*
> recommendations: **F8's recommended option (a) would have broken a live contract.** Recorded as Corrections
> #4, same convention — the spec below is amended to match, not silently.

## Fix order

**F1 ships first and ships alone** — it is a live production data leak with no dependencies. Everything else
is ordinary queue work.

| # | Finding | RF | Severity | Depends on |
|---|---|---|---|---|
| F1 | Participants can read correct answers **in production** | RF-16 | **Critical — live** | — |
| F2 | Gateway prod config missing the trivia route | RF-16 | High | — |
| F3 | Never-activated Draft mission can start a live session | RF-03 | Medium | — |
| F4 | Session event history unreadable → **owned by DES-100** | RF-15 | Medium | see F4 |
| F5 | Scoring swallows publish failures; no outbox | — | Medium | — |
| F6 | mission-design has no ASP.NET auth middleware | RF-16 | Low | — |
| F7 | Trivia teams lose the clock between questions | RF-06 edge | Low | — |
| F8 | identity-access policies are dead code | RF-16 | Low | — |

RF-07 is **not** in this batch — it is scoped feature work already ticketed as **HU-27**. See the appendix.

## Corrections

Items 1–3 correct the first draft (found in review). Item 4 corrects *this* draft (found in implementation).

1. **"Missing `UseAuthentication()`/`UseAuthorization()` makes `[Authorize]` silently no-op" — wrong, and it
   invented a blocker.** mission-design authorizes through its **own** attribute,
   `Application/Common/Security/AuthorizeAttribute.cs` (a plain `Attribute`, unrelated to ASP.NET's), read by
   reflection in `Application/Common/Behaviours/AuthorizationBehaviour.cs:20-21`, which resolves identity from
   trusted headers via `Api/Services/CurrentUser.cs:17-26`. **This path does not involve ASP.NET authorization
   middleware and works without it.** The no-op claim is true only of ASP.NET's `[Authorize]` on controllers —
   which the first draft had itself prescribed, manufacturing a dependency that does not exist. F1 needs no
   middleware work; the middleware gap is real but is defence-in-depth (F6).

2. **"The leak is masked in production by the missing trivia route" — wrong, and it understated severity.**
   `/api/trivias/**` is indeed absent from prod (F2), but `/api/missions/{**catch-all}` **is** routed
   (`api-gateway/src/appsettings.json:13-19`), and `GET /api/missions/{id}/runtime-plan`
   (`MissionsController.cs:78`) is ungated and returns every option's `IsCorrect` via
   `MissionRuntimeTriviaOptionResponse` (`MissionsController.cs:643-655`). **Participants can retrieve correct
   answers in production right now.** F1 is not a latent risk to sequence behind other work; it is live.

3. Minor: the first draft credited both event stores with "actor, correlation ID, and timestamps". Only
   session-ops has all three (`session-operations-service/src/Domain/Entities/SessionEvent.cs:56`).
   scoring-monitoring has timestamp + optional `ResponsibleUserId` + a `SourceEventKey` idempotency key, and
   **no correlation ID** (`scoring-monitoring-service/src/Domain/Entities/SessionEvent.cs:36-48`).

4. **F8's "apply the policies at the controllers" — wrong for `Participant`, and following it would have
   broken a live contract.** This doc called `AuthorizationPolicies.Participant` dead code and recommended
   wiring it up. It is **unusable, not merely unused**. The participant-facing `PermissionsController` routes
   deliberately answer a non-participant with a **reason-coded 200**, not a 403:

   ```csharp
   // ParticipantMembershipAccessAuthorizationProxy.cs:42-48
   if (actor.Role != Role.Participant)
   {
       return Deny(request, ParticipantMembershipAccessReasonCodes.UserNotParticipant, "User is not a participant.");
   }
   ```

   `GetParticipantEligibleTeamsQueryHandler` does the same, and its own comment states why: *"an empty Teams
   list is otherwise 'whitelisted for nothing', which is distinct from a denied user."* Callers (mobile,
   session-ops) read `user-not-participant` to tell those apart. An `[Authorize(Policy = Participant)]` on
   that controller collapses the distinction into a 403 the callers cannot read.

   **What was done instead:** the `Participant` registration was **deleted** and the reason why is recorded in
   `AuthorizationPolicies.cs`; `PermissionsController` takes a plain authenticated `[Authorize]`. So F8 landed
   as **(a) for Teams/Users, (b) for the Participant policy** — a split the "pick one" framing did not admit.
   Do not "finish the job" by re-adding it.

   The general lesson, since this doc will be read again: **"registered but unused" is not sufficient evidence
   that a policy is inert scaffolding.** Check whether the routes it would guard answer with a reason code
   instead of a status code. One grep of the handler would have caught this at authoring time.

---

## F1 — Participants can read the correct answers, in production

**RF-16** · **Critical — live in production** · `mission-design-service`

### Root cause

Two ungated read paths return the answer key to **any authenticated caller, including a Participant**:

- `GET /api/missions/{id}/runtime-plan` — `MissionsController.cs:78`, no `[Authorize]`, returns
  `MissionRuntimeTriviaOptionResponse(OptionText, SequenceOrder, **IsCorrect**)` at `MissionsController.cs:643-655`.
  **Routed in production** via `/api/missions/{**catch-all}` (`api-gateway/src/appsettings.json:13-19`). Also leaks
  target/clue placement.
- `GET /api/trivias/{id}` — `TriviasController.cs:58`, returns `TriviaOptionDto.IsCorrect`
  (`Application/Dtos/Trivias/TriviaOptionDto.cs:3-7`). Not reachable through the prod gateway today (F2), but
  reachable in Development and in-cluster.

All **seven** mission-design query types lack the service's `AuthorizeAttribute`: `GetTriviaCatalog`,
`GetTriviaDetail`, `GetMissionCatalog`, `GetMissionDetail`, `GetMissionRuntimePlan`, `GetMissionReadiness`,
`GetDifficultyCatalog` (verified: zero `Authorize` matches under `Application/{Missions,Trivias}/Queries/`).
Every *mutation* command in the same service **is** gated `Roles.Administrator`/`Roles.Operator`. The gateway's
`"default"` policy authenticates but does not discriminate by role.

This is an **omission, not a design choice** — the read side was simply never covered.

### Blast radius — verified safe to gate

Every caller traced:

- **`/api/trivias/*`** — called only from `frontend/app/lib/trivias.ts:8,20,36,54,74,96,117,134,151,168,185`, all
  operator/admin authoring screens. **No participant path calls it.** Participants receive questions through the
  session-ops runtime snapshot, never from mission-design.
- **`/readiness` and `/runtime-plan`** — called **only** server-to-server from session-ops
  (`MissionReadinessSource.cs:30`, `MissionRuntimeSource.cs:23`). The sole consumer of both is
  `CreateSessionCommandHandler` (operator-gated), and those adapters forward the caller's own `X-User-Role`
  (`MissionReadinessSource.cs:45-50`) — an Operator's in that flow.

So gating all seven to `Administrator,Operator` breaks nothing. **`runtime-plan` in particular is safe** despite
being cross-service, because the only caller is an operator-gated command.

### Fix — one change, no dependencies

Add the service's own attribute to all seven query types:

```csharp
using umbral_backend.Application.Common.Security;   // NOT Microsoft.AspNetCore.Authorization
using umbral_backend.Domain.Constants;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Operator}")]
public sealed record GetMissionRuntimePlanQuery(int MissionId) : IRequest<MissionRuntimePlanDto>;
```

`AuthorizationBehaviour` is already registered in the pipeline (`Application/DependencyInjection.cs:17`), so this
takes effect immediately — no middleware, no `Program.cs` change, no dependency on F6. `Roles`
(`Domain/Constants/Roles.cs:3-6`) has only `Administrator` and `Operator`; no `Participant` constant is needed.

Ship this alone, ahead of everything else in this doc.

### Follow-up (same PR if cheap, separate if not)

Gating stops the leak; it does not stop `IsCorrect` from riding on a DTO a future participant-facing route could
reuse. Split the shapes while there is no participant consumer to migrate:

```csharp
// Authoring surface — operators/admins only.
public sealed record TriviaOptionDto(int Id, string OptionText, int SequenceOrder, bool IsCorrect);

// Play surface — never carries the answer key.
public sealed record TriviaOptionPlayDto(int Id, string OptionText, int SequenceOrder);
```

Same treatment for `MissionRuntimeTriviaOptionResponse` (`MissionsController.cs:643`). Any future participant read
projects to the play shape.

### Tests

- **The security regression test** (`tests/IntegrationTests/Api/`): `GET /api/missions/{id}/runtime-plan` with
  `X-User-Role: Participant` → 403. Same for `GET /api/trivias/{id}`. As an Operator → 200 with `IsCorrect` present.
- A test asserting **every** type under `Application/{Missions,Trivias}/Queries/` carries an `AuthorizeAttribute`.
  This is the one that prevents recurrence — the individual 403 tests only cover the endpoints someone remembered.
- If the DTO split lands: assert `TriviaOptionPlayDto` has no `IsCorrect` member, and that the play projection's
  JSON contains no `isCorrect` key.

---

## F2 — Gateway production config is missing the trivia route

**RF-16** · High · `api-gateway`

### Root cause

`api-gateway/src/appsettings.json` has exactly one mission-design route, `/api/missions/{**catch-all}`
(lines 13-19). The `trivia-design` route exists **only** in `appsettings.Development.json:21-27`. All 12
`TriviasController` endpoints — which `frontend/app/lib/trivias.ts` calls via `API_GATEWAY_URL` — therefore **404
at the gateway in production**. Trivia authoring is entirely broken in prod.

This is an **availability bug, not a security one**. The first draft claimed this route masked F1 and must
therefore land after it; that was wrong (see Corrections #2) — the leak is already open via `runtime-plan`.
Landing F1 first is still the sensible order, since this route adds a second path to the same data, but F2 is not
what makes F1 exploitable.

### Fix

Add to `ReverseProxy.Routes` in `appsettings.json`, matching the Development entry and existing route style:

```json
"trivia-design": {
  "ClusterId": "mission-design",
  "Match": { "Path": "/api/trivias/{**catch-all}" },
  "AuthorizationPolicy": "default"
}
```

`"default"` is correct — the gateway authenticates, the service authorizes by role after F1. The `mission-design`
cluster already exists (`appsettings.json:108-113`); no cluster change needed.

Also: diff Development against production route-by-route and reconcile. This drifted silently once.

### Tests

- Gateway integration test: `/api/trivias/1` routes to the mission-design cluster rather than 404.
- Better: assert the route **names** in `appsettings.json` are a superset of those in
  `appsettings.Development.json`. That catches the whole drift class, not just this instance.

---

## F3 — A never-activated Draft mission can start a live session

**RF-03** · Medium · `session-operations-service`

### Root cause

`src/Infrastructure/Integrations/MissionDesign/MissionReadinessSource.cs:68`:

```csharp
var isActive = !string.Equals(ActivationState, InactiveActivationState, StringComparison.OrdinalIgnoreCase);
```

Activeness is derived **negatively**, but `MissionActivation`
(`mission-design-service/src/Domain/Enums/MissionActivation.cs:3`) has **three** members — `Draft=0, Ready=1,
Inactive=2`. So `Draft` maps to `isActive: true`. Only `Inactive` (terminal retirement) is actually blocked.

`IsReady` cannot compensate: it is computed **structurally and independently of `ActivationState`** in
`GetMissionReadinessQueryHandler.cs:30-45` (`failures.Count == 0`). mission-design is explicit that structural
completeness is *not* activation — `Mission.cs:294-312`: *"Authoring never auto-promotes a mission to Ready — that
requires an explicit `Activate()`."* `CreateSessionCommandHandler.cs:38` then trusts only the two derived booleans.

**Failure scenario:** author a structurally complete mission (all stages have substages, targets active, quizzes
published) and never call `Activate()`. `ActivationState = "Draft"` → `isActive = true`; readiness returns 0
failures → `isReady = true`. `SessionCreationPolicy.EnsureMissionEligible` (`SessionCreationPolicy.cs:16-27`)
passes both clauses and a live session is created from a mission that was never activated. RF-03 requires
*"a partir de una misión **activa**"*.

The policy is correct and well-tested — the bug is purely the transport mapping.

### Fix

`MissionReadinessSource.cs` — derive activeness positively:

```csharp
private const string ReadyActivationState = "Ready";
...
var isActive = string.Equals(ActivationState, ReadyActivationState, StringComparison.OrdinalIgnoreCase);
```

Delete the now-unused `InactiveActivationState` constant (line 17). Leave `SessionCreationPolicy` untouched.

The enum member is `Ready`, not `Active` — worth a one-line comment noting that mission-design's *Ready* is what
RF-03 calls *activa*, since that mismatch is what invited the negative check.

### Tests

Existing coverage has the exact blind spot that let this through — `MissionReadinessSourceIntegrationTests.cs:14`
and `CreateSessionCommandHandlerTests.cs:71` cover `Ready/true`, `Inactive/false`, and `Draft/false`, but **not
`Draft/true`**.

- **Add that case**: a `Draft` + structurally-ready mission → `MissionNotEligibleForSessionCreationException`.
- Mapping unit tests over `MissionReadinessResponse.ToMissionReadinessDto()` for all three enum values:
  `Ready → isActive:true`, `Draft → isActive:false`, `Inactive → isActive:false`. Note the class is
  `[ExcludeFromCodeCoverage]` (line 14) — narrow that to the HTTP plumbing so the mapping record is covered.

---

## F4 — Session event history is unreadable → **already owned by DES-100**

**RF-15** · Medium · `scoring-monitoring-service` · **do not spec this here**

### Status

**This gap has an owner and a decided plan: `backend/plans/hu-40-des-100-implementation.md` (DES-100 / HU-40),
dated today.** Its first delivery — §1 (schema + attribution) + §2 (read slice: query/handler/DTO) + §3 (read
endpoint + gateway route) — closes RF-15. Every open question is resolved; only §6 is blocked (on DES-92), and
§6 is not needed for RF-15.

The audit independently reached the same conclusion the plan already records at
`plans/hu-40-des-100-implementation.md:60` ("What does NOT exist… No read path of any kind") and `:574`
("**RF-15** … currently at zero coverage despite the write model existing, because nothing can read the table").

**Action: none here. Track under DES-100.** An earlier draft of this doc re-spec'd the read slice from scratch —
that would have competed with a more thorough plan.

### The one thing to fix outside DES-100

`docs/hu-24b-evidence-realtime-plan-2026-07-16.md:145` asserts the opposite:

> "The RF-15 read path: `SessionEventHistory` in scoring-monitoring stays write-only. RF-15's existence is
> satisfied; broadening it is a separate ticket."

That is wrong on the merits — an audit trail nobody can query is not an audit capability, and RF-15 asks for
*trazabilidad … para auditoría* — and it directly contradicts DES-100's own "zero coverage" assessment. Two
planning docs currently disagree about whether a requirement is met.

Correct that line to point at DES-100 rather than claiming satisfaction. Cheap, and it stops the next person
inheriting the wrong conclusion.

### Traceability qualification

The audit overstated the write model slightly (Corrections #3): scoring-monitoring's `SessionEvent`
(`src/Domain/Entities/SessionEvent.cs:36-48`) carries `OccurredAt`, optional `ResponsibleUserId`, and a
`SourceEventKey` idempotency key — but **no correlation ID**. session-ops' own `SessionEvent`
(`src/Domain/Entities/SessionEvent.cs:56`) does have all three. Worth confirming DES-100 §1's attribution
contract closes that, since "trazabilidad mínima" is the actual bar.

---

## F5 — Scoring silently drops score events; no transactional outbox

Non-RF · Medium · `scoring-monitoring-service` · reliability of RF-10/RF-12

### Root cause

Two compounding problems:

1. `Application/Scores/EventHandlers/PublishScoreEntryRegisteredIntegrationEventHandler.cs:39-46` catches
   **every** exception from `Publish` and logs without rethrowing.
2. `Infrastructure/Messaging/MassTransitMessagingRegistration.cs:17-37` registers RabbitMQ with **no outbox** —
   contrast session-ops (`.../MassTransitMessagingRegistration.cs:31-37`: `UsePostgres()`, `UseBusOutbox()`,
   30-min dedup, migration `20260713020055_AddMassTransitTransactionalOutbox`).

**Failure scenario:** an operator applies a penalty during a broker hiccup. `ScoreEntry` commits; the
`ScoreEntryRegisteredIntegrationEvent` publish throws; the handler swallows it. `ScoreEntryRegisteredConsumer`
never runs, `RecalculateRankingCommand` never fires. The penalty sits in the ledger but **the ranking never
reflects it** — no retry, no user-visible error, a wrong ranking (RF-12) shown as authoritative. Only a log line
records it.

RF-10 and RF-12 are still *met* — the wiring is correct on the happy path — but this undermines both in exactly
the conditions RF-14's outbox exists to survive.

### Fix

Add the EF outbox to scoring, mirroring session-ops:

```csharp
bus.AddEntityFrameworkOutbox<ScoringMonitoringDbContext>(outbox =>
{
    outbox.UsePostgres();
    outbox.UseBusOutbox();
    outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
});
```
plus the MassTransit outbox migration on `ScoringMonitoringDbContext`.

Then **delete the try/catch**. With the outbox, `Publish` enlists in the same transaction as the `ScoreEntry`
write: either both commit and the event delivers with retry, or neither does. The swallow becomes both
unnecessary and harmful — it would hide a genuine failure the outbox is designed to retry.

Audit sibling handlers in scoring for the same swallow pattern while here.

### Tests

- Mirror session-ops' `OutboxDeliveryOnRecoveryTests`: with the broker down, apply a penalty → `ScoreEntry` row
  and outbox row commit together; on recovery the event delivers and the ranking recalculates.
- Assert the ranking's `CalculationVersion` increments after recovery — i.e. the RF-10 chain completed.

---

## F6 — mission-design has no ASP.NET authentication/authorization middleware

**RF-16** · Low · defence-in-depth · `mission-design-service`

### Scope — read Corrections #1 first

`src/Api/Program.cs` never calls `UseAuthentication()`/`UseAuthorization()`, and `src/Api/DependencyInjection.cs`
has no `AddAuthentication()`/`AddAuthorization()`. The other three services wire both.

**This does not block F1** and is not why the leak exists. mission-design's real gate is the MediatR
`AuthorizationBehaviour` + custom attribute, which works fine without middleware. The consequence is narrower:
**ASP.NET's `[Authorize]` on a mission-design controller would silently do nothing**, and there are no
controller-level policies as a second layer.

The genuine risk is a trap for the next contributor: someone adds `[Authorize(Policy = …)]` to a controller here
(as every sibling service does), gets no error, and ships an endpoint they believe is protected.

### Fix — pick one

- **(a)** Wire it, mirroring `session-operations-service/src/Api/DependencyInjection.cs:38-75` (port the
  `TrustedHeadersAuthenticationHandler` and `TrustedHeadersAuthenticationDefaults` `file`-scoped classes,
  `ibid.:84-126`), add `app.UseAuthentication(); app.UseAuthorization();` between `Program.cs:27` and `:29`, and
  add an `AuthorizationPolicies` constants class. Aligns the four services.
- **(b)** Leave it, and add a comment at the top of `Api/DependencyInjection.cs` stating that mission-design
  authorizes at the MediatR layer only and ASP.NET `[Authorize]` will not work here.

Recommend **(a)** for consistency, but **(b)** is defensible and honest — the MediatR gate is real. What is not
acceptable is the status quo, where the absence is invisible.

### Decision point if you take (a) — the email header

session-ops' handler returns `AuthenticateResult.NoResult()` unless **all three** of `X-User-Id`, `X-User-Role`,
`X-User-Email` are present (`ibid.:100-105`), but the gateway adds `X-User-Email` only *if the claim exists*
(`api-gateway/src/Transforms/TrustedHeadersTransform.cs:24,38-44`). A Keycloak user with no email claim would
therefore start getting 401s on mission-design — a latent trap already live in session-ops.

Require only `X-User-Id` + `X-User-Role` here and treat email as optional; mission-design's `ICurrentUser` doesn't
expose `Email` at all (`CurrentUser.cs:17-26`). Either align session-ops too, or comment why they differ.

### Tests

If (a): handler unit tests (headers → `ClaimsPrincipal` with role claim; missing/blank → `NoResult`), plus an
integration test that a policy-decorated endpoint returns 401 with no headers and 403 with a wrong role.

---

## F7 — Trivia teams lose the session clock between questions

RF-06 edge · Low · `mobile`

### Root cause

`mobile/src/components/active-question-stage.tsx:80` gates `showTimer` on `questionSequenceOrder !== undefined`.
When `view.kind` is waiting/none, `team-space.tsx:580-583` renders only `ActiveQuestionStageHeader` with `score`
and no timer. Between questions a trivia team sees score + clues but **no session clock**.

RF-06 (*"Cada equipo debe visualizar su temporizador, puntaje y pistas habilitadas"*) is met overall — the treasure
board shows all three — but this sub-view drops a required element.

### Fix

Distinguish the two clocks, which the current code conflates:

- **Question countdown** — correctly hidden when no question is active.
- **Session timer** — from `useSessionTimer` (`mobile/src/lib/realtime/use-session-timer.ts:47-159`); should stay
  visible for the whole session, as it already does on the treasure board via `SessionTimerBar`
  (`treasure-hunt-board.tsx:171`).

Render `SessionTimerBar` in the waiting/none branch at `team-space.tsx:580-583`, reusing the `display` already
computed for the active-question path (`team-space.tsx:559`). No new state.

**Confirm with product before building.** If the blank is deliberate, close as won't-fix and note it against
RF-06 so the next audit doesn't re-raise it.

### Tests

Extend `mobile/src/__tests__/team-space-question-stage.test.tsx`: with no active question, the session timer
renders and the question countdown does not.

---

## F8 — identity-access authorization policies are dead code

**RF-16** · Low · `identity-access-service` · **implemented 2026-07-17 — read Corrections #4 first**

> **The recommendation below was partly wrong and is amended in place.** `.Participant` was **deleted, not
> applied**: applying it would have broken the reason-coded 200 contract that mobile and session-ops depend on.
> Corrections #4 has the evidence.

### Root cause

`AuthorizationPolicies.AdminOrOperator` and `.Participant` are registered (`src/Api/DependencyInjection.cs:27,34`)
but have **zero consumers**: `TeamsController.cs:15-17`, `UsersController.cs:16-18`, `PermissionsController.cs:10-12`
carry no `[Authorize]`.

Not exploitable — the MediatR `[Authorize(Roles=…)]` attributes cover Teams/Users — so this is defence-in-depth.
The risk is the same shape as F1/F6: registered-but-unused policies read as "this is protected" to the next person.

One genuine thinness: `GetAuthenticatedActorProfileQuery` (`GET /api/users/me`) has no attribute on **either**
layer, relying purely on the gateway's `"default"` policy. Defensible for a self-profile route, but it should be
deliberate rather than incidental.

### Fix — what landed

The original "pick one — (a) apply them all / (b) delete them all" was a false choice: the right answer is
**per-policy**, because `AdminOrOperator` and `Participant` are not the same kind of thing.

- **`AdminOrOperator` → applied.** `TeamsController` takes it controller-level (all seven of its routes are
  `Administrator,Operator` at the MediatR layer, so per-action would be noise). `UsersController` takes it on
  `GET /api/users` only.
- **New `Administrator` policy → added.** The admin-only routes (`invitations`, `{id}/access` ×2, `{id}/role`)
  had no policy to apply — the doc missed that (a) was not implementable as written.
- **`Participant` → deleted.** See Corrections #4. `PermissionsController` takes a plain `[Authorize]`.
- **`GetAuthenticatedActorProfileQuery` → explicit MediatR `[Authorize]`** (no roles = any authenticated user),
  as the original text asked. `AuthorizationBehaviour.cs:39` (`roles.Length > 0 && …`) treats a role-less
  attribute as authenticate-only.

Two carve-outs are load-bearing and easy to destroy with a controller-level `[Authorize]`:

- **`POST /api/users/register` and `POST /api/users/forgot-password` need `[AllowAnonymous]`** (ADR-0016 §1 —
  the caller has no account yet). `UsersController`'s baseline `[Authorize]` would otherwise break
  self-registration silently.
- **`PermissionsController` must not carry a role policy** — Corrections #4.

### The trap this fix walks into — ProblemDetails

**Applying any policy silently degrades the error contract, and the status codes do not reveal it.** The
authorization middleware rejects *before* MediatR, so 401/403 never reach `ProblemDetailsExceptionHandler` and
return an **empty body**. `response.StatusCode.Should().Be(Forbidden)` still passes; only a test that reads the
body fails. Three existing tests caught it — a green spot-check would have shipped it.

Fix: register `AddProblemDetails(...)` with `CustomizeProblemDetails` aligning **only** the 401/403 arms to the
handler's wording (`"Unauthorized."` / `"Forbidden."` — note the trailing period; the framework's default
reason phrase omits it), plus `app.UseStatusCodePages()`. All 76 pre-existing identity-access assertions then
pass **untouched**.

The same wiring was added to mission-design under F6, so the `[Authorize]` that F6 exists to enable does not
hand back empty-body errors the first time someone uses it.

### Tests

Landed in `tests/IntegrationTests/Api/AuthorizationPolicyEnforcementTests.cs` (12):

- Per-route 401 (no headers) / 403 (wrong role), including **Operator → 403 on the admin-only routes** — the
  case that distinguishes `Administrator` from `AdminOrOperator`. The `DeactivateUserAccess` case deliberately
  targets a **non-existent id**, asserting the policy rejects before the handler looks anything up.
- **The contract-preservation test that matters:** `participant-eligible-teams` as an **Operator** → **200**
  with `reasonCode: user-not-participant`, *not* 403. This is the test that fails if someone re-adds the
  `Participant` policy.
- Both anonymous routes still reachable with no headers.
- A policy rejection returns ProblemDetails matching the MediatR layer (the trap above).

---

## Appendix — RF-07, out of scope (ticketed as HU-27)

Recorded so the next audit doesn't re-derive it.

The canon requires both modes: *"The `Operator` must be able to perform `ClueRelease` manually **or conditioned by
progression rules**"* (`docs/academic-requirements-canon.md:37`). The conditioned half is **already scoped as
HU-27** — `docs/required_patterns_matrix.md:120`: *"Rule-conditioned automatic clue release — the runtime enables a
clue when a team's advancement condition is met."* So this is a known, ticketed gap, not an oversight.

The manual half is complete and correct (`ReleaseClueCommand` → `ClueReleaseFacade.ReleaseCluesAsync` →
`LiveSession.ReleaseClueToTeam`/`ReleaseClueToAllTeams`, operator-gated, broadcast on release).

The conditioned half is **enum-only scaffolding**:

- `Domain/Enums/ReleaseMode.cs:3` declares `Manual = 1, Automatic = 2, Policy = 3`.
- `ClueReleaseRecord` exposes exactly one factory, `CreateManual`, **hardcoding `ReleaseMode.Manual`**
  (`Domain/Entities/ClueReleaseRecord.cs:51-66`, literal at `:63`). No `CreateAutomatic`/`CreatePolicy`.
- Both domain methods call `AppendManualClueRelease` unconditionally (`LiveSession.cs:503,522`).
- Repo-wide grep for `ReleaseMode.Automatic|ReleaseMode.Policy` in non-test source: **zero hits**.
- No progression-condition evaluator, and no event-to-release binding. The only call sites are operator-triggered.

⚠️ **`tests/UnitTests/Domain/Enums/ReleaseModeTests.cs:5-12` is a misleading green signal** — it freezes the enum's
integer values and proves no behaviour, so it passes while the feature is unreachable in production code. Anyone
scanning coverage will read RF-07 as tested. Worth a comment on that test pointing here.

Note `ClueNotReleasableException` (`LiveSession.cs:1241-1284`) is **not** a progression rule despite the name —
it's existence/active-substage validation constraining which clue the *operator* may pick.

---

## Verification

Per-fix tests are specified above. Full sweep:

```bash
cd backend && make test          # all four services
cd frontend && pnpm test         # F7 is mobile: cd mobile && pnpm test
```

**Manual check for F1** — the one to do by hand, since it is the live leak. Through the production gateway,
authenticated as a Participant:

```
GET /api/missions/{id}/runtime-plan     → expect 403 after the fix (returns IsCorrect today)
GET /api/trivias/{id}                   → expect 403 after F1+F2
```
Then repeat as an Operator and confirm 200 with `IsCorrect` present.
